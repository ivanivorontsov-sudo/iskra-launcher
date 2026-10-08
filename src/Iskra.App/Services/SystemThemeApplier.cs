using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

public sealed record ApplyReport(List<string> Changed, List<string> Skipped, int RegErrors);

/// <summary>
/// Применяет тему ко всей Windows: цвета, обои, экран блокировки, курсоры, звуки, интервал значков,
/// рамки/материал окон, контекстное меню, панель задач — и запоминает «как было». Системный шрифт — отдельно, с правами администратора.
/// </summary>
internal sealed class SystemThemeApplier
{
    public SystemSnapshot Snapshot { get; private set; }
    private static readonly SemaphoreSlim ApplyGate = new(1, 1);

    public SystemThemeApplier()
    {
        Snapshot = LoadSnapshot();
    }

    public static SystemSnapshot LoadSnapshot()
    {
        try { if (File.Exists(Paths.Snapshot)) return SystemSnapshot.FromJson(File.ReadAllText(Paths.Snapshot)) ?? new(); }
        catch (Exception ex) { Log.Error("Чтение снимка", ex); }
        return new SystemSnapshot();
    }

    private void SaveSnapshot()
    {
        try { Iskra.Core.Settings.AppSettings.SaveAtomic(Paths.Snapshot, Snapshot.ToJson()); }
        catch (Exception ex) { Log.Error("Сохранение снимка", ex); }

        try { File.WriteAllText(Paths.RestoreScript, RestoreScriptGenerator.Generate(Snapshot, AppInfo.ExePath), new System.Text.UTF8Encoding(true)); }
        catch (Exception ex) { Log.Error("Скрипт восстановления", ex); }
    }

    public async Task<ApplyReport> ApplyAsync(ThemeDefinition theme, Iskra.Core.Windows.SystemApplyOptions options, (int w, int h) screen)
    {
        var changed = new List<string>();
        var skipped = new List<string>();
        var applier = new RegistryApplier(Snapshot);

        await Task.Run(() =>
        {
            // 1. Ресурсы (курсоры/звуки/обои) — генерируем до записи в реестр.
            SystemAssets assets = new();
            if (options.Cursors)
            {
                var (map, scheme) = AssetFactory.GenerateCursors(theme);
                foreach (var kv in map) assets.Cursors[kv.Key] = kv.Value;
                assets.CursorSchemeName = scheme;
                Snapshot.CursorsTouched = true;
            }
            if (options.Sounds && theme.Sound != SoundPack.None)
                foreach (var kv in AssetFactory.GenerateSounds(theme)) assets.Sounds[kv.Key] = kv.Value;
            if (options.ShellIcons)
            {
                foreach (var kv in ShapeIconFactory.GenerateShellIcons(theme)) assets.IconFiles[kv.Key] = kv.Value;
                Snapshot.ShellIconsTouched = true;
            }

            // 2. Реестровые изменения (цвета, звуки, курсоры, значки, контекстное меню, панель задач).
            var plan = SystemPlan.Build(theme.System, options, assets);
            applier.Apply(plan);
            if (options.Colors) changed.Add(L("colors"));
            if (options.Cursors && assets.Cursors.Count > 0) changed.Add(L("cursors"));
            if (options.Sounds && assets.Sounds.Count > 0) changed.Add(L("sounds"));
            if (options.IconSpacing) { changed.Add(L("icons")); Snapshot.IconSpacingTouched = true; }
            if (options.ContextMenu) { changed.Add(L("contextmenu")); Snapshot.ContextMenuTouched = true; }
            if (options.Taskbar && theme.System.TaskbarAlignment != TaskbarAlign.Unchanged) changed.Add(L("taskbar"));
            if (options.WindowMetrics) changed.Add(L("metrics"));
            if (options.ShellIcons && assets.IconFiles.Count > 0) { changed.Add(L("shellicons")); ShortcutIconApplier.RefreshIcons(); }
            if (options.ShapedShortcutIcons)
            {
                int n = ShortcutIconApplier.Apply(theme, Snapshot);
                if (n > 0) changed.Add(L("shortcuticons") + " (" + n + ")");
            }

            // 3. Обои (и слайд-шоу).
            if (options.Wallpaper)
            {
                try
                {
                    CaptureWallpaperOriginal();
                    if (theme.System.Slideshow)
                    {
                        var frames = AssetFactory.GenerateSlideshow(theme, screen.w, screen.h);
                        WallpaperManager.SetSlideshow(frames, theme.System.SlideshowMinutes);
                    }
                    else
                    {
                        var wall = AssetFactory.GenerateWallpaper(theme, screen.w, screen.h);
                        WallpaperManager.SetWallpaper(wall);
                    }
                    changed.Add(L("wallpaper"));
                }
                catch (Exception ex) { Log.Error("Обои", ex); skipped.Add(L("wallpaper")); }
            }

            // 4. Экран блокировки (через UWP API; CSP-реестр HKLM — только при наличии прав администратора).
            if (options.LockScreen && theme.System.LockScreen)
            {
                try
                {
                    BackupLockScreen();
                    var img = AssetFactory.GenerateWallpaper(theme, screen.w, screen.h, frame: 2);
                    if (LockScreenManager.TrySet(img)) { changed.Add(L("lockscreen")); Snapshot.LockScreenTouched = true; }
                    else skipped.Add(L("lockscreen"));
                }
                catch (Exception ex) { Log.Error("Экран блокировки", ex); skipped.Add(L("lockscreen")); }
            }

            if (options.WindowFrames) changed.Add(L("windows"));

            SaveSnapshot();
        });

        // 5. Рамки/материал окон (Проводник) — ТОЛЬКО в UI-потоке: DwmWatcher использует DispatcherTimer.
        //    Раньше это вызывалось из Task.Run и падало, обрывая применение темы на середине (причина «глюков»).
        RunOnUi(() =>
        {
            try { DwmWatcher.Instance.Configure(theme, enabled: options.WindowFrames); } catch (Exception ex) { Log.Error("DWM-наблюдатель", ex); }
            try { ThemeBroadcast.Notify(); } catch (Exception ex) { Log.Error("Рассылка темы", ex); }
            try { BackdropCaps.Invalidate(); } catch { }
        });

        return new ApplyReport(changed, skipped, 0);
    }

    private static void RunOnUi(Action a)
    {
        var app = System.Windows.Application.Current;
        if (app == null) { try { a(); } catch { } return; }
        if (app.Dispatcher.CheckAccess()) a();
        else app.Dispatcher.Invoke(a);
    }

    /// <summary>Применение темы с сериализацией (без гонок) и защитой по времени, чтобы ничего не «подвисало» навсегда.</summary>
    public async Task<ApplyReport> ApplySerializedAsync(ThemeDefinition theme, Iskra.Core.Windows.SystemApplyOptions options, (int w, int h) screen, int timeoutMs = 25000)
    {
        if (!await ApplyGate.WaitAsync(timeoutMs))
        {
            Log.Warn("Применение темы занято — пропуск повторного вызова.");
            return new ApplyReport(new(), new(), 0);
        }
        try
        {
            var task = ApplyAsync(theme, options, screen);
            var done = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (done != task) { Log.Warn("Применение темы превысило лимит времени; продолжит в фоне."); return new ApplyReport(new(), new(), 0); }
            return await task;
        }
        catch (Exception ex) { Log.Error("Применение темы", ex); return new ApplyReport(new(), new(), 1); }
        finally { ApplyGate.Release(); }
    }

    private void CaptureWallpaperOriginal()
    {
        if (Snapshot.Wallpaper != null) return;
        Snapshot.Wallpaper = WallpaperManager.CaptureCurrent();
    }

    private void BackupLockScreen()
    {
        if (!string.IsNullOrEmpty(Snapshot.LockScreenBackup) && File.Exists(Snapshot.LockScreenBackup)) return;
        var backup = LockScreenManager.Backup();
        if (backup != null) Snapshot.LockScreenBackup = backup;
    }

    /// <summary>Полный откат: реестр из снимка + обои/курсоры/панель задач + остановка наблюдателя DWM.</summary>
    public int Restore(bool includeHklm = true)
    {
        int errors = RegistryApplier.Restore(Snapshot, includeHklm);
        try
        {
            if (Snapshot.Wallpaper != null) WallpaperManager.Restore(Snapshot.Wallpaper);
            if (Snapshot.CursorsTouched) WallpaperManager.ReloadCursors();
            if (Snapshot.LockScreenTouched && !string.IsNullOrEmpty(Snapshot.LockScreenBackup) && File.Exists(Snapshot.LockScreenBackup))
                LockScreenManager.TrySet(Snapshot.LockScreenBackup);
            if (Snapshot.Shortcuts.Count > 0) ShortcutIconApplier.Restore(Snapshot);
            if (Snapshot.ShellIconsTouched) ShortcutIconApplier.RefreshIcons();
        }
        catch (Exception ex) { Log.Error("Откат ресурсов", ex); errors++; }
        DwmWatcher.Instance.Stop();
        ThemeBroadcast.Notify();
        // Снимок очищаем: Искра больше ничего не держит.
        Snapshot = new SystemSnapshot();
        try { if (File.Exists(Paths.Snapshot)) File.Delete(Paths.Snapshot); } catch { }
        return errors;
    }

    public bool HasSnapshot => !Snapshot.IsEmpty;

    private static string L(string cat) => cat switch
    {
        "colors" => Iskra.Core.Loc.L.English ? "colours" : "цвета",
        "cursors" => Iskra.Core.Loc.L.English ? "cursors" : "курсоры",
        "sounds" => Iskra.Core.Loc.L.English ? "sounds" : "звуки",
        "icons" => Iskra.Core.Loc.L.English ? "icon spacing" : "значки",
        "contextmenu" => Iskra.Core.Loc.L.English ? "context menu" : "контекстное меню",
        "taskbar" => Iskra.Core.Loc.L.English ? "taskbar" : "панель задач",
        "wallpaper" => Iskra.Core.Loc.L.English ? "wallpaper" : "обои",
        "lockscreen" => Iskra.Core.Loc.L.English ? "lock screen" : "экран блокировки",
        "windows" => Iskra.Core.Loc.L.English ? "window frames" : "рамки окон",
        "metrics" => Iskra.Core.Loc.L.English ? "window metrics" : "метрики окон",
        "shellicons" => Iskra.Core.Loc.L.English ? "folder/PC/bin icons" : "значки папок/ПК/корзины",
        "shortcuticons" => Iskra.Core.Loc.L.English ? "shortcut icons" : "значки ярлыков",
        _ => cat,
    };
}

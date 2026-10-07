using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

            // 2. Реестровые изменения (цвета, звуки, курсоры, значки, контекстное меню, панель задач).
            var plan = SystemPlan.Build(theme.System, options, assets);
            applier.Apply(plan);
            if (options.Colors) changed.Add(L("colors"));
            if (options.Cursors && assets.Cursors.Count > 0) changed.Add(L("cursors"));
            if (options.Sounds && assets.Sounds.Count > 0) changed.Add(L("sounds"));
            if (options.IconSpacing) { changed.Add(L("icons")); Snapshot.IconSpacingTouched = true; }
            if (options.ContextMenu) { changed.Add(L("contextmenu")); Snapshot.ContextMenuTouched = true; }
            if (options.Taskbar && theme.System.TaskbarAlignment != TaskbarAlign.Unchanged) changed.Add(L("taskbar"));

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

            // 5. Рамки/материал/углы прочих окон (Проводник и др.) — через внешний «наблюдатель» DWM.
            if (options.WindowFrames)
            {
                DwmWatcher.Instance.Configure(theme, enabled: true);
                changed.Add(L("windows"));
            }
            else DwmWatcher.Instance.Configure(theme, enabled: false);

            SaveSnapshot();
        });

        // 6. Разослать смену темы Проводнику.
        ThemeBroadcast.Notify();

        return new ApplyReport(changed, skipped, 0);
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
        _ => cat,
    };
}

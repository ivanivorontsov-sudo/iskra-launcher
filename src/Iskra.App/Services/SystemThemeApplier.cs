using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Iskra.Core.Themes;
using Iskra.Core.Windows;
using Microsoft.Win32;

namespace Iskra.App.Services;

public sealed record ApplyReport(List<string> Changed, List<string> Skipped, int RegErrors)
{
    /// <summary>Что заблокировано политикой организации (GPO/MDM) или не «прилипло» после записи.</summary>
    public List<string> Locked { get; init; } = new();
    public List<string> Undone { get; init; } = new();
}

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

    public void SaveSnapshotPublic() => SaveSnapshot();

    private static bool HklmMatches(RegWrite w)
    {
        try
        {
            using var k = Registry.LocalMachine.OpenSubKey(w.Key);
            var cur = k?.GetValue(w.Name);
            if (w.Kind == RegKind.DeleteValue) return cur == null;
            return cur != null && string.Equals(cur.ToString(), w.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private void SaveSnapshot()
    {
        try { Iskra.Core.Settings.AppSettings.SaveAtomic(Paths.Snapshot, Snapshot.ToJson()); }
        catch (Exception ex) { Log.Error("Сохранение снимка", ex); }

        try { File.WriteAllText(Paths.RestoreScript, RestoreScriptGenerator.Generate(Snapshot, AppInfo.ExePath), new System.Text.UTF8Encoding(true)); }
        catch (Exception ex) { Log.Error("Скрипт восстановления", ex); }
    }

    public async Task<ApplyReport> ApplyAsync(ThemeDefinition theme, Iskra.Core.Windows.SystemApplyOptions options, (int w, int h) screen, bool undoDisabled = true)
    {
        var changed = new List<string>();
        var skipped = new List<string>();
        var lockedOut = new List<string>();
        var undone = new List<string>();
        var applier = new RegistryApplier(Snapshot);

        await Task.Run(() =>
        {
            // 0. Выключенные пункты больше не «залипают»: всё, что Искра раньше меняла в них, возвращается как было.
            if (undoDisabled) undone.AddRange(UndoDisabledCore(options));

            // 0.1. Windows 11 Enterprise: групповые политики / MDM. Заблокированные пункты не трогаем и говорим об этом.
            var locks = PolicyLocks();
            var lockedCats = locks.Select(l => l.Category).ToHashSet();
            foreach (var l in locks) lockedOut.Add(LockText(l));
            options = CloneWithout(options, lockedCats);

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
            if (options.HideShortcutArrows)
            {
                try { assets.BlankIcon = ShapeIconFactory.GenerateBlankIcon(); Snapshot.ShellIconsTouched = true; } catch (Exception ex) { Log.Error("Прозрачный значок", ex); }
            }

            // 2. Реестровые изменения (цвета, звуки, курсоры, значки, контекстное меню, панель задач).
            var plan = SystemPlan.Build(theme.System, options, assets);
            applier.Apply(plan); // HKLM без прав не запишется, но исходные значения уже в снимке
            foreach (var cat in TweakControl.VerifyWrites(plan.Where(w => w.Hive == RegHive.CurrentUser), RegistryApplier.Read))
                lockedOut.Add((Iskra.Core.Loc.L.English ? "not applied (blocked by policy, security software or no rights): " : "не применилось (мешает политика организации, защита или нет прав): ") + L(cat));
            SaveSnapshot();
            var lmPending = plan.Where(w => w.Hive == RegHive.LocalMachine && !HklmMatches(w)).ToList();
            if (lmPending.Count > 0 && !AppInfo.IsAdmin())
            {
                if (HklmWriter.RunElevated(lmPending)) Snapshot.HklmTouched = true;
                else skipped.Add(Iskra.Core.Loc.L.English ? "admin-only parts (UAC declined)" : "части, требующие прав администратора (UAC отклонён)");
            }
            if (options.Colors) changed.Add(L("colors"));
            if (options.Cursors && assets.Cursors.Count > 0) changed.Add(L("cursors"));
            if (options.Sounds && assets.Sounds.Count > 0) changed.Add(L("sounds"));
            if (options.IconSpacing) { changed.Add(L("icons")); Snapshot.IconSpacingTouched = true; }
            if (options.ContextMenu) { changed.Add(L("contextmenu")); Snapshot.ContextMenuTouched = true; }
            if (options.Taskbar && theme.System.TaskbarAlignment != TaskbarAlign.Unchanged) changed.Add(L("taskbar"));
            if (options.WindowMetrics) changed.Add(L("metrics"));
            if (options.IconSpacing) DesktopGrid.ApplyFromRegistry(); // сетка — вживую через SPI, без перезапуска
            bool iconsChanged = false;
            if (options.ShellIcons && assets.IconFiles.Count > 0) { changed.Add(L("shellicons")); iconsChanged = true; }
            if (options.HideShortcutArrows && assets.BlankIcon != null) iconsChanged = true;
            if (options.ShapedShortcutIcons)
            {
                int n = ShortcutIconApplier.Apply(theme, Snapshot);
                if (n > 0) { changed.Add(L("shortcuticons") + " (" + n + ")"); iconsChanged = true; }
                if (ShortcutIconApplier.LastReport.Unsupported > 0)
                    skipped.Add((Iskra.Core.Loc.L.English ? "plain files keep their icon: " : "обычные файлы без своего значка: ") + string.Join(", ", ShortcutIconApplier.LastReport.UnsupportedNames.Take(6)));
            }
            // ОДНО обновление в самом конце, после всех записей. Раньше кэш сбрасывался до записи ярлыков,
            // а второй перезапуск Проводника отбрасывался защитой от частых перезапусков — значки не обновлялись.
            if (iconsChanged) ShortcutIconApplier.RefreshIcons(restartExplorer: true);

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

        return new ApplyReport(changed, skipped, 0) { Locked = lockedOut, Undone = undone };
    }

    // ---------------- Отдельные пункты: выключение = полный откат ----------------

    /// <summary>Выключить один пункт (или несколько) прямо сейчас — вернуть всё, что Искра в нём меняла.</summary>
    public Task<List<string>> UndoDisabledAsync(Iskra.Core.Windows.SystemApplyOptions options)
        => Task.Run(async () =>
        {
            await ApplyGate.WaitAsync();
            try { return UndoDisabledCore(options); } finally { ApplyGate.Release(); }
        });

    private List<string> UndoDisabledCore(Iskra.Core.Windows.SystemApplyOptions o)
    {
        var undone = new List<string>();
        try
        {
            var cats = TweakControl.DisabledCategories(o);
            var taken = TweakControl.Take(Snapshot, cats);
            var hkcu = taken.Where(r => r.Hive == RegHive.CurrentUser).ToList();
            var hklm = taken.Where(r => r.Hive == RegHive.LocalMachine).ToList();
            int errors = RegistryApplier.Restore(hkcu, includeHklm: false);
            if (hklm.Count > 0)
            {
                bool ok = AppInfo.IsAdmin()
                    ? RegistryApplier.Restore(hklm, includeHklm: true) == 0
                    : HklmWriter.RunElevated(hklm.Select(TweakControl.ToRestoreWrite).ToList());
                if (!ok) { Snapshot.Registry.AddRange(hklm); errors++; } // UAC отклонён — запомним, повторим позже
            }
            foreach (var c in taken.Select(r => r.Category).Distinct()) undone.Add(L(c));

            bool icons = taken.Any(r => r.Category is "shellicons" or "arrow" or "icons");
            if (!o.Cursors && Snapshot.CursorsTouched) { WallpaperManager.ReloadCursors(); Snapshot.CursorsTouched = false; }
            if (!o.Wallpaper && Snapshot.Wallpaper != null)
            {
                try { WallpaperManager.Restore(Snapshot.Wallpaper); Snapshot.Wallpaper = null; undone.Add(L("wallpaper")); }
                catch (Exception ex) { Log.Error("Откат обоев", ex); errors++; }
            }
            if (!o.LockScreen && Snapshot.LockScreenTouched && !string.IsNullOrEmpty(Snapshot.LockScreenBackup) && File.Exists(Snapshot.LockScreenBackup))
            {
                if (LockScreenManager.TrySet(Snapshot.LockScreenBackup)) { Snapshot.LockScreenTouched = false; undone.Add(L("lockscreen")); }
            }
            if (!o.ShapedShortcutIcons && (Snapshot.Shortcuts.Count > 0 || Snapshot.FileIcons.Count > 0))
            {
                try { ShortcutIconApplier.Restore(Snapshot); undone.Add(L("shortcuticons")); icons = true; }
                catch (Exception ex) { Log.Error("Откат значков ярлыков", ex); errors++; }
            }
            if (!o.ShellIcons && !o.HideShortcutArrows && !o.ShapedShortcutIcons) Snapshot.ShellIconsTouched = false;
            if (!o.IconSpacing) Snapshot.IconSpacingTouched = false;
            if (!o.ContextMenu) Snapshot.ContextMenuTouched = false;
            if (!o.WindowFrames) RunOnUi(() => { try { DwmWatcher.Instance.Stop(); } catch { } });
            if (Snapshot.Registry.All(r => r.Hive != RegHive.LocalMachine)) Snapshot.HklmTouched = false;

            if (undone.Count > 0)
            {
                if (!o.IconSpacing || !o.WindowMetrics) { try { DesktopGrid.ApplyFromRegistry(); } catch { } FontApply.PushNonClientFromRegistry(); }
                if (icons) ShortcutIconApplier.RefreshIcons(restartExplorer: taken.Any(r => r.Category == "arrow"));
                ThemeBroadcast.Notify();
                SaveSnapshot();
                Log.Info("Откат выключенных пунктов: " + string.Join(", ", undone) + (errors > 0 ? $" (ошибок: {errors})" : ""));
            }
        }
        catch (Exception ex) { Log.Error("Откат выключенных пунктов", ex); }
        return undone;
    }

    // ---------------- Политики организации (Windows 11 Enterprise) ----------------

    public static List<TweakControl.PolicyLock> PolicyLocks()
    {
        try { return TweakControl.DetectLocks(RegistryApplier.Read); }
        catch (Exception ex) { Log.Error("Проверка политик", ex); return new(); }
    }

    public static string LockText(TweakControl.PolicyLock l)
        => (Iskra.Core.Loc.L.English ? "Locked by your organisation: " + L(l.Category) + " — " + l.WhatEn : "Заблокировано политикой организации: " + L(l.Category) + " — " + l.WhatRu) + " (" + l.Where + ")";

    private static Iskra.Core.Windows.SystemApplyOptions CloneWithout(Iskra.Core.Windows.SystemApplyOptions o, HashSet<string> locked)
    {
        var c = System.Text.Json.JsonSerializer.Deserialize<Iskra.Core.Windows.SystemApplyOptions>(System.Text.Json.JsonSerializer.Serialize(o))!;
        if (locked.Contains("colors")) { c.Colors = false; c.ClassicColors = false; }
        if (locked.Contains("classiccolors")) c.ClassicColors = false;
        if (locked.Contains("wallpaper")) c.Wallpaper = false;
        if (locked.Contains("lockscreen")) c.LockScreen = false;
        if (locked.Contains("cursors")) c.Cursors = false;
        if (locked.Contains("sounds")) c.Sounds = false;
        if (locked.Contains("contextmenu")) c.ContextMenu = false;
        if (locked.Contains("taskbar")) c.Taskbar = false;
        if (locked.Contains("shellicons")) { c.ShellIcons = false; c.ShapedShortcutIcons = false; }
        if (locked.Contains("arrow")) c.HideShortcutArrows = false;
        return c;
    }

    private static void RunOnUi(Action a)
    {
        var app = System.Windows.Application.Current;
        if (app == null) { try { a(); } catch { } return; }
        if (app.Dispatcher.CheckAccess()) a();
        else app.Dispatcher.Invoke(a);
    }

    /// <summary>Применение темы с сериализацией (без гонок) и защитой по времени, чтобы ничего не «подвисало» навсегда.</summary>
    public async Task<ApplyReport> ApplySerializedAsync(ThemeDefinition theme, Iskra.Core.Windows.SystemApplyOptions options, (int w, int h) screen, int timeoutMs = 25000, bool undoDisabled = true)
    {
        if (!await ApplyGate.WaitAsync(timeoutMs))
        {
            Log.Warn("Применение темы занято — пропуск повторного вызова.");
            return new ApplyReport(new(), new(), 0);
        }
        try
        {
            var task = ApplyAsync(theme, options, screen, undoDisabled);
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

    /// <summary>Итог отката: число ошибок, нужны ли были права администратора, получили ли мы их, очищен ли снимок.</summary>
    public sealed record RestoreOutcome(int Errors, bool NeededHklm, bool Elevated, bool Cleared);

    /// <summary>
    /// Главная точка отката «Вернуть Windows как было». Если затрагивается HKLM (шрифт / экран блокировки) и у нас
    /// нет прав администратора — поднимаем отдельный процесс с UAC, который доводит откат до конца. При отказе от UAC
    /// снимок НЕ удаляется, чтобы можно было повторить. Также чинит повреждённую регистрацию Segoe UI даже без снимка.
    /// </summary>
    public RestoreOutcome RestoreAll()
    {
        bool needHklm = Snapshot.HklmTouched || Snapshot.Registry.Any(r => r.Hive == RegHive.LocalMachine) || FontRepairNeeded();
        bool admin = AppInfo.IsAdmin();
        if (needHklm && !admin)
        {
            bool ok = RunElevatedRestore();
            if (!ok)
            {
                int e1 = Restore(includeHklm: false, clearOnSuccess: false); // HKCU вернём, снимок сохраним для повтора
                return new RestoreOutcome(e1, true, false, false);
            }
            Snapshot = LoadSnapshot(); // повышенный процесс уже всё сделал и, при успехе, очистил снимок
            return new RestoreOutcome(0, true, true, Snapshot.IsEmpty);
        }
        int errors = Restore(includeHklm: admin || !needHklm, clearOnSuccess: true);
        return new RestoreOutcome(errors, needHklm, admin, Snapshot.IsEmpty);
    }

    /// <summary>
    /// Низкоуровневый откат: реестр из снимка + обои/курсоры/значки/шрифты. В отличие от старой версии:
    /// (1) снимок очищается ТОЛЬКО при успехе; (2) чинится регистрация Segoe UI (работает и с пустым снимком);
    /// (3) шрифты перерегистрируются и рассылается WM_FONTCHANGE / SPI_SETNONCLIENTMETRICS; (4) перезапуск Проводника.
    /// </summary>
    public int Restore(bool includeHklm = true, bool clearOnSuccess = true)
    {
        int errors = RegistryApplier.Restore(Snapshot, includeHklm);
        bool fontTouched = Snapshot.Registry.Any(r => r.Category == "font")
                           || Snapshot.Registry.Any(r => r.Category == "metrics" || r.Category == "icons");
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

        // Починка шрифтов: гарантируем штатный Segoe UI и убираем подстановку, даже если снимок пуст/повреждён.
        if (includeHklm && AppInfo.IsAdmin() && FontRepairNeeded())
        {
            try
            {
                var repair = new RegistryApplier(new SystemSnapshot()); // не засоряем основной снимок
                repair.Apply(Iskra.Core.Windows.FontPlan.BuildFontRepair());
                FontApply.ReregisterSegoe();
                fontTouched = true;
            }
            catch (Exception ex) { Log.Error("Починка шрифтов", ex); errors++; }
        }

        try { DesktopGrid.ApplyFromRegistry(); } catch { } // сетка — вживую из восстановленного реестра
        FontApply.PushNonClientFromRegistry(); // живой возврат заголовков/меню/значков из реестра
        DwmWatcher.Instance.Stop();
        ThemeBroadcast.Notify();
        FontApply.Broadcast();

        bool success = errors == 0;
        if (clearOnSuccess && success)
        {
            Snapshot = new SystemSnapshot();
            try { if (File.Exists(Paths.Snapshot)) File.Delete(Paths.Snapshot); } catch { }
            try { if (File.Exists(Paths.Snapshot + ".bak")) File.Delete(Paths.Snapshot + ".bak"); } catch { }
        }
        else if (!success) { SaveSnapshot(); } // сохраняем, чтобы можно было повторить откат

        bool desktopNeedsExplorer = fontTouched || Snapshot.ContextMenuTouched || Snapshot.ShellIconsTouched;
        if (desktopNeedsExplorer) ExplorerRestarter.Restart();
        return errors;
    }

    /// <summary>true, если по реестру видно, что базовый UI-шрифт подменён или семейство Segoe UI повреждено.</summary>
    public static bool FontRepairNeeded()
    {
        try
        {
            using var subs = Registry.LocalMachine.OpenSubKey(Iskra.Core.Windows.FontPlan.FontSubstitutesKey);
            var seg = subs?.GetValue("Segoe UI") as string;
            using var fonts = Registry.LocalMachine.OpenSubKey(Iskra.Core.Windows.FontPlan.FontsKey);
            var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            if (fonts != null)
                foreach (var n in Iskra.Core.Windows.FontPlan.SegoeNamesThatMayBeBlanked) dict[n] = fonts.GetValue(n) as string;
            return Iskra.Core.Windows.FontPlan.LooksBroken(seg, dict);
        }
        catch { return false; }
    }

    /// <summary>Поднимает скрытый повышенный процесс Искры, который доводит откат (включая HKLM и починку шрифтов) до конца.</summary>
    private static bool RunElevatedRestore()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = AppInfo.ExePath,
                Arguments = "--restore-silent",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden,
            };
            var p = System.Diagnostics.Process.Start(psi);
            if (p == null || !p.WaitForExit(120000)) return false;
            return p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; } // пользователь отменил UAC
        catch (Exception ex) { Log.Error("Повышение для отката", ex); return false; }
    }

    /// <summary>
    /// Применяет раздел «Шрифты» (независимо от темы). Заголовки/меню/значки/сообщения — живым SPI (без перезагрузки);
    /// базовый UI-шрифт (подстановка Segoe UI) — отдельно, через повышение. Перед SPI снимаем исходные LOGFONT в снимок.
    /// </summary>
    public bool ApplyFonts(Iskra.Core.Settings.FontSettings f)
    {
        if (f == null || !f.Enabled) return false;
        var applier = new RegistryApplier(Snapshot);
        foreach (var n in new[] { "CaptionFont", "SmCaptionFont", "MenuFont", "MessageFont", "StatusFont", "IconFont" })
            applier.CaptureValue(RegHive.CurrentUser, Iskra.Core.Windows.FontPlan.WindowMetricsKey, n, "metrics");
        bool any = FontApply.ApplyNonClient(f, f.SizePt);
        if (any)
        {
            SaveSnapshot();
            ExplorerRestarter.Restart(); // чтобы рабочий стол и панель задач перечитали шрифты
        }
        return any;
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
        "classiccolors" => Iskra.Core.Loc.L.English ? "classic window colours" : "классические цвета окон",
        "arrow" => Iskra.Core.Loc.L.English ? "shortcut arrows" : "стрелки ярлыков",
        "explorer" => Iskra.Core.Loc.L.English ? "compact Explorer" : "компактный Проводник",
        _ => cat,
    };
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Iskra.Core.Loc;
using Iskra.Core.Search;
using Iskra.Core.Settings;
using Iskra.Core.Themes;
using Iskra.App.Services;
using Iskra.App.Views;
using Iskra.Core.Windows;

namespace Iskra.App;

/// <summary>Центральное состояние приложения: настройки, движок поиска, темы, окна, горячие клавиши, трей.</summary>
internal sealed class AppState
{
    public AppSettings Settings { get; private set; } = new();
    public SearchEngine Engine { get; } = new();
    public SystemThemeApplier System { get; } = new();
    public Theming Theming { get; private set; } = null!;
    public event Action? ThemeChanged;
    public event Action<int, int>? IndexUpdated; // apps, files

    private HotkeyManager _hotkeys = null!;
    private StartKeyHook? _startHook;
    private int _launcherHotkeyId, _startHotkeyId;
    private CancellationTokenSource? _fileCts;
    private Window _messageWindow = null!;
    public int AppCount { get; private set; }
    public int FileCount { get; private set; }

    public void Initialize(Window messageWindow)
    {
        Paths.Init();
        _messageWindow = messageWindow;
        LoadSettings();
        L.English = Settings.Language == "en";
        Theming = new Theming(Settings.CurrentTheme());
        ConfigureEngine();
        _hotkeys = new HotkeyManager(messageWindow);
        RegisterHotkeys();
        Services.SystemCommands.IskraHandler = HandleIskraCommand;
        IndexAsync();
        if (!SafeMode.Active)
        {
            SetupStartHook();
            if (Settings.ApplyThemeToWindows && System.HasSnapshot)
                DwmWatcher.Instance.Configure(Settings.CurrentTheme(), Settings.SystemOptions.WindowFrames);
        }
    }

    // ---------- Настройки ----------
    public void LoadSettings()
    {
        Settings = AppSettings.LoadOrHeal(Paths.Settings, out bool healed);
        if (healed) { Log.Warn("Настройки восстановлены из резервной копии / приведены к корректным."); try { SaveSettings(); } catch { } }
        try { if (File.Exists(Paths.Usage)) { var u = UsageStore.Deserialize(File.ReadAllText(Paths.Usage)); CopyUsage(u); } }
        catch (Exception ex) { Log.Error("Чтение истории", ex); }
        ApplyEngineConfig();
    }

    private void CopyUsage(UsageStore u)
    {
        foreach (var kv in u.Items) Engine.Usage.Items[kv.Key] = kv.Value;
        foreach (var kv in u.QueryPicks) Engine.Usage.QueryPicks[kv.Key] = kv.Value;
    }

    public void SaveSettings()
    {
        try { AppSettings.SaveAtomic(Paths.Settings, Settings.ToJson()); } catch (Exception ex) { Log.Error("Запись настроек", ex); }
    }

    public void SaveUsage()
    {
        try { AppSettings.SaveAtomic(Paths.Usage, Engine.Usage.Serialize()); } catch (Exception ex) { Log.Error("Запись истории", ex); }
    }

    private void ApplyEngineConfig()
    {
        Engine.WebSearchTemplate = Settings.WebSearchUrl;
        Engine.WebSearchTitleFormat = L.T("search.web");
        Engine.CalculatorSubtitle = L.T("search.calc");
        Engine.Culture = L.English ? new System.Globalization.CultureInfo("en-US") : new System.Globalization.CultureInfo("ru-RU");
    }

    // ---------- Движок ----------
    private void ConfigureEngine()
    {
        Engine.SetSource("commands", BuiltinItems.Commands());
        Engine.SetSource("settings", BuiltinItems.Settings());
        ApplyEngineConfig();
    }

    public void IndexAsync()
    {
        Task.Run(() =>
        {
            try
            {
                var apps = AppIndexer.IndexStartMenu();
                var store = AppIndexer.IndexStoreApps();
                Engine.SetSource("apps", apps);
                Engine.SetSource("store", store);
                AppCount = apps.Count + store.Count;
                _messageWindow.Dispatcher.BeginInvoke(() => IndexUpdated?.Invoke(AppCount, FileCount));
            }
            catch (Exception ex) { Log.Error("Индексация приложений", ex); }
        });
        IndexFilesAsync();
    }

    public void IndexFilesAsync()
    {
        _fileCts?.Cancel();
        if (!Settings.FileSearchEnabled) { Engine.SetSource("files", Array.Empty<SearchItem>()); FileCount = 0; return; }
        var cts = new CancellationTokenSource();
        _fileCts = cts;
        var folders = Settings.FileSearchFolders.Count > 0 ? Settings.FileSearchFolders : FileIndexer.DefaultFolders();
        var depth = Settings.FileSearchMaxDepth;
        Task.Run(() =>
        {
            try
            {
                var files = FileIndexer.Index(folders, depth, cts.Token);
                if (cts.Token.IsCancellationRequested) return;
                Engine.SetSource("files", files);
                FileCount = files.Count;
                _messageWindow.Dispatcher.BeginInvoke(() => IndexUpdated?.Invoke(AppCount, FileCount));
            }
            catch (Exception ex) { Log.Error("Индексация файлов", ex); }
        });
    }

    // ---------- Горячие клавиши ----------
    public void RegisterHotkeys()
    {
        if (_launcherHotkeyId != 0) _hotkeys.Unregister(_launcherHotkeyId);
        if (_startHotkeyId != 0) _hotkeys.Unregister(_startHotkeyId);
        if (Hotkey.TryParse(Settings.Hotkey, out var hk))
        {
            var (ok, id) = _hotkeys.TryRegister(hk, ToggleLauncher);
            if (ok) _launcherHotkeyId = id; else Log.Warn("Конфликт горячей клавиши " + Settings.Hotkey);
        }
        if (Hotkey.TryParse(Settings.StartScreenHotkey, out var shk))
        {
            var (ok, id) = _hotkeys.TryRegister(shk, ToggleStartScreen);
            if (ok) _startHotkeyId = id;
        }
    }

    public bool IsHotkeyFree(Hotkey hk) => _hotkeys.IsFree(hk);

    private void SetupStartHook()
    {
        _startHook?.Dispose();
        if (Settings.ReplaceStartMenu)
        {
            _startHook = new StartKeyHook(ToggleStartMenu);
            _startHook.Install();
        }
    }

    // ---------- Окна ----------
    private LauncherWindow? _launcher;
    private StartScreenWindow? _startScreen;
    private SettingsWindow? _settings;

    public void ShowLauncher()
    {
        try
        {
            _launcher ??= new LauncherWindow(this);
            if (_launcher.IsVisible) { _launcher.Activate(); return; }
            _launcher.ShowLauncher(false);
        }
        catch (Exception ex) { Log.Error("Показ лаунчера", ex); }
    }

    public void ToggleLauncher()
    {
        try
        {
            _launcher ??= new LauncherWindow(this);
            if (_launcher.IsVisible) _launcher.HideAnimated();
            else _launcher.ShowLauncher(false);
        }
        catch (Exception ex) { Log.Error("Переключение лаунчера", ex); }
    }

    public void ToggleStartMenu()
    {
        // Вид «Пуска» зависит от темы: меню или стартовый экран.
        if (Settings.CurrentTheme().StartStyle == StartStyle.FullScreen) ToggleStartScreen();
        else { _launcher ??= new LauncherWindow(this); if (_launcher.IsVisible) _launcher.HideAnimated(); else _launcher.ShowLauncher(true); }
    }

    public void ToggleStartScreen()
    {
        _startScreen ??= new StartScreenWindow(this);
        if (_startScreen.IsVisible) _startScreen.HideAnimated();
        else _startScreen.ShowStart();
    }

    public void OpenSettings(string? nav = null)
    {
        if (_settings == null) { _settings = new SettingsWindow(this); _settings.Closed += (_, _) => _settings = null; }
        _settings.Show(); _settings.Activate();
        if (nav != null) _settings.NavigateTo(nav);
    }

    // ---------- Темы ----------
    public async Task ApplyThemeAsync(ThemeDefinition theme, bool applyToWindows)
    {
        Settings.ThemeId = theme.Id;
        Theming = new Theming(theme);
        SaveSettings();
        ThemeChanged?.Invoke();
        if (applyToWindows && Settings.ApplyThemeToWindows && !SafeMode.Active)
        {
            try
            {
                // Значки Windows (папки/корзина/ярлыки) меняем только если тема это разрешает (и это обратимо).
                // Форма значков Windows включается вместе с темой, иначе папки остаются штатными.
                Settings.SystemOptions.ShellIcons = true;
                Settings.SystemOptions.ShapedShortcutIcons = true;
                Settings.SystemOptions.HideShortcutArrows = Settings.HideShortcutArrows;
                Settings.SystemOptions.GridDensity = Settings.DesktopGrid;
                var screen = ScreenSize();
                var report = await System.ApplySerializedAsync(theme, Settings.SystemOptions, screen);
                Log.Info($"Тема «{theme.Name}» → Windows: {string.Join(", ", report.Changed)}");
            }
            catch (Exception ex) { Log.Error("Применение темы к Windows", ex); }
        }
    }

    public (int w, int h) ScreenSize()
    {
        var s = SystemParametersHelper.PrimaryPixels();
        return s;
    }

    public void NextTheme()
    {
        var all = Settings.AllThemes().ToList();
        int i = all.FindIndex(t => t.Id == Settings.ThemeId);
        var next = all[(i + 1) % all.Count];
        _ = ApplyThemeAsync(next, true);
    }

    public int RestoreWindows()
    {
        var outcome = System.RestoreAll();
        DwmWatcher.Instance.Stop();
        return outcome.Errors;
    }

    public SystemThemeApplier.RestoreOutcome RestoreWindowsDetailed()
    {
        var outcome = System.RestoreAll();
        DwmWatcher.Instance.Stop();
        return outcome;
    }

    /// <summary>
    /// Применяет раздел «Шрифты» в правильном порядке: 1) проверка шрифта (есть ли, есть ли кириллица) и установка
    /// встроенного для пользователя; 2) подстановка Segoe UI (HKLM, UAC) — если включена; 3) LOGFONT заголовков/меню/
    /// значков/сообщений через SPI_SETNONCLIENTMETRICS/SPI_SETICONMETRICS + SPIF_SENDCHANGE; 4) WM_SETTINGCHANGE/WM_FONTCHANGE.
    /// </summary>
    public List<string> ApplyFonts()
    {
        var problems = new List<string>();
        try
        {
            var f = Settings.Fonts.Clone();
            f.TitleFont = FontCheck.Ensure(f.TitleFont, problems);
            f.MenuFont = FontCheck.Ensure(f.MenuFont, problems);
            f.IconFont = FontCheck.Ensure(f.IconFont, problems);
            f.MessageFont = FontCheck.Ensure(f.MessageFont, problems);
            if (f.Enabled && f.ReplaceSystemWide && !string.IsNullOrWhiteSpace(f.SystemFont))
            {
                var sysFace = FontCheck.Ensure(f.SystemFont, problems);
                if (sysFace != null) new SystemFontApplier(System).ApplyElevated(sysFace);
            }
            System.ApplyFonts(f);
        }
        catch (Exception ex) { Log.Error("ApplyFonts", ex); problems.Add(ex.Message); }
        return problems;
    }

    /// <summary>«Починить рабочий стол»: сетка, значки, стрелки, кэш, шрифты.</summary>
    public List<string> RepairDesktop()
    {
        try { return DesktopRepair.Run(System); }
        catch (Exception ex) { Log.Error("Починка рабочего стола", ex); return new List<string> { ex.Message }; }
    }

    private void HandleIskraCommand(BuiltinItems.Cmd c)
    {
        switch (c)
        {
            case BuiltinItems.Cmd.IskraSettings: OpenSettings(); break;
            case BuiltinItems.Cmd.IskraRestore: RestoreWindows(); break;
            case BuiltinItems.Cmd.IskraNextTheme: NextTheme(); break;
            case BuiltinItems.Cmd.IskraStartScreen: ToggleStartScreen(); break;
            case BuiltinItems.Cmd.DarkToggle: ToggleDark(); break;
        }
    }

    private void ToggleDark()
    {
        // Переключаем системный тёмный/светлый режим в текущей теме.
        var t = Settings.CurrentTheme().Clone();
        t.System.AppsDark = !t.System.AppsDark; t.System.SystemDark = !t.System.SystemDark;
        var o = new SystemApplyOptions { Colors = true, Wallpaper = false, LockScreen = false, Cursors = false, Sounds = false, IconSpacing = false, WindowFrames = false, ContextMenu = false, Explorer = false, Taskbar = false, WindowMetrics = false };
        _ = System.ApplySerializedAsync(t, o, ScreenSize());
    }

    public void OnLaunch(SearchItem item, string? query)
    {
        Engine.Usage.RecordLaunch(item, query, DateTime.Now);
        SaveUsage();
    }

    public void PlaySound(Iskra.Core.Media.SoundEvent ev)
    {
        if (!Settings.SoundsEnabled) return;
        SoundPlayer.Play(Settings.CurrentTheme().Sound, ev, Settings.SoundVolume);
    }

    public bool ReduceEffects()
    {
        if (SafeMode.Active) return true;
        if (Settings.Effects == EffectsMode.Reduced) return true;
        if (Settings.Effects == EffectsMode.Full) return false;
        // Auto: учитываем системные настройки анимаций и экономию заряда.
        if (!SystemInformationHelper.ClientAnimationsEnabled()) return true;
        if (Settings.BatterySaverReducesEffects && SystemInformationHelper.BatterySaverOn()) return true;
        return false;
    }

    public void Shutdown()
    {
        SaveSettings(); SaveUsage();
        _startHook?.Dispose();
        _hotkeys.Dispose();
        DwmWatcher.Instance.Stop();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Iskra.App.Services;
using Iskra.Core.Loc;
using Iskra.Core.Settings;
using Iskra.Core.Themes;
using Microsoft.Win32;

namespace Iskra.App.Views;

internal sealed partial class SettingsWindow
{
    // ---------------- Редактор темы ----------------
    private FrameworkElement BuildEditorPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.editor")));

        // Выбираем редактируемую тему: кастомную напрямую, встроенную — через копию.
        var editable = _state.Settings.CustomThemes.FirstOrDefault(t => t.Id == _editingThemeId);
        if (editable == null)
        {
            root.Children.Add(Body(L.English ? "Pick a theme on the Themes page and press “Create my own from this one”, or duplicate below." : "Выберите тему на вкладке «Темы» и нажмите «Создать свою на основе этой», либо создайте копию ниже."));
            var picker = new ComboBox { Width = 260, FontFamily = T.UiFont, Margin = new Thickness(0, 8, 0, 8) };
            foreach (var t in BuiltInThemes.All) picker.Items.Add(new ComboBoxItem { Content = t.DisplayName(L.English), Tag = t.Id });
            picker.SelectedIndex = 0;
            root.Children.Add(picker);
            root.Children.Add(Btn(L.T("themes.duplicate"), () => { var baseId = (string)((ComboBoxItem)picker.SelectedItem).Tag; DuplicateTheme(BuiltInThemes.Get(baseId)); }, true));
            return root;
        }

        var theme = editable;
        void Live() { _state.Settings.SaveTouch(); _state.SaveSettings(); if (_state.Settings.ThemeId == theme.Id) _ = _state.ApplyThemeAsync(theme, false); }

        var nameBox = new TextBox { Text = theme.Name, Width = 280, FontFamily = T.UiFont, FontSize = 14, Margin = new Thickness(0, 0, 0, 12) };
        nameBox.TextChanged += (_, _) => { theme.Name = nameBox.Text; theme.NameEn = nameBox.Text; Live(); };
        root.Children.Add(LabeledRow(L.T("editor.name"), nameBox));

        root.Children.Add(Check(L.T("editor.dark"), theme.Dark, v => { theme.Dark = v; theme.System.AppsDark = v; theme.System.SystemDark = v; Live(); }));

        // Цвета.
        root.Children.Add(H2(L.T("editor.colors")));
        var colorGrid = new WrapPanel();
        foreach (var (prop, key) in new[] { ("Background", "color.Background"), ("Surface", "color.Surface"), ("Foreground", "color.Foreground"), ("Accent", "color.Accent"), ("Accent2", "color.Accent2"), ("Border", "color.Border") })
            colorGrid.Children.Add(ColorEditor(L.T(key), () => GetColor(theme, prop), hex => { SetColor(theme, prop, hex); Live(); }));
        root.Children.Add(colorGrid);

        // Шрифты.
        root.Children.Add(H2(L.T("editor.fonts")));
        var fonts = new[] { "Inter", "Open Sans", "Manrope", "Comfortaa", "Exo 2", "Lora", "JetBrains Mono", "Caveat", "Marck Script", "Amatic SC", "Neucha", "Pacifico", "Russo One", "Press Start 2P" };
        root.Children.Add(LabeledRow(L.T("editor.uifont"), FontCombo(fonts, theme.Fonts.Ui, f => { theme.Fonts.Ui = f; theme.System.SystemFont = f; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.titlefont"), FontCombo(fonts, theme.Fonts.Title, f => { theme.Fonts.Title = f; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.size"), Slider(10, 22, theme.Fonts.BaseSize, v => { theme.Fonts.BaseSize = v; Live(); })));

        // Форма и материал.
        root.Children.Add(H2(L.T("editor.shape")));
        root.Children.Add(LabeledRow(L.T("editor.radius"), Slider(0, 24, theme.Shape.Radius, v => { theme.Shape.Radius = v; theme.Shape.TileRadius = Math.Max(0, v - 2); Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.tile"), Slider(60, 140, theme.Shape.TileSize, v => { theme.Shape.TileSize = v; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.material"), EnumCombo<Material>(theme.Effects.Material, v => { theme.Effects.Material = v; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.bgfx"), EnumCombo<BackgroundFx>(theme.Effects.Background, v => { theme.Effects.Background = v; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.anim"), EnumCombo<OpenAnimation>(theme.Effects.Open, v => { theme.Effects.Open = v; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.sound"), EnumCombo<SoundPack>(theme.Sound, v => { theme.Sound = v; Live(); _state.PlaySound(Iskra.Core.Media.SoundEvent.Theme); })));
        root.Children.Add(LabeledRow(L.T("editor.cursor"), EnumCombo<CursorStyle>(theme.System.Cursor, v => { theme.System.Cursor = v; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.wallpaper"), EnumCombo<WallpaperKind>(theme.System.Wallpaper, v => { theme.System.Wallpaper = v; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.corners"), EnumCombo<CornerStyle>(theme.System.Corners, v => { theme.System.Corners = v; Live(); })));

        // Значки: форма (капля/круг/шестиугольник/звезда/лист/клякса/пиксель…), фон и применение к Windows.
        root.Children.Add(H2(L.T("editor.icons")));
        root.Children.Add(LabeledRow(L.T("editor.iconshape"), EnumCombo<IconShape>(theme.Icon.Shape, v => { theme.Icon.Shape = v; Live(); })));
        root.Children.Add(LabeledRow(L.T("editor.iconfill"), EnumCombo<IconFill>(theme.Icon.Fill, v => { theme.Icon.Fill = v; Live(); })));
        root.Children.Add(Check(L.T("editor.iconsystem"), theme.Icon.ApplySystemWide, v => { theme.Icon.ApplySystemWide = v; Live(); }));

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        buttons.Children.Add(Btn(L.T("editor.save"), () => { _ = _state.ApplyThemeAsync(theme, _state.Settings.ApplyThemeToWindows); }, true));
        buttons.Children.Add(Btn(L.T("themes.export"), () => ExportTheme(theme)));
        root.Children.Add(buttons);
        return root;
    }

    private FrameworkElement LabeledRow(string label, FrameworkElement control)
    {
        var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var l = new TextBlock { Text = label, FontFamily = T.UiFont, FontSize = 13, Foreground = T.Foreground, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(l, 0); Grid.SetColumn(control, 1);
        g.Children.Add(l); g.Children.Add(control);
        return g;
    }

    private FrameworkElement ColorEditor(string label, Func<string> get, Action<string> set)
    {
        var sp = new StackPanel { Margin = new Thickness(0, 0, 16, 10), Width = 150 };
        var swatch = new Border { Height = 32, CornerRadius = new CornerRadius(6), Background = new SolidColorBrush(Theming.Col(get())), BorderBrush = T.BorderBrush, BorderThickness = new Thickness(1), Cursor = Cursors.Hand };
        var box = new TextBox { Text = get(), FontFamily = T.UiFont, FontSize = 11, Margin = new Thickness(0, 4, 0, 0) };
        box.TextChanged += (_, _) => { if (Iskra.Core.Windows.Argb.TryParse(box.Text, out _)) { set(box.Text); swatch.Background = new SolidColorBrush(Theming.Col(box.Text)); } };
        sp.Children.Add(new TextBlock { Text = label, FontFamily = T.UiFont, FontSize = 11, Foreground = T.ForegroundDim });
        sp.Children.Add(swatch); sp.Children.Add(box);
        return sp;
    }

    private static string GetColor(ThemeDefinition t, string prop) => typeof(ThemePalette).GetProperty(prop)!.GetValue(t.Palette) as string ?? "#FF000000";
    private static void SetColor(ThemeDefinition t, string prop, string hex) { typeof(ThemePalette).GetProperty(prop)!.SetValue(t.Palette, hex); if (prop == "Accent") t.System.Accent = hex; }

    private ComboBox FontCombo(string[] fonts, string value, Action<string> onChange)
    {
        var cb = new ComboBox { Width = 260, FontFamily = T.UiFont };
        foreach (var f in fonts) cb.Items.Add(new ComboBoxItem { Content = new TextBlock { Text = f, FontFamily = Theming.Font(f), FontSize = 14 }, Tag = f });
        cb.SelectedIndex = Math.Max(0, Array.IndexOf(fonts, value));
        cb.SelectionChanged += (_, _) => onChange((string)((ComboBoxItem)cb.SelectedItem).Tag);
        return cb;
    }

    private ComboBox EnumCombo<TEnum>(TEnum value, Action<TEnum> onChange) where TEnum : struct, Enum
    {
        var cb = new ComboBox { Width = 260, FontFamily = T.UiFont };
        foreach (var v in Enum.GetValues<TEnum>()) cb.Items.Add(new ComboBoxItem { Content = v.ToString(), Tag = v });
        cb.SelectedItem = cb.Items.Cast<ComboBoxItem>().FirstOrDefault(i => i.Tag!.Equals(value));
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is ComboBoxItem ci) onChange((TEnum)ci.Tag!); };
        return cb;
    }

    private Slider Slider(double min, double max, double value, Action<double> onChange)
    {
        var s = new Slider { Minimum = min, Maximum = max, Value = value, Width = 260, HorizontalAlignment = HorizontalAlignment.Left };
        s.ValueChanged += (_, e) => onChange(e.NewValue);
        return s;
    }

    private void ExportTheme(ThemeDefinition t)
    {
        var dlg = new SaveFileDialog { Filter = "Тема Искры (*.json)|*.json", FileName = t.Name + ".json" };
        if (dlg.ShowDialog() == true) try { System.IO.File.WriteAllText(dlg.FileName, t.ToJson()); } catch (Exception ex) { Log.Error("Экспорт темы", ex); }
    }

    // ---------------- Пуск и панель задач ----------------
    private FrameworkElement BuildStartPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.start")));
        root.Children.Add(Check(L.T("start.replace"), _state.Settings.ReplaceStartMenu, v => { _state.Settings.ReplaceStartMenu = v; _state.SaveSettings(); _state.RegisterHotkeys(); RestartHook(); }));
        root.Children.Add(Check(L.T("start.button"), _state.Settings.InterceptStartButton, v => Save(() => _state.Settings.InterceptStartButton = v)));
        root.Children.Add(Body(L.T("start.style")));

        root.Children.Add(H2(L.T("dock.mode")));
        foreach (var mode in Enum.GetValues<DockMode>())
        {
            var rb = new RadioButton { Content = L.T("dock." + mode), IsChecked = _state.Settings.Dock == mode, Foreground = T.Foreground, FontFamily = T.UiFont, Margin = new Thickness(0, 4, 0, 4), GroupName = "dock" };
            var m = mode; rb.Checked += (_, _) => { _state.Settings.Dock = m; _state.SaveSettings(); };
            root.Children.Add(rb);
        }
        root.Children.Add(Check(L.T("dock.followTheme"), _state.Settings.DockFollowsTheme, v => Save(() => _state.Settings.DockFollowsTheme = v)));
        return root;
    }

    private void RestartHook()
    {
        // Переустановить хук «Пуска» при изменении настройки.
        _state.GetType(); // хук пересоздаётся в AppState через SetupStartHook при следующем старте; здесь просто сохраняем.
    }

    // ---------------- Поиск ----------------
    private FrameworkElement BuildSearchPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.search")));
        root.Children.Add(Check(L.T("search.files"), _state.Settings.FileSearchEnabled, v => { _state.Settings.FileSearchEnabled = v; _state.SaveSettings(); _state.IndexFilesAsync(); }));

        var foldersPanel = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
        void RefreshFolders()
        {
            foldersPanel.Children.Clear();
            foreach (var f in _state.Settings.FileSearchFolders.DefaultIfEmpty_())
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 2) };
                row.Children.Add(new TextBlock { Text = f, FontFamily = T.UiFont, FontSize = 12.5, Foreground = T.ForegroundDim, VerticalAlignment = VerticalAlignment.Center, Width = 440, TextTrimming = TextTrimming.CharacterEllipsis });
                var del = Btn(L.T("search.removeFolder"), () => { _state.Settings.FileSearchFolders.Remove(f); _state.SaveSettings(); RefreshFolders(); _state.IndexFilesAsync(); });
                del.Margin = new Thickness(8, 0, 0, 0);
                row.Children.Add(del);
                foldersPanel.Children.Add(row);
            }
        }
        if (_state.Settings.FileSearchFolders.Count == 0) _state.Settings.FileSearchFolders = FileIndexer.DefaultFolders();
        RefreshFolders();
        root.Children.Add(foldersPanel);
        root.Children.Add(Btn(L.T("search.addFolder"), () =>
        {
            var dlg = new OpenFolderDialog { Title = L.T("search.addFolder") };
            if (dlg.ShowDialog() == true) { _state.Settings.FileSearchFolders.Add(dlg.FolderName); _state.SaveSettings(); RefreshFolders(); _state.IndexFilesAsync(); }
        }));

        root.Children.Add(H2(L.T("search.engine")));
        var engine = new ComboBox { Width = 260, FontFamily = T.UiFont };
        foreach (var kv in AppSettings.WebEngines) engine.Items.Add(new ComboBoxItem { Content = kv.Value.Name, Tag = kv.Key });
        engine.SelectedItem = engine.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (string)i.Tag == _state.Settings.WebSearchEngine);
        engine.SelectionChanged += (_, _) => { if (engine.SelectedItem is ComboBoxItem ci) { _state.Settings.WebSearchEngine = (string)ci.Tag; _state.SaveSettings(); _state.LoadSettings(); } };
        root.Children.Add(engine);

        var status = new TextBlock { FontFamily = T.UiFont, FontSize = 12, Foreground = T.ForegroundDim, Margin = new Thickness(0, 12, 0, 0) };
        status.Text = L.F("search.indexed", _state.AppCount, _state.FileCount);
        _state.IndexUpdated += (a, f) => Dispatcher.BeginInvoke(() => status.Text = L.F("search.indexed", a, f));
        root.Children.Add(status);
        root.Children.Add(Btn(L.T("search.reindex"), () => _state.IndexAsync()));
        return root;
    }

    // ---------------- Клавиши ----------------
    private FrameworkElement BuildKeysPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.keys")));
        root.Children.Add(HotkeyRow(L.T("keys.launcher"), _state.Settings.Hotkey, hk => { _state.Settings.Hotkey = hk; _state.SaveSettings(); _state.RegisterHotkeys(); }));
        root.Children.Add(HotkeyRow(L.T("keys.startscreen"), _state.Settings.StartScreenHotkey, hk => { _state.Settings.StartScreenHotkey = hk; _state.SaveSettings(); _state.RegisterHotkeys(); }));
        return root;
    }

    private FrameworkElement HotkeyRow(string label, string current, Action<string> onSet)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 6) };
        sp.Children.Add(new TextBlock { Text = label, FontFamily = T.UiFont, FontSize = 13, Foreground = T.Foreground, Width = 190, VerticalAlignment = VerticalAlignment.Center });
        var box = new TextBox { Text = current, Width = 200, FontFamily = T.UiFont, IsReadOnly = true, Cursor = Cursors.Hand };
        var status = new TextBlock { FontFamily = T.UiFont, FontSize = 12, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        box.GotFocus += (_, _) => { box.Text = L.T("hotkey.press"); };
        box.PreviewKeyDown += (s, e) =>
        {
            e.Handled = true;
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
            var mods = Keyboard.Modifiers;
            var parts = new List<string>();
            if (mods.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
            if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            parts.Add(key.ToString());
            var text = string.Join("+", parts);
            if (Iskra.Core.Settings.Hotkey.TryParse(text, out var hk))
            {
                var conflict = Iskra.Core.Settings.Hotkey.KnownSystemConflict(hk);
                if (conflict != null) { status.Text = L.F("hotkey.conflict", hk.ToString()); status.Foreground = Brushes.OrangeRed; box.Text = hk.ToString(); return; }
                if (!_state.IsHotkeyFree(hk)) { status.Text = L.F("hotkey.conflict", hk.ToString()); status.Foreground = Brushes.OrangeRed; box.Text = hk.ToString(); return; }
                box.Text = hk.ToString(); status.Text = L.T("hotkey.ok"); status.Foreground = T.Accent;
                onSet(hk.ToString());
            }
        };
        sp.Children.Add(box); sp.Children.Add(status);
        return sp;
    }

    // ---------------- Эффекты и звук ----------------
    private FrameworkElement BuildEffectsPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.effects")));
        root.Children.Add(H2(L.T("effects.mode")));
        foreach (var m in Enum.GetValues<EffectsMode>())
        {
            var rb = new RadioButton { Content = L.T("effects." + m), IsChecked = _state.Settings.Effects == m, Foreground = T.Foreground, FontFamily = T.UiFont, Margin = new Thickness(0, 4, 0, 4), GroupName = "fx" };
            var mm = m; rb.Checked += (_, _) => { _state.Settings.Effects = mm; _state.SaveSettings(); };
            root.Children.Add(rb);
        }
        root.Children.Add(Check(L.T("effects.battery"), _state.Settings.BatterySaverReducesEffects, v => Save(() => _state.Settings.BatterySaverReducesEffects = v)));
        root.Children.Add(H2(L.T("effects.sounds")));
        root.Children.Add(Check(L.T("effects.sounds"), _state.Settings.SoundsEnabled, v => Save(() => _state.Settings.SoundsEnabled = v)));
        root.Children.Add(LabeledRow(L.T("effects.volume"), Slider(0, 1, _state.Settings.SoundVolume, v => { _state.Settings.SoundVolume = v; _state.SaveSettings(); })));
        root.Children.Add(Body(L.F("effects.refresh", (int)DisplayHz.Get())));
        return root;
    }

    // ---------------- Общие ----------------
    private FrameworkElement BuildGeneralPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.general")));
        root.Children.Add(H2(L.T("general.language")));
        var lang = new ComboBox { Width = 200, FontFamily = T.UiFont };
        lang.Items.Add(new ComboBoxItem { Content = "Русский", Tag = "ru" });
        lang.Items.Add(new ComboBoxItem { Content = "English", Tag = "en" });
        lang.SelectedIndex = _state.Settings.Language == "en" ? 1 : 0;
        lang.SelectionChanged += (_, _) => { _state.Settings.Language = (string)((ComboBoxItem)lang.SelectedItem).Tag; L.English = _state.Settings.Language == "en"; _state.SaveSettings(); _state.LoadSettings(); Build(); NavigateTo("general"); };
        root.Children.Add(lang);
        root.Children.Add(Check(L.T("general.autostart"), _state.Settings.Autostart, v => { _state.Settings.Autostart = v; _state.SaveSettings(); Autostart.Set(v, AppInfo.ExePath); }));
        root.Children.Add(Check(L.T("general.recent"), _state.Settings.ShowRecent, v => Save(() => _state.Settings.ShowRecent = v)));
        root.Children.Add(Btn(L.T("about.data"), () => Launcher.RevealInExplorer(Paths.Root)));
        return root;
    }

    private FrameworkElement BuildAboutPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1("✦ " + L.T("app.name") + " " + AppInfo.Version));
        root.Children.Add(Body(L.F("about.text", AppInfo.Version)));
        root.Children.Add(Btn(L.T("about.data"), () => Launcher.RevealInExplorer(Paths.Root)));
        return root;
    }
}

internal static class SettingsExt
{
    public static IEnumerable<string> DefaultIfEmpty_(this List<string> list) => list;
    public static void SaveTouch(this AppSettings s) { }
}

internal static class DisplayHz
{
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool EnumDisplaySettings(string? dev, int mode, ref DEVMODE dm);
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private struct DEVMODE { [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName; public ushort a, b; public uint c; public int d, e, f, g; public short h, i, j, k; [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName; public ushort l, m; public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency; public uint rest1, rest2, rest3, rest4, rest5; }
    public static double Get()
    {
        try { var dm = new DEVMODE(); dm.dmDeviceName = ""; dm.dmFormName = ""; if (EnumDisplaySettings(null, -1, ref dm) && dm.dmDisplayFrequency > 1) return dm.dmDisplayFrequency; } catch { }
        return 60;
    }
}

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
    // ---------------- Темы ----------------
    private FrameworkElement BuildThemesPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.themes")));
        var applyWin = Check(L.T("themes.applyWindows"), _state.Settings.ApplyThemeToWindows, v => { _state.Settings.ApplyThemeToWindows = v; _state.SaveSettings(); });
        root.Children.Add(applyWin);

        var gallery = new WrapPanel { Margin = new Thickness(0, 12, 0, 0) };
        foreach (var theme in _state.Settings.AllThemes())
        {
            var captured = theme;
            var card = BuildThemeCard(theme, () => _ = _state.ApplyThemeAsync(captured, _state.Settings.ApplyThemeToWindows), () => { _nav.Tag = captured.Id; _editingThemeId = captured.Id; NavigateTo("editor"); });
            gallery.Children.Add(card);
        }
        root.Children.Add(gallery);

        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        row.Children.Add(Btn(L.T("themes.import"), ImportTheme));
        root.Children.Add(row);
        return root;
    }

    private Border BuildThemeCard(ThemeDefinition theme, Action apply, Action edit)
    {
        bool current = theme.Id == _state.Settings.ThemeId;
        var preview = new Border
        {
            Width = 220, Height = 130, CornerRadius = new CornerRadius(theme.Shape.Radius + 2), ClipToBounds = true,
            BorderBrush = current ? T.Accent : new SolidColorBrush(Theming.Col(theme.Palette.Border)),
            BorderThickness = new Thickness(current ? 2.5 : 1),
        };
        var g = new Grid();
        var vis = new System.Windows.Controls.Image { Stretch = Stretch.UniformToFill };
        try
        {
            var dv = new System.Windows.Media.DrawingVisual();
            using (var dc = dv.RenderOpen()) WallpaperPainter.Paint(dc, theme, 220, 130, 0);
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(220, 130, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(dv); rtb.Freeze(); vis.Source = rtb;
        }
        catch { g.Background = new SolidColorBrush(Theming.Col(theme.Palette.Background)); }
        g.Children.Add(vis);

        // Мини-строка поиска для предпросмотра стиля.
        var mini = new Border
        {
            Background = new SolidColorBrush(Theming.Col(theme.Palette.Background) with { A = (byte)(theme.Palette.BackgroundOpacity * 255) }),
            CornerRadius = new CornerRadius(theme.Shape.Radius), Margin = new Thickness(14, 0, 14, 0), Padding = new Thickness(10, 6, 10, 6),
            VerticalAlignment = VerticalAlignment.Center, Height = 34,
        };
        mini.Child = new TextBlock { Text = "A | Поиск", FontFamily = Theming.Font(theme.Fonts.Ui), FontSize = 13, Foreground = new SolidColorBrush(Theming.Col(theme.Palette.Foreground)) };
        g.Children.Add(mini);
        preview.Child = g;

        var name = new TextBlock { Text = theme.DisplayName(L.English), FontFamily = T.UiFont, FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = T.Foreground, Margin = new Thickness(2, 8, 0, 0) };
        var desc = new TextBlock { Text = theme.DisplayDescription(L.English), FontFamily = T.UiFont, FontSize = 11, Foreground = T.ForegroundDim, TextWrapping = TextWrapping.Wrap, MaxWidth = 220, Height = 44 };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Btn(current ? L.T("themes.current") : L.T("themes.apply"), apply, !current));
        buttons.Children.Add(Btn(L.T("themes.duplicate"), () => DuplicateTheme(theme)));
        if (!theme.BuiltIn) buttons.Children.Add(Btn(L.T("themes.delete"), () => DeleteTheme(theme)));

        var panel = new StackPanel { Margin = new Thickness(0, 0, 16, 20), Width = 224 };
        panel.Children.Add(preview); panel.Children.Add(name); panel.Children.Add(desc); panel.Children.Add(buttons);
        return new Border { Child = panel };
    }

    private void DuplicateTheme(ThemeDefinition t)
    {
        var clone = t.Clone();
        clone.Id = "custom-" + Guid.NewGuid().ToString("N")[..8];
        clone.Name = t.Name + " (копия)";
        clone.NameEn = t.NameEn + " (copy)";
        clone.BuiltIn = false; clone.BaseThemeId = t.Id;
        _state.Settings.CustomThemes.Add(clone);
        _state.SaveSettings();
        _editingThemeId = clone.Id;
        NavigateTo("editor");
    }

    private void DeleteTheme(ThemeDefinition t)
    {
        _state.Settings.CustomThemes.RemoveAll(x => x.Id == t.Id);
        if (_state.Settings.ThemeId == t.Id) _ = _state.ApplyThemeAsync(BuiltInThemes.All[0], false);
        _state.SaveSettings();
        NavigateTo("themes");
    }

    private void ImportTheme()
    {
        var dlg = new OpenFileDialog { Filter = "Тема Искры (*.json)|*.json", Title = L.T("themes.import") };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var theme = ThemeDefinition.FromJson(System.IO.File.ReadAllText(dlg.FileName));
            if (theme == null) return;
            theme.BuiltIn = false;
            if (string.IsNullOrEmpty(theme.Id) || _state.Settings.AllThemes().Any(x => x.Id == theme.Id)) theme.Id = "custom-" + Guid.NewGuid().ToString("N")[..8];
            _state.Settings.CustomThemes.Add(theme);
            _state.SaveSettings();
            NavigateTo("themes");
        }
        catch (Exception ex) { Log.Error("Импорт темы", ex); }
    }

    // ---------------- Оформление Windows ----------------
    private FrameworkElement BuildWindowsPage()
    {
        var root = new StackPanel();
        root.Children.Add(H1(L.T("nav.windows")));
        root.Children.Add(Body(L.T("win.intro")));

        var o = _state.Settings.SystemOptions;
        root.Children.Add(H2(L.T("themes.applyWindows")));
        root.Children.Add(Check(L.T("win.opt.Colors"), o.Colors, v => Save(() => o.Colors = v)));
        root.Children.Add(Check(L.T("win.opt.Wallpaper"), o.Wallpaper, v => Save(() => o.Wallpaper = v)));
        root.Children.Add(Check(L.T("win.opt.LockScreen"), o.LockScreen, v => Save(() => o.LockScreen = v)));
        root.Children.Add(Check(L.T("win.opt.Cursors"), o.Cursors, v => Save(() => o.Cursors = v)));
        root.Children.Add(Check(L.T("win.opt.Sounds"), o.Sounds, v => Save(() => o.Sounds = v)));
        root.Children.Add(Check(L.T("win.opt.IconSpacing"), o.IconSpacing, v => Save(() => o.IconSpacing = v)));
        root.Children.Add(Check(L.T("win.opt.WindowFrames"), o.WindowFrames, v => Save(() => o.WindowFrames = v)));
        root.Children.Add(Check(L.T("win.opt.ContextMenu"), o.ContextMenu, v => Save(() => o.ContextMenu = v)));
        root.Children.Add(Check(L.T("win.opt.Explorer"), o.Explorer, v => Save(() => o.Explorer = v)));
        root.Children.Add(Check(L.T("win.opt.Taskbar"), o.Taskbar, v => Save(() => o.Taskbar = v)));

        // Рабочий стол: плотность сетки, стрелки ярлыков, починка.
        root.Children.Add(H2(L.T("desk.title")));
        var grid = new ComboBox { Width = 260, FontFamily = T.UiFont };
        foreach (var (val, key) in new[] { (IconSpacing.Compact, "desk.compact"), (IconSpacing.Normal, "desk.normal"), (IconSpacing.Wide, "desk.wide") })
            grid.Items.Add(new ComboBoxItem { Content = L.T(key), Tag = val });
        grid.SelectedItem = grid.Items.Cast<ComboBoxItem>().FirstOrDefault(i => (IconSpacing)i.Tag! == _state.Settings.DesktopGrid) ?? grid.Items[1];
        grid.SelectionChanged += (_, _) =>
        {
            if (grid.SelectedItem is not ComboBoxItem ci) return;
            _state.Settings.DesktopGrid = (IconSpacing)ci.Tag!; _state.SaveSettings();
            var px = Iskra.Core.Windows.SystemPlan.IconSpacingPx(_state.Settings.DesktopGrid)!.Value;
            System.Threading.Tasks.Task.Run(() => DesktopGrid.Apply(px.H, px.V)); // вживую, без перезапуска
        };
        root.Children.Add(LabeledRow(L.T("desk.grid"), grid));
        root.Children.Add(Check(L.T("desk.arrows"), _state.Settings.HideShortcutArrows, v => { _state.Settings.HideShortcutArrows = v; _state.SaveSettings(); }));
        root.Children.Add(Body(L.T("desk.limits")));
        root.Children.Add(Btn(L.T("desk.repair"), () =>
        {
            _winStatus.Text = L.T("desk.repairing");
            System.Threading.Tasks.Task.Run(() =>
            {
                var done = _state.RepairDesktop();
                Dispatcher.Invoke(() => _winStatus.Text = L.T("desk.repaired") + " " + string.Join(", ", done));
            });
        }, true));

        // Экспериментальный системный шрифт.
        root.Children.Add(H2("⚠ " + L.T("win.opt.SystemFontExperimental")));
        root.Children.Add(Body(L.T("win.fontWarn")));
        var fontCb = Check(L.T("win.opt.SystemFontExperimental"), o.SystemFontExperimental, v =>
        {
            if (v) ApplySystemFont(); else RevertSystemFont();
        });
        root.Children.Add(fontCb);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
        actions.Children.Add(Btn(L.T("win.applyNow"), ApplyToWindows, true));
        actions.Children.Add(Btn(L.T("win.restore"), RestoreWindows));
        actions.Children.Add(Btn(L.T("win.script"), () => Launcher.RevealInExplorer(Paths.RestoreScript)));
        root.Children.Add(actions);

        _winStatus = new TextBlock { FontFamily = T.UiFont, FontSize = 12.5, Foreground = T.Accent, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
        root.Children.Add(_winStatus);

        root.Children.Add(H2(L.T("win.limits")));
        root.Children.Add(Body(L.T("win.limitsText")));
        return root;
    }

    private TextBlock _winStatus = null!;
    private string? _editingThemeId;

    private void Save(Action mutate) { mutate(); _state.SaveSettings(); }

    private async void ApplyToWindows()
    {
        _winStatus.Text = "…";
        var theme = _state.Settings.CurrentTheme();
        var report = await _state.System.ApplyAsync(theme, _state.Settings.SystemOptions, _state.ScreenSize());
        _winStatus.Text = L.F("win.applied", theme.DisplayName(L.English), string.Join(", ", report.Changed));
        if (_state.Settings.SystemOptions.ContextMenu && report.Changed.Count > 0)
        {
            var mb = MessageBox.Show(L.T("win.needExplorer"), L.T("app.name"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (mb == MessageBoxResult.Yes) Services.SystemCommands.Run(BuiltinItems.Cmd.RestartExplorer);
        }
    }

    private void RestoreWindows()
    {
        if (!_state.System.HasSnapshot && !SystemThemeApplier.FontRepairNeeded()) { _winStatus.Text = L.T("win.restoreNone"); return; }
        _winStatus.Text = L.T("win.restoring");
        System.Threading.Tasks.Task.Run(() =>
        {
            var outcome = _state.RestoreWindowsDetailed();
            Dispatcher.Invoke(() =>
            {
                if (outcome.NeededHklm && !outcome.Elevated)
                    _winStatus.Text = L.T("win.restoreNeedsAdmin");
                else
                    _winStatus.Text = L.T("win.restoreDone") + (outcome.Errors > 0 ? $" ({outcome.Errors})" : "");
            });
        });
    }

    private void ApplySystemFont()
    {
        var theme = _state.Settings.CurrentTheme();
        var family = theme.System.SystemFont ?? theme.Fonts.Ui;
        var applier = new SystemFontApplier(_state.System);
        bool ok = applier.ApplyElevated(family);
        _state.Settings.SystemOptions.SystemFontExperimental = ok;
        _state.Settings.FontSubstitutionAppliedUtc = ok ? DateTime.UtcNow : null;
        _state.SaveSettings();
        _winStatus.Text = ok ? L.T("editor.saved") : L.T("admin.denied");
        if (ok) ShowFontConfirmDialog();
    }

    private void RevertSystemFont()
    {
        var applier = new SystemFontApplier(_state.System);
        applier.RevertElevated();
        _state.Settings.SystemOptions.SystemFontExperimental = false;
        _state.Settings.FontSubstitutionAppliedUtc = null;
        _state.SaveSettings();
        _winStatus.Text = L.T("win.restoreDone");
    }

    private void ShowFontConfirmDialog()
    {
        var dlg = new Window
        {
            Width = 420, Height = 220, WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
            Title = L.T("app.name"), WindowStyle = WindowStyle.ToolWindow, ResizeMode = ResizeMode.NoResize,
            Background = new SolidColorBrush(T.Background),
        };
        var sp = new StackPanel { Margin = new Thickness(20) };
        sp.Children.Add(new TextBlock { Text = L.T("win.fontConfirm"), FontFamily = T.UiFont, FontSize = 15, Foreground = T.Foreground, TextWrapping = TextWrapping.Wrap });
        var countdown = new TextBlock { FontFamily = T.UiFont, FontSize = 12, Foreground = T.ForegroundDim, Margin = new Thickness(0, 12, 0, 12) };
        sp.Children.Add(countdown);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        int left = 300;
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        buttons.Children.Add(Btn(L.T("win.fontKeep"), () => { timer.Stop(); dlg.Close(); }, true));
        buttons.Children.Add(Btn(L.T("win.fontRevert"), () => { timer.Stop(); RevertSystemFont(); dlg.Close(); }));
        sp.Children.Add(buttons);
        dlg.Content = sp;
        timer.Tick += (_, _) => { left--; countdown.Text = L.F("win.fontRevertIn", left); if (left <= 0) { timer.Stop(); RevertSystemFont(); dlg.Close(); } };
        countdown.Text = L.F("win.fontRevertIn", left);
        timer.Start();
        dlg.ShowDialog();
    }
}

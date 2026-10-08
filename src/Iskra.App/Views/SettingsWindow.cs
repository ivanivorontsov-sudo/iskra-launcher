using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using Iskra.App.Services;
using Iskra.Core.Loc;
using Iskra.Core.Settings;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Окно настроек: темы, оформление Windows, редактор темы, пуск/панель, поиск, клавиши, эффекты, общие, о программе.</summary>
internal sealed partial class SettingsWindow : Window
{
    private readonly AppState _state;
    private ListBox _nav = null!;
    private ContentControl _host = null!;
    private readonly Dictionary<string, Func<FrameworkElement>> _pages;

    public SettingsWindow(AppState state)
    {
        _state = state;
        Width = 1040; Height = 720; MinWidth = 820; MinHeight = 560;
        Title = L.T("settings.title");
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _pages = new()
        {
            ["themes"] = BuildThemesPage,
            ["editor"] = BuildEditorPage,
            ["windows"] = BuildWindowsPage,
            ["start"] = BuildStartPage,
            ["search"] = BuildSearchPage,
            ["keys"] = BuildKeysPage,
            ["effects"] = BuildEffectsPage,
            ["general"] = BuildGeneralPage,
            ["about"] = BuildAboutPage,
        };
        Build();
        _state.ThemeChanged += () => { if (IsLoaded) { ApplyWindowChrome(); } };
        Loaded += (_, _) => ApplyWindowChrome();
    }

    private Theming T => _state.Theming;

    private void ApplyWindowChrome()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero) WindowStyler.ApplyToHwnd(hwnd, _state.Settings.CurrentTheme(), _state.ReduceEffects());
        Background = (SolidColorBrush)T.WindowOpaque;
    }

    private void Build()
    {
        Background = (SolidColorBrush)T.WindowOpaque;
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _nav = new ListBox { BorderThickness = new Thickness(0), Background = new SolidColorBrush(T.Background2), Padding = new Thickness(8, 16, 8, 8) };
        var navItems = new (string key, string glyph)[]
        {
            ("themes", "\uE790"), ("editor", "\uE70F"), ("windows", "\uE770"), ("start", "\uE71D"),
            ("search", "\uE721"), ("keys", "\uE765"), ("effects", "\uE9E9"), ("general", "\uE713"), ("about", "\uE946"),
        };
        foreach (var (key, glyph) in navItems)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(6) };
            sp.Children.Add(UiKit.Glyph(glyph, 18, T.ForegroundDim));
            sp.Children.Add(new TextBlock { Text = L.T("nav." + key), FontFamily = T.UiFont, FontSize = 14, Foreground = T.Foreground, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            _nav.Items.Add(new ListBoxItem { Content = sp, Tag = key, Padding = new Thickness(4) });
        }
        _nav.SelectionChanged += (_, _) => { if (_nav.SelectedItem is ListBoxItem li) Navigate((string)li.Tag); };

        var titleBar = new TextBlock { Text = "✦ " + L.T("app.name"), FontFamily = T.TitleFont, FontSize = 20, FontWeight = FontWeights.Bold, Foreground = T.Accent, Margin = new Thickness(16, 12, 0, 8) };
        var left = new DockPanel();
        DockPanel.SetDock(titleBar, Dock.Top);
        left.Children.Add(titleBar); left.Children.Add(_nav);
        Grid.SetColumn(left, 0);

        _host = new ContentControl { Margin = new Thickness(0) };
        Grid.SetColumn(_host, 1);
        grid.Children.Add(left); grid.Children.Add(_host);
        Content = grid;
        _nav.SelectedIndex = 0;
    }

    public void NavigateTo(string key)
    {
        for (int i = 0; i < _nav.Items.Count; i++)
            if (_nav.Items[i] is ListBoxItem li && (string)li.Tag == key) { _nav.SelectedIndex = i; return; }
    }

    private void Navigate(string key)
    {
        if (_pages.TryGetValue(key, out var builder))
        {
            var page = builder();
            var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(28, 24, 28, 24) };
            _host.Content = scroll;
            UiKit.FadeIn(page, _state);
        }
    }

    // ---- Общие виджеты ----
    private TextBlock H1(string text) => new() { Text = text, FontFamily = T.TitleFont, FontSize = 24, FontWeight = FontWeights.Bold, Foreground = T.Foreground, Margin = new Thickness(0, 0, 0, 16) };
    private TextBlock H2(string text) => new() { Text = text, FontFamily = T.UiFont, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = T.Foreground, Margin = new Thickness(0, 16, 0, 8) };
    private TextBlock Body(string text) => new() { Text = text, FontFamily = T.UiFont, FontSize = 13, Foreground = T.ForegroundDim, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) };

    private CheckBox Check(string text, bool value, Action<bool> onChange)
    {
        var cb = new CheckBox { Content = text, IsChecked = value, Foreground = T.Foreground, FontFamily = T.UiFont, FontSize = 13, Margin = new Thickness(0, 5, 0, 5) };
        cb.Checked += (_, _) => onChange(true);
        cb.Unchecked += (_, _) => onChange(false);
        return cb;
    }

    private Button Btn(string text, Action onClick, bool primary = false)
    {
        var b = new Button
        {
            Content = text, FontFamily = T.UiFont, FontSize = 13, Padding = new Thickness(16, 8, 16, 8), Margin = new Thickness(0, 8, 8, 0),
            Background = primary ? T.Accent : T.Surface, Foreground = primary ? T.AccentForeground : T.Foreground,
            BorderThickness = new Thickness(0), Cursor = Cursors.Hand,
        };
        b.Click += (_, _) => onClick();
        return b;
    }
}

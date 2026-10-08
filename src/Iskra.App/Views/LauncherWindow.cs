using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Iskra.App.Services;
using Iskra.Core.Loc;
using Iskra.Core.Media;
using Iskra.Core.Search;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Главное окно-поиск: вызывается горячей клавишей, мгновенный поиск, клавиатурная навигация, запуск.</summary>
internal sealed class LauncherWindow : Window
{
    private readonly AppState _state;
    private TextBox _search = null!;
    private StackPanel _list = null!;
    private ScrollViewer _scroll = null!;
    private Border _card = null!;
    private Grid _bgLayer = null!;
    private TextBlock _hint = null!;
    private readonly List<ResultRow> _rows = new();
    private int _selected = -1;
    private bool _startMode;
    private IReadOnlyList<SearchResult> _current = Array.Empty<SearchResult>();
    private readonly DispatcherTimer _debounce;
    private bool _confirmPending;

    public LauncherWindow(AppState state)
    {
        _state = state;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = false; // нужен DWM-материал
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Width = 720; SizeToContent = SizeToContent.Height;
        Title = "Искра";
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(55) };
        _debounce.Tick += (_, _) => { _debounce.Stop(); RunSearch(); };
        _state.ThemeChanged += () => { if (IsVisible) BuildChrome(); };
        BuildChrome();
        Deactivated += (_, _) => { if (IsVisible && !_state.Settings.FirstRunDone == false) HideAnimated(); };
        PreviewKeyDown += OnKeyDown;
    }

    private Theming T => _state.Theming;

    private void BuildChrome()
    {
        var reduce = _state.ReduceEffects();
        bool backdrop = BackdropCaps.Available(reduce);
        var theme = _state.Settings.CurrentTheme();

        // Если материал DWM не рисуется — окно должно быть непрозрачным, иначе текст «пропадает» на стекле.
        Background = backdrop ? Brushes.Transparent : T.WindowOpaque;

        _bgLayer = new Grid();
        var bgEffect = new BackgroundFxControl(_state) { IsHitTestVisible = false };
        _bgLayer.Children.Add(bgEffect);

        _search = new TextBox
        {
            FontSize = theme.Fonts.SearchSize,
            FontFamily = T.UiFont,
            Foreground = T.Foreground,
            CaretBrush = T.Accent,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(6),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _search.TextChanged += (_, _) => { _confirmPending = false; _debounce.Stop(); _debounce.Start(); };

        var searchGlyph = UiKit.Glyph("\uE721", theme.Fonts.SearchSize, T.ForegroundDim);
        searchGlyph.Margin = new Thickness(18, 0, 10, 0);

        var placeholder = new TextBlock
        {
            Text = L.T("search.placeholder"), FontFamily = T.UiFont, FontSize = theme.Fonts.SearchSize,
            Foreground = T.ForegroundDim, IsHitTestVisible = false, VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 0, 0),
        };
        placeholder.SetBinding(VisibilityProperty, new System.Windows.Data.Binding("Text.IsEmpty") { Source = _search });
        _search.TextChanged += (_, _) => placeholder.Visibility = string.IsNullOrEmpty(_search.Text) ? Visibility.Visible : Visibility.Collapsed;

        var searchGrid = new Grid();
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(searchGlyph, 0); Grid.SetColumn(_search, 1);
        var phHost = new Grid(); Grid.SetColumn(phHost, 1); phHost.Children.Add(placeholder);
        searchGrid.Children.Add(searchGlyph); searchGrid.Children.Add(phHost); searchGrid.Children.Add(_search);

        var searchBox = new Border
        {
            Height = 64, Child = searchGrid,
            CornerRadius = T.Corner,
        };

        _list = new StackPanel { Margin = new Thickness(8, 6, 8, 8) };
        _scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = _list, MaxHeight = 460 };

        _hint = new TextBlock
        {
            Text = L.T("search.hint"), FontFamily = T.UiFont, FontSize = 11.5, Foreground = T.ForegroundDim,
            Margin = new Thickness(18, 2, 18, 10), TextTrimming = TextTrimming.CharacterEllipsis,
        };

        var content = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(searchBox, Dock.Top);
        DockPanel.SetDock(_hint, Dock.Bottom);
        content.Children.Add(searchBox);
        content.Children.Add(_hint);
        content.Children.Add(_scroll);

        var root = new Grid();
        root.Children.Add(_bgLayer);
        root.Children.Add(new Border { Child = content, Padding = new Thickness(6) });

        _card = new Border
        {
            Background = T.WindowBackground(reduce, backdrop),
            CornerRadius = new CornerRadius(theme.Shape.Radius + 4),
            BorderBrush = T.BorderBrush,
            BorderThickness = new Thickness(theme.Shape.BorderThickness),
            Child = root,
            ClipToBounds = true,
        };
        if (theme.Effects.Glow && !reduce) _card.Effect = UiKit.Glow(Theming.Col(theme.Palette.Glow), 28, 0.5);

        Content = _card;
    }

    public void ShowLauncher(bool startMode)
    {
        _startMode = startMode;
        PositionWindow();
        Opacity = 0;
        Show();
        var hwnd = new WindowInteropHelper(this).Handle;
        WindowStyler.ApplyToHwnd(hwnd, _state.Settings.CurrentTheme(), _state.ReduceEffects());
        Activate();
        _search.Clear();
        _search.Focus();
        ShowDefault();
        Opacity = 1;
        UiKit.PlayOpen(_state, _card, _state.Settings.CurrentTheme().Effects.Open);
        _state.PlaySound(SoundEvent.Open);
    }

    private void PositionWindow()
    {
        var (w, h) = _state.ScreenSize();
        var dpi = VisualTreeHelper.GetDpi(this);
        double sw = w / dpi.DpiScaleX, sh = h / dpi.DpiScaleY;
        Left = (sw - Width) / 2;
        Top = sh * 0.22;
    }

    public void HideAnimated()
    {
        if (!IsVisible) return;
        _state.PlaySound(SoundEvent.Close);
        UiKit.PlayClose(_state, _card, Hide);
    }

    private void ShowDefault()
    {
        _list.Children.Clear();
        _rows.Clear();
        _selected = -1;
        _hint.Visibility = Visibility.Visible;

        if (_state.Settings.Pinned.Count > 0)
            AddSection(L.T("section.pinned"), _state.Settings.Pinned.Select(ToItem));

        if (_state.Settings.ShowRecent)
        {
            var recent = _state.Engine.Usage.MostRecent(6).Select(x => _state.Engine.Find(x.Id)).Where(i => i != null).Cast<SearchItem>().ToList();
            if (recent.Count > 0) AddSection(L.T("section.recent"), recent);
            var freq = _state.Engine.Usage.MostUsed(6).Select(x => _state.Engine.Find(x.Id)).Where(i => i != null).Cast<SearchItem>().ToList();
            if (freq.Count > 0) AddSection(L.T("section.frequent"), freq);
        }
        if (_rows.Count > 0) Select(0);
    }

    private SearchItem ToItem(Iskra.Core.Settings.PinnedItem p) =>
        _state.Engine.Find(p.Id) ?? new SearchItem(p.Id, p.Title, Enum.TryParse<ItemKind>(p.Kind, out var k) ? k : ItemKind.App, p.Target) { FilePath = p.FilePath };

    private void AddSection(string title, IEnumerable<SearchItem> items)
    {
        var header = new TextBlock
        {
            Text = T.CaseTransform(title), FontFamily = T.UiFont, FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            Foreground = T.ForegroundDim, Margin = new Thickness(14, 10, 0, 4),
        };
        _list.Children.Add(header);
        foreach (var it in items) AddRow(new SearchResult(it, 0, Array.Empty<int>(), "pin"));
    }

    private void RunSearch()
    {
        var q = _search.Text;
        if (string.IsNullOrWhiteSpace(q)) { ShowDefault(); return; }
        _hint.Visibility = Visibility.Collapsed;
        _current = _state.Engine.Search(q, 40);
        _list.Children.Clear();
        _rows.Clear();
        _selected = -1;
        foreach (var r in _current) AddRow(r, q);
        if (_rows.Count > 0) Select(0);
    }

    private void AddRow(SearchResult r, string? query = null)
    {
        var row = new ResultRow(_state, r, query);
        row.Activated += (asAdmin, loc) => Execute(r, asAdmin, loc);
        row.MouseEnter += (_, _) => Select(_rows.IndexOf(row), false);
        int idx = _rows.Count;
        row.ContextRequested += () => ShowContext(r, row);
        _rows.Add(row);
        _list.Children.Add(row);
        if (!_state.ReduceEffects() && idx < 10)
        {
            row.Opacity = 0;
            var a = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(UiKit.Scaled(_state, 160)))
            { BeginTime = TimeSpan.FromMilliseconds(idx * 18), EasingFunction = UiKit.OutCubic };
            row.BeginAnimation(OpacityProperty, a);
        }
    }

    private void Select(int index, bool scroll = true)
    {
        if (index < 0 || index >= _rows.Count) return;
        if (_selected >= 0 && _selected < _rows.Count) _rows[_selected].SetSelected(false);
        _selected = index;
        _rows[_selected].SetSelected(true);
        if (scroll) _rows[_selected].BringIntoView();
        _confirmPending = false;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape: HideAnimated(); e.Handled = true; break;
            case Key.Down: Select(Math.Min(_selected + 1, _rows.Count - 1)); e.Handled = true; break;
            case Key.Up: Select(Math.Max(_selected - 1, 0)); e.Handled = true; break;
            case Key.PageDown: Select(Math.Min(_selected + 6, _rows.Count - 1)); e.Handled = true; break;
            case Key.PageUp: Select(Math.Max(_selected - 6, 0)); e.Handled = true; break;
            case Key.Tab:
                Select((_selected + (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1) + _rows.Count) % Math.Max(1, _rows.Count));
                e.Handled = true; break;
            case Key.Enter:
                if (_selected >= 0 && _selected < _rows.Count)
                {
                    bool admin = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
                    bool loc = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
                    Execute(_rows[_selected].Result, admin, loc);
                }
                else if (!string.IsNullOrWhiteSpace(_search.Text))
                    Execute(new SearchResult(_state.Engine.WebItem(_search.Text), 0, Array.Empty<int>(), "web"), false, false);
                e.Handled = true; break;
            case Key.P when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                if (_selected >= 0) TogglePin(_rows[_selected].Result.Item);
                e.Handled = true; break;
        }
    }

    private void Execute(SearchResult r, bool asAdmin, bool openLocation)
    {
        if (r.Item.NeedsConfirmation && !_confirmPending)
        {
            _confirmPending = true;
            _hint.Visibility = Visibility.Visible;
            _hint.Text = L.T("cmd.confirm");
            _hint.Foreground = T.Accent;
            return;
        }
        _confirmPending = false;
        _state.PlaySound(SoundEvent.Launch);
        _state.OnLaunch(r.Item, _search.Text);
        if (r.Item.Kind != ItemKind.Calculator) HideAnimated();
        Launcher.Launch(r.Item, asAdmin, openLocation);
    }

    private void ShowContext(SearchResult r, ResultRow row)
    {
        var menu = new ContextMenu();
        void Item(string key, Action act) { var mi = new MenuItem { Header = L.T(key) }; mi.Click += (_, _) => act(); menu.Items.Add(mi); }
        Item("menu.open", () => Execute(r, false, false));
        if (r.Item.Kind is ItemKind.App or ItemKind.StoreApp) Item("menu.runas", () => Execute(r, true, false));
        if (r.Item.FilePath != null) { Item("menu.location", () => Launcher.RevealInExplorer(r.Item.FilePath!)); Item("menu.copypath", () => { try { Clipboard.SetText(r.Item.FilePath!); } catch { } }); }
        bool pinned = _state.Settings.Pinned.Any(p => p.Id == r.Item.Id);
        Item(pinned ? "menu.unpin" : "menu.pin", () => TogglePin(r.Item));
        menu.PlacementTarget = row; menu.IsOpen = true;
    }

    private void TogglePin(SearchItem item)
    {
        var pinned = _state.Settings.Pinned;
        var existing = pinned.FirstOrDefault(p => p.Id == item.Id);
        if (existing != null) pinned.Remove(existing);
        else pinned.Add(new Iskra.Core.Settings.PinnedItem { Id = item.Id, Title = item.Title, Target = item.Target, Kind = item.Kind.ToString(), FilePath = item.FilePath });
        _state.SaveSettings();
        if (string.IsNullOrWhiteSpace(_search.Text)) ShowDefault();
    }
}

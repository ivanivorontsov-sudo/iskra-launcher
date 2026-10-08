using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Iskra.App.Services;
using Iskra.Core.Loc;
using Iskra.Core.Media;
using Iskra.Core.Search;
using Iskra.Core.Settings;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Полноэкранный «стартовый экран»: сетка живых плиток с перетаскиванием, поиск сверху, кнопки питания.</summary>
internal sealed class StartScreenWindow : Window
{
    private readonly AppState _state;
    private WrapPanel _tiles = null!;
    private TextBox _search = null!;
    private Border _root = null!;
    private Grid _searchOverlay = null!;
    private StackPanel _searchResults = null!;

    public StartScreenWindow(AppState state)
    {
        _state = state;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        WindowState = WindowState.Maximized;
        AllowsTransparency = false;
        Background = Brushes.Black;
        ShowInTaskbar = false;
        Topmost = true;
        Title = "Искра — стартовый экран";
        _state.ThemeChanged += () => { if (IsVisible) Build(); };
        PreviewKeyDown += OnKey;
        Build();
    }

    private Theming T => _state.Theming;

    private void Build()
    {
        var reduce = _state.ReduceEffects();
        var theme = _state.Settings.CurrentTheme();
        var bg = new Grid();
        // Фон: обои темы + слой эффектов + затемнение.
        try
        {
            var wall = AssetFactory.GenerateWallpaper(theme, 1920, 1080);
            var img = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(wall)), Stretch = Stretch.UniformToFill };
            bg.Children.Add(img);
        }
        catch { bg.Background = T.WindowOpaque; }
        bg.Children.Add(new BackgroundFxControl(_state));
        bg.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb((byte)(theme.Dark ? 150 : 90), 0, 0, 0)) });

        // Верхняя панель: поиск + кнопки.
        var top = new Grid { Margin = new Thickness(60, 40, 60, 10) };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _search = new TextBox
        {
            FontFamily = T.UiFont, FontSize = 22, Foreground = T.Foreground, CaretBrush = T.Accent,
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(0),
            Padding = new Thickness(18, 12, 18, 12), Width = 520, HorizontalAlignment = HorizontalAlignment.Left,
        };
        var searchHost = new Border { CornerRadius = T.Corner, Child = _search, HorizontalAlignment = HorizontalAlignment.Left, Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)) };
        searchHost.Child = _search; _search.Background = Brushes.Transparent;
        _search.TextChanged += (_, _) => OnSearch();
        Grid.SetColumn(searchHost, 0);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        buttons.Children.Add(TopButton("\uE713", L.T("start.settings"), () => { HideAnimated(); _state.OpenSettings(); }));
        buttons.Children.Add(TopButton("\uE7E8", L.T("start.power"), ShowPowerMenu));
        Grid.SetColumn(buttons, 1);
        top.Children.Add(searchHost); top.Children.Add(buttons);

        _tiles = new WrapPanel { Margin = new Thickness(60, 20, 60, 40) };
        BuildTiles();
        var tileScroll = new ScrollViewer { Content = _tiles, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };

        var layout = new DockPanel { LastChildFill = true };
        DockPanel.SetDock(top, Dock.Top);
        layout.Children.Add(top);
        layout.Children.Add(tileScroll);

        _searchResults = new StackPanel { Margin = new Thickness(60, 10, 60, 40) };
        _searchOverlay = new Grid { Visibility = Visibility.Collapsed };
        _searchOverlay.Children.Add(new ScrollViewer { Content = _searchResults, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(0, 120, 0, 0) });

        var outer = new Grid();
        outer.Children.Add(bg);
        outer.Children.Add(layout);
        outer.Children.Add(_searchOverlay);
        _root = new Border { Child = outer };
        Content = _root;
    }

    private FrameworkElement TopButton(string glyph, string tip, Action act)
    {
        var b = new Button
        {
            Width = 48, Height = 48, Margin = new Thickness(8, 0, 0, 0), ToolTip = tip, Cursor = Cursors.Hand,
            Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), BorderThickness = new Thickness(0),
            Content = UiKit.Glyph(glyph, 20, T.Foreground),
        };
        b.Click += (_, _) => act();
        return b;
    }

    private void BuildTiles()
    {
        _tiles.Children.Clear();
        var items = GetStartItems();
        int idx = 0;
        foreach (var it in items)
        {
            var tile = new LiveTile(_state, it, PinnedTileSize(it));
            tile.Activated += () => { HideAnimated(); _state.OnLaunch(it, null); _state.PlaySound(SoundEvent.Launch); Launcher.Launch(it); };
            tile.ReorderRequested += (from, to) => Reorder(from, to);
            tile.Index = idx++;
            if (!_state.ReduceEffects())
            {
                tile.Opacity = 0;
                var a = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(UiKit.Scaled(_state, 220)))
                { BeginTime = TimeSpan.FromMilliseconds(idx * 22), EasingFunction = UiKit.OutCubic };
                tile.BeginAnimation(OpacityProperty, a);
            }
            _tiles.Children.Add(tile);
        }
    }

    private int PinnedTileSize(SearchItem it) => _state.Settings.Pinned.FirstOrDefault(p => p.Id == it.Id)?.TileSize ?? 2;

    private List<SearchItem> GetStartItems()
    {
        var list = new List<SearchItem>();
        var seen = new HashSet<string>();
        foreach (var p in _state.Settings.Pinned)
        {
            var it = _state.Engine.Find(p.Id) ?? new SearchItem(p.Id, p.Title, Enum.TryParse<ItemKind>(p.Kind, out var k) ? k : ItemKind.App, p.Target) { FilePath = p.FilePath };
            if (seen.Add(it.Id)) list.Add(it);
        }
        if (list.Count == 0)
            foreach (var x in _state.Engine.Usage.MostUsed(16))
            {
                var it = _state.Engine.Find(x.Id);
                if (it != null && seen.Add(it.Id)) list.Add(it);
            }
        if (list.Count < 12)
            foreach (var it in _state.Engine.All.Where(i => i.Kind is ItemKind.App or ItemKind.StoreApp).Take(24))
                if (seen.Add(it.Id)) { list.Add(it); if (list.Count >= 20) break; }
        return list;
    }

    private void Reorder(int from, int to)
    {
        // Переносим в списке закреплённых; если плитки были автоматическими — фиксируем текущий порядок как закреплённый.
        EnsurePinnedFromCurrent();
        var p = _state.Settings.Pinned;
        if (from < 0 || from >= p.Count) return;
        to = Math.Clamp(to, 0, p.Count - 1);
        var item = p[from]; p.RemoveAt(from); p.Insert(to, item);
        _state.SaveSettings();
        BuildTiles();
    }

    private void EnsurePinnedFromCurrent()
    {
        if (_state.Settings.Pinned.Count > 0) return;
        foreach (var child in _tiles.Children.OfType<LiveTile>())
            _state.Settings.Pinned.Add(new PinnedItem { Id = child.Item.Id, Title = child.Item.Title, Target = child.Item.Target, Kind = child.Item.Kind.ToString(), FilePath = child.Item.FilePath, TileSize = child.TileSizeUnit });
    }

    private void OnSearch()
    {
        var q = _search.Text;
        if (string.IsNullOrWhiteSpace(q)) { _searchOverlay.Visibility = Visibility.Collapsed; return; }
        _searchOverlay.Visibility = Visibility.Visible;
        _searchResults.Children.Clear();
        foreach (var r in _state.Engine.Search(q, 20))
        {
            var row = new ResultRow(_state, r, q);
            row.Activated += (admin, loc) => { HideAnimated(); _state.OnLaunch(r.Item, q); Launcher.Launch(r.Item, admin, loc); };
            _searchResults.Children.Add(row);
        }
    }

    private void ShowPowerMenu()
    {
        var menu = new ContextMenu();
        foreach (var c in new[] { BuiltinItems.Cmd.Lock, BuiltinItems.Cmd.Sleep, BuiltinItems.Cmd.SignOut, BuiltinItems.Cmd.Restart, BuiltinItems.Cmd.Shutdown })
        {
            var mi = new MenuItem { Header = L.T("cmd." + c.ToString().ToLowerInvariant()) };
            var cc = c; mi.Click += (_, _) => { HideAnimated(); Services.SystemCommands.Run(cc); };
            menu.Items.Add(mi);
        }
        menu.IsOpen = true;
    }

    public void ShowStart()
    {
        Build();
        Opacity = 0; Show();
        var hwnd = new WindowInteropHelper(this).Handle;
        WindowStyler.ApplyToHwnd(hwnd, _state.Settings.CurrentTheme(), _state.ReduceEffects());
        Activate();
        _search.Clear();
        _search.Focus();
        Opacity = 1;
        UiKit.PlayOpen(_state, _root, _state.Settings.CurrentTheme().Effects.Open);
        _state.PlaySound(SoundEvent.Open);
    }

    public void HideAnimated()
    {
        if (!IsVisible) return;
        _state.PlaySound(SoundEvent.Close);
        UiKit.PlayClose(_state, _root, Hide);
    }

    private void OnKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (_searchOverlay.Visibility == Visibility.Visible) { _search.Clear(); } else HideAnimated(); e.Handled = true; }
    }
}

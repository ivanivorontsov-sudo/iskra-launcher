using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Iskra.App.Services;
using Iskra.Core.Search;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Плитка стартового экрана: иконка/глиф + подпись; «живой» переворот; перетаскивание для смены порядка.</summary>
internal sealed class LiveTile : Border
{
    public SearchItem Item { get; }
    public int TileSizeUnit { get; }
    public int Index { get; set; }
    public event Action? Activated;
    public event Action<int, int>? ReorderRequested;
    private readonly AppState _state;
    private Point _dragStart;
    private bool _dragging;

    public LiveTile(AppState state, SearchItem item, int sizeUnit)
    {
        _state = state; Item = item; TileSizeUnit = sizeUnit;
        var t = state.Theming;
        var theme = state.Settings.CurrentTheme();
        double unit = theme.Shape.TileSize;
        double w = sizeUnit == 3 ? unit * 2 + theme.Shape.TileGap : unit;
        double h = sizeUnit == 1 ? unit / 2 - theme.Shape.TileGap / 2 : unit;
        Width = w; Height = h;
        Margin = new Thickness(theme.Shape.TileGap / 2);
        CornerRadius = t.TileCorner;
        Background = TileBrush(t, theme);
        BorderBrush = t.BorderBrush;
        BorderThickness = new Thickness(theme.Shape.BorderThickness);
        Cursor = Cursors.Hand;
        ClipToBounds = true;
        if (theme.System.PreferDock && theme.Effects.Glow && !state.ReduceEffects())
            Effect = UiKit.Glow(Theming.Col(theme.Palette.Glow), 12, 0.35);

        var content = new Grid();
        var glyph = item.Glyph ?? "\uE737";
        var icon = UiKit.Glyph(glyph, sizeUnit == 1 ? 18 : 30, t.Foreground);
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = sizeUnit == 1 ? VerticalAlignment.Center : VerticalAlignment.Top;
        icon.Margin = new Thickness(0, sizeUnit == 1 ? 0 : 14, 0, 0);

        // Иконка приложения, если есть.
        if (item.Kind is ItemKind.App or ItemKind.StoreApp or ItemKind.File or ItemKind.Folder)
        {
            var img = new Image { Width = 30, Height = 30, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 14, 0, 0) };
            System.Threading.Tasks.Task.Run(() => { var b = IconLoader.Load(item.FilePath ?? item.Target); if (b != null) Dispatcher.BeginInvoke(() => { img.Source = b; }); });
            content.Children.Add(img);
        }
        else content.Children.Add(icon);

        var label = new TextBlock
        {
            Text = t.CaseTransform(item.Title), FontFamily = t.UiFont, FontSize = sizeUnit == 1 ? 11 : 12.5,
            Foreground = t.Foreground, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
            Margin = new Thickness(8, 0, 8, 7), VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (sizeUnit != 1) content.Children.Add(label);
        Child = content;

        MouseLeftButtonDown += (_, e) => { _dragStart = e.GetPosition(Parent as IInputElement); _dragging = false; CaptureMouse(); };
        MouseMove += OnMove;
        MouseLeftButtonUp += OnUp;
        MouseRightButtonUp += (_, e) => { ShowMenu(); e.Handled = true; };

        if (theme.Effects.LiveTiles && !state.ReduceEffects() && sizeUnit >= 2) EnableFlip(content, t);
    }

    private Brush TileBrush(Theming t, ThemeDefinition theme)
    {
        if (theme.Id == "tiles" || theme.Effects.LiveTiles)
        {
            // Плоская плитка цветом акцента с вариацией по индексу.
            var baseC = Theming.Col(theme.Palette.Accent);
            return new SolidColorBrush(baseC);
        }
        return t.Surface;
    }

    private void EnableFlip(Grid content, Theming t)
    {
        // Иногда плитка переворачивается, показывая вторую грань (например, время).
        var back = new Border { Background = new SolidColorBrush(Theming.Col(_state.Settings.CurrentTheme().Palette.Accent2)), Visibility = Visibility.Collapsed };
        back.Child = new TextBlock { Text = DateTime.Now.ToString("HH:mm"), FontFamily = t.TitleFont, FontSize = 22, Foreground = t.Foreground, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var host = Child as Grid;
        host?.Children.Add(back);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6 + Index % 5) };
        timer.Tick += (_, _) =>
        {
            if (!IsLoaded) { timer.Stop(); return; }
            bool showBack = back.Visibility != Visibility.Visible;
            if (back.Child is TextBlock tb) tb.Text = DateTime.Now.ToString("HH:mm");
            var flip = new DoubleAnimation(0, 90, new Duration(TimeSpan.FromMilliseconds(180))) { EasingFunction = UiKit.InCubic, AutoReverse = false };
            var st = new ScaleTransform(1, 1); RenderTransform = st; RenderTransformOrigin = new Point(0.5, 0.5);
            var sx = new DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(170)));
            sx.Completed += (_, _) => { back.Visibility = showBack ? Visibility.Visible : Visibility.Collapsed; st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(170)))); };
            st.BeginAnimation(ScaleTransform.ScaleXProperty, sx);
        };
        Loaded += (_, _) => timer.Start();
        Unloaded += (_, _) => timer.Stop();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var pos = e.GetPosition(Parent as IInputElement);
        if (!_dragging && (Math.Abs(pos.X - _dragStart.X) > 8 || Math.Abs(pos.Y - _dragStart.Y) > 8))
        {
            _dragging = true; Opacity = 0.6; Panel.SetZIndex(this, 100);
        }
        if (_dragging && Parent is WrapPanel wp)
        {
            var rt = RenderTransform as TranslateTransform ?? new TranslateTransform();
            rt.X = pos.X - _dragStart.X; rt.Y = pos.Y - _dragStart.Y; RenderTransform = rt;
        }
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (!_dragging) { Activated?.Invoke(); return; }
        RenderTransform = null; Opacity = 1; Panel.SetZIndex(this, 0);
        if (Parent is WrapPanel wp)
        {
            var pos = e.GetPosition(wp);
            int target = HitIndex(wp, pos);
            if (target >= 0 && target != Index) ReorderRequested?.Invoke(Index, target);
        }
        _dragging = false;
    }

    private static int HitIndex(WrapPanel wp, Point p)
    {
        for (int i = 0; i < wp.Children.Count; i++)
        {
            var c = (FrameworkElement)wp.Children[i];
            var tl = c.TranslatePoint(new Point(0, 0), wp);
            var rect = new Rect(tl, new Size(c.ActualWidth, c.ActualHeight));
            if (rect.Contains(p)) return i;
        }
        return -1;
    }

    private void ShowMenu()
    {
        var menu = new ContextMenu();
        void Add(string key, Action a) { var mi = new MenuItem { Header = Iskra.Core.Loc.L.T(key) }; mi.Click += (_, _) => a(); menu.Items.Add(mi); }
        Add("menu.open", () => Activated?.Invoke());
        Add("menu.tile.small", () => SetSize(1));
        Add("menu.tile.medium", () => SetSize(2));
        Add("menu.tile.wide", () => SetSize(3));
        Add("menu.unpin", () => { var p = _state.Settings.Pinned.FirstOrDefault_(Item.Id); if (p != null) { _state.Settings.Pinned.Remove(p); _state.SaveSettings(); } ReorderRequested?.Invoke(Index, Index); });
        menu.PlacementTarget = this; menu.IsOpen = true;
    }

    private void SetSize(int unit)
    {
        var p = _state.Settings.Pinned.FirstOrDefault_(Item.Id);
        if (p == null) { p = new Iskra.Core.Settings.PinnedItem { Id = Item.Id, Title = Item.Title, Target = Item.Target, Kind = Item.Kind.ToString(), FilePath = Item.FilePath }; _state.Settings.Pinned.Add(p); }
        p.TileSize = unit; _state.SaveSettings();
        ReorderRequested?.Invoke(Index, Index);
    }
}

internal static class PinnedExt
{
    public static Iskra.Core.Settings.PinnedItem? FirstOrDefault_(this System.Collections.Generic.List<Iskra.Core.Settings.PinnedItem> list, string id)
    { foreach (var p in list) if (p.Id == id) return p; return null; }
}

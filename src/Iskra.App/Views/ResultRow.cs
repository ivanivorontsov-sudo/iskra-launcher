using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Iskra.App.Services;
using Iskra.Core.Loc;
using Iskra.Core.Search;

namespace Iskra.App.Views;

/// <summary>Строка результата: иконка/глиф, название с подсветкой совпадения, подпись, подсветка при наведении.</summary>
internal sealed class ResultRow : Border
{
    public SearchResult Result { get; }
    public event Action<bool, bool>? Activated; // asAdmin, openLocation
    public event Action? ContextRequested;
    private readonly AppState _state;
    private bool _selected;
    private readonly Brush _hover, _normal;

    public ResultRow(AppState state, SearchResult r, string? query)
    {
        _state = state; Result = r;
        var t = state.Theming;
        _hover = t.SurfaceHover; _normal = Brushes.Transparent;
        Padding = new Thickness(12, 9, 12, 9);
        Margin = new Thickness(4, 1, 4, 1);
        CornerRadius = t.TileCorner;
        Background = _normal;
        Cursor = Cursors.Hand;

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var icon = BuildIcon(r.Item, t);
        Grid.SetColumn(icon, 0);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        var title = new TextBlock { FontFamily = t.UiFont, FontSize = state.Settings.CurrentTheme().Fonts.BaseSize + 1, Foreground = t.Foreground, TextTrimming = TextTrimming.CharacterEllipsis };
        FillTitle(title, r, t);
        texts.Children.Add(title);
        var sub = SubtitleText(r);
        if (!string.IsNullOrEmpty(sub))
            texts.Children.Add(new TextBlock { Text = sub, FontFamily = t.UiFont, FontSize = 11.5, Foreground = t.ForegroundDim, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(texts, 1);

        var badge = new TextBlock { Text = L.T("kind." + r.Item.Kind), FontFamily = t.UiFont, FontSize = 10.5, Foreground = t.ForegroundDim, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(badge, 2);

        grid.Children.Add(icon); grid.Children.Add(texts); grid.Children.Add(badge);
        Child = grid;

        MouseLeftButtonUp += (_, e) => Activated?.Invoke(Keyboard.Modifiers.HasFlag(ModifierKeys.Control), Keyboard.Modifiers.HasFlag(ModifierKeys.Shift));
        MouseRightButtonUp += (_, e) => { ContextRequested?.Invoke(); e.Handled = true; };
    }

    private static string SubtitleText(SearchResult r)
    {
        if (r.Item.Kind == ItemKind.File || r.Item.Kind == ItemKind.Folder) return r.Item.FilePath ?? r.Item.Subtitle;
        return r.Item.Subtitle;
    }

    private FrameworkElement BuildIcon(SearchItem item, Theming t)
    {
        const double sz = 36;
        // Команды/настройки/веб/калькулятор/темы — глиф в форме темы.
        if (item.Kind is ItemKind.Command or ItemKind.Setting or ItemKind.Web or ItemKind.Calculator or ItemKind.Theme || item.Glyph != null)
            return Host(ShapedIcon.Build(t, sz, null, item.Glyph ?? "\uE700"));

        // Приложения/файлы: место под значок, грузим лениво и помещаем в форму темы.
        var host = new Grid { Width = sz, Height = sz, VerticalAlignment = VerticalAlignment.Center };
        host.Children.Add(ShapedIcon.Build(t, sz, null, "\uE737")); // плейсхолдер в форме
        var path = item.FilePath ?? item.Target;
        System.Threading.Tasks.Task.Run(() =>
        {
            var bmp = IconLoader.Load(path);
            Dispatcher.BeginInvoke(() =>
            {
                host.Children.Clear();
                host.Children.Add(bmp != null ? ShapedIcon.Build(t, sz, bmp, null) : ShapedIcon.Build(t, sz, null, "\uE737"));
            });
        });
        return Host(host);

        static FrameworkElement Host(FrameworkElement fe) { fe.VerticalAlignment = VerticalAlignment.Center; return fe; }
    }

    private void FillTitle(TextBlock tb, SearchResult r, Theming t)
    {
        var title = r.Item.Title;
        if (r.Highlight.Length == 0) { tb.Text = title; return; }
        var set = new System.Collections.Generic.HashSet<int>(r.Highlight);
        for (int i = 0; i < title.Length; i++)
        {
            var run = new Run(title[i].ToString());
            if (set.Contains(i)) { run.Foreground = t.Accent; run.FontWeight = FontWeights.Bold; }
            tb.Inlines.Add(run);
        }
    }

    public void SetSelected(bool sel)
    {
        _selected = sel;
        Background = sel ? _hover : _normal;
        if (sel && _state.Settings.CurrentTheme().Effects.Reveal && !_state.ReduceEffects())
            BorderBrush = new SolidColorBrush(Color.FromArgb(60, _state.Theming.Accent.Color.R, _state.Theming.Accent.Color.G, _state.Theming.Accent.Color.B));
        else BorderBrush = Brushes.Transparent;
        BorderThickness = new Thickness(1);
    }
}

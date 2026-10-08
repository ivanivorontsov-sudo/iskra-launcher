using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Iskra.App.Services;
using Iskra.Core.Themes;

namespace Iskra.App.Views;

/// <summary>Рисует значок в форме темы: фон-плитка нужной формы + значок приложения/глиф внутри, с тенью и бликом.</summary>
internal static class ShapedIcon
{
    public static FrameworkElement Build(Theming t, double size, BitmapSource? image, string? glyph, Brush? glyphBrush = null)
    {
        var style = t.Theme.Icon;
        var geom = ShapeGeometry.For(style.Shape, size);
        var root = new Grid { Width = size, Height = size };

        // Фон-плитка.
        Brush fill = style.Fill switch
        {
            IconFill.AccentGradient => Freeze(new LinearGradientBrush(
                t.Accent.Color, Shade(t.Accent.Color, t.Accent2.Color), 60)),
            IconFill.Solid => t.Accent,
            IconFill.Glass => new SolidColorBrush(Color.FromArgb(46, t.Accent.Color.R, t.Accent.Color.G, t.Accent.Color.B)),
            _ => Brushes.Transparent,
        };
        if (style.Fill != IconFill.None)
        {
            var bg = new System.Windows.Shapes.Path { Data = geom, Fill = fill };
            if (style.Shadow && style.Fill != IconFill.Glass)
                bg.Effect = new DropShadowEffect { BlurRadius = size * 0.22, ShadowDepth = size * 0.05, Opacity = 0.35, Color = Colors.Black };
            root.Children.Add(bg);
        }

        // Содержимое (значок приложения либо глиф), обрезанное по форме.
        double pad = size * style.Padding;
        FrameworkElement inner;
        if (image != null)
        {
            inner = new Image { Source = image, Stretch = Stretch.Uniform, Margin = new Thickness(pad) };
        }
        else
        {
            Brush gb = style.Fill == IconFill.None ? (glyphBrush ?? t.Accent) : t.AccentForeground;
            inner = UiKit.Glyph(glyph ?? "\uE700", size * 0.46, (SolidColorBrush)gb);
            inner.HorizontalAlignment = HorizontalAlignment.Center;
            inner.VerticalAlignment = VerticalAlignment.Center;
        }
        if (style.Fill != IconFill.None)
            inner.Clip = geom; // значок не вылезает за форму
        root.Children.Add(inner);

        // Блик сверху.
        if (style.Gloss && style.Fill is IconFill.AccentGradient or IconFill.Solid)
        {
            var gloss = new System.Windows.Shapes.Path
            {
                Data = geom,
                Fill = Freeze(new LinearGradientBrush(
                    Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90)),
                IsHitTestVisible = false,
            };
            root.Children.Add(gloss);
        }
        return root;
    }

    private static Color Shade(Color a, Color b) => b;

    private static T Freeze<T>(T f) where T : Freezable { if (f.CanFreeze) f.Freeze(); return f; }
}

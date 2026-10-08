using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Iskra.App.Views;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// Рисует значки в форме темы и сохраняет как .ico для применения в самой Windows
/// (папки, «Этот компьютер», корзина, ярлыки). Всё рисуется кодом — без сторонних иконпаков.
/// </summary>
internal static class ShapeIconFactory
{
    private static readonly int[] Sizes = { 16, 32, 48, 64, 128, 256 };

    // Значки оболочки: роль → символ Segoe Fluent Icons.
    private static readonly Dictionary<string, string> ShellGlyphs = new()
    {
        ["folder"] = "\uE8B7", ["folderOpen"] = "\uE838", ["thispc"] = "\uE977",
        ["recycleEmpty"] = "\uE74D", ["recycleFull"] = "\uE74D",
    };

    /// <summary>Создаёт набор значков оболочки (папка/этот компьютер/корзина) в форме темы. Возвращает роль→путь.ico.</summary>
    public static Dictionary<string, string> GenerateShellIcons(ThemeDefinition t)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(Paths.Icons, t.Id);
        Directory.CreateDirectory(dir);
        foreach (var (role, glyph) in ShellGlyphs)
        {
            try
            {
                var path = Path.Combine(dir, role + ".ico");
                IcoWriter.Write(path, RenderFrames(t, glyph, null));
                map[role] = path;
            }
            catch (Exception ex) { Log.Error("Значок " + role, ex); }
        }
        return map;
    }

    /// <summary>Значок в форме темы вокруг переданного значка приложения (для ярлыков). Возвращает путь .ico.</summary>
    public static string GenerateShortcutIcon(ThemeDefinition t, BitmapSource appIcon, string key)
    {
        var dir = Path.Combine(Paths.Icons, t.Id, "lnk");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, key + ".ico");
        IcoWriter.Write(path, RenderFrames(t, null, appIcon));
        return path;
    }

    private static List<(int, byte[])> RenderFrames(ThemeDefinition t, string? glyph, BitmapSource? inner)
    {
        var frames = new List<(int, byte[])>();
        foreach (var s in Sizes) frames.Add((s, RenderPng(t, glyph, inner, s)));
        return frames;
    }

    private static byte[] RenderPng(ThemeDefinition t, string? glyph, BitmapSource? inner, int size)
    {
        var style = t.Icon;
        double s = size;
        var geom = ShapeGeometry.For(style.Shape, s);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            var accent = ToColor(t.Palette.Accent);
            var accent2 = ToColor(t.Palette.Accent2);
            Brush fill = style.Fill switch
            {
                IconFill.AccentGradient => new LinearGradientBrush(accent, accent2, 60),
                IconFill.Solid => new SolidColorBrush(accent),
                IconFill.Glass => new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B)),
                _ => Brushes.Transparent,
            };
            if (style.Fill != IconFill.None) dc.DrawGeometry(fill, null, geom);

            double pad = s * style.Padding;
            if (inner != null)
            {
                var rect = new Rect(pad, pad, s - 2 * pad, s - 2 * pad);
                dc.PushClip(geom);
                dc.DrawImage(inner, rect);
                dc.Pop();
            }
            else if (glyph != null)
            {
                var brush = style.Fill == IconFill.None ? new SolidColorBrush(accent) : new SolidColorBrush(ToColor(t.Palette.AccentForeground));
                var tf = new Typeface(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var ft = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, s * 0.5, brush, 1.0);
                dc.DrawText(ft, new Point((s - ft.Width) / 2, (s - ft.Height) / 2));
            }
            if (style.Gloss && style.Fill is IconFill.AccentGradient or IconFill.Solid)
                dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(70, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90), null, geom);
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static Color ToColor(string hex) { var a = Argb.Parse(hex, new Argb(0xFF, 0x5A, 0xA9, 0xFF)); return Color.FromArgb(a.A, a.R, a.G, a.B); }
}

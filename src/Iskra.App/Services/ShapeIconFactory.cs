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
    public static readonly int[] Sizes = { 16, 24, 32, 48, 64, 96, 128, 256 };

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

    /// <summary>
    /// Полностью прозрачный значок для скрытия стрелки ярлыка (Shell Icons\29). Кадры — классические 32-битные DIB
    /// с нулевой альфой и маской AND из единиц: с PNG-кадрами Проводник рисует вместо стрелки чёрный квадрат.
    /// </summary>
    public static string GenerateBlankIcon()
    {
        var path = Path.Combine(Paths.Icons, "blank.ico");
        Directory.CreateDirectory(Paths.Icons);
        File.WriteAllBytes(path, BlankIco.Build(new[] { 16, 24, 32, 48, 64, 256 }));
        return path;
    }

    internal static byte[] RenderPng(ThemeDefinition t, string? glyph, BitmapSource? inner, int size)
    {
        var style = t.Icon;
        double s = size;
        // Небольшой отступ под тень, чтобы форма не обрезалась краем значка.
        double m = size >= 32 ? s * 0.04 : 0;
        double body = s - 2 * m;
        var geom = ShapeGeometry.For(style.Shape, body).Clone();
        geom.Transform = new TranslateTransform(m, m * 0.6);
        var dv = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(dv, BitmapScalingMode.HighQuality);
        RenderOptions.SetEdgeMode(dv, EdgeMode.Unspecified);
        using (var dc = dv.RenderOpen())
        {
            var accent = ToColor(t.Palette.Accent);
            var accent2 = ToColor(t.Palette.Accent2);

            if (style.Shadow && size >= 32 && style.Fill != IconFill.None)
            {
                var sh = geom.Clone(); sh.Transform = new TranslateTransform(m, m * 1.6);
                dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(46, 0, 0, 0)), null, sh);
            }

            Brush fill = style.Fill switch
            {
                IconFill.AccentGradient => new LinearGradientBrush(Lighten(accent, 0.18), accent2, 70),
                IconFill.Solid => new LinearGradientBrush(Lighten(accent, 0.12), accent, 90),
                IconFill.Glass => new LinearGradientBrush(Color.FromArgb(150, 255, 255, 255), Color.FromArgb(90, accent.R, accent.G, accent.B), 90),
                _ => Brushes.Transparent,
            };
            if (style.Fill != IconFill.None)
                dc.DrawGeometry(fill, new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 0)), Math.Max(0.6, s / 128)), geom);

            if (inner != null)
            {
                // Исходный значок целиком внутри формы (без «обрубков»), масштаб высокого качества.
                double pad = body * Math.Clamp(style.Padding <= 0 ? 0.2 : style.Padding, 0.14, 0.3);
                if (style.Shape is IconShape.Droplet or IconShape.Star or IconShape.RoundedTriangle) pad = body * 0.27;
                if (style.Fill == IconFill.None) pad = body * 0.06;
                var rect = new Rect(m + pad, m * 0.6 + pad * (style.Shape == IconShape.Droplet ? 1.25 : 1), body - 2 * pad, body - 2 * pad);
                dc.PushClip(geom);
                dc.DrawImage(inner, rect);
                dc.Pop();
            }
            else if (glyph != null)
            {
                var brush = style.Fill == IconFill.None ? new SolidColorBrush(accent) : new SolidColorBrush(ToColor(t.Palette.AccentForeground));
                var tf = new Typeface(new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                var ft = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, tf, body * 0.48, brush, 1.0);
                dc.DrawText(ft, new Point(m + (body - ft.Width) / 2, m * 0.6 + (body - ft.Height) / 2));
            }
            if (style.Gloss && style.Fill != IconFill.None && size >= 24)
            {
                var gloss = new LinearGradientBrush(Color.FromArgb(90, 255, 255, 255), Color.FromArgb(0, 255, 255, 255), 90) { EndPoint = new Point(0, 0.55) };
                dc.PushClip(geom);
                dc.DrawEllipse(gloss, null, new Point(m + body / 2, m), body * 0.62, body * 0.42);
                dc.Pop();
            }
        }
        var rtb = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static Color Lighten(Color c, double k) => Color.FromArgb(c.A,
        (byte)(c.R + (255 - c.R) * k), (byte)(c.G + (255 - c.G) * k), (byte)(c.B + (255 - c.B) * k));

    private static Color ToColor(string hex) { var a = Argb.Parse(hex, new Argb(0xFF, 0x5A, 0xA9, 0xFF)); return Color.FromArgb(a.A, a.R, a.G, a.B); }
}

/// <summary>Сборка прозрачного .ico из DIB-кадров (вынесено отдельно для тестов).</summary>
internal static class BlankIco
{
    public static byte[] Build(int[] sizes)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write((short)0); bw.Write((short)1); bw.Write((short)sizes.Length);
        var frames = new List<byte[]>();
        foreach (var sz in sizes) frames.Add(Frame(sz));
        int offset = 6 + 16 * sizes.Length;
        for (int i = 0; i < sizes.Length; i++)
        {
            int sz = sizes[i];
            bw.Write((byte)(sz >= 256 ? 0 : sz)); bw.Write((byte)(sz >= 256 ? 0 : sz));
            bw.Write((byte)0); bw.Write((byte)0); bw.Write((short)1); bw.Write((short)32);
            bw.Write(frames[i].Length); bw.Write(offset);
            offset += frames[i].Length;
        }
        foreach (var f in frames) bw.Write(f);
        return ms.ToArray();
    }

    private static byte[] Frame(int s)
    {
        int maskRow = (s + 31) / 32 * 4;
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(40); bw.Write(s); bw.Write(s * 2); bw.Write((short)1); bw.Write((short)32);
        bw.Write(0); bw.Write(s * s * 4 + maskRow * s); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0);
        bw.Write(new byte[s * s * 4]);                       // BGRA = 0 (альфа 0)
        var mask = new byte[maskRow * s]; Array.Fill(mask, (byte)0xFF); // AND = 1 → прозрачно
        bw.Write(mask);
        return ms.ToArray();
    }
}

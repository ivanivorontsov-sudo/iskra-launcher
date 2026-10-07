using System;
using System.Windows;
using System.Windows.Media;
using Iskra.Core.Themes;
using CW = Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>Рисует обои темы в WPF DrawingContext. Используется и для файла обоев, и как фон окна.</summary>
internal static class WallpaperPainter
{
    private static Color C(string hex, Color fb = default) { var a = CW.Argb.Parse(hex, new CW.Argb(fb.A, fb.R, fb.G, fb.B)); return Color.FromArgb(a.A, a.R, a.G, a.B); }

    public static void Paint(DrawingContext dc, ThemeDefinition t, int w, int h, int frame)
    {
        var p = t.Palette;
        var bg1 = C(p.Background); var bg2 = C(p.Background2); var accent = C(p.Accent); var accent2 = C(p.Accent2);
        double phase = frame / 4.0;
        var rect = new Rect(0, 0, w, h);
        switch (t.System.Wallpaper)
        {
            case WallpaperKind.MicaWaves: MicaWaves(dc, rect, bg1, bg2, accent, phase); break;
            case WallpaperKind.Aurora: Aurora(dc, rect, bg1, bg2, accent, accent2, phase); break;
            case WallpaperKind.NeonCity: NeonCity(dc, rect, bg1, bg2, accent, accent2); break;
            case WallpaperKind.Space: Space(dc, rect, bg1, bg2, accent, phase); break;
            case WallpaperKind.Tiles: FlatDiagonal(dc, rect, bg1, accent, accent2); break;
            case WallpaperKind.Paper: Paper(dc, rect, bg1, bg2, accent); break;
            case WallpaperKind.Terminal: Terminal(dc, rect, bg1, accent); break;
            case WallpaperKind.Pixel: Pixel(dc, rect, bg1, bg2, accent, accent2); break;
            case WallpaperKind.Sunset: Sunset(dc, rect, bg1, bg2, accent, accent2); break;
            case WallpaperKind.Library: Library(dc, rect, bg1, bg2, accent); break;
            case WallpaperKind.Sakura: Sakura(dc, rect, bg1, bg2, accent, phase); break;
            case WallpaperKind.Minimal: Minimal(dc, rect, bg1, accent); break;
            default: Gradient(dc, rect, bg1, bg2); break;
        }
    }

    private static void Gradient(DrawingContext dc, Rect r, Color a, Color b, double angle = 90)
    {
        var br = new LinearGradientBrush(a, b, angle);
        dc.DrawRectangle(br, null, r);
    }

    private static void MicaWaves(DrawingContext dc, Rect r, Color a, Color b, Color accent, double phase)
    {
        Gradient(dc, r, a, b, 120);
        for (int i = 0; i < 4; i++)
        {
            double cx = r.Width * (0.2 + 0.2 * i) + Math.Sin(phase * 6.28 + i) * 60;
            double cy = r.Height * (0.3 + 0.12 * i);
            var rg = new RadialGradientBrush(Blend(accent, b, 0.35 - i * 0.05), Color.FromArgb(0, b.R, b.G, b.B))
            { Center = new Point(cx / r.Width, cy / r.Height), GradientOrigin = new Point(cx / r.Width, cy / r.Height), RadiusX = 0.5, RadiusY = 0.5 };
            dc.DrawRectangle(rg, null, r);
        }
    }

    private static void Aurora(DrawingContext dc, Rect r, Color a, Color b, Color accent, Color accent2, double phase)
    {
        Gradient(dc, r, a, b, 90);
        for (int band = 0; band < 5; band++)
        {
            var geo = new StreamGeometry();
            using (var g = geo.Open())
            {
                double baseY = r.Height * (0.2 + band * 0.13);
                g.BeginFigure(new Point(0, baseY), true, true);
                for (double x = 0; x <= r.Width; x += r.Width / 24)
                {
                    double y = baseY + Math.Sin(x / r.Width * 6.28 * 2 + band + phase * 6.28) * 60 + Math.Cos(x / 200 + band) * 30;
                    g.LineTo(new Point(x, y), true, true);
                }
                g.LineTo(new Point(r.Width, baseY + 120), true, false);
                g.LineTo(new Point(0, baseY + 120), true, false);
            }
            var col = Blend(band % 2 == 0 ? accent : accent2, b, 0.4);
            var br = new LinearGradientBrush(Color.FromArgb(120, col.R, col.G, col.B), Color.FromArgb(0, col.R, col.G, col.B), 90);
            dc.DrawGeometry(br, null, geo);
        }
    }

    private static void NeonCity(DrawingContext dc, Rect r, Color a, Color b, Color accent, Color accent2)
    {
        Gradient(dc, r, a, b, 90);
        double horizon = r.Height * 0.62;
        // Сетка-перспектива.
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, accent.R, accent.G, accent.B)), 1.2);
        for (int i = -20; i <= 20; i++)
        {
            double x = r.Width / 2 + i * (r.Width / 10);
            dc.DrawLine(pen, new Point(r.Width / 2 + i * 12, horizon), new Point(x, r.Height));
        }
        for (int i = 1; i <= 14; i++)
        {
            double y = horizon + Math.Pow(i / 14.0, 2) * (r.Height - horizon);
            dc.DrawLine(pen, new Point(0, y), new Point(r.Width, y));
        }
        // Солнце.
        var sun = new RadialGradientBrush(accent2, Color.FromArgb(0, accent2.R, accent2.G, accent2.B));
        dc.DrawEllipse(sun, null, new Point(r.Width / 2, horizon), r.Width * 0.12, r.Width * 0.12);
    }

    private static void Space(DrawingContext dc, Rect r, Color a, Color b, Color accent, double phase)
    {
        var rg = new RadialGradientBrush(b, a) { Center = new Point(0.3, 0.3), GradientOrigin = new Point(0.3, 0.3), RadiusX = 0.9, RadiusY = 0.9 };
        dc.DrawRectangle(rg, null, r);
        var rnd = new Random(7);
        for (int i = 0; i < 400; i++)
        {
            double x = rnd.NextDouble() * r.Width, y = rnd.NextDouble() * r.Height;
            double rad = rnd.NextDouble() * 1.6 + 0.3;
            byte br = (byte)(120 + rnd.Next(135));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(br, 255, 255, 255)), null, new Point(x, y), rad, rad);
        }
        var neb = new RadialGradientBrush(Color.FromArgb(90, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B))
        { Center = new Point(0.7, 0.65), GradientOrigin = new Point(0.7, 0.65), RadiusX = 0.4, RadiusY = 0.4 };
        dc.DrawRectangle(neb, null, r);
    }

    private static void FlatDiagonal(DrawingContext dc, Rect r, Color bg, Color a1, Color a2)
    {
        dc.DrawRectangle(new SolidColorBrush(bg), null, r);
        double sz = r.Width / 12;
        var rnd = new Random(3);
        for (double y = 0; y < r.Height; y += sz)
            for (double x = 0; x < r.Width; x += sz)
            {
                double v = rnd.NextDouble();
                if (v < 0.12) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(40, a1.R, a1.G, a1.B)), null, new Rect(x, y, sz - 2, sz - 2));
                else if (v < 0.18) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(55, a2.R, a2.G, a2.B)), null, new Rect(x, y, sz - 2, sz - 2));
            }
    }

    private static void Paper(DrawingContext dc, Rect r, Color a, Color b, Color accent)
    {
        dc.DrawRectangle(new SolidColorBrush(a), null, r);
        var line = new Pen(new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B)), 1);
        for (double y = r.Height * 0.08; y < r.Height; y += r.Height / 26) dc.DrawLine(line, new Point(0, y), new Point(r.Width, y));
        var margin = new Pen(new SolidColorBrush(Color.FromArgb(120, 0xD9, 0x53, 0x4F)), 1.5);
        dc.DrawLine(margin, new Point(r.Width * 0.12, 0), new Point(r.Width * 0.12, r.Height));
    }

    private static void Terminal(DrawingContext dc, Rect r, Color bg, Color accent)
    {
        dc.DrawRectangle(new SolidColorBrush(bg), null, r);
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(22, accent.R, accent.G, accent.B)), 1);
        for (double y = 0; y < r.Height; y += 3) dc.DrawLine(pen, new Point(0, y), new Point(r.Width, y));
        var glow = new RadialGradientBrush(Color.FromArgb(40, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B))
        { Center = new Point(0.5, 0.5), GradientOrigin = new Point(0.5, 0.5), RadiusX = 0.6, RadiusY = 0.6 };
        dc.DrawRectangle(glow, null, r);
    }

    private static void Pixel(DrawingContext dc, Rect r, Color a, Color b, Color a1, Color a2)
    {
        Gradient(dc, r, a, b, 90);
        var rnd = new Random(42);
        double sz = Math.Max(8, r.Width / 80);
        var cols = new[] { a1, a2, Colors.White };
        for (int i = 0; i < 90; i++)
        {
            double x = Math.Floor(rnd.NextDouble() * (r.Width / sz)) * sz;
            double y = Math.Floor(rnd.NextDouble() * (r.Height / sz)) * sz;
            var c = cols[rnd.Next(cols.Length)];
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(120, c.R, c.G, c.B)), null, new Rect(x, y, sz, sz));
        }
    }

    private static void Sunset(DrawingContext dc, Rect r, Color a, Color b, Color accent, Color accent2)
    {
        var sky = new LinearGradientBrush();
        sky.StartPoint = new Point(0, 0); sky.EndPoint = new Point(0, 1);
        sky.GradientStops.Add(new GradientStop(a, 0));
        sky.GradientStops.Add(new GradientStop(Blend(accent, a, 0.5), 0.5));
        sky.GradientStops.Add(new GradientStop(accent2, 0.72));
        sky.GradientStops.Add(new GradientStop(b, 1));
        dc.DrawRectangle(sky, null, r);
        double hy = r.Height * 0.72;
        var sun = new LinearGradientBrush(accent2, accent, 90);
        dc.DrawEllipse(sun, null, new Point(r.Width / 2, hy), r.Width * 0.14, r.Width * 0.14);
        var band = new SolidColorBrush(a);
        for (int i = 0; i < 6; i++) dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(200, a.R, a.G, a.B)), null, new Rect(r.Width / 2 - r.Width * 0.14, hy + 4 + i * 8, r.Width * 0.28, 4));
    }

    private static void Library(DrawingContext dc, Rect r, Color a, Color b, Color accent)
    {
        Gradient(dc, r, b, a, 120);
        var rnd = new Random(11);
        double shelfH = r.Height / 5;
        for (int s = 0; s < 5; s++)
        {
            double y = s * shelfH;
            dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(60, accent.R, accent.G, accent.B)), null, new Rect(0, y + shelfH - 8, r.Width, 8));
            double x = 10;
            while (x < r.Width - 20)
            {
                double bw = 14 + rnd.Next(26); double bh = shelfH * (0.55 + rnd.NextDouble() * 0.33);
                var c = Blend(accent, b, rnd.NextDouble() * 0.7);
                dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(150, c.R, c.G, c.B)), null, new Rect(x, y + shelfH - 8 - bh, bw, bh));
                x += bw + 3;
            }
        }
    }

    private static void Sakura(DrawingContext dc, Rect r, Color a, Color b, Color accent, double phase)
    {
        Gradient(dc, r, a, b, 110);
        var rnd = new Random(5);
        for (int i = 0; i < 70; i++)
        {
            double x = (rnd.NextDouble() + phase) % 1 * r.Width;
            double y = (rnd.NextDouble() + phase * 0.7) % 1 * r.Height;
            double s = 4 + rnd.NextDouble() * 7;
            var petal = new SolidColorBrush(Color.FromArgb((byte)(120 + rnd.Next(100)), accent.R, accent.G, accent.B));
            dc.PushTransform(new RotateTransform(rnd.Next(360), x, y));
            dc.DrawEllipse(petal, null, new Point(x, y), s, s * 0.6);
            dc.Pop();
        }
    }

    private static void Minimal(DrawingContext dc, Rect r, Color bg, Color accent)
    {
        dc.DrawRectangle(new SolidColorBrush(bg), null, r);
        var rg = new RadialGradientBrush(Color.FromArgb(26, accent.R, accent.G, accent.B), Color.FromArgb(0, accent.R, accent.G, accent.B))
        { Center = new Point(0.78, 0.26), GradientOrigin = new Point(0.78, 0.26), RadiusX = 0.4, RadiusY = 0.4 };
        dc.DrawRectangle(rg, null, r);
    }

    private static Color Blend(Color a, Color b, double t) => Color.FromArgb(255,
        (byte)(a.R * (1 - t) + b.R * t), (byte)(a.G * (1 - t) + b.G * t), (byte)(a.B * (1 - t) + b.B * t));
}

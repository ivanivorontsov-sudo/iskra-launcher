using System;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>Программный растеризатор курсоров: заливка BGRA + обводка, со сглаживанием по супер-сэмплингу.</summary>
internal static class CursorPainter
{
    // Буфер BGRA (не premultiplied), сверху вниз.
    private sealed class Canvas
    {
        public readonly int S; public readonly byte[] P;
        public Canvas(int s) { S = s; P = new byte[s * s * 4]; }
        public void SetAA(double x, double y, Argb c, double a)
        {
            int ix = (int)Math.Round(x), iy = (int)Math.Round(y);
            if (ix < 0 || iy < 0 || ix >= S || iy >= S || a <= 0) return;
            int o = (iy * S + ix) * 4;
            double na = Math.Clamp(a, 0, 1);
            double ba = P[o + 3] / 255.0;
            double outA = na + ba * (1 - na);
            if (outA <= 0) return;
            P[o] = Mix(P[o], c.B, na, ba, outA);
            P[o + 1] = Mix(P[o + 1], c.G, na, ba, outA);
            P[o + 2] = Mix(P[o + 2], c.R, na, ba, outA);
            P[o + 3] = (byte)Math.Round(outA * 255);
        }
        private static byte Mix(byte bg, byte fg, double na, double ba, double outA) => (byte)Math.Clamp((fg * na + bg * ba * (1 - na)) / outA, 0, 255);
    }

    private static byte[] Fill(int s, Func<double, double, (Argb c, double a)> shade)
    {
        var cv = new Canvas(s);
        const int SS = 3;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                double ar = 0; double rr = 0, gg = 0, bb = 0;
                for (int sy = 0; sy < SS; sy++)
                    for (int sx = 0; sx < SS; sx++)
                    {
                        var (c, a) = shade(x + (sx + 0.5) / SS, y + (sy + 0.5) / SS);
                        ar += a; rr += c.R * a; gg += c.G * a; bb += c.B * a;
                    }
                ar /= SS * SS;
                if (ar <= 0.001) continue;
                var col = new Argb(255, (byte)(rr / (ar * SS * SS)), (byte)(gg / (ar * SS * SS)), (byte)(bb / (ar * SS * SS)));
                cv.SetAA(x, y, col, ar);
            }
        return cv.P;
    }

    private static bool InTriangle((double x, double y) p, (double x, double y) a, (double x, double y) b, (double x, double y) c)
    {
        double d1 = Sign(p, a, b), d2 = Sign(p, b, c), d3 = Sign(p, c, a);
        bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(neg && pos);
        static double Sign((double x, double y) p1, (double x, double y) p2, (double x, double y) p3) => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static double DistToSeg(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay; double len2 = dx * dx + dy * dy;
        double t = len2 == 0 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / len2, 0, 1);
        double cx = ax + t * dx, cy = ay + t * dy;
        return Math.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
    }

    public static byte[] Arrow(int s, CursorStyle style, Argb fill, Argb outline)
    {
        double k = s / 32.0;
        // Классическая стрелка: вершины в единицах 32×32.
        var pts = new (double x, double y)[] { (1, 1), (1, 22), (7, 16), (11, 25), (14, 24), (10, 15), (18, 15) };
        double round = style is CursorStyle.Rounded or CursorStyle.Ink ? 1.4 * k : style == CursorStyle.Pixel ? 0 : 0.6 * k;
        double ow = (style == CursorStyle.Pixel ? 1.4 : 1.8) * k;
        return Fill(s, (x, y) =>
        {
            double minD = double.MaxValue; bool inside = IsInsidePoly(x / k, y / k, pts, out double edge);
            minD = edge * k;
            if (inside && minD >= round) return (fill, 1);
            double d = inside ? 0 : minD - 0;
            if (minD < ow + round) return (outline, inside ? SmoothEdge(round - minD, 1) : SmoothEdge(ow + round - minD, 1) * (1 - SmoothEdge(-(minD - ow - round), 0)));
            return (fill, 0);
        });
    }

    private static bool IsInsidePoly(double px, double py, (double x, double y)[] poly, out double dist)
    {
        bool inside = false; dist = double.MaxValue;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            var a = poly[i]; var b = poly[j];
            if (((a.y > py) != (b.y > py)) && (px < (b.x - a.x) * (py - a.y) / (b.y - a.y) + a.x)) inside = !inside;
            dist = Math.Min(dist, DistToSeg(px, py, a.x, a.y, b.x, b.y));
        }
        if (inside) return true;
        return false;
    }

    private static double SmoothEdge(double d, double soft) => Math.Clamp(d + 0.5, 0, 1);

    public static byte[] Hand(int s, CursorStyle style, Argb fill, Argb outline)
    {
        double k = s / 32.0, ow = 1.6 * k;
        // Простая «указывающая рука»: вытянутый указательный палец + кулак.
        return Fill(s, (x, y) =>
        {
            double u = x / k, v = y / k;
            bool finger = u >= 10 && u <= 14 && v >= 1 && v <= 16;
            bool palm = u >= 7 && u <= 22 && v >= 12 && v <= 27;
            bool round = Dist(u, v, 14.5, 19) < 8;
            bool inside = finger || palm || round;
            double d = EdgeDist(u, v, finger, palm);
            if (inside) return (fill, 1);
            if (d < ow) return (outline, 1);
            return (fill, 0);
        });
        static double Dist(double x, double y, double cx, double cy) => Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        static double EdgeDist(double u, double v, bool f, bool p) => f || p ? 0 : 3;
    }

    public static byte[] IBeam(int s, Argb fill, Argb outline)
    {
        double k = s / 32.0;
        return Fill(s, (x, y) =>
        {
            double u = x / k, v = y / k;
            bool bar = Math.Abs(u - 16) < 1.1 && v > 3 && v < 29;
            bool topSer = Math.Abs(v - 4) < 1.1 && Math.Abs(u - 16) < 4;
            bool botSer = Math.Abs(v - 28) < 1.1 && Math.Abs(u - 16) < 4;
            return (fill, bar || topSer || botSer ? 1 : 0);
        });
    }

    public static byte[] SizeAll(int s, Argb fill, Argb outline)
    {
        double k = s / 32.0;
        return Fill(s, (x, y) =>
        {
            double u = x / k - 16, v = y / k - 16;
            bool cross = (Math.Abs(u) < 2 && Math.Abs(v) < 12) || (Math.Abs(v) < 2 && Math.Abs(u) < 12);
            bool arrows = (Math.Abs(v) < 5 && Math.Abs(u) + Math.Abs(v) > 10 && Math.Abs(u) < 15 && Math.Abs(v) <= Math.Abs(Math.Abs(u) - 12) + 0.1) ||
                          (Math.Abs(u) < 5 && Math.Abs(v) < 15 && Math.Abs(u) <= Math.Abs(Math.Abs(v) - 12) + 0.1);
            return (fill, cross || arrows ? 1 : 0);
        });
    }

    public static byte[] No(int s, Argb accent, Argb outline)
    {
        double k = s / 32.0;
        return Fill(s, (x, y) =>
        {
            double u = x / k - 16, v = y / k - 16; double r = Math.Sqrt(u * u + v * v);
            bool ring = r > 9 && r < 13;
            bool slash = Math.Abs(u + v) < 2.2 && r < 13;
            return (accent, ring || slash ? 1 : 0);
        });
    }

    public static byte[] Busy(int s, Argb accent, Argb fill, Argb outline) => BusyFrame(s, 0, accent, fill, outline);

    public static byte[] BusyFrame(int s, double phase, Argb accent, Argb fill, Argb outline)
    {
        double k = s / 32.0;
        return Fill(s, (x, y) =>
        {
            double u = x / k - 16, v = y / k - 16; double r = Math.Sqrt(u * u + v * v);
            if (r < 8 || r > 12) return (fill, 0);
            double ang = Math.Atan2(v, u) / (2 * Math.PI) + 0.5; // 0..1
            double head = phase;
            double d = ang - head; d -= Math.Floor(d);
            double a = 1 - d; // хвост затухает
            return (accent, Math.Clamp(a, 0.15, 1));
        });
    }
}

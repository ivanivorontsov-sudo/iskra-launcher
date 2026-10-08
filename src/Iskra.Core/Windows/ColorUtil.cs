using System.Globalization;

namespace Iskra.Core.Windows;

public readonly record struct Argb(byte A, byte R, byte G, byte B)
{
    public static Argb Parse(string? hex, Argb fallback = default)
    {
        if (string.IsNullOrWhiteSpace(hex)) return fallback;
        var h = hex.Trim().TrimStart('#');
        try
        {
            if (h.Length == 6) return new(0xFF, Byte(h, 0), Byte(h, 2), Byte(h, 4));
            if (h.Length == 8) return new(Byte(h, 0), Byte(h, 2), Byte(h, 4), Byte(h, 6));
            if (h.Length == 3) { var x = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]); return new(0xFF, Byte(x, 0), Byte(x, 2), Byte(x, 4)); }
        }
        catch (FormatException) { }
        return fallback;
        static byte Byte(string s, int i) => byte.Parse(s.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }

    public static bool TryParse(string? hex, out Argb c)
    {
        var sentinel = new Argb(1, 2, 3, 4);
        c = Parse(hex, sentinel);
        return c != sentinel || string.Equals(hex?.Trim().TrimStart('#'), "01020304", StringComparison.OrdinalIgnoreCase);
    }

    public string ToHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";
    public string ToHexRgb() => $"#{R:X2}{G:X2}{B:X2}";
    /// <summary>COLORREF/ABGR для DWM и реестра: 0xAABBGGRR.</summary>
    public uint ToAbgr(byte alpha = 0xFF) => (uint)(alpha << 24 | B << 16 | G << 8 | R);
    public uint ToColorRef() => (uint)(B << 16 | G << 8 | R);
    public uint ToArgbUint(byte? alpha = null) => (uint)((alpha ?? A) << 24 | R << 16 | G << 8 | B);
    public double Luminance()
    {
        static double Ch(byte v) { var c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        return 0.2126 * Ch(R) + 0.7152 * Ch(G) + 0.0722 * Ch(B);
    }

    public (double H, double S, double L) ToHsl()
    {
        double r = R / 255.0, g = G / 255.0, b = B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double h = 0, s, l = (max + min) / 2;
        if (max == min) s = 0;
        else
        {
            double d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h /= 6;
        }
        return (h, s, l);
    }

    public static Argb FromHsl(double h, double s, double l, byte a = 0xFF)
    {
        double r, g, b;
        if (s == 0) r = g = b = l;
        else
        {
            double q = l < 0.5 ? l * (1 + s) : l + s - l * s, p = 2 * l - q;
            r = Hue(p, q, h + 1.0 / 3); g = Hue(p, q, h); b = Hue(p, q, h - 1.0 / 3);
        }
        return new(a, (byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1; if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 0.5) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
    }

    public Argb Shade(double deltaL)
    {
        var (h, s, l) = ToHsl();
        return FromHsl(h, s, Math.Clamp(l + deltaL, 0, 1), A);
    }

    public Argb WithAlpha(byte a) => this with { A = a };

    /// <summary>Коэффициент контраста WCAG между двумя цветами (1..21).</summary>
    public static double Contrast(Argb a, Argb b)
    {
        double la = a.Luminance(), lb = b.Luminance();
        double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
        return (hi + 0.05) / (lo + 0.05);
    }

    /// <summary>Смешать поверх непрозрачного фона (учёт альфы) — получить фактически видимый цвет.</summary>
    public Argb Over(Argb bg)
    {
        double fa = A / 255.0, ba = 1 - fa;
        byte M(byte f, byte b) => (byte)Math.Round(f * fa + b * ba);
        return new Argb(0xFF, M(R, bg.R), M(G, bg.G), M(B, bg.B));
    }

    /// <summary>Гарантированно читаемый текст поверх bg: вернуть желаемый, если контраст достаточен, иначе чёрный/белый.</summary>
    public static Argb Readable(Argb desired, Argb bg, double min = 4.0)
    {
        var eff = desired.A < 0xFF ? desired.Over(bg) : desired;
        if (Contrast(eff, bg) >= min) return desired;
        var black = new Argb(0xFF, 0x12, 0x12, 0x14);
        var white = new Argb(0xFF, 0xF4, 0xF4, 0xF6);
        return Contrast(white, bg) >= Contrast(black, bg) ? white : black;
    }
}

/// <summary>Палитра акцента Windows (8 оттенков RGBA, 32 байта) для HKCU\…\Explorer\Accent\AccentPalette.</summary>
public static class AccentPalette
{
    public static Argb[] Shades(Argb accent) => new[]
    {
        accent.Shade(+0.30), accent.Shade(+0.20), accent.Shade(+0.10), accent,
        accent.Shade(-0.10), accent.Shade(-0.20), accent.Shade(-0.30), new Argb(0xFF, 0x69, 0x69, 0x69),
    };

    public static byte[] Bytes(Argb accent)
    {
        var shades = Shades(accent);
        var bytes = new byte[32];
        for (int i = 0; i < 8; i++)
        {
            bytes[i * 4] = shades[i].R; bytes[i * 4 + 1] = shades[i].G; bytes[i * 4 + 2] = shades[i].B; bytes[i * 4 + 3] = 0;
        }
        return bytes;
    }
}

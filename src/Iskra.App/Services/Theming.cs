using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Iskra.Core.Themes;
using CW = Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>Переводит ThemeDefinition в кисти, шрифты и параметры WPF. Шрифты — из встроенных ресурсов (pack://).</summary>
internal sealed class Theming
{
    public ThemeDefinition Theme { get; }

    public Theming(ThemeDefinition t) { Theme = t; }

    public static Color Col(string hex, Color fb = default)
    {
        var a = CW.Argb.Parse(hex, new CW.Argb(fb.A, fb.R, fb.G, fb.B));
        return Color.FromArgb(a.A, a.R, a.G, a.B);
    }

    public SolidColorBrush Brush(string hex) => Freeze(new SolidColorBrush(Col(hex)));
    public Color Background => Col(Theme.Palette.Background);
    public Color Background2 => Col(Theme.Palette.Background2);
    public SolidColorBrush Foreground => Brush(Theme.Palette.Foreground);
    public SolidColorBrush ForegroundDim => Brush(Theme.Palette.ForegroundDim);
    public SolidColorBrush Accent => Brush(Theme.Palette.Accent);
    public SolidColorBrush Accent2 => Brush(Theme.Palette.Accent2);
    public SolidColorBrush AccentForeground => Brush(Theme.Palette.AccentForeground);
    public SolidColorBrush Surface => Brush(Theme.Palette.Surface);
    public SolidColorBrush SurfaceHover => Brush(Theme.Palette.SurfaceHover);
    public SolidColorBrush BorderBrush => Brush(Theme.Palette.Border);

    /// <summary>Фон окна: при материале слюды/акрила — полупрозрачный поверх DWM; иначе градиент/сплошной.</summary>
    public Brush WindowBackground(bool reduceEffects)
    {
        var o = Theme.Palette.BackgroundOpacity;
        bool material = !reduceEffects && Theme.Effects.Material is Material.Mica or Material.Acrylic or Material.MicaAlt;
        byte alpha = material ? (byte)Math.Clamp(o * 255, 0, 255) : (byte)255;
        var c1 = Background; c1.A = alpha;
        if (Theme.Effects.Material == Material.Gradient || Theme.Effects.Background is BackgroundFx.Aurora or BackgroundFx.NeonGrid)
        {
            var c2 = Background2; c2.A = alpha;
            return Freeze(new LinearGradientBrush(c1, c2, 120));
        }
        return Freeze(new SolidColorBrush(c1));
    }

    public FontFamily UiFont => Font(Theme.Fonts.Ui);
    public FontFamily TitleFont => Font(Theme.Fonts.Title);

    private static readonly Dictionary<string, FontFamily> FontCache = new();

    public static FontFamily Font(string family)
    {
        if (FontCache.TryGetValue(family, out var f)) return f;
        // Семейства подхватываются из встроенных ресурсов Fonts/ по имени семейства.
        try { f = new FontFamily(new Uri("pack://application:,,,/"), "./Fonts/#" + family); }
        catch { f = new FontFamily(family); }
        FontCache[family] = f;
        return f;
    }

    public FontWeight TitleWeight => Theme.Fonts.TitleWeight switch
    {
        "Light" => FontWeights.Light,
        "SemiBold" => FontWeights.SemiBold,
        "Bold" => FontWeights.Bold,
        _ => FontWeights.Normal,
    };

    public CornerRadius Corner => new(Theme.Shape.Radius);
    public CornerRadius TileCorner => new(Theme.Shape.TileRadius);

    public string CaseTransform(string s) => Theme.Fonts.TitleCase switch
    {
        "Upper" => s.ToUpperInvariant(),
        "Lower" => s.ToLowerInvariant(),
        _ => s,
    };

    private static T Freeze<T>(T f) where T : Freezable { if (f.CanFreeze) f.Freeze(); return f; }

    public Color SegoeGlyphColor => Col(Theme.Palette.Foreground);
    public FontFamily SymbolFont => new("Segoe Fluent Icons, Segoe MDL2 Assets");
}

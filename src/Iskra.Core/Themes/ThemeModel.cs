using System.Text.Json;
using System.Text.Json.Serialization;

namespace Iskra.Core.Themes;

public enum Material { Solid, Mica, Acrylic, MicaAlt, Gradient }
public enum BackgroundFx { None, Aurora, Stars, Petals, Snow, Scanlines, NeonGrid, Paper, Pixels, Bokeh }
public enum OpenAnimation { ScaleFade, Spring, SlideUp, Turnstile, Typewriter, Glitch, Stepped, Drop, Fade }
public enum SoundPack { None, Glass, Metro, Soft, Neon, Paper, Terminal, Chip, Space, Zen }
public enum CursorStyle { Classic, Rounded, Pixel, Neon, Ink, Minimal }
public enum WallpaperKind { MicaWaves, Tiles, Spotlight, NeonCity, Paper, Terminal, Aurora, Pixel, Minimal, Space, Sunset, Library, Sakura }
public enum IconSpacing { Unchanged, Compact, Normal, Wide }
public enum CornerStyle { Default, Round, RoundSmall, Square }
public enum WindowBackdrop { Default, Mica, Acrylic, MicaAlt, None }
public enum TaskbarAlign { Unchanged, Left, Center }
public enum StartStyle { Menu, FullScreen }

public sealed class ThemePalette
{
    public string Background { get; set; } = "#FF202020";
    /// <summary>Второй цвет фона (градиент / анимация).</summary>
    public string Background2 { get; set; } = "#FF2B2B2B";
    public string Surface { get; set; } = "#14FFFFFF";
    public string SurfaceHover { get; set; } = "#26FFFFFF";
    public string Foreground { get; set; } = "#FFFFFFFF";
    public string ForegroundDim { get; set; } = "#B3FFFFFF";
    public string Accent { get; set; } = "#FF5AA9FF";
    public string Accent2 { get; set; } = "#FF9B7BFF";
    public string AccentForeground { get; set; } = "#FF000000";
    public string Border { get; set; } = "#1FFFFFFF";
    public string Glow { get; set; } = "#005AA9FF";
    /// <summary>Непрозрачность собственного фона поверх материала (0 — чистая слюда, 1 — сплошной цвет).</summary>
    public double BackgroundOpacity { get; set; } = 0.55;
}

public sealed class ThemeFonts
{
    public string Ui { get; set; } = "Inter";
    public string Title { get; set; } = "Inter";
    public string TitleWeight { get; set; } = "SemiBold";
    public double BaseSize { get; set; } = 14;
    public double TitleSize { get; set; } = 22;
    public double SearchSize { get; set; } = 22;
    /// <summary>Заголовки строчными (как в плиточном стиле) или прописными.</summary>
    public string TitleCase { get; set; } = "Normal"; // Normal | Lower | Upper
}

public sealed class ThemeShape
{
    public double Radius { get; set; } = 8;
    public double TileRadius { get; set; } = 6;
    public double BorderThickness { get; set; } = 1;
    public double TileSize { get; set; } = 96;
    public double TileGap { get; set; } = 8;
    /// <summary>Пиксельная «ступенчатая» рамка (ретро).</summary>
    public bool PixelBorder { get; set; }
}

public sealed class ThemeEffects
{
    public Material Material { get; set; } = Material.Mica;
    public BackgroundFx Background { get; set; } = BackgroundFx.None;
    public OpenAnimation Open { get; set; } = OpenAnimation.Spring;
    public double Speed { get; set; } = 1.0;
    public bool Parallax { get; set; } = true;
    public bool Reveal { get; set; } = true;
    public bool Glow { get; set; }
    public bool LiveTiles { get; set; }
}

/// <summary>Что тема меняет в самой Windows.</summary>
public sealed class SystemProfile
{
    public bool AppsDark { get; set; } = true;
    public bool SystemDark { get; set; } = true;
    public string Accent { get; set; } = "#FF5AA9FF";
    public bool AccentOnTitleBars { get; set; }
    public bool AccentOnTaskbar { get; set; }
    public bool Transparency { get; set; } = true;
    public WallpaperKind Wallpaper { get; set; } = WallpaperKind.MicaWaves;
    public bool Slideshow { get; set; }
    public int SlideshowMinutes { get; set; } = 30;
    public bool LockScreen { get; set; } = true;
    public CursorStyle Cursor { get; set; } = CursorStyle.Rounded;
    public string CursorFill { get; set; } = "#FFFFFFFF";
    public string CursorOutline { get; set; } = "#FF000000";
    public int CursorSize { get; set; } = 32;
    public SoundPack Sounds { get; set; } = SoundPack.Glass;
    public IconSpacing IconSpacing { get; set; } = IconSpacing.Normal;
    public CornerStyle Corners { get; set; } = CornerStyle.Round;
    public WindowBackdrop Backdrop { get; set; } = WindowBackdrop.Default;
    /// <summary>null — по умолчанию, "accent" — цвет акцента, иначе #RRGGBB.</summary>
    public string? BorderColor { get; set; }
    public string? CaptionColor { get; set; }
    public string? CaptionTextColor { get; set; }
    public bool ClassicContextMenu { get; set; }
    public bool ExplorerCompact { get; set; }
    public TaskbarAlign TaskbarAlignment { get; set; } = TaskbarAlign.Unchanged;
    /// <summary>Тема рекомендует собственную панель задач (док) вместо системной.</summary>
    public bool PreferDock { get; set; }
    /// <summary>Шрифт интерфейса Windows (ЭКСПЕРИМЕНТАЛЬНО, применяется только если включено отдельно).</summary>
    public string? SystemFont { get; set; }
}

public sealed class ThemeDefinition
{
    public string Id { get; set; } = "custom";
    public string Name { get; set; } = "Своя тема";
    public string NameEn { get; set; } = "Custom theme";
    public string Description { get; set; } = string.Empty;
    public string DescriptionEn { get; set; } = string.Empty;
    public bool Dark { get; set; } = true;
    public bool BuiltIn { get; set; }
    public string? BaseThemeId { get; set; }
    public ThemePalette Palette { get; set; } = new();
    public ThemeFonts Fonts { get; set; } = new();
    public ThemeShape Shape { get; set; } = new();
    public ThemeEffects Effects { get; set; } = new();
    public SoundPack Sound { get; set; } = SoundPack.Glass;
    public StartStyle StartStyle { get; set; } = StartStyle.Menu;
    /// <summary>Значок кнопки «Пуск» в доке — собственный символ, не логотип Windows.</summary>
    public string StartGlyph { get; set; } = "✦";
    public SystemProfile System { get; set; } = new();

    public string DisplayName(bool english) => english ? NameEn : Name;
    public string DisplayDescription(bool english) => english && DescriptionEn.Length > 0 ? DescriptionEn : Description;

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public ThemeDefinition Clone() => JsonSerializer.Deserialize<ThemeDefinition>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;
    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);
    public static ThemeDefinition? FromJson(string json)
    {
        try { return JsonSerializer.Deserialize<ThemeDefinition>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }
}

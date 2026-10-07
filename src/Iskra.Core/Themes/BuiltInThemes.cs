namespace Iskra.Core.Themes;

/// <summary>
/// Встроенные темы. Только собственные названия, цвета и OFL-шрифты — без логотипов, фирменных
/// значков и чужих торговых марок. «Вдохновлено» стилями, но не копирует их ресурсы.
/// </summary>
public static class BuiltInThemes
{
    public static IReadOnlyList<ThemeDefinition> All { get; } = Create();

    public static ThemeDefinition Get(string id) => All.FirstOrDefault(t => t.Id == id) ?? All[0];

    private static IReadOnlyList<ThemeDefinition> Create() => Mark(new[]
    {
        new ThemeDefinition
        {
            Id = "mica", Name = "Слюда", NameEn = "Mica Glass",
            Description = "Матовое стекло, мягкие пружинные анимации, аккуратные скругления.",
            DescriptionEn = "Frosted glass, soft spring motion, gentle rounded corners.",
            Dark = true, StartGlyph = "✦", Sound = SoundPack.Glass,
            Palette = new() { Background = "#FF1C2230", Background2 = "#FF232B3D", Surface = "#12FFFFFF", SurfaceHover = "#24FFFFFF",
                Foreground = "#FFF3F6FC", ForegroundDim = "#A8E4EAF6", Accent = "#FF5AA9FF", Accent2 = "#FF9B8CFF", AccentForeground = "#FF06121F",
                Border = "#1EFFFFFF", Glow = "#405AA9FF", BackgroundOpacity = 0.35 },
            Fonts = new() { Ui = "Inter", Title = "Inter", TitleWeight = "SemiBold", BaseSize = 14, TitleSize = 22, SearchSize = 22 },
            Shape = new() { Radius = 10, TileRadius = 8, TileSize = 92 },
            Effects = new() { Material = Material.Mica, Open = OpenAnimation.Spring, Parallax = true, Reveal = true },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FF5AA9FF", Transparency = true, Wallpaper = WallpaperKind.MicaWaves,
                Cursor = CursorStyle.Rounded, CursorFill = "#FFFFFFFF", CursorOutline = "#FF1B2333", Sounds = SoundPack.Glass,
                Corners = CornerStyle.Round, Backdrop = WindowBackdrop.Mica, IconSpacing = IconSpacing.Normal, TaskbarAlignment = TaskbarAlign.Center,
                SystemFont = "Inter" },
        },
        new ThemeDefinition
        {
            Id = "tiles", Name = "Плитки", NameEn = "Tiles",
            Description = "Плоские живые плитки, переворачивающиеся анимации, тонкий крупный шрифт, строчные заголовки.",
            DescriptionEn = "Flat live tiles that flip, thin large type and lowercase headings.",
            Dark = true, StartGlyph = "▦", Sound = SoundPack.Metro, StartStyle = StartStyle.FullScreen,
            Palette = new() { Background = "#FF0A0A0A", Background2 = "#FF161616", Surface = "#FF1F1F1F", SurfaceHover = "#FF2D2D2D",
                Foreground = "#FFFFFFFF", ForegroundDim = "#FF9E9E9E", Accent = "#FF1B6EF3", Accent2 = "#FF00A3A3", AccentForeground = "#FFFFFFFF",
                Border = "#00000000", Glow = "#00000000", BackgroundOpacity = 0.94 },
            Fonts = new() { Ui = "Open Sans", Title = "Open Sans Light", TitleWeight = "Light", BaseSize = 14, TitleSize = 40, SearchSize = 24, TitleCase = "Lower" },
            Shape = new() { Radius = 0, TileRadius = 0, BorderThickness = 0, TileSize = 104, TileGap = 6 },
            Effects = new() { Material = Material.Solid, Open = OpenAnimation.Turnstile, Parallax = false, Reveal = true, LiveTiles = true, Speed = 1.1 },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FF1B6EF3", AccentOnTitleBars = true, AccentOnTaskbar = true, Transparency = true,
                Wallpaper = WallpaperKind.Tiles, Cursor = CursorStyle.Classic, CursorFill = "#FFFFFFFF", CursorOutline = "#FF000000", Sounds = SoundPack.Metro,
                Corners = CornerStyle.Square, Backdrop = WindowBackdrop.None, BorderColor = "accent", IconSpacing = IconSpacing.Wide,
                TaskbarAlignment = TaskbarAlign.Left, PreferDock = true, SystemFont = "Open Sans" },
        },
        new ThemeDefinition
        {
            Id = "spotlight", Name = "Прожектор", NameEn = "Spotlight",
            Description = "Светлая строка поиска по центру, размытое стекло, крупные скругления и плавное появление.",
            DescriptionEn = "Light centred search bar, blurred glass, big radii and a smooth fade-in.",
            Dark = false, StartGlyph = "◎", Sound = SoundPack.Soft,
            Palette = new() { Background = "#FFF4F5F8", Background2 = "#FFE9ECF3", Surface = "#0F000000", SurfaceHover = "#1A000000",
                Foreground = "#FF16181D", ForegroundDim = "#9916181D", Accent = "#FF2F7CF6", Accent2 = "#FFAF52DE", AccentForeground = "#FFFFFFFF",
                Border = "#1A000000", Glow = "#302F7CF6", BackgroundOpacity = 0.55 },
            Fonts = new() { Ui = "Inter", Title = "Inter", TitleWeight = "SemiBold", BaseSize = 14, TitleSize = 20, SearchSize = 26 },
            Shape = new() { Radius = 16, TileRadius = 14, TileSize = 88 },
            Effects = new() { Material = Material.Acrylic, Open = OpenAnimation.ScaleFade, Parallax = true, Reveal = false, Speed = 1.15 },
            System = new() { AppsDark = false, SystemDark = false, Accent = "#FF2F7CF6", Transparency = true, Wallpaper = WallpaperKind.Spotlight,
                Cursor = CursorStyle.Minimal, CursorFill = "#FF111111", CursorOutline = "#FFFFFFFF", Sounds = SoundPack.Soft,
                Corners = CornerStyle.Round, Backdrop = WindowBackdrop.Acrylic, TaskbarAlignment = TaskbarAlign.Center, SystemFont = "Inter" },
        },
        new ThemeDefinition
        {
            Id = "neon", Name = "Неон", NameEn = "Neon",
            Description = "Киберпанк: светящиеся рамки, сетка до горизонта, глитч при открытии.",
            DescriptionEn = "Cyberpunk: glowing borders, horizon grid, glitch on open.",
            Dark = true, StartGlyph = "⟁", Sound = SoundPack.Neon,
            Palette = new() { Background = "#FF0B0614", Background2 = "#FF1A0B2E", Surface = "#1AFF2BD6", SurfaceHover = "#33FF2BD6",
                Foreground = "#FFF6EDFF", ForegroundDim = "#B300F0FF", Accent = "#FFFF2BD6", Accent2 = "#FF00F0FF", AccentForeground = "#FF0B0614",
                Border = "#AAFF2BD6", Glow = "#CCFF2BD6", BackgroundOpacity = 0.86 },
            Fonts = new() { Ui = "Exo 2", Title = "Russo One", TitleWeight = "Normal", BaseSize = 14, TitleSize = 24, SearchSize = 24, TitleCase = "Upper" },
            Shape = new() { Radius = 4, TileRadius = 3, BorderThickness = 1.5, TileSize = 92 },
            Effects = new() { Material = Material.Acrylic, Background = BackgroundFx.NeonGrid, Open = OpenAnimation.Glitch, Glow = true, Parallax = true, Reveal = true },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FFFF2BD6", AccentOnTaskbar = true, Transparency = true, Wallpaper = WallpaperKind.NeonCity,
                Cursor = CursorStyle.Neon, CursorFill = "#FF0B0614", CursorOutline = "#FF00F0FF", Sounds = SoundPack.Neon,
                Corners = CornerStyle.RoundSmall, Backdrop = WindowBackdrop.Acrylic, BorderColor = "accent", CaptionColor = "#FF140A24", CaptionTextColor = "#FF00F0FF",
                TaskbarAlignment = TaskbarAlign.Center, PreferDock = true, SystemFont = "Exo 2" },
        },
        new ThemeDefinition
        {
            Id = "notebook", Name = "Тетрадь", NameEn = "Notebook",
            Description = "Бумага в линейку, рукописные кириллические шрифты, звук карандаша, листок падает на стол.",
            DescriptionEn = "Lined paper, handwritten Cyrillic fonts, pencil sounds, a sheet dropping onto the desk.",
            Dark = false, StartGlyph = "✎", Sound = SoundPack.Paper,
            Palette = new() { Background = "#FFFBF7EC", Background2 = "#FFF3ECD9", Surface = "#0F1F2A6B", SurfaceHover = "#1F1F2A6B",
                Foreground = "#FF1F2A6B", ForegroundDim = "#B31F2A6B", Accent = "#FF2B4FA8", Accent2 = "#FFD9534F", AccentForeground = "#FFFBF7EC",
                Border = "#552B4FA8", Glow = "#00000000", BackgroundOpacity = 1.0 },
            Fonts = new() { Ui = "Caveat", Title = "Marck Script", TitleWeight = "Normal", BaseSize = 20, TitleSize = 30, SearchSize = 30 },
            Shape = new() { Radius = 3, TileRadius = 3, BorderThickness = 1.2, TileSize = 96 },
            Effects = new() { Material = Material.Solid, Background = BackgroundFx.Paper, Open = OpenAnimation.Drop, Parallax = true, Reveal = false },
            System = new() { AppsDark = false, SystemDark = false, Accent = "#FF2B4FA8", Transparency = false, Wallpaper = WallpaperKind.Paper,
                Cursor = CursorStyle.Ink, CursorFill = "#FF2B4FA8", CursorOutline = "#FFFBF7EC", Sounds = SoundPack.Paper,
                Corners = CornerStyle.RoundSmall, Backdrop = WindowBackdrop.None, BorderColor = "#FF8FA8D8", IconSpacing = IconSpacing.Wide,
                TaskbarAlignment = TaskbarAlign.Center, SystemFont = "Caveat" },
        },
        new ThemeDefinition
        {
            Id = "terminal", Name = "Терминал", NameEn = "Terminal",
            Description = "Зелёный моноширинный текст на чёрном, строки развёртки, печатная машинка и писк.",
            DescriptionEn = "Green monospace on black, scanlines, typewriter reveal and beeps.",
            Dark = true, StartGlyph = ">_", Sound = SoundPack.Terminal,
            Palette = new() { Background = "#FF050A06", Background2 = "#FF0A140C", Surface = "#1433FF66", SurfaceHover = "#2633FF66",
                Foreground = "#FF39FF7A", ForegroundDim = "#9939FF7A", Accent = "#FF39FF7A", Accent2 = "#FFFFB000", AccentForeground = "#FF050A06",
                Border = "#6639FF7A", Glow = "#5539FF7A", BackgroundOpacity = 0.97 },
            Fonts = new() { Ui = "JetBrains Mono", Title = "JetBrains Mono", TitleWeight = "Bold", BaseSize = 13.5, TitleSize = 18, SearchSize = 20, TitleCase = "Upper" },
            Shape = new() { Radius = 0, TileRadius = 0, BorderThickness = 1, TileSize = 88 },
            Effects = new() { Material = Material.Solid, Background = BackgroundFx.Scanlines, Open = OpenAnimation.Typewriter, Glow = true, Parallax = false, Reveal = false },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FF1FA34A", AccentOnTitleBars = true, Transparency = false, Wallpaper = WallpaperKind.Terminal,
                Cursor = CursorStyle.Pixel, CursorFill = "#FF39FF7A", CursorOutline = "#FF000000", Sounds = SoundPack.Terminal,
                Corners = CornerStyle.Square, Backdrop = WindowBackdrop.None, BorderColor = "#FF1FA34A", CaptionColor = "#FF050A06", CaptionTextColor = "#FF39FF7A",
                ClassicContextMenu = true, ExplorerCompact = true, IconSpacing = IconSpacing.Compact, TaskbarAlignment = TaskbarAlign.Left, SystemFont = "JetBrains Mono" },
        },
        new ThemeDefinition
        {
            Id = "aurora", Name = "Сияние", NameEn = "Aurora",
            Description = "Живой градиент северного сияния, мягкие формы, плавные пружины, слайд-шоу обоев.",
            DescriptionEn = "Living aurora gradient, soft shapes, smooth springs and a wallpaper slideshow.",
            Dark = true, StartGlyph = "❋", Sound = SoundPack.Zen,
            Palette = new() { Background = "#FF07111F", Background2 = "#FF0E2236", Surface = "#143DF5C2", SurfaceHover = "#263DF5C2",
                Foreground = "#FFF0FFFB", ForegroundDim = "#B3CDEFE8", Accent = "#FF3DF5C2", Accent2 = "#FF8A5CFF", AccentForeground = "#FF04121C",
                Border = "#283DF5C2", Glow = "#553DF5C2", BackgroundOpacity = 0.6 },
            Fonts = new() { Ui = "Comfortaa", Title = "Comfortaa", TitleWeight = "Bold", BaseSize = 14, TitleSize = 22, SearchSize = 22 },
            Shape = new() { Radius = 20, TileRadius = 16, TileSize = 92 },
            Effects = new() { Material = Material.Acrylic, Background = BackgroundFx.Aurora, Open = OpenAnimation.Spring, Glow = true, Parallax = true, Reveal = true, Speed = 0.9 },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FF2FC79F", Transparency = true, Wallpaper = WallpaperKind.Aurora, Slideshow = true, SlideshowMinutes = 20,
                Cursor = CursorStyle.Rounded, CursorFill = "#FFE9FFF9", CursorOutline = "#FF0B3B44", Sounds = SoundPack.Zen,
                Corners = CornerStyle.Round, Backdrop = WindowBackdrop.MicaAlt, TaskbarAlignment = TaskbarAlign.Center, SystemFont = "Comfortaa" },
        },
        new ThemeDefinition
        {
            Id = "retro", Name = "8 бит", NameEn = "8-bit",
            Description = "Пиксельный шрифт, ступенчатые рамки, покадровая анимация и чиптюн-звуки.",
            DescriptionEn = "Pixel font, stepped borders, frame-by-frame motion and chiptune sounds.",
            Dark = true, StartGlyph = "▶", Sound = SoundPack.Chip,
            Palette = new() { Background = "#FF1D1240", Background2 = "#FF2A1B5C", Surface = "#FF2E2266", SurfaceHover = "#FF46358A",
                Foreground = "#FFFFF1E8", ForegroundDim = "#FFC2C3C7", Accent = "#FFFF004D", Accent2 = "#FFFFEC27", AccentForeground = "#FFFFF1E8",
                Border = "#FFFFF1E8", Glow = "#00000000", BackgroundOpacity = 1.0 },
            Fonts = new() { Ui = "Press Start 2P", Title = "Press Start 2P", TitleWeight = "Normal", BaseSize = 10.5, TitleSize = 16, SearchSize = 16, TitleCase = "Upper" },
            Shape = new() { Radius = 0, TileRadius = 0, BorderThickness = 3, TileSize = 96, PixelBorder = true },
            Effects = new() { Material = Material.Solid, Background = BackgroundFx.Pixels, Open = OpenAnimation.Stepped, Parallax = false, Reveal = false },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FFFF004D", AccentOnTitleBars = true, AccentOnTaskbar = true, Transparency = false,
                Wallpaper = WallpaperKind.Pixel, Cursor = CursorStyle.Pixel, CursorFill = "#FFFFF1E8", CursorOutline = "#FF1D1240", CursorSize = 48,
                Sounds = SoundPack.Chip, Corners = CornerStyle.Square, Backdrop = WindowBackdrop.None, BorderColor = "#FFFFEC27",
                ClassicContextMenu = true, IconSpacing = IconSpacing.Wide, TaskbarAlignment = TaskbarAlign.Left, PreferDock = true },
        },
        new ThemeDefinition
        {
            Id = "minimal", Name = "Минимал", NameEn = "Minimal",
            Description = "Чистый белый лист, один акцент, никаких лишних эффектов — максимум скорости.",
            DescriptionEn = "A clean white sheet, one accent, no extra effects — maximum speed.",
            Dark = false, StartGlyph = "○", Sound = SoundPack.Soft,
            Palette = new() { Background = "#FFFFFFFF", Background2 = "#FFF6F6F6", Surface = "#0A000000", SurfaceHover = "#14000000",
                Foreground = "#FF111111", ForegroundDim = "#FF777777", Accent = "#FF3B5BDB", Accent2 = "#FF111111", AccentForeground = "#FFFFFFFF",
                Border = "#14000000", Glow = "#00000000", BackgroundOpacity = 0.92 },
            Fonts = new() { Ui = "Manrope", Title = "Manrope", TitleWeight = "Bold", BaseSize = 14, TitleSize = 20, SearchSize = 22 },
            Shape = new() { Radius = 10, TileRadius = 10, TileSize = 84 },
            Effects = new() { Material = Material.Mica, Open = OpenAnimation.Fade, Parallax = false, Reveal = false, Speed = 1.3 },
            System = new() { AppsDark = false, SystemDark = false, Accent = "#FF3B5BDB", Transparency = false, Wallpaper = WallpaperKind.Minimal,
                Cursor = CursorStyle.Minimal, CursorFill = "#FF111111", CursorOutline = "#FFFFFFFF", Sounds = SoundPack.Soft,
                Corners = CornerStyle.Round, Backdrop = WindowBackdrop.Mica, TaskbarAlignment = TaskbarAlign.Center, SystemFont = "Manrope" },
        },
        new ThemeDefinition
        {
            Id = "space", Name = "Космос", NameEn = "Space",
            Description = "Летящие звёзды с параллаксом, туманности, глубокий синий, слайд-шоу из космических обоев.",
            DescriptionEn = "Flying stars with parallax, nebulae, deep blue and a space wallpaper slideshow.",
            Dark = true, StartGlyph = "✺", Sound = SoundPack.Space,
            Palette = new() { Background = "#FF050816", Background2 = "#FF0D1433", Surface = "#147C9CFF", SurfaceHover = "#2A7C9CFF",
                Foreground = "#FFE6ECFF", ForegroundDim = "#A6B9C6F0", Accent = "#FF7C9CFF", Accent2 = "#FFFF9A5C", AccentForeground = "#FF050816",
                Border = "#287C9CFF", Glow = "#667C9CFF", BackgroundOpacity = 0.8 },
            Fonts = new() { Ui = "Exo 2", Title = "Exo 2", TitleWeight = "SemiBold", BaseSize = 14, TitleSize = 24, SearchSize = 22 },
            Shape = new() { Radius = 14, TileRadius = 12, TileSize = 92 },
            Effects = new() { Material = Material.Acrylic, Background = BackgroundFx.Stars, Open = OpenAnimation.ScaleFade, Glow = true, Parallax = true, Reveal = true },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FF6A86F5", Transparency = true, Wallpaper = WallpaperKind.Space, Slideshow = true,
                Cursor = CursorStyle.Neon, CursorFill = "#FF0D1433", CursorOutline = "#FF9DB4FF", Sounds = SoundPack.Space,
                Corners = CornerStyle.Round, Backdrop = WindowBackdrop.Acrylic, TaskbarAlignment = TaskbarAlign.Center, SystemFont = "Exo 2" },
        },
        new ThemeDefinition
        {
            Id = "sunset", Name = "Закат", NameEn = "Sunset",
            Description = "Ретровейв: закатное солнце, полосатый горизонт, рукописные заголовки, боке.",
            DescriptionEn = "Retrowave: a setting sun, striped horizon, script headings and bokeh.",
            Dark = true, StartGlyph = "☀", Sound = SoundPack.Soft,
            Palette = new() { Background = "#FF2B1055", Background2 = "#FF7A1F7A", Surface = "#1AFFFFFF", SurfaceHover = "#2EFFFFFF",
                Foreground = "#FFFFF4EC", ForegroundDim = "#CCFFD7C2", Accent = "#FFFF7A59", Accent2 = "#FFFFC857", AccentForeground = "#FF2B1055",
                Border = "#33FFC857", Glow = "#66FF7A59", BackgroundOpacity = 0.78 },
            Fonts = new() { Ui = "Manrope", Title = "Pacifico", TitleWeight = "Normal", BaseSize = 14, TitleSize = 26, SearchSize = 22 },
            Shape = new() { Radius = 14, TileRadius = 12, TileSize = 92 },
            Effects = new() { Material = Material.Gradient, Background = BackgroundFx.Bokeh, Open = OpenAnimation.SlideUp, Glow = true, Parallax = true, Reveal = true },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FFE8613F", AccentOnTaskbar = true, Transparency = true, Wallpaper = WallpaperKind.Sunset,
                Cursor = CursorStyle.Rounded, CursorFill = "#FFFFF4EC", CursorOutline = "#FF7A1F7A", Sounds = SoundPack.Soft,
                Corners = CornerStyle.Round, Backdrop = WindowBackdrop.Acrylic, BorderColor = "#FFFF7A59", TaskbarAlignment = TaskbarAlign.Center },
        },
        new ThemeDefinition
        {
            Id = "library", Name = "Библиотека", NameEn = "Library",
            Description = "Тёплая кожа и золото, книжная антиква, неторопливые мягкие переходы.",
            DescriptionEn = "Warm leather and gold, bookish serif type and unhurried soft transitions.",
            Dark = true, StartGlyph = "❦", Sound = SoundPack.Paper,
            Palette = new() { Background = "#FF1E1611", Background2 = "#FF2C2018", Surface = "#14F3E6CF", SurfaceHover = "#24F3E6CF",
                Foreground = "#FFF3E6CF", ForegroundDim = "#B3E0CFAF", Accent = "#FFC8963E", Accent2 = "#FF8E3B2E", AccentForeground = "#FF1E1611",
                Border = "#40C8963E", Glow = "#33C8963E", BackgroundOpacity = 0.9 },
            Fonts = new() { Ui = "Lora", Title = "Lora SemiBold", TitleWeight = "SemiBold", BaseSize = 15, TitleSize = 24, SearchSize = 22 },
            Shape = new() { Radius = 6, TileRadius = 4, TileSize = 92 },
            Effects = new() { Material = Material.MicaAlt, Open = OpenAnimation.Fade, Parallax = true, Reveal = false, Speed = 0.85 },
            System = new() { AppsDark = true, SystemDark = true, Accent = "#FFB07F2C", AccentOnTitleBars = true, Transparency = true, Wallpaper = WallpaperKind.Library,
                Cursor = CursorStyle.Classic, CursorFill = "#FFF3E6CF", CursorOutline = "#FF2C2018", Sounds = SoundPack.Paper,
                Corners = CornerStyle.RoundSmall, Backdrop = WindowBackdrop.MicaAlt, BorderColor = "#FF8A6A35", TaskbarAlignment = TaskbarAlign.Center, SystemFont = "Lora" },
        },
        new ThemeDefinition
        {
            Id = "sakura", Name = "Сакура", NameEn = "Sakura",
            Description = "Нежно-розовая весна, падающие лепестки, рукописный шрифт и колокольчики.",
            DescriptionEn = "Soft pink spring, falling petals, handwritten type and chimes.",
            Dark = false, StartGlyph = "✿", Sound = SoundPack.Zen,
            Palette = new() { Background = "#FFFFF5F7", Background2 = "#FFFFE4EC", Surface = "#12E75480", SurfaceHover = "#24E75480",
                Foreground = "#FF4A2C3A", ForegroundDim = "#B34A2C3A", Accent = "#FFE75480", Accent2 = "#FF7FB685", AccentForeground = "#FFFFFFFF",
                Border = "#33E75480", Glow = "#33E75480", BackgroundOpacity = 0.82 },
            Fonts = new() { Ui = "Neucha", Title = "Amatic SC", TitleWeight = "Bold", BaseSize = 16, TitleSize = 32, SearchSize = 26 },
            Shape = new() { Radius = 18, TileRadius = 16, TileSize = 92 },
            Effects = new() { Material = Material.Mica, Background = BackgroundFx.Petals, Open = OpenAnimation.Drop, Parallax = true, Reveal = false },
            System = new() { AppsDark = false, SystemDark = false, Accent = "#FFD64575", Transparency = true, Wallpaper = WallpaperKind.Sakura,
                Cursor = CursorStyle.Rounded, CursorFill = "#FFFFFFFF", CursorOutline = "#FFD64575", Sounds = SoundPack.Zen,
                Corners = CornerStyle.Round, Backdrop = WindowBackdrop.Mica, TaskbarAlignment = TaskbarAlign.Center, SystemFont = "Neucha" },
        },
    });

    private static IReadOnlyList<ThemeDefinition> Mark(ThemeDefinition[] themes)
    {
        foreach (var t in themes) t.BuiltIn = true;
        return themes;
    }
}

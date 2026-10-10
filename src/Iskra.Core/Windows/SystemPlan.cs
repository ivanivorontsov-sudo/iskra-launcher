using Iskra.Core.Themes;

namespace Iskra.Core.Windows;

/// <summary>Что применять к Windows вместе с темой. Каждая категория отключается отдельно.</summary>
public sealed class SystemApplyOptions
{
    public bool Colors { get; set; } = true;          // тёмный/светлый режим, акцент, прозрачность, акцент на заголовках/панели
    public bool Wallpaper { get; set; } = true;       // обои / слайд-шоу
    public bool LockScreen { get; set; } = true;      // экран блокировки
    public bool Cursors { get; set; } = true;         // указатели мыши
    public bool Sounds { get; set; } = true;          // звуковая схема
    public bool IconSpacing { get; set; } = true;     // интервал значков рабочего стола
    public bool WindowFrames { get; set; } = true;    // скругления/рамки/материал окон (DWM, без внедрения в процессы)
    public bool ContextMenu { get; set; } = true;     // классическое/новое контекстное меню
    public bool Explorer { get; set; } = true;        // компактный вид Проводника
    public bool Taskbar { get; set; } = true;         // выравнивание панели задач
    public bool Dock { get; set; } = true;            // собственная панель задач, если тема её рекомендует
    public bool SystemFontExperimental { get; set; }  // ЭКСПЕРИМЕНТ: системный шрифт через FontSubstitutes (нужны права администратора)
    public bool WindowMetrics { get; set; } = true;   // ширина полосы прокрутки / высота заголовка / отступ рамки
    public bool ShellIcons { get; set; }              // значки папок/этот компьютер/корзина (ЭКСПЕРИМЕНТ, по умолчанию выкл.)
    public bool ShapedShortcutIcons { get; set; }     // перерисовать значки ярлыков рабочего стола/Пуска в форме темы (с бэкапом)
    public bool HideShortcutArrows { get; set; }      // скрыть стрелку ярлыка (Shell Icons 29 → прозрачный значок), с откатом
    /// <summary>Плотность сетки рабочего стола из настроек (Unchanged = как в теме).</summary>
    public Iskra.Core.Themes.IconSpacing GridDensity { get; set; } = Iskra.Core.Themes.IconSpacing.Unchanged;
}

/// <summary>Пути к сгенерированным файлам темы (курсоры, звуки).</summary>
public sealed class SystemAssets
{
    /// <summary>Роль курсора (Arrow, Hand, IBeam…) → путь к .cur/.ani.</summary>
    public Dictionary<string, string> Cursors { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Событие звуковой схемы (SystemAsterisk…) → путь к .wav.</summary>
    public Dictionary<string, string> Sounds { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string CursorSchemeName { get; set; } = "Искра";
    /// <summary>Роль значка (folder, folderOpen, thispc, recycleFull, recycleEmpty) → путь к .ico.</summary>
    public Dictionary<string, string> IconFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Прозрачный .ico для скрытия стрелки ярлыка.</summary>
    public string? BlankIcon { get; set; }
}

/// <summary>
/// Чистая (без побочных эффектов) функция: тема + настройки → список записей в HKCU.
/// Само применение и снимок «как было» делает приложение.
/// </summary>
public static class SystemPlan
{
    public const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    public const string DwmKey = @"Software\Microsoft\Windows\DWM";
    public const string AccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";
    public const string AdvancedKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    public const string CursorsKey = @"Control Panel\Cursors";
    public const string DesktopKey = @"Control Panel\Desktop";
    public const string WindowMetricsKey = @"Control Panel\Desktop\WindowMetrics";
    public const string SchemesKey = @"AppEvents\Schemes";
    public const string ClassicMenuClsid = @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}";
    public const string ClassicMenuKey = ClassicMenuClsid + @"\InprocServer32";
    public const string ContentDeliveryKey = @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
    public const string ColorsKey = @"Control Panel\Colors";
    public const string ShellIconsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
    public const string ShellIconsKeyLm = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons";
    public const string ThisPcIconKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}\DefaultIcon";
    public const string RecycleIconKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\DefaultIcon";
    // Windows 11 рисует папки отсюда, а не из устаревшего Shell Icons 3/4.
    public const string FolderIconKey = @"Software\Classes\Folder\DefaultIcon";
    public const string DirectoryIconKey = @"Software\Classes\Directory\DefaultIcon";

    /// <summary>Роли курсоров в HKCU\Control Panel\Cursors.</summary>
    public static readonly string[] CursorRoles =
    {
        "Arrow", "Help", "AppStarting", "Wait", "Crosshair", "IBeam", "NWPen", "No", "SizeNS", "SizeWE",
        "SizeNWSE", "SizeNESW", "SizeAll", "UpArrow", "Hand", "Pin", "Person",
    };

    /// <summary>События звуковой схемы (подмножество, которое реально слышно в повседневной работе).</summary>
    public static readonly string[] SoundEvents =
    {
        ".Default", "SystemAsterisk", "SystemExclamation", "SystemHand", "SystemNotification", "Notification.Default",
        "DeviceConnect", "DeviceDisconnect", "MailBeep", "WindowsUAC", "Notification.Reminder",
    };

    // Сетка рабочего стола. WindowMetrics\IconSpacing/IconVerticalSpacing хранятся в твипах: −15 × пиксели (при 96 dpi;
    // Windows сама масштабирует под AppliedDPI). Допустимо 32…182 px. В 1.1.0 «Широкая» давала −1500 (100 px),
    // а с масштабом 125 % — 125 px на ячейку: огромная разреженная сетка с обрезанной правой колонкой.
    public const int GridMinPx = 43, GridMaxPx = 150;
    public static (int H, int V)? IconSpacingPx(IconSpacing s) => s switch
    {
        IconSpacing.Compact => (68, 68),
        IconSpacing.Normal => (75, 75),   // штатное значение Windows (−1125)
        IconSpacing.Wide => (84, 80),
        _ => null,
    };
    public static int PxToTwips(int px) => -15 * Math.Clamp(px, GridMinPx, GridMaxPx);
    public static int TwipsToPx(int twips) => Math.Clamp(Math.Abs(twips) / 15, GridMinPx, GridMaxPx);
    /// <summary>Значение реестра вне разумных пределов (сломанная сетка) — его надо починить.</summary>
    public static bool IsGridValueBroken(string? twips)
    {
        if (string.IsNullOrWhiteSpace(twips)) return false;            // нет значения — Windows берёт своё
        if (!int.TryParse(twips, out var t) || t == 0) return true;
        int px = t < 0 ? -t / 15 : t;                                   // положительное значение трактуется как пиксели
        return px < 32 || px > GridMaxPx;
    }
    public static int? IconSpacingTwips(IconSpacing s) => IconSpacingPx(s) is { } px ? PxToTwips(px.H) : null;

    public static List<RegWrite> Build(SystemProfile p, SystemApplyOptions o, SystemAssets? assets = null)
    {
        var w = new List<RegWrite>();
        void Add(string key, string name, RegKind kind, object? value, string cat) => w.Add(new RegWrite(RegHive.CurrentUser, key, name, kind, value, cat));

        if (o.Colors)
        {
            var accent = Argb.Parse(p.Accent, new Argb(0xFF, 0x5A, 0xA9, 0xFF));
            var shades = AccentPalette.Shades(accent);
            Add(PersonalizeKey, "AppsUseLightTheme", RegKind.DWord, p.AppsDark ? 0 : 1, "colors");
            Add(PersonalizeKey, "SystemUsesLightTheme", RegKind.DWord, p.SystemDark ? 0 : 1, "colors");
            Add(PersonalizeKey, "EnableTransparency", RegKind.DWord, p.Transparency ? 1 : 0, "colors");
            // Акцент на «Пуске» и панели задач Windows разрешает только в тёмном системном режиме.
            Add(PersonalizeKey, "ColorPrevalence", RegKind.DWord, p.AccentOnTaskbar && p.SystemDark ? 1 : 0, "colors");
            Add(DwmKey, "ColorPrevalence", RegKind.DWord, p.AccentOnTitleBars ? 1 : 0, "colors");
            Add(DwmKey, "AccentColor", RegKind.DWord, unchecked((int)accent.ToAbgr()), "colors");
            Add(DwmKey, "ColorizationColor", RegKind.DWord, unchecked((int)accent.ToArgbUint(0xC4)), "colors");
            Add(DwmKey, "ColorizationAfterglow", RegKind.DWord, unchecked((int)accent.ToArgbUint(0xC4)), "colors");
            Add(AccentKey, "AccentPalette", RegKind.Binary, AccentPalette.Bytes(accent), "colors");
            Add(AccentKey, "AccentColorMenu", RegKind.DWord, unchecked((int)shades[4].ToAbgr()), "colors");
            Add(AccentKey, "StartColorMenu", RegKind.DWord, unchecked((int)shades[5].ToAbgr()), "colors");
            Add(DesktopKey, "AutoColorization", RegKind.DWord, 0, "colors"); // не подбирать акцент по обоям
        }

        if (o.Colors)
        {
            // Классические цвета (HKCU\Control Panel\Colors) — для старых окон/диалогов. Значения "R G B".
            var accent = Argb.Parse(p.Accent, new Argb(0xFF, 0x5A, 0xA9, 0xFF));
            bool dark = p.AppsDark;
            Argb win = dark ? new(0xFF,0x20,0x20,0x24) : new(0xFF,0xFF,0xFF,0xFF);
            Argb winText = dark ? new(0xFF,0xF0,0xF0,0xF2) : new(0xFF,0x10,0x10,0x12);
            Argb btnFace = dark ? new(0xFF,0x2B,0x2B,0x30) : new(0xFF,0xF0,0xF0,0xF0);
            Argb menu = btnFace, menuText = winText;
            // Текст Проводника берёт WindowText/TitleText. На светлой теме белый акцент делает подписи нечитаемыми.
            Argb titleText = dark ? new(0xFF, 0xF0, 0xF0, 0xF2) : new(0xFF, 0x10, 0x10, 0x12);
            Argb hilight = dark ? accent : new(0xFF, 0x2F, 0x5F, 0xC4);
            // Гарантия контраста (WCAG): текст каждой пары подбирается к своему фону, иначе — чёрный/белый.
            winText = Argb.Readable(winText, win, 7.0);
            menuText = Argb.Readable(menuText, menu, 7.0);
            Argb btnText = Argb.Readable(winText, btnFace, 7.0);
            Argb activeTitle = dark ? accent : btnFace;
            // Средний по яркости акцент не даёт 4.5:1 ни с белым, ни с чёрным — тогда затемняем фон заголовка.
            for (int i = 0; i < 8 && Argb.Contrast(Argb.Readable(titleText, activeTitle, 4.5), activeTitle) < 4.5; i++)
                activeTitle = activeTitle.Shade(-0.15);
            titleText = Argb.Readable(titleText, activeTitle, 4.5);
            for (int i = 0; i < 8 && Argb.Contrast(Argb.Readable(new Argb(0xFF, 0xFF, 0xFF, 0xFF), hilight, 4.5), hilight) < 4.5; i++)
                hilight = hilight.Shade(-0.15);
            Argb hilightText = Argb.Readable(new Argb(0xFF, 0xFF, 0xFF, 0xFF), hilight, 4.5);
            Argb inactiveTitleText = Argb.Readable(winText, btnFace, 4.5);
            Argb grayText = Argb.Readable(dark ? new(0xFF,0x80,0x80,0x86) : new(0xFF,0x5C,0x5C,0x62), win, 3.0);
            void Col(string name, Argb c) => Add(ColorsKey, name, RegKind.String, $"{c.R} {c.G} {c.B}", "colors");
            Col("Window", win); Col("WindowText", winText);
            Col("Menu", menu); Col("MenuText", menuText);
            Col("ButtonFace", btnFace); Col("ButtonText", btnText);
            Col("Hilight", hilight); Col("HilightText", hilightText); Col("HotTrackingColor", hilight);
            Col("GrayText", grayText);
            Col("ActiveTitle", activeTitle); Col("InactiveTitle", btnFace);
            Col("TitleText", titleText); Col("InactiveTitleText", inactiveTitleText);
            Col("ActiveBorder", btnFace); Col("InactiveBorder", btnFace);
            Col("Background", dark ? new(0xFF,0x0A,0x0A,0x0C) : new(0xFF,0xE8,0xE8,0xEC));
        }

        if (o.Cursors && assets != null && assets.Cursors.Count > 0)
        {
            Add(CursorsKey, "", RegKind.String, assets.CursorSchemeName, "cursors");
            foreach (var role in CursorRoles)
                if (assets.Cursors.TryGetValue(role, out var path)) Add(CursorsKey, role, RegKind.ExpandString, path, "cursors");
            Add(CursorsKey, "CursorBaseSize", RegKind.DWord, Math.Clamp(p.CursorSize, 32, 128), "cursors");
            Add(CursorsKey, "Scheme Source", RegKind.DWord, 1, "cursors"); // 1 = пользовательская схема
        }

        if (o.Sounds && assets != null && assets.Sounds.Count > 0 && p.Sounds != SoundPack.None)
        {
            Add(SchemesKey, "", RegKind.String, ".Current", "sounds");
            foreach (var ev in SoundEvents)
                if (assets.Sounds.TryGetValue(ev, out var path))
                    Add($@"{SchemesKey}\Apps\.Default\{ev}\.Current", "", RegKind.String, path, "sounds");
        }

        var grid = o.GridDensity != IconSpacing.Unchanged ? o.GridDensity : p.IconSpacing;
        if (o.IconSpacing && IconSpacingPx(grid) is { } gp)
        {
            Add(WindowMetricsKey, "IconSpacing", RegKind.String, PxToTwips(gp.H).ToString(System.Globalization.CultureInfo.InvariantCulture), "icons");
            Add(WindowMetricsKey, "IconVerticalSpacing", RegKind.String, PxToTwips(gp.V).ToString(System.Globalization.CultureInfo.InvariantCulture), "icons");
        }

        if (o.HideShortcutArrows && assets?.BlankIcon is { Length: > 0 } blank)
            w.Add(new RegWrite(RegHive.LocalMachine, ShellIconsKeyLm, "29", RegKind.String, blank + ",0", "shellicons")); // только HKLM работает

        // Тень под подписями значков: подписи рабочего стола Windows всегда белые — на светлых обоях без тени не читаются.
        if (o.IconSpacing) Add(AdvancedKey, "ListviewShadow", RegKind.DWord, 1, "icons");

        if (o.ContextMenu)
        {
            if (p.ClassicContextMenu) Add(ClassicMenuKey, "", RegKind.String, "", "contextmenu");
            else Add(ClassicMenuClsid, "", RegKind.DeleteKey, null, "contextmenu");
        }

        if (o.Explorer)
            Add(AdvancedKey, "UseCompactMode", RegKind.DWord, p.ExplorerCompact ? 1 : 0, "explorer");

        if (o.Taskbar && p.TaskbarAlignment != TaskbarAlign.Unchanged)
            Add(AdvancedKey, "TaskbarAl", RegKind.DWord, p.TaskbarAlignment == TaskbarAlign.Center ? 1 : 0, "taskbar");

        if (o.WindowMetrics)
        {
            string Tw(int px) => (-px * 15).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (p.ScrollbarWidth > 0) { Add(WindowMetricsKey, "ScrollWidth", RegKind.String, Tw(p.ScrollbarWidth), "metrics"); Add(WindowMetricsKey, "ScrollHeight", RegKind.String, Tw(p.ScrollbarWidth), "metrics"); }
            if (p.TitleBarHeight > 0) { Add(WindowMetricsKey, "CaptionHeight", RegKind.String, Tw(p.TitleBarHeight), "metrics"); Add(WindowMetricsKey, "CaptionWidth", RegKind.String, Tw(p.TitleBarHeight), "metrics"); }
            if (p.BorderPadding >= 0) Add(WindowMetricsKey, "PaddedBorderWidth", RegKind.String, Tw(p.BorderPadding), "metrics");
        }

        if (o.ShellIcons && assets != null && assets.IconFiles.Count > 0)
        {
            if (assets.IconFiles.TryGetValue("folder", out var f))
            {
                Add(ShellIconsKey, "3", RegKind.String, f + ",0", "shellicons");
                Add(FolderIconKey, "", RegKind.String, f + ",0", "shellicons");
                Add(DirectoryIconKey, "", RegKind.String, f + ",0", "shellicons");
            }
            if (assets.IconFiles.TryGetValue("folderOpen", out var fo)) Add(ShellIconsKey, "4", RegKind.String, fo + ",0", "shellicons");
            if (assets.IconFiles.TryGetValue("thispc", out var pc)) Add(ThisPcIconKey, "", RegKind.String, pc + ",0", "shellicons");
            if (assets.IconFiles.TryGetValue("recycleEmpty", out var re)) Add(RecycleIconKey, "empty", RegKind.String, re + ",0", "shellicons");
            if (assets.IconFiles.TryGetValue("recycleFull", out var rf)) Add(RecycleIconKey, "full", RegKind.String, rf + ",0", "shellicons");
        }

        return w;
    }

    public const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    public const string FontSubstitutesKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes";
    public const string PersonalizationCspKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP";

    /// <summary>
    /// УСТАРЕЛО: старая версия обнуляла штатные записи Segoe UI, из-за чего ломался откат. Теперь делегирует
    /// безопасной <see cref="FontPlan.BuildSystemFontSubstitute"/> (без обнуления). Оставлено для совместимости.
    /// </summary>
    public static List<RegWrite> BuildSystemFont(string familyName, string fontFileName)
        => FontPlan.BuildSystemFontSubstitute(familyName, fontFileName);

    public static List<RegWrite> BuildLockScreenCsp(string imagePath) => new()
    {
        new(RegHive.LocalMachine, PersonalizationCspKey, "LockScreenImagePath", RegKind.String, imagePath, "lockscreen"),
        new(RegHive.LocalMachine, PersonalizationCspKey, "LockScreenImageUrl", RegKind.String, imagePath, "lockscreen"),
        new(RegHive.LocalMachine, PersonalizationCspKey, "LockScreenImageStatus", RegKind.DWord, 1, "lockscreen"),
    };
}

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
    public const string ThisPcIconKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}\DefaultIcon";
    public const string RecycleIconKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\DefaultIcon";

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

    /// <summary>Интервал значков в твипах (отрицательное значение = пиксели × 15).</summary>
    public static int? IconSpacingTwips(IconSpacing s) => s switch
    {
        IconSpacing.Compact => -1050,
        IconSpacing.Normal => -1125,
        IconSpacing.Wide => -1500,
        _ => null,
    };

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
            Argb accentText = Argb.Readable(Argb.Parse(p.Accent), accent) ;
            void Col(string name, Argb c) => Add(ColorsKey, name, RegKind.String, $"{c.R} {c.G} {c.B}", "colors");
            Col("Window", win); Col("WindowText", winText);
            Col("Menu", menu); Col("MenuText", menuText);
            Col("ButtonFace", btnFace); Col("ButtonText", winText);
            Col("Hilight", accent); Col("HilightText", accentText); Col("HotTrackingColor", accent);
            Col("GrayText", dark ? new(0xFF,0x80,0x80,0x86) : new(0xFF,0x6D,0x6D,0x6D));
            Col("ActiveTitle", accent); Col("InactiveTitle", btnFace);
            Col("TitleText", accentText); Col("InactiveTitleText", winText);
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

        if (o.IconSpacing && IconSpacingTwips(p.IconSpacing) is int tw)
        {
            Add(WindowMetricsKey, "IconSpacing", RegKind.String, tw.ToString(System.Globalization.CultureInfo.InvariantCulture), "icons");
            Add(WindowMetricsKey, "IconVerticalSpacing", RegKind.String, tw.ToString(System.Globalization.CultureInfo.InvariantCulture), "icons");
        }

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
            if (assets.IconFiles.TryGetValue("folder", out var f)) Add(ShellIconsKey, "3", RegKind.ExpandString, f + ",0", "shellicons");
            if (assets.IconFiles.TryGetValue("folderOpen", out var fo)) Add(ShellIconsKey, "4", RegKind.ExpandString, fo + ",0", "shellicons");
            if (assets.IconFiles.TryGetValue("thispc", out var pc)) Add(ThisPcIconKey, "", RegKind.ExpandString, pc + ",0", "shellicons");
            if (assets.IconFiles.TryGetValue("recycleEmpty", out var re)) Add(RecycleIconKey, "empty", RegKind.ExpandString, re + ",0", "shellicons");
            if (assets.IconFiles.TryGetValue("recycleFull", out var rf)) Add(RecycleIconKey, "full", RegKind.ExpandString, rf + ",0", "shellicons");
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

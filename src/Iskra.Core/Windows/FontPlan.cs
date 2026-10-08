using System.Text;

namespace Iskra.Core.Windows;

/// <summary>
/// Шрифтовая часть оформления: подстановка системного шрифта (FontSubstitutes, HKLM) и «починка» штатной
/// регистрации семейства Segoe UI. Вынесено в Core, чтобы покрыть тестами (построение планов и разбор LOGFONT).
/// </summary>
public static class FontPlan
{
    public const string FontsKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts";
    public const string FontSubstitutesKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes";
    public const string WindowMetricsKey = @"Control Panel\Desktop\WindowMetrics";

    /// <summary>Штатные значения реестра для семейства Segoe UI в Windows 11 (имя значения → имя файла в C:\Windows\Fonts).</summary>
    public static readonly (string Name, string File)[] SegoeDefaults =
    {
        ("Segoe UI (TrueType)", "segoeui.ttf"),
        ("Segoe UI Black (TrueType)", "seguibl.ttf"),
        ("Segoe UI Black Italic (TrueType)", "seguibli.ttf"),
        ("Segoe UI Bold (TrueType)", "segoeuib.ttf"),
        ("Segoe UI Bold Italic (TrueType)", "segoeuiz.ttf"),
        ("Segoe UI Emoji (TrueType)", "seguiemj.ttf"),
        ("Segoe UI Historic (TrueType)", "seguihis.ttf"),
        ("Segoe UI Italic (TrueType)", "segoeuii.ttf"),
        ("Segoe UI Light (TrueType)", "segoeuil.ttf"),
        ("Segoe UI Light Italic (TrueType)", "seguili.ttf"),
        ("Segoe UI Semibold (TrueType)", "seguisb.ttf"),
        ("Segoe UI Semibold Italic (TrueType)", "seguisbi.ttf"),
        ("Segoe UI Semilight (TrueType)", "segoeuisl.ttf"),
        ("Segoe UI Semilight Italic (TrueType)", "seguisli.ttf"),
        ("Segoe UI Symbol (TrueType)", "seguisym.ttf"),
    };

    /// <summary>Записи, которые когда-то обнулял старый код применения шрифта (их нужно вернуть при починке).</summary>
    public static readonly string[] SegoeNamesThatMayBeBlanked =
    {
        "Segoe UI (TrueType)", "Segoe UI Bold (TrueType)", "Segoe UI Bold Italic (TrueType)",
        "Segoe UI Italic (TrueType)", "Segoe UI Light (TrueType)", "Segoe UI Semibold (TrueType)",
        "Segoe UI Symbol (TrueType)",
    };

    /// <summary>Имена возможных подстановок базового UI-шрифта, которые надо удалить при откате/починке.</summary>
    public static readonly string[] SubstituteNames = { "Segoe UI", "Segoe UI Variable" };

    /// <summary>
    /// План подстановки системного шрифта: прописывает файл семейства и заменяет «Segoe UI» на выбранный шрифт.
    /// Требует прав администратора (HKLM). Нужен перезапуск программ / повторный вход для большинства приложений.
    /// </summary>
    public static List<RegWrite> BuildSystemFontSubstitute(string familyName, string fontFileName)
    {
        var w = new List<RegWrite>();
        w.Add(new RegWrite(RegHive.LocalMachine, FontsKey, familyName + " (TrueType)", RegKind.String, fontFileName, "font"));
        foreach (var s in SubstituteNames)
            w.Add(new RegWrite(RegHive.LocalMachine, FontSubstitutesKey, s, RegKind.String, familyName, "font"));
        return w;
    }

    /// <summary>
    /// План «починки» шрифтов: возвращает штатную регистрацию Segoe UI и удаляет любую подстановку «Segoe UI».
    /// Работает даже без снимка (значения жёстко заданы под Windows 11) — именно это чинит уже сломанный ПК.
    /// Идемпотентен и безопасен: пишет только канонические значения семейства Segoe UI, которое есть в каждой Windows 11.
    /// </summary>
    public static List<RegWrite> BuildFontRepair()
    {
        var w = new List<RegWrite>();
        foreach (var (name, file) in SegoeDefaults)
            w.Add(new RegWrite(RegHive.LocalMachine, FontsKey, name, RegKind.String, file, "font"));
        foreach (var s in SubstituteNames)
            w.Add(new RegWrite(RegHive.LocalMachine, FontSubstitutesKey, s, RegKind.DeleteValue, null, "font"));
        return w;
    }

    // ---- LOGFONTW: 92 байта. Нужен для чтения/замены шрифтов в Control Panel\Desktop\WindowMetrics и для SPI. ----
    public const int LogFontSize = 92;     // 28 полей + 64 байта имени (WCHAR[32])
    private const int FaceOffset = 28;
    private const int FaceBytes = 64;

    /// <summary>Читает имя шрифта из блоба LOGFONTW (как хранится в реестре WindowMetrics).</summary>
    public static string ReadFaceName(byte[] logFont)
    {
        if (logFont == null || logFont.Length < LogFontSize) return "";
        return Encoding.Unicode.GetString(logFont, FaceOffset, FaceBytes).TrimEnd('\0');
    }

    /// <summary>Возвращает копию блоба LOGFONTW с заменённым именем шрифта (остальные поля — вес, размер — сохраняются).</summary>
    public static byte[] WithFaceName(byte[] logFont, string face)
    {
        var copy = new byte[LogFontSize];
        if (logFont != null) System.Array.Copy(logFont, copy, System.Math.Min(logFont.Length, LogFontSize));
        for (int i = 0; i < FaceBytes; i++) copy[FaceOffset + i] = 0;
        var name = Encoding.Unicode.GetBytes((face ?? "").Length > 31 ? face!.Substring(0, 31) : face ?? "");
        System.Array.Copy(name, 0, copy, FaceOffset, System.Math.Min(name.Length, FaceBytes - 2));
        return copy;
    }

    /// <summary>Строит LOGFONTW с нуля: имя + размер в пунктах (96 dpi) + насыщенность.</summary>
    public static byte[] BuildLogFont(string face, double pointSize = 9, int weight = 400, bool italic = false)
    {
        var b = new byte[LogFontSize];
        int height = -(int)System.Math.Round(pointSize * 96.0 / 72.0);
        System.BitConverter.GetBytes(height).CopyTo(b, 0);  // lfHeight
        System.BitConverter.GetBytes(weight).CopyTo(b, 16); // lfWeight
        b[20] = (byte)(italic ? 1 : 0);                     // lfItalic
        b[23] = 1;                                           // lfCharSet = DEFAULT_CHARSET
        b[26] = 5;                                           // lfQuality = CLEARTYPE_QUALITY
        return WithFaceName(b, face);
    }

    /// <summary>true, если по значениям реестра видно, что базовый UI-шрифт подменён или семейство Segoe UI повреждено.</summary>
    public static bool LooksBroken(string? segoeUiSubstitute, IDictionary<string, string?> fontsValues)
    {
        if (!string.IsNullOrEmpty(segoeUiSubstitute)) return true; // есть подстановка «Segoe UI» → подменён
        foreach (var name in SegoeNamesThatMayBeBlanked)
            if (fontsValues.TryGetValue(name, out var v) && string.IsNullOrEmpty(v)) return true; // обнулённая запись
        return false;
    }
}

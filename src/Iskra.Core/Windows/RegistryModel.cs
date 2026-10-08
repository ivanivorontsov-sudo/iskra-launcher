namespace Iskra.Core.Windows;

public enum RegHive { CurrentUser, LocalMachine }
public enum RegKind { DWord, String, ExpandString, Binary, MultiString, DeleteValue, DeleteKey }

/// <summary>Одна запись в реестр. Value: int/uint для DWord, string, byte[], string[].</summary>
public sealed record RegWrite(RegHive Hive, string Key, string Name, RegKind Kind, object? Value, string Category)
{
    public string FullPath => (Hive == RegHive.CurrentUser ? "HKCU\\" : "HKLM\\") + Key;
    public override string ToString() => $"{FullPath} [{(Name.Length == 0 ? "(по умолчанию)" : Name)}] = {Kind}:{Describe(Value)}";
    public static string Describe(object? v) => v switch
    {
        null => "∅",
        byte[] b => BitConverter.ToString(b),
        string[] a => string.Join("|", a),
        _ => v.ToString() ?? "",
    };
}

/// <summary>
/// Белый список того, что «Искра» вообще имеет право трогать в реестре. Всё, что связано с политиками
/// (DisableTaskMgr, DisableRegistryTools, NoDelete…), службами, автозапуском чужих программ и «Стоп-экраном», — запрещено.
/// </summary>
public static class RegistryGuard
{
    private static readonly (RegHive hive, string prefix, string[]? names)[] Allowed =
    {
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", null),
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\DWM", new[] { "ColorPrevalence", "AccentColor", "ColorizationColor", "ColorizationAfterglow", "AccentColorInactive" }),
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent", new[] { "AccentPalette", "AccentColorMenu", "StartColorMenu" }),
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", new[] { "TaskbarAl", "UseCompactMode", "TaskbarSi", "TaskbarGlomLevel" }),
        (RegHive.CurrentUser, @"Control Panel\Cursors", null),
        (RegHive.CurrentUser, @"Control Panel\Desktop\WindowMetrics", new[] { "IconSpacing", "IconVerticalSpacing", "ScrollWidth", "ScrollHeight", "CaptionHeight", "CaptionWidth", "PaddedBorderWidth", "BorderWidth", "MenuHeight", "MenuWidth", "SmCaptionHeight", "SmCaptionWidth", "IconFont", "CaptionFont", "MenuFont", "MessageFont", "SmCaptionFont", "StatusFont", "AppliedDPI" }),
        (RegHive.CurrentUser, @"Control Panel\Desktop", new[] { "AutoColorization", "WallPaper", "WallpaperStyle", "TileWallpaper" }),
        (RegHive.CurrentUser, @"Control Panel\Colors", null),
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\Shell Icons", null),
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{20D04FE0-3AEA-1069-A2D8-08002B30309D}\DefaultIcon", null),
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\CLSID\{645FF040-5081-101B-9F08-00AA002F954E}\DefaultIcon", null),
        (RegHive.CurrentUser, @"AppEvents\Schemes", null),
        (RegHive.CurrentUser, @"Software\Classes\CLSID\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}", null),
        (RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", new[] { "RotatingLockScreenEnabled", "RotatingLockScreenOverlayEnabled" }),
        (RegHive.CurrentUser, @"Software\Iskra", null),
        (RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\FontSubstitutes", null),
        (RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Fonts", null),
        (RegHive.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\PersonalizationCSP", null),
    };

    private static readonly string[] ForbiddenFragments =
    {
        @"\Policies", "Policies\\", "StopScreen", "Стоп-экран", "Stop-Screen", @"\Winlogon", "Image File Execution Options",
        @"CurrentControlSet\Services", @"\Run", "RunOnce", "SafeBoot", "Windows Defender", "UCPD", @"\Lsa",
    };

    public static bool IsAllowed(RegHive hive, string key, string name)
    {
        var k = Normalize(key);
        foreach (var f in ForbiddenFragments)
            if (k.Contains(f, StringComparison.OrdinalIgnoreCase)) return false;
        if (name.Contains("Policies", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Disable", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("NoDelete", StringComparison.OrdinalIgnoreCase)) return false;
        foreach (var (h, prefix, names) in Allowed)
        {
            if (h != hive) continue;
            bool exact = k.Equals(prefix, StringComparison.OrdinalIgnoreCase);
            bool sub = k.StartsWith(prefix + "\\", StringComparison.OrdinalIgnoreCase);
            if (!exact && !sub) continue;
            if (names == null) return true;
            // Для ограниченных ключей — только перечисленные значения и только в самом ключе.
            if (exact && names.Contains(name, StringComparer.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    public static bool IsAllowed(RegWrite w) => IsAllowed(w.Hive, w.Key, w.Name);

    public static string Normalize(string key) => key.Replace('/', '\\').Trim('\\');
}

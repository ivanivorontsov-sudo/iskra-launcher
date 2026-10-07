namespace Iskra.Core.Settings;

[Flags]
public enum HotkeyModifiers { None = 0, Alt = 1, Ctrl = 2, Shift = 4, Win = 8 }

/// <summary>Сочетание клавиш в текстовом виде «Ctrl+Alt+K». Ключ — имя клавиши WPF (Space, K, F1, OemTilde…).</summary>
public readonly record struct Hotkey(HotkeyModifiers Modifiers, string Key)
{
    public static readonly Hotkey Empty = new(HotkeyModifiers.None, "");
    public bool IsEmpty => string.IsNullOrEmpty(Key);

    public static bool TryParse(string? text, out Hotkey hk)
    {
        hk = Empty;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var parts = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var mods = HotkeyModifiers.None;
        string? key = null;
        foreach (var p in parts)
        {
            switch (p.ToLowerInvariant())
            {
                case "alt": mods |= HotkeyModifiers.Alt; break;
                case "ctrl": case "control": case "ctl": mods |= HotkeyModifiers.Ctrl; break;
                case "shift": mods |= HotkeyModifiers.Shift; break;
                case "win": case "windows": case "meta": mods |= HotkeyModifiers.Win; break;
                default:
                    if (key != null) return false;
                    key = NormalizeKey(p);
                    break;
            }
        }
        if (key == null) return false;
        // Одиночная буква без модификатора — плохая глобальная клавиша.
        if (mods == HotkeyModifiers.None && !(key.StartsWith('F') && key.Length > 1 && char.IsDigit(key[1]))) return false;
        hk = new Hotkey(mods, key);
        return true;
    }

    private static string NormalizeKey(string k)
    {
        var l = k.ToLowerInvariant();
        return l switch
        {
            "space" or "пробел" => "Space",
            "enter" or "return" => "Enter",
            "esc" or "escape" => "Escape",
            "tab" => "Tab",
            "`" or "~" or "tilde" or "oemtilde" or "oem3" => "OemTilde",
            _ when k.Length == 1 && char.IsLetter(k[0]) => k.ToUpperInvariant(),
            _ when k.Length == 1 && char.IsDigit(k[0]) => "D" + k,
            _ => char.ToUpperInvariant(k[0]) + k[1..],
        };
    }

    public override string ToString()
    {
        if (IsEmpty) return "";
        var parts = new List<string>();
        if (Modifiers.HasFlag(HotkeyModifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(HotkeyModifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(HotkeyModifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(HotkeyModifiers.Win)) parts.Add("Win");
        parts.Add(Key.StartsWith('D') && Key.Length == 2 && char.IsDigit(Key[1]) ? Key[1..] : Key == "OemTilde" ? "`" : Key);
        return string.Join("+", parts);
    }

    /// <summary>Сочетания, которые заняты самой Windows (RegisterHotKey их не всегда отвергает — предупреждаем заранее).</summary>
    public static string? KnownSystemConflict(Hotkey hk)
    {
        var s = hk.ToString();
        return s switch
        {
            "Win+L" or "Ctrl+Alt+Delete" => "Windows: блокировка / безопасность",
            "Win+D" or "Win+E" or "Win+R" or "Win+X" or "Win+I" or "Win+S" or "Win+Tab" or "Win+V" or "Win+Space" => "Windows: системное сочетание",
            "Alt+Tab" or "Alt+F4" or "Ctrl+Escape" => "Windows: переключение/закрытие окон",
            "Ctrl+Shift+Escape" => "Windows: диспетчер задач",
            _ => null,
        };
    }
}

using System.Text;

namespace Iskra.Core.Search;

/// <summary>Нормализация текста для поиска: нижний регистр, ё→е, без диакритики и лишних пробелов.</summary>
public static class TextNorm
{
    public static string Normalize(string? s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        bool lastSpace = true;
        foreach (var raw in s.Normalize(NormalizationForm.FormD))
        {
            var cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(raw);
            if (cat == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                // «й» распадается на «и» + бреве: возвращаем «й», иначе «и»/«й» перепутаются.
                if (raw == '\u0306' && sb.Length > 0 && sb[^1] == 'и') sb[^1] = 'й';
                continue;
            }
            char c = char.ToLowerInvariant(raw);
            if (c == 'ё') c = 'е';
            if (char.IsWhiteSpace(c) || c == '_' )
            {
                if (!lastSpace) { sb.Append(' '); lastSpace = true; }
                continue;
            }
            sb.Append(c);
            lastSpace = false;
        }
        if (sb.Length > 0 && sb[^1] == ' ') sb.Length--;
        return sb.ToString();
    }

    public static bool IsWordStart(string s, int i)
    {
        if (i == 0) return true;
        char p = s[i - 1], c = s[i];
        if (!char.IsLetterOrDigit(p)) return true;
        if (char.IsLower(p) && char.IsUpper(c)) return true;
        if (char.IsLetter(p) != char.IsLetter(c)) return true;
        return false;
    }

    public static bool HasCyrillic(string s)
    {
        foreach (var c in s) if (c >= '\u0400' && c <= '\u04FF') return true;
        return false;
    }

    public static bool HasLatin(string s)
    {
        foreach (var c in s) if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) return true;
        return false;
    }
}

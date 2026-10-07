using System.Text;

namespace Iskra.Core.Search;

/// <summary>
/// Переключение раскладки «набрал не в той раскладке»: ghbdtn → привет и обратно (ЙЦУКЕН ↔ QWERTY).
/// </summary>
public static class KeyboardLayout
{
    private const string En = "`qwertyuiop[]asdfghjkl;'zxcvbnm,./~QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?";
    private const string Ru = "ёйцукенгшщзхъфывапролджэячсмитьбю.ЁЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ,";

    private static readonly Dictionary<char, char> EnToRuMap = new();
    private static readonly Dictionary<char, char> RuToEnMap = new();

    static KeyboardLayout()
    {
        for (int i = 0; i < En.Length; i++)
        {
            EnToRuMap[En[i]] = Ru[i];
            // «.» и «,» в русской раскладке неоднозначны — обратное отображение только для букв.
            if (char.IsLetter(Ru[i])) RuToEnMap[Ru[i]] = En[i];
        }
    }

    public static string EnToRu(string s) => Map(s, EnToRuMap);
    public static string RuToEn(string s) => Map(s, RuToEnMap);

    private static string Map(string s, Dictionary<char, char> map)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) sb.Append(map.TryGetValue(c, out var m) ? m : c);
        return sb.ToString();
    }

    /// <summary>Похоже ли, что строка набрана латиницей вместо кириллицы (есть [ ; ' , . внутри слова или нет гласных).</summary>
    public static bool LooksLikeWrongLayout(string s)
    {
        if (!TextNorm.HasLatin(s) || TextNorm.HasCyrillic(s)) return false;
        int vowels = 0, letters = 0;
        foreach (var c in s.ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z') { letters++; if ("aeiouy".IndexOf(c) >= 0) vowels++; }
            else if ("[];',.`".IndexOf(c) >= 0) return true;
        }
        return letters >= 3 && vowels * 5 < letters; // в английских словах гласных обычно ≥ 20 %
    }
}

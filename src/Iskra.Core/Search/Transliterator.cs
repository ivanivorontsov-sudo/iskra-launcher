using System.Text;

namespace Iskra.Core.Search;

/// <summary>Транслитерация кириллица ↔ латиница (упрощённый ГОСТ/ИКАО + бытовые варианты).</summary>
public static class Transliterator
{
    private static readonly Dictionary<char, string> RuLat = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ё'] = "e",
        ['ж'] = "zh", ['з'] = "z", ['и'] = "i", ['й'] = "y", ['к'] = "k", ['л'] = "l", ['м'] = "m",
        ['н'] = "n", ['о'] = "o", ['п'] = "p", ['р'] = "r", ['с'] = "s", ['т'] = "t", ['у'] = "u",
        ['ф'] = "f", ['х'] = "h", ['ц'] = "ts", ['ч'] = "ch", ['ш'] = "sh", ['щ'] = "sch", ['ъ'] = "",
        ['ы'] = "y", ['ь'] = "", ['э'] = "e", ['ю'] = "yu", ['я'] = "ya",
    };

    // Порядок важен: сначала длинные сочетания.
    private static readonly (string lat, string ru)[] LatRu =
    {
        ("shch", "щ"), ("sch", "щ"), ("zh", "ж"), ("kh", "х"), ("ts", "ц"), ("ch", "ч"), ("sh", "ш"),
        ("yu", "ю"), ("ju", "ю"), ("ya", "я"), ("ja", "я"), ("yo", "ё"), ("jo", "ё"), ("ye", "е"), ("ph", "ф"),
        ("ck", "к"), ("oo", "у"), ("ee", "и"), ("qu", "кв"),
        ("a", "а"), ("b", "б"), ("c", "к"), ("d", "д"), ("e", "е"), ("f", "ф"), ("g", "г"), ("h", "х"),
        ("i", "и"), ("j", "дж"), ("k", "к"), ("l", "л"), ("m", "м"), ("n", "н"), ("o", "о"), ("p", "п"),
        ("q", "к"), ("r", "р"), ("s", "с"), ("t", "т"), ("u", "у"), ("v", "в"), ("w", "в"), ("x", "кс"),
        ("y", "й"), ("z", "з"),
    };

    public static string RuToLat(string s)
    {
        var sb = new StringBuilder(s.Length + 8);
        foreach (var ch in s)
        {
            var lower = char.ToLowerInvariant(ch);
            if (RuLat.TryGetValue(lower, out var rep)) sb.Append(rep);
            else sb.Append(lower);
        }
        return sb.ToString();
    }

    public static string LatToRu(string s)
    {
        var src = s.ToLowerInvariant();
        var sb = new StringBuilder(src.Length);
        int i = 0;
        while (i < src.Length)
        {
            bool matched = false;
            foreach (var (lat, ru) in LatRu)
            {
                if (string.CompareOrdinal(src, i, lat, 0, lat.Length) == 0)
                {
                    // «y» после согласной в конце/середине слова чаще «ы»: «tyk» → «тык».
                    if (lat == "y" && i > 0 && IsLatConsonant(src[i - 1]) && (i + 1 >= src.Length || IsLatConsonant(src[i + 1])))
                        sb.Append('ы');
                    else
                        sb.Append(ru);
                    i += lat.Length;
                    matched = true;
                    break;
                }
            }
            if (!matched) { sb.Append(src[i]); i++; }
        }
        return sb.ToString();
    }

    private static bool IsLatConsonant(char c) => c is >= 'a' and <= 'z' && "aeiouy".IndexOf(c) < 0;
}

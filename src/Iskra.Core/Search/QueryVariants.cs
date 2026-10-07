namespace Iskra.Core.Search;

/// <summary>Вариант запроса: сам запрос, другая раскладка или транслитерация, со штрафом к рангу.</summary>
public readonly record struct QueryVariant(string Text, int Penalty, string Kind);

public static class QueryVariants
{
    public static IReadOnlyList<QueryVariant> Build(string query)
    {
        var list = new List<QueryVariant>(6);
        var q = TextNorm.Normalize(query);
        if (q.Length == 0) return list;
        void Add(string text, int penalty, string kind)
        {
            var t = TextNorm.Normalize(text);
            if (t.Length == 0) return;
            foreach (var v in list) if (v.Text == t) return;
            list.Add(new QueryVariant(t, penalty, kind));
        }

        Add(q, 0, "exact");
        bool cyr = TextNorm.HasCyrillic(q), lat = TextNorm.HasLatin(q);
        // Раскладка из исходного (ненормализованного) текста — важны [ ] ; ' , .
        var raw = query.Trim();
        if (lat && !cyr)
        {
            var asRu = KeyboardLayout.EnToRu(raw);
            Add(asRu, KeyboardLayout.LooksLikeWrongLayout(raw) ? 2 : 12, "layout");
            Add(Transliterator.LatToRu(q), 15, "translit");
            Add(Transliterator.RuToLat(TextNorm.Normalize(asRu)), 18, "layout+translit"); // ntktuhfv → телеграм → telegram
        }
        else if (cyr && !lat)
        {
            var asEn = KeyboardLayout.RuToEn(raw);
            Add(asEn, 10, "layout");
            Add(Transliterator.RuToLat(q), 12, "translit"); // хром → hrom; телеграм → telegram
            Add(Transliterator.LatToRu(TextNorm.Normalize(asEn)), 20, "layout+translit");
        }
        return list;
    }
}

using System.Collections.Concurrent;

namespace Iskra.Core.Search;

/// <summary>
/// Движок поиска: объединяет источники (приложения, файлы, параметры, команды), ранжирует с учётом
/// раскладки/транслита, типа элемента и истории запусков. Потокобезопасен: источники заменяются атомарно.
/// </summary>
public sealed class SearchEngine
{
    private readonly ConcurrentDictionary<string, SearchItem[]> _sources = new();
    private volatile SearchItem[] _all = Array.Empty<SearchItem>();
    public UsageStore Usage { get; }

    /// <summary>Шаблон веб-поиска, {0} — запрос (URL-кодированный).</summary>
    public string WebSearchTemplate { get; set; } = "https://yandex.ru/search/?text={0}";
    public string WebSearchTitleFormat { get; set; } = "Искать в интернете: «{0}»";
    public string CalculatorSubtitle { get; set; } = "Калькулятор — Enter, чтобы скопировать";
    public System.Globalization.CultureInfo Culture { get; set; } = System.Globalization.CultureInfo.CurrentCulture;

    public SearchEngine(UsageStore? usage = null) { Usage = usage ?? new UsageStore(); }

    public void SetSource(string name, IEnumerable<SearchItem> items)
    {
        _sources[name] = items.ToArray();
        Rebuild();
    }

    public IReadOnlyCollection<SearchItem> All => _all;
    public int Count => _all.Length;

    public SearchItem? Find(string id)
    {
        foreach (var i in _all) if (i.Id == id) return i;
        return null;
    }

    private void Rebuild()
    {
        // Порядок источников фиксирован, дубликаты (одинаковое имя приложения) убираем.
        var seen = new HashSet<string>();
        var list = new List<SearchItem>();
        foreach (var key in _sources.Keys.OrderBy(k => k, StringComparer.Ordinal))
            foreach (var it in _sources[key])
            {
                var dedupeKey = it.Kind is ItemKind.App or ItemKind.StoreApp ? "app:" + it.NormTitle : it.Id;
                if (seen.Add(dedupeKey)) list.Add(it);
            }
        _all = list.ToArray();
    }

    public static int KindBonus(ItemKind k) => k switch
    {
        ItemKind.App or ItemKind.StoreApp => 60,
        ItemKind.Command => 25,
        ItemKind.Setting => 15,
        ItemKind.Theme => 10,
        ItemKind.Folder => -10,
        ItemKind.File => -30,
        _ => 0,
    };

    public IReadOnlyList<SearchResult> Search(string query, int max = 30, DateTime? now = null)
    {
        var results = new List<SearchResult>(max + 4);
        var trimmed = query.Trim();
        if (trimmed.Length == 0) return results;
        var when = now ?? DateTime.Now;

        // Принудительный веб-поиск: «? котики» или «g котики».
        bool forceWeb = trimmed.StartsWith('?') || trimmed.StartsWith("g ", StringComparison.OrdinalIgnoreCase);
        if (forceWeb)
        {
            var q = trimmed.StartsWith('?') ? trimmed[1..].Trim() : trimmed[2..].Trim();
            if (q.Length > 0) results.Add(new SearchResult(WebItem(q), 5000, Array.Empty<int>(), "web"));
            return results;
        }

        if (Calculator.TryEvaluate(trimmed, out var value))
        {
            var text = Calculator.Format(value, Culture);
            var calc = new SearchItem("calc:" + trimmed, "= " + text, ItemKind.Calculator, text, CalculatorSubtitle) { Glyph = "\uE8EF" };
            results.Add(new SearchResult(calc, 4000, Array.Empty<int>(), "calc"));
        }

        var variants = QueryVariants.Build(trimmed);
        var normQuery = variants.Count > 0 ? variants[0].Text : string.Empty;
        var scored = new List<SearchResult>();
        foreach (var item in _all)
        {
            if (item.Kind is ItemKind.File or ItemKind.Folder && normQuery.Length < 2) continue;
            int best = 0; int[] hl = Array.Empty<int>(); string kind = "";
            bool isFile = item.Kind is ItemKind.File or ItemKind.Folder;
            foreach (var v in variants)
            {
                // Для файлов — только точный запрос и раскладка, без опечаток: их десятки тысяч.
                if (isFile && v.Penalty > 12) continue;
                var m = FuzzyMatcher.Match(v.Text, item.NormTitle, allowTypo: !isFile && v.Penalty == 0);
                if (m.IsMatch)
                {
                    int s = m.Score - v.Penalty * 6;
                    if (s > best) { best = s; hl = v.Penalty == 0 && item.NormTitle.Length == item.Title.Length ? m.Positions : Array.Empty<int>(); kind = v.Kind; }
                }
                foreach (var kw in item.NormKeywords)
                {
                    var km = FuzzyMatcher.Match(v.Text, kw, allowTypo: v.Penalty == 0);
                    if (!km.IsMatch) continue;
                    int s = (int)(km.Score * 0.86) - v.Penalty * 6;
                    if (s > best) { best = s; hl = Array.Empty<int>(); kind = v.Kind + "/kw"; }
                }
            }
            if (best <= 0) continue;
            // Слабые нечёткие совпадения файлов не засоряют выдачу.
            if (item.Kind is ItemKind.File or ItemKind.Folder && best < FuzzyMatcher.Substring - 120) continue;
            int total = best + KindBonus(item.Kind) + Usage.Boost(item.Id, normQuery, when);
            if (best >= FuzzyMatcher.Exact - 60) total += 200; // точное совпадение не перебивается частотой
            scored.Add(new SearchResult(item, total, hl, kind));
        }
        scored.Sort((a, b) =>
        {
            int c = b.Score.CompareTo(a.Score);
            if (c != 0) return c;
            c = a.Item.Title.Length.CompareTo(b.Item.Title.Length);
            return c != 0 ? c : string.Compare(a.Item.Title, b.Item.Title, StringComparison.CurrentCultureIgnoreCase);
        });
        foreach (var r in scored)
        {
            if (results.Count >= max) break;
            results.Add(r);
        }
        results.Add(new SearchResult(WebItem(trimmed), 1, Array.Empty<int>(), "web"));
        return results;
    }

    public SearchItem WebItem(string q) =>
        new("web:" + q, string.Format(WebSearchTitleFormat, q), ItemKind.Web, string.Format(WebSearchTemplate, Uri.EscapeDataString(q)))
        { Glyph = "\uE774" };
}

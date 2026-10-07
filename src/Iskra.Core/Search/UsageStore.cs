using System.Text.Json;
using System.Text.Json.Serialization;

namespace Iskra.Core.Search;

/// <summary>
/// «Частота + свежесть» (frecency, как в адресной строке Firefox) плюс обучение на запросах:
/// если по «тг» вы трижды выбирали «Telegram», он станет первым по «тг».
/// </summary>
public sealed class UsageStore
{
    public sealed class Entry
    {
        public int Count { get; set; }
        public List<DateTime> Recent { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Kind { get; set; } = string.Empty;
    }

    [JsonInclude] public Dictionary<string, Entry> Items { get; private set; } = new();
    /// <summary>нормализованный префикс запроса → (id → сколько раз выбран).</summary>
    [JsonInclude] public Dictionary<string, Dictionary<string, int>> QueryPicks { get; private set; } = new();

    private readonly object _lock = new();
    public const int MaxRecent = 10;

    public void RecordLaunch(SearchItem item, string? query, DateTime now)
    {
        lock (_lock)
        {
            if (!Items.TryGetValue(item.Id, out var e)) Items[item.Id] = e = new Entry();
            e.Count++;
            e.Title = item.Title;
            e.Kind = item.Kind.ToString();
            e.Recent.Insert(0, now);
            if (e.Recent.Count > MaxRecent) e.Recent.RemoveRange(MaxRecent, e.Recent.Count - MaxRecent);

            var q = TextNorm.Normalize(query);
            if (q.Length > 0)
            {
                // Запоминаем все префиксы до 6 символов: «т», «тг», «тел»…
                for (int len = 1; len <= Math.Min(6, q.Length); len++)
                {
                    var p = q[..len];
                    if (!QueryPicks.TryGetValue(p, out var d)) QueryPicks[p] = d = new();
                    d[item.Id] = d.TryGetValue(item.Id, out var c) ? c + 1 : 1;
                }
                if (QueryPicks.Count > 5000) QueryPicks.Remove(QueryPicks.Keys.First());
            }
        }
    }

    public void Forget(string id)
    {
        lock (_lock)
        {
            Items.Remove(id);
            foreach (var d in QueryPicks.Values) d.Remove(id);
        }
    }

    public static int AgeWeight(TimeSpan age) => age.TotalHours switch
    {
        <= 4 => 100,
        <= 24 => 80,
        <= 72 => 60,
        <= 24 * 7 => 40,
        <= 24 * 30 => 20,
        _ => 8,
    };

    /// <summary>Бонус к рангу 0…440.</summary>
    public int Boost(string id, string normQuery, DateTime now)
    {
        lock (_lock)
        {
            int b = 0;
            if (Items.TryGetValue(id, out var e) && e.Recent.Count > 0)
            {
                double sum = 0;
                foreach (var t in e.Recent) sum += AgeWeight(now - t);
                double frecency = e.Count * sum / e.Recent.Count; // как в Firefox: count × средний вес
                b += (int)Math.Min(280, Math.Log2(1 + frecency) * 28);
            }
            if (normQuery.Length > 0)
            {
                var p = normQuery.Length > 6 ? normQuery[..6] : normQuery;
                if (QueryPicks.TryGetValue(p, out var d) && d.TryGetValue(id, out var picks))
                    b += Math.Min(160, 60 + picks * 25);
            }
            return b;
        }
    }

    public IReadOnlyList<(string Id, Entry Entry)> MostUsed(int max)
    {
        lock (_lock) return Items.OrderByDescending(kv => kv.Value.Count).ThenByDescending(kv => kv.Value.Recent.FirstOrDefault()).Take(max).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    public IReadOnlyList<(string Id, Entry Entry)> MostRecent(int max)
    {
        lock (_lock) return Items.Where(kv => kv.Value.Recent.Count > 0).OrderByDescending(kv => kv.Value.Recent[0]).Take(max).Select(kv => (kv.Key, kv.Value)).ToList();
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };
    public string Serialize() { lock (_lock) return JsonSerializer.Serialize(this, Json); }
    public static UsageStore Deserialize(string json)
    {
        try { return JsonSerializer.Deserialize<UsageStore>(json, Json) ?? new UsageStore(); }
        catch { return new UsageStore(); }
    }
}

namespace Iskra.Core.Search;

public readonly record struct MatchResult(int Score, int[] Positions)
{
    public static readonly MatchResult None = new(0, Array.Empty<int>());
    public bool IsMatch => Score > 0;
}

/// <summary>
/// Нечёткий поиск в духе fzf/Raycast: точное совпадение &gt; префикс &gt; начала слов (аббревиатура) &gt;
/// подстрока &gt; подпоследовательность с бонусами за смежность и начала слов &gt; опечатка (1 правка).
/// Обе строки должны быть нормализованы <see cref="TextNorm.Normalize"/>.
/// </summary>
public static class FuzzyMatcher
{
    public const int Exact = 1000, Prefix = 900, Acronym = 820, WordPrefix = 780, Substring = 650, FuzzyMax = 560, Typo = 280;

    public static MatchResult Match(string query, string candidate, bool allowTypo = true)
    {
        if (query.Length == 0 || candidate.Length == 0 || query.Length > candidate.Length + 1) return MatchResult.None;
        if (candidate == query) return new(Exact, Range(0, query.Length));
        if (candidate.StartsWith(query, StringComparison.Ordinal))
            return new(Prefix - Math.Min(80, candidate.Length - query.Length), Range(0, query.Length));

        // Каждое слово запроса — префикс какого-то слова кандидата («vis code» → «Visual Studio Code»).
        var wp = WordPrefixes(query, candidate);
        if (wp != null) return new(WordPrefix - Math.Min(60, candidate.Length - query.Length) / 2, wp);

        var acr = AcronymMatch(query, candidate);
        if (acr != null) return new(Acronym - Math.Min(40, candidate.Length / 4), acr);

        int idx = candidate.IndexOf(query, StringComparison.Ordinal);
        // Предпочитаем вхождение с начала слова: «s» в «Visual Studio» → «Studio».
        for (int k = idx; k > 0; k = candidate.IndexOf(query, k + 1, StringComparison.Ordinal))
            if (TextNorm.IsWordStart(candidate, k)) { idx = k; break; }
        if (idx > 0)
        {
            int s = Substring - Math.Min(100, idx * 3);
            if (TextNorm.IsWordStart(candidate, idx)) s += 60;
            return new(s, Range(idx, query.Length));
        }

        var fz = Subsequence(query, candidate);
        if (fz.IsMatch) return fz;

        if (allowTypo && query.Length >= 4)
        {
            var typo = TypoMatch(query, candidate);
            if (typo.IsMatch) return typo;
        }
        return MatchResult.None;
    }

    private static int[] Range(int start, int len)
    {
        var a = new int[len];
        for (int i = 0; i < len; i++) a[i] = start + i;
        return a;
    }

    private static int[]? WordPrefixes(string query, string candidate)
    {
        if (query.IndexOf(' ') < 0) return null;
        var qWords = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var positions = new List<int>();
        int from = 0;
        foreach (var w in qWords)
        {
            int found = -1;
            for (int i = from; i <= candidate.Length - w.Length; i++)
            {
                if (TextNorm.IsWordStart(candidate, i) && string.CompareOrdinal(candidate, i, w, 0, w.Length) == 0) { found = i; break; }
            }
            if (found < 0) return null;
            for (int k = 0; k < w.Length; k++) positions.Add(found + k);
            from = found + w.Length;
        }
        return positions.ToArray();
    }

    private static int[]? AcronymMatch(string query, string candidate)
    {
        if (query.Length < 2 || query.IndexOf(' ') >= 0) return null;
        var pos = new List<int>();
        int qi = 0;
        for (int i = 0; i < candidate.Length && qi < query.Length; i++)
        {
            if (TextNorm.IsWordStart(candidate, i) && char.IsLetterOrDigit(candidate[i]))
            {
                if (candidate[i] == query[qi]) { pos.Add(i); qi++; }
            }
        }
        return qi == query.Length ? pos.ToArray() : null;
    }

    /// <summary>Подпоследовательность: оптимальная раскладка по DP с бонусами за смежность и начала слов.</summary>
    private static MatchResult Subsequence(string q, string c)
    {
        int n = q.Length, m = c.Length;
        int t = 0;
        for (int i = 0; i < m && t < n; i++) if (c[i] == q[t]) t++;
        if (t < n) return MatchResult.None;
        if (n > 64 || m > 256) return new(FuzzyMax / 3, Array.Empty<int>());

        const int NEG = int.MinValue / 4;
        int size = n * m;
        if (_score == null || _score.Length < size) { _score = new int[Math.Max(size, 1024)]; _cons = new int[_score.Length]; _from = new int[_score.Length]; }
        var score = new Grid(_score, m); var cons = new Grid(_cons!, m); var from = new Grid(_from!, m);
        for (int i = 0; i < n; i++)
        {
            int best = NEG, bestJ = -1; // максимум score[i-1, j'] по j' <= j-2
            for (int j = 0; j < m; j++)
            {
                if (i > 0 && j >= 2 && score[i - 1, j - 2] > best) { best = score[i - 1, j - 2]; bestJ = j - 2; }
                score[i, j] = NEG; from[i, j] = -1; cons[i, j] = 0;
                if (c[j] != q[i]) continue;
                int bonus = 10;
                if (TextNorm.IsWordStart(c, j)) bonus += 18;
                if (j == 0) bonus += 8;
                if (i == 0)
                {
                    score[i, j] = bonus - Math.Min(j, 15);
                    cons[i, j] = 1;
                    continue;
                }
                int sCons = NEG, sGap = NEG;
                if (j > 0 && score[i - 1, j - 1] > NEG / 2)
                    sCons = score[i - 1, j - 1] + bonus + 12 + Math.Min(cons[i - 1, j - 1], 4) * 3;
                if (bestJ >= 0 && best > NEG / 2)
                    sGap = best + bonus - Math.Min(j - bestJ - 1, 12);
                if (sCons > NEG / 2 && sCons >= sGap) { score[i, j] = sCons; cons[i, j] = cons[i - 1, j - 1] + 1; from[i, j] = j - 1; }
                else if (sGap > NEG / 2) { score[i, j] = sGap; cons[i, j] = 1; from[i, j] = bestJ; }
            }
        }
        int endJ = -1, top = NEG;
        for (int j = 0; j < m; j++) if (score[n - 1, j] > top) { top = score[n - 1, j]; endJ = j; }
        if (endJ < 0 || top <= NEG / 2) return MatchResult.None;
        var pos = new int[n];
        int cur = endJ;
        for (int i = n - 1; i >= 0; i--)
        {
            if (cur < 0) return new(Clamp(top, n, m), Array.Empty<int>());
            pos[i] = cur;
            cur = from[i, cur];
        }
        return new(Clamp(top, n, m), pos);
    }

    [ThreadStatic] private static int[]? _score, _cons, _from;

    private readonly struct Grid
    {
        private readonly int[] _a; private readonly int _m;
        public Grid(int[] a, int m) { _a = a; _m = m; }
        public int this[int i, int j] { get => _a[i * _m + j]; set => _a[i * _m + j] = value; }
    }

    private static int Clamp(int raw, int n, int m)
    {
        // Максимум ≈ n*(10+18+12+12) — нормируем в диапазон [60; FuzzyMax].
        double ideal = n * 52.0;
        double r = Math.Max(0, raw) / ideal;
        int s = (int)(120 + r * (FuzzyMax - 120)) - Math.Min(60, (m - n) / 2);
        return Math.Clamp(s, 40, FuzzyMax);
    }

    private static MatchResult TypoMatch(string q, string c)
    {
        // Сравниваем с началами слов кандидата той же длины (±1): «telgram» ≈ «telegram».
        for (int i = 0; i < c.Length; i++)
        {
            if (!TextNorm.IsWordStart(c, i)) continue;
            for (int len = q.Length - 1; len <= q.Length + 1; len++)
            {
                if (i + len > c.Length || len <= 0) continue;
                if (c[i] != q[0] && (len < 2 || c[i + 1] != q[0])) continue; // первая буква почти всегда верна
                if (Osa(q, c.AsSpan(i, len)) <= 1)
                    return new(Typo - i, Range(i, Math.Min(len, c.Length - i)));
            }
        }
        return MatchResult.None;
    }

    /// <summary>Расстояние Дамерау–Левенштейна (оптимальное выравнивание строк).</summary>
    public static int Osa(string a, ReadOnlySpan<char> b)
    {
        int n = a.Length, m = b.Length;
        var d = new int[n + 1, m + 1];
        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;
        for (int i = 1; i <= n; i++)
            for (int j = 1; j <= m; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[n, m];
    }
}

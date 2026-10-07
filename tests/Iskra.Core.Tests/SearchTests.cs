using Iskra.Core.Search;
using Xunit;

namespace Iskra.Core.Tests;

public class TextTests
{
    [Theory]
    [InlineData("Ёлка  Новогодняя", "елка новогодняя")]
    [InlineData("  Visual_Studio Code ", "visual studio code")]
    [InlineData("Мой компьютер", "мой компьютер")]
    [InlineData("Café", "cafe")]
    public void Normalize(string input, string expected) => Assert.Equal(expected, TextNorm.Normalize(input));

    [Theory]
    [InlineData("ghbdtn", "привет")]
    [InlineData("ntktuhfv", "телеграм")]
    [InlineData("vfqrhjcjan", "майкрософт")]
    [InlineData("rfkmrekznjh", "калькулятор")]
    public void EnToRuLayout(string typed, string expected) => Assert.Equal(expected, KeyboardLayout.EnToRu(typed));

    [Theory]
    [InlineData("ыеуфь", "steam")]
    [InlineData("сркщьу", "chrome")]
    public void RuToEnLayout(string typed, string expected) => Assert.Equal(expected, KeyboardLayout.RuToEn(typed));

    [Theory]
    [InlineData("телеграм", "telegram")]
    [InlineData("хром", "hrom")]
    [InlineData("щука", "schuka")]
    [InlineData("Яндекс", "yandeks")]
    public void RuToLat(string ru, string lat) => Assert.Equal(lat, Transliterator.RuToLat(ru));

    [Theory]
    [InlineData("privet", "привет")]
    [InlineData("zhurnal", "журнал")]
    [InlineData("shchuka", "щука")]
    [InlineData("yandex", "яндекс")]
    public void LatToRu(string lat, string ru) => Assert.Equal(ru, Transliterator.LatToRu(lat));

    [Fact]
    public void WrongLayoutDetection()
    {
        Assert.True(KeyboardLayout.LooksLikeWrongLayout("ghbdtn"));
        Assert.True(KeyboardLayout.LooksLikeWrongLayout("ntktuhfv"));
        Assert.False(KeyboardLayout.LooksLikeWrongLayout("telegram"));
        Assert.False(KeyboardLayout.LooksLikeWrongLayout("привет"));
    }

    [Fact]
    public void VariantsIncludeLayoutAndTranslit()
    {
        var v = QueryVariants.Build("ntktuhfv").Select(x => x.Text).ToList();
        Assert.Contains("ntktuhfv", v);
        Assert.Contains("телеграм", v);
        Assert.Contains("telegram", v);
        var v2 = QueryVariants.Build("хром").Select(x => x.Text).ToList();
        Assert.Contains("hrom", v2);
        Assert.Contains("[hjv", v2);
    }
}

public class FuzzyTests
{
    private static int S(string q, string c) => FuzzyMatcher.Match(TextNorm.Normalize(q), TextNorm.Normalize(c)).Score;

    [Fact]
    public void OrderOfMatchKinds()
    {
        int exact = S("paint", "Paint");
        int prefix = S("pai", "Paint");
        int acronym = S("vsc", "Visual Studio Code");
        int substring = S("int", "Paint");
        int fuzzy = S("pnt", "Paint");
        Assert.True(exact > prefix, $"{exact} > {prefix}");
        Assert.True(prefix > acronym, $"{prefix} > {acronym}");
        Assert.True(acronym > substring, $"{acronym} > {substring}");
        Assert.True(substring > fuzzy, $"{substring} > {fuzzy}");
        Assert.True(fuzzy > 0);
    }

    [Theory]
    [InlineData("vis code", "Visual Studio Code")]
    [InlineData("блок", "Блокнот")]
    [InlineData("кальк", "Калькулятор")]
    [InlineData("ножн", "Ножницы")]
    [InlineData("telgram", "Telegram")] // опечатка
    [InlineData("tlegram", "Telegram")]
    [InlineData("pwsh", "PowerShell")]
    public void Matches(string q, string c) => Assert.True(S(q, c) > 0, $"«{q}» должно находить «{c}»");

    [Theory]
    [InlineData("xyz", "Telegram")]
    [InlineData("калькулятор", "Блокнот")]
    [InlineData("ab", "Paint")]
    public void DoesNotMatch(string q, string c) => Assert.Equal(0, S(q, c));

    [Fact]
    public void ConsecutiveBeatsScattered()
    {
        Assert.True(S("note", "Notepad") > S("note", "Nothing To Explore"));
        Assert.True(S("code", "Code Editor") > S("code", "Color Dialog Editor"));
    }

    [Fact]
    public void HighlightPositionsArePresent()
    {
        var m = FuzzyMatcher.Match("vsc", TextNorm.Normalize("Visual Studio Code"));
        Assert.Equal(new[] { 0, 7, 14 }, m.Positions);
    }

    [Fact]
    public void OsaDistance()
    {
        Assert.Equal(1, FuzzyMatcher.Osa("telgram", "telegram"));
        Assert.Equal(1, FuzzyMatcher.Osa("teelgram", "telegram"));
        Assert.Equal(0, FuzzyMatcher.Osa("abc", "abc"));
    }
}

public class EngineTests
{
    private static SearchEngine Engine()
    {
        var e = new SearchEngine();
        e.SetSource("apps", new[]
        {
            new SearchItem("app:telegram", "Telegram", ItemKind.App, "telegram"),
            new SearchItem("app:notepad", "Блокнот", ItemKind.App, "notepad", keywords: new[] { "Notepad" }),
            new SearchItem("app:calc", "Калькулятор", ItemKind.StoreApp, "calc", keywords: new[] { "Calculator" }),
            new SearchItem("app:chrome", "Google Chrome", ItemKind.App, "chrome"),
            new SearchItem("app:steam", "Steam", ItemKind.App, "steam"),
            new SearchItem("app:vscode", "Visual Studio Code", ItemKind.App, "code"),
            new SearchItem("app:paint", "Paint", ItemKind.StoreApp, "paint"),
            new SearchItem("app:word", "Word", ItemKind.App, "word"),
            new SearchItem("app:yandex", "Яндекс Браузер", ItemKind.App, "yb", keywords: new[] { "Yandex Browser" }),
        });
        e.SetSource("settings", new[]
        {
            new SearchItem("set:display", "Дисплей", ItemKind.Setting, "ms-settings:display", keywords: new[] { "Экран", "Display", "Яркость" }),
            new SearchItem("set:bluetooth", "Bluetooth и устройства", ItemKind.Setting, "ms-settings:bluetooth", keywords: new[] { "Блютуз" }),
        });
        e.SetSource("files", new[]
        {
            new SearchItem("file:a", "telegram-notes.txt", ItemKind.File, @"C:\Users\I\telegram-notes.txt"),
            new SearchItem("file:b", "отчёт.docx", ItemKind.File, @"C:\Users\I\отчёт.docx"),
        });
        return e;
    }

    private static string Top(SearchEngine e, string q) => e.Search(q)[0].Item.Id;

    [Theory]
    [InlineData("tel", "app:telegram")]
    [InlineData("ntktuhfv", "app:telegram")]   // «телеграм» в английской раскладке
    [InlineData("телеграм", "app:telegram")]   // по-русски → транслит
    [InlineData("ыеуфь", "app:steam")]         // «steam» в русской раскладке
    [InlineData("блокнот", "app:notepad")]
    [InlineData("notepad", "app:notepad")]     // английское ключевое слово
    [InlineData(",kjryjn", "app:notepad")]     // «блокнот» в английской раскладке
    [InlineData("calc", "app:calc")]
    [InlineData("rfkm", "app:calc")]           // «каль»
    [InlineData("vsc", "app:vscode")]
    [InlineData("яркость", "set:display")]
    [InlineData("блютуз", "set:bluetooth")]
    [InlineData("yandex", "app:yandex")]
    [InlineData("отчет", "file:b")]             // ё/е
    public void FindsExpectedTop(string q, string id) => Assert.Equal(id, Top(Engine(), q));

    [Fact]
    public void AppsOutrankFilesWithSameName()
    {
        var r = Engine().Search("telegram");
        Assert.Equal("app:telegram", r[0].Item.Id);
        Assert.Contains(r, x => x.Item.Id == "file:a");
    }

    [Fact]
    public void CalculatorComesFirst()
    {
        var r = Engine().Search("2+2*2");
        Assert.Equal(ItemKind.Calculator, r[0].Item.Kind);
        Assert.Equal("6", r[0].Item.Target);
    }

    [Fact]
    public void WebFallbackAlwaysLast()
    {
        var r = Engine().Search("котики смешные");
        Assert.Equal(ItemKind.Web, r[^1].Item.Kind);
        Assert.Contains("%D0%BA%D0%BE%D1%82", r[^1].Item.Target);
    }

    [Fact]
    public void ForcedWebSearch()
    {
        var r = Engine().Search("? погода владивосток");
        Assert.Single(r);
        Assert.Equal(ItemKind.Web, r[0].Item.Kind);
    }

    [Fact]
    public void DuplicateAppsAreMerged()
    {
        var e = new SearchEngine();
        e.SetSource("a", new[] { new SearchItem("1", "Telegram", ItemKind.App, "x") });
        e.SetSource("b", new[] { new SearchItem("2", "Telegram", ItemKind.StoreApp, "y") });
        Assert.Equal(1, e.Count);
    }

    [Fact]
    public void LearnsFromQueryPicks()
    {
        var e = Engine();
        // «s» по умолчанию — Steam; после трёх выборов «Bluetooth» по «s»… берём реальный конфликт: «c» — Chrome/Calc/Code.
        var before = Top(e, "c");
        var target = e.Find("app:vscode")!;
        var now = new DateTime(2026, 10, 8, 9, 0, 0);
        for (int i = 0; i < 3; i++) e.Usage.RecordLaunch(target, "c", now.AddMinutes(-i));
        Assert.Equal("app:vscode", e.Search("c", now: now)[0].Item.Id);
        Assert.NotNull(before);
    }

    [Fact]
    public void FrecencyPrefersRecentlyUsed()
    {
        var e = Engine();
        var now = new DateTime(2026, 10, 8, 9, 0, 0);
        var word = e.Find("app:word")!;
        // «w» совпадает только с Word, поэтому проверим на общем префиксе: Paint vs Steam по «a»? Используем «s»: Steam (префикс) vs Visual Studio Code (начало слова).
        var code = e.Find("app:vscode")!;
        for (int i = 0; i < 6; i++) e.Usage.RecordLaunch(code, null, now.AddHours(-i));
        var r = e.Search("s", now: now);
        int iCode = r.ToList().FindIndex(x => x.Item.Id == "app:vscode");
        int iSteam = r.ToList().FindIndex(x => x.Item.Id == "app:steam");
        Assert.True(iCode < iSteam, $"code {iCode} steam {iSteam}");
        Assert.NotNull(word);
    }

    [Fact]
    public void OldUsageWeighsLess()
    {
        Assert.True(UsageStore.AgeWeight(TimeSpan.FromHours(1)) > UsageStore.AgeWeight(TimeSpan.FromDays(10)));
    }

    [Fact]
    public void UsageRoundTrip()
    {
        var u = new UsageStore();
        u.RecordLaunch(new SearchItem("x", "X", ItemKind.App, "x"), "икс", DateTime.Now);
        var u2 = UsageStore.Deserialize(u.Serialize());
        Assert.True(u2.Boost("x", "икс", DateTime.Now) > 0);
        Assert.Single(u2.MostUsed(5));
    }

    [Fact]
    public void SearchIsFastOnLargeIndex()
    {
        var e = new SearchEngine();
        var rnd = new Random(1);
        var words = new[] { "alpha", "beta", "отчёт", "проект", "photo", "music", "договор", "invoice", "report", "draft" };
        e.SetSource("files", Enumerable.Range(0, 20000).Select(i =>
            new SearchItem("f" + i, $"{words[rnd.Next(words.Length)]}_{words[rnd.Next(words.Length)]}_{i}.txt", ItemKind.File, "p" + i)));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 5; i++) e.Search("договор про");
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds / 5 < 400, $"среднее {sw.ElapsedMilliseconds / 5} мс");
    }
}

public class CalculatorTests
{
    [Theory]
    [InlineData("2+2*2", 6)]
    [InlineData("(2+2)*2", 8)]
    [InlineData("2^10", 1024)]
    [InlineData("2^3^2", 512)]
    [InlineData("10/4", 2.5)]
    [InlineData("2,5*4", 10)]
    [InlineData("-3+5", 2)]
    [InlineData("200*15%", 30)]
    [InlineData("sqrt(16)+1", 5)]
    [InlineData("корень(81)", 9)]
    [InlineData("1 000 + 1", 1001)]
    [InlineData("7 mod 3", 1)]
    [InlineData("=5", 5)]
    [InlineData("3×4÷2", 6)]
    public void Evaluates(string expr, double expected)
    {
        Assert.True(Calculator.TryEvaluate(expr, out var v), expr);
        Assert.Equal(expected, v, 9);
    }

    [Theory]
    [InlineData("telegram")]
    [InlineData("1/0")]
    [InlineData("2+")]
    [InlineData("(2")]
    [InlineData("windows 11")]
    [InlineData("")]
    public void Rejects(string expr) => Assert.False(Calculator.TryEvaluate(expr, out _));

    [Fact]
    public void FormatsWithCulture()
    {
        var ru = new System.Globalization.CultureInfo("ru-RU");
        Assert.Equal("2,5", Calculator.Format(2.5, ru));
        Assert.Equal("1" + ru.NumberFormat.NumberGroupSeparator + "000", Calculator.Format(1000, ru));
    }
}

namespace Iskra.Core.Search;

public enum ItemKind { App, StoreApp, File, Folder, Setting, Command, Calculator, Web, Theme }

/// <summary>Элемент поиска. Неизменяемый; нормализованные строки считаются один раз.</summary>
public sealed class SearchItem
{
    public SearchItem(string id, string title, ItemKind kind, string target, string? subtitle = null, IEnumerable<string>? keywords = null)
    {
        Id = id; Title = title; Kind = kind; Target = target; Subtitle = subtitle ?? string.Empty;
        Keywords = keywords?.Where(k => !string.IsNullOrWhiteSpace(k)).ToArray() ?? Array.Empty<string>();
        NormTitle = TextNorm.Normalize(title);
        NormKeywords = Keywords.Select(TextNorm.Normalize).Where(k => k.Length > 0).Distinct().ToArray();
    }

    public string Id { get; }
    public string Title { get; }
    public string Subtitle { get; }
    public ItemKind Kind { get; }
    /// <summary>Что запускать: AUMID/путь, ms-settings: URI, команда и т. п.</summary>
    public string Target { get; }
    public string[] Keywords { get; }
    public string NormTitle { get; }
    public string[] NormKeywords { get; }
    /// <summary>Путь к реальному файлу (для «Открыть расположение» и «Запуск от имени администратора»).</summary>
    public string? FilePath { get; init; }
    /// <summary>Глиф для элементов без иконки (команды, настройки).</summary>
    public string? Glyph { get; init; }
    public bool NeedsConfirmation { get; init; }

    public override string ToString() => $"{Kind}: {Title}";
}

public sealed class SearchResult
{
    public SearchResult(SearchItem item, int score, int[] highlight, string matchKind)
    {
        Item = item; Score = score; Highlight = highlight; MatchKind = matchKind;
    }
    public SearchItem Item { get; }
    public int Score { get; }
    /// <summary>Позиции подсвеченных символов в Title (если совпало по названию).</summary>
    public int[] Highlight { get; }
    public string MatchKind { get; }
    public override string ToString() => $"{Score} {Item.Title} ({MatchKind})";
}

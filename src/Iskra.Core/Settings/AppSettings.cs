using System.Text.Json;
using System.Text.Json.Serialization;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.Core.Settings;

public enum EffectsMode { Auto, Full, Reduced }
public enum DockMode { Off, Floating, ReplaceTaskbar }

public sealed class PinnedItem
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Target { get; set; } = "";
    public string Kind { get; set; } = "App";
    public string? FilePath { get; set; }
    /// <summary>Размер плитки на стартовом экране: 1 — маленькая, 2 — средняя, 3 — широкая.</summary>
    public int TileSize { get; set; } = 2;
    public string Group { get; set; } = "";
}

public sealed class AppSettings
{
    public int Version { get; set; } = 1;
    public string Language { get; set; } = "ru";
    public string ThemeId { get; set; } = "mica";
    public List<ThemeDefinition> CustomThemes { get; set; } = new();
    public string Hotkey { get; set; } = "Alt+Space";
    public string StartScreenHotkey { get; set; } = "Alt+Shift+Space";
    public bool ReplaceStartMenu { get; set; } = true;
    public bool InterceptStartButton { get; set; } = true;
    public bool ApplyThemeToWindows { get; set; } = true;
    public SystemApplyOptions SystemOptions { get; set; } = new();
    public EffectsMode Effects { get; set; } = EffectsMode.Auto;
    public bool BatterySaverReducesEffects { get; set; } = true;
    public bool SoundsEnabled { get; set; } = true;
    public double SoundVolume { get; set; } = 0.6;
    public DockMode Dock { get; set; } = DockMode.Off;
    public bool DockFollowsTheme { get; set; } = true;
    public bool Autostart { get; set; } = true;
    public bool FileSearchEnabled { get; set; } = true;
    public List<string> FileSearchFolders { get; set; } = new();
    public int FileSearchMaxDepth { get; set; } = 5;
    public string WebSearchEngine { get; set; } = "yandex";
    public string CustomWebSearchUrl { get; set; } = "https://yandex.ru/search/?text={0}";
    public List<PinnedItem> Pinned { get; set; } = new();
    public bool FirstRunDone { get; set; }
    public bool ShowRecent { get; set; } = true;
    public bool FontSubstitutionPendingConfirm { get; set; }
    public DateTime? FontSubstitutionAppliedUtc { get; set; }
    // --- Необязательные глубокие интеграции (ВЫКЛ по умолчанию, ставятся только по согласию, полностью обратимы) ---
    public bool WindhawkIntegrationEnabled { get; set; }
    public bool MsStylesIntegrationEnabled { get; set; }
    public bool ShapedIconsSystemWide { get; set; }

    public static readonly Dictionary<string, (string Name, string Url)> WebEngines = new()
    {
        ["yandex"] = ("Яндекс", "https://yandex.ru/search/?text={0}"),
        ["google"] = ("Google", "https://www.google.com/search?q={0}"),
        ["duckduckgo"] = ("DuckDuckGo", "https://duckduckgo.com/?q={0}"),
        ["bing"] = ("Bing", "https://www.bing.com/search?q={0}"),
        ["custom"] = ("Свой адрес", ""),
    };

    public string WebSearchUrl => WebSearchEngine == "custom" || !WebEngines.ContainsKey(WebSearchEngine)
        ? (CustomWebSearchUrl.Contains("{0}") ? CustomWebSearchUrl : CustomWebSearchUrl + "{0}")
        : WebEngines[WebSearchEngine].Url;

    public IEnumerable<ThemeDefinition> AllThemes() => BuiltInThemes.All.Concat(CustomThemes);

    public ThemeDefinition CurrentTheme() => AllThemes().FirstOrDefault(t => t.Id == ThemeId) ?? BuiltInThemes.All[0];

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    public static AppSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AppSettings();
        try { var s = JsonSerializer.Deserialize<AppSettings>(json, Json) ?? new AppSettings(); s.Sanitize(); return s; }
        catch (JsonException) { return new AppSettings(); }
    }

    /// <summary>Самовосстановление: читаем файл, при порче — .bak, иначе значения по умолчанию. Всегда валидируем.</summary>
    public static AppSettings LoadOrHeal(string path, out bool healed)
    {
        healed = false;
        string? ReadSafe(string f) { try { return File.Exists(f) ? File.ReadAllText(f) : null; } catch { return null; } }
        var main = ReadSafe(path);
        if (main != null)
        {
            try { var s = JsonSerializer.Deserialize<AppSettings>(main, Json); if (s != null) { s.Sanitize(); return s; } } catch { }
        }
        var bak = ReadSafe(path + ".bak");
        if (bak != null)
        {
            try { var s = JsonSerializer.Deserialize<AppSettings>(bak, Json); if (s != null) { healed = true; s.Sanitize(); return s; } } catch { }
        }
        healed = main != null; // был файл, но испорчен
        return new AppSettings();
    }

    /// <summary>Чинит заведомо некорректные значения, чтобы приложение не падало и не «белело».</summary>
    public void Sanitize()
    {
        Language = Language == "en" ? "en" : "ru";
        SoundVolume = Math.Clamp(double.IsFinite(SoundVolume) ? SoundVolume : 0.6, 0, 1);
        FileSearchMaxDepth = Math.Clamp(FileSearchMaxDepth <= 0 ? 5 : FileSearchMaxDepth, 1, 12);
        FileSearchFolders ??= new();
        Pinned ??= new();
        CustomThemes ??= new();
        SystemOptions ??= new();
        if (string.IsNullOrWhiteSpace(Hotkey)) Hotkey = "Alt+Space";
        if (string.IsNullOrWhiteSpace(StartScreenHotkey)) StartScreenHotkey = "Alt+Shift+Space";
        // чиним пользовательские темы
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CustomThemes.RemoveAll(t => t == null);
        foreach (var t in CustomThemes) t.Sanitize();
        CustomThemes.RemoveAll(t => string.IsNullOrWhiteSpace(t.Id) || !seen.Add(t.Id));
        // тема должна существовать
        if (AllThemes().All(t => t.Id != ThemeId)) ThemeId = BuiltInThemes.All[0].Id;
    }

    /// <summary>Атомарная запись: во временный файл, затем замена.</summary>
    public static void SaveAtomic(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content, new System.Text.UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(tmp, path, path + ".bak", ignoreMetadataErrors: true);
        else File.Move(tmp, path);
    }
}

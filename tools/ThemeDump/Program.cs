using Iskra.Core.Themes;
using System.Text.Json;
var arr = BuiltInThemes.All.Select(t => JsonDocument.Parse(t.ToJson()).RootElement).ToArray();
var json = JsonSerializer.Serialize(arr, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
var outPath = args.Length > 0 ? args[0] : "themes.json";
File.WriteAllText(outPath, json);
Console.WriteLine($"Wrote {BuiltInThemes.All.Count} themes to {outPath}");

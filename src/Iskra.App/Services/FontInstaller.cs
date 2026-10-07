using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Iskra.App.Services;

/// <summary>Извлекает встроенные OFL-шрифты темы на диск (для системного шрифта и установки без прав администратора).</summary>
internal static class FontInstaller
{
    /// <summary>Возвращает путь к .ttf для семейства (копирует из ресурсов приложения в %LOCALAPPDATA%\Iskra\fonts).</summary>
    public static string? ExtractForSystem(string familyName)
    {
        var file = FamilyToFile(familyName);
        if (file == null) return null;
        var dst = Path.Combine(Paths.FontsForSystem, file);
        if (File.Exists(dst)) return dst;
        try
        {
            var uri = new Uri($"pack://application:,,,/Fonts/{file}");
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri == null) return null;
            using var fs = File.Create(dst);
            sri.Stream.CopyTo(fs);
            return dst;
        }
        catch (Exception ex) { Log.Error("Извлечение шрифта " + familyName, ex); return null; }
    }

    public static string? FamilyToFile(string family) => family switch
    {
        "Inter" => "Inter-400.ttf",
        "Open Sans" or "Open Sans Light" => "OpenSans-400.ttf",
        "Caveat" => "Caveat-400.ttf",
        "Marck Script" => "MarckScript-400.ttf",
        "Amatic SC" => "AmaticSC-400.ttf",
        "Neucha" => "Neucha-400.ttf",
        "JetBrains Mono" => "JetBrainsMono-400.ttf",
        "Press Start 2P" => "PressStart2P-400.ttf",
        "Russo One" => "RussoOne-400.ttf",
        "Exo 2" => "Exo2-300.ttf",
        "Comfortaa" => "Comfortaa-400.ttf",
        "Manrope" => "Manrope-400.ttf",
        "Lora" or "Lora SemiBold" => "Lora-400.ttf",
        "Pacifico" => "Pacifico-400.ttf",
        _ => null,
    };
}

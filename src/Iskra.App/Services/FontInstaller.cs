using System;
using System.Collections.Generic;
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

/// <summary>Проверка шрифта перед применением: установлен ли он в системе (GDI его видит) и есть ли кириллица.</summary>
internal static class FontCheck
{
    public static bool Exists(string face)
    {
        if (string.IsNullOrWhiteSpace(face)) return false;
        try
        {
            return System.Windows.Media.Fonts.SystemFontFamilies.Any(f =>
                f.FamilyNames.Values.Any(n => string.Equals(n, face, StringComparison.OrdinalIgnoreCase)));
        }
        catch { return false; }
    }

    public static bool HasCyrillic(string face)
    {
        try
        {
            var tf = new System.Windows.Media.Typeface(face);
            if (!tf.TryGetGlyphTypeface(out var g)) return false;
            return "ЖжЁёЯя".All(c => g.CharacterToGlyphMap.ContainsKey(c));
        }
        catch { return false; }
    }

    /// <summary>
    /// Делает шрифт пригодным для системных областей. Встроенный шрифт ставится для текущего пользователя
    /// (%LOCALAPPDATA%\Microsoft\Windows\Fonts + HKCU\...\Fonts, без прав администратора). Возвращает имя шрифта
    /// или null, если шрифта нет / нет кириллицы (тогда вызывающий берёт штатный).
    /// </summary>
    public static string? Ensure(string? face, List<string>? problems = null)
    {
        if (string.IsNullOrWhiteSpace(face)) return null;
        if (!Exists(face))
        {
            var ttf = FontInstaller.ExtractForSystem(face);
            if (ttf == null) { problems?.Add($"{face}: шрифт не найден"); return null; }
            if (!InstallForUser(face, ttf)) { problems?.Add($"{face}: не удалось установить"); return null; }
        }
        if (!HasCyrillic(face)) { problems?.Add($"{face}: нет кириллицы"); return null; }
        return face;
    }

    public const string UserFontsKey = @"Software\Microsoft\Windows NT\CurrentVersion\Fonts";

    private static bool InstallForUser(string face, string ttf)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts");
            Directory.CreateDirectory(dir);
            var dst = Path.Combine(dir, Path.GetFileName(ttf));
            if (!File.Exists(dst)) File.Copy(ttf, dst);
            using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(UserFontsKey))
                k.SetValue(face + " (TrueType)", dst);
            Native.AddFontResource(dst);                     // 1. регистрация в GDI текущего сеанса
            FontApply.Broadcast();                           //    + WM_FONTCHANGE
            return true;
        }
        catch (Exception ex) { Log.Error("Установка шрифта " + face, ex); return false; }
    }
}

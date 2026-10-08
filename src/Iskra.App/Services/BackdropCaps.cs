using System;
using System.Windows.Media;
using Microsoft.Win32;

namespace Iskra.App.Services;

/// <summary>
/// Определяет, будет ли DWM реально рисовать слюду/акрил. Если нет (прозрачность выключена,
/// экономия заряда, Windows 10, слабый рендер) — окно обязано иметь НЕПРОЗРАЧНЫЙ фон,
/// иначе получится «засветлённое» окно с невидимым текстом (жалоба пользователя).
/// </summary>
internal static class BackdropCaps
{
    private static bool? _cached;

    public static bool Available(bool reduceEffects)
    {
        if (reduceEffects) return false;
        _cached ??= Compute();
        return _cached.Value;
    }

    public static void Invalidate() => _cached = null;

    private static bool Compute()
    {
        try
        {
            // Mica/Acrylic требуют Windows 11 (build 22000+).
            var os = Environment.OSVersion.Version;
            if (os.Major < 10 || (os.Major == 10 && os.Build < 22000)) return false;

            // Прозрачность выключена пользователем → DWM рисует сплошной резервный цвет, а не материал.
            if (ReadDword(Registry.CurrentUser,
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 1) == 0)
                return false;

            // Программная отрисовка (Tier 0) — материал не тянем, рисуем сплошным.
            if ((RenderCapability.Tier >> 16) < 1) return false;

            // Режим экономии заряда — материал заменяется сплошным; считаем недоступным.
            if (SystemInformationHelper.BatterySaverOn()) return false;

            return true;
        }
        catch { return false; }
    }

    private static int ReadDword(RegistryKey root, string sub, string name, int def)
    {
        try { using var k = root.OpenSubKey(sub); var v = k?.GetValue(name); return v is int i ? i : def; }
        catch { return def; }
    }
}

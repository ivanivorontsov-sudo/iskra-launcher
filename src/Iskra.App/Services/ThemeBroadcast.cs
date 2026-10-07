using System;

namespace Iskra.App.Services;

/// <summary>Сообщить системе и Проводнику о смене темы/настроек.</summary>
internal static class ThemeBroadcast
{
    public static void Notify()
    {
        foreach (var area in new[] { "ImmersiveColorSet", "WindowsThemeElement", "Policy", "Environment" })
            Native.SendMessageTimeout(new IntPtr(0xFFFF), Native.WM_SETTINGCHANGE, IntPtr.Zero, area, Native.SMTO_ABORTIFHUNG, 3000, out _);
    }
}

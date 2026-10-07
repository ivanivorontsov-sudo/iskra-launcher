using System;
using System.Media;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Iskra.Core.Media;
using Iskra.Core.Themes;

namespace Iskra.App.Services;

internal static class SystemParametersHelper
{
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    public static (int w, int h) PrimaryPixels()
    {
        int w = GetSystemMetrics(0), h = GetSystemMetrics(1);
        return (w > 0 ? w : 1920, h > 0 ? h : 1080);
    }
}

internal static class SystemInformationHelper
{
    public static bool ClientAnimationsEnabled()
    {
        try { return SystemParametersInfoBool(0x1042); } catch { return true; } // SPI_GETCLIENTAREAANIMATION
    }

    public static bool BatterySaverOn()
    {
        try
        {
            if (GetSystemPowerStatus(out var s) && s.SystemStatusFlag == 1) return true;
        }
        catch { }
        return false;
    }

    private static bool SystemParametersInfoBool(uint action)
    {
        bool val = true;
        SystemParametersInfo(action, 0, ref val, 0);
        return val;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SystemParametersInfo(uint action, uint uParam, ref bool pv, uint winIni);
    [DllImport("kernel32.dll")] private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
    [StructLayout(LayoutKind.Sequential)] private struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }
}

/// <summary>Проигрывание сгенерированных UI-звуков без блокировки потока.</summary>
internal static class SoundPlayer
{
    public static void Play(SoundPack pack, SoundEvent ev, double volume)
    {
        if (pack == SoundPack.None || volume <= 0) return;
        Task.Run(() =>
        {
            try
            {
                var wav = AssetFactory.UiSound(pack, ev);
                using var ms = new System.IO.MemoryStream(wav);
                using var sp = new System.Media.SoundPlayer(ms);
                sp.PlaySync();
            }
            catch (Exception ex) { Log.Error("Звук UI", ex); }
        });
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Iskra.Core.Settings;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// Живое применение и возврат шрифтов. Заголовки/меню/значки/сообщения — через SystemParametersInfo
/// (SPI_SETNONCLIENTMETRICS / SPI_SETICONMETRICS, SPIF_SENDCHANGE): меняются сразу, без перезагрузки.
/// Базовый UI-шрифт (подстановка Segoe UI) — отдельно, с правами администратора (нужен повторный вход).
/// </summary>
internal static class FontApply
{
    private const uint SPIF = Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE;

    /// <summary>Применяет выбор шрифтов «вживую» (без HKLM). Возвращает true, если что-то поменяли.</summary>
    public static bool ApplyNonClient(FontSettings f, double sizePt)
    {
        if (f == null) return false;
        bool any = false;
        try
        {
            var ncm = new Native.NONCLIENTMETRICS { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.NONCLIENTMETRICS>() };
            if (Native.SystemParametersInfo(Native.SPI_GETNONCLIENTMETRICS, (uint)ncm.cbSize, ref ncm, 0))
            {
                // Пустое имя = оставить текущий шрифт, но размер (если задан) применяем ко всем областям.
                string Face(string? want, Native.LOGFONT cur) => string.IsNullOrEmpty(want) ? cur.lfFaceName : want!;
                bool size = sizePt > 0;
                if (!string.IsNullOrEmpty(f.TitleFont) || size) { ncm.lfCaptionFont = SetFace(ncm.lfCaptionFont, Face(f.TitleFont, ncm.lfCaptionFont), sizePt); ncm.lfSmCaptionFont = SetFace(ncm.lfSmCaptionFont, Face(f.TitleFont, ncm.lfSmCaptionFont), sizePt); any = true; }
                if (!string.IsNullOrEmpty(f.MenuFont) || size) { ncm.lfMenuFont = SetFace(ncm.lfMenuFont, Face(f.MenuFont, ncm.lfMenuFont), sizePt); any = true; }
                if (!string.IsNullOrEmpty(f.MessageFont) || size) { ncm.lfMessageFont = SetFace(ncm.lfMessageFont, Face(f.MessageFont, ncm.lfMessageFont), sizePt); ncm.lfStatusFont = SetFace(ncm.lfStatusFont, Face(f.MessageFont, ncm.lfStatusFont), sizePt); any = true; }
                if (any) Native.SystemParametersInfo(Native.SPI_SETNONCLIENTMETRICS, (uint)ncm.cbSize, ref ncm, SPIF);
            }
            if (!string.IsNullOrEmpty(f.IconFont) || sizePt > 0)
            {
                var im = new Native.ICONMETRICS { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.ICONMETRICS>() };
                if (Native.SystemParametersInfo(Native.SPI_GETICONMETRICS, (uint)im.cbSize, ref im, 0))
                {
                    im.lfFont = SetFace(im.lfFont, string.IsNullOrEmpty(f.IconFont) ? im.lfFont.lfFaceName : f.IconFont!, sizePt);
                    Native.SystemParametersInfo(Native.SPI_SETICONMETRICS, (uint)im.cbSize, ref im, SPIF);
                    any = true;
                }
            }
        }
        catch (Exception ex) { Log.Error("ApplyNonClient", ex); }
        if (any) Broadcast();
        return any;
    }

    private static Native.LOGFONT SetFace(Native.LOGFONT lf, string face, double sizePt)
    {
        lf.lfFaceName = face.Length > 31 ? face.Substring(0, 31) : face;
        if (sizePt > 0) lf.lfHeight = -(int)Math.Round(sizePt * 96.0 / 72.0);
        return lf;
    }

    /// <summary>Возвращает заголовки/меню/значки/сообщения к состоянию реестра (после отката снимка) — живым SPI.</summary>
    public static void PushNonClientFromRegistry()
    {
        try
        {
            using var wm = Registry.CurrentUser.OpenSubKey(FontPlan.WindowMetricsKey);
            if (wm == null) return;
            var ncm = new Native.NONCLIENTMETRICS { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.NONCLIENTMETRICS>() };
            if (Native.SystemParametersInfo(Native.SPI_GETNONCLIENTMETRICS, (uint)ncm.cbSize, ref ncm, 0))
            {
                ncm.lfCaptionFont = FaceFromReg(wm, "CaptionFont", ncm.lfCaptionFont);
                ncm.lfSmCaptionFont = FaceFromReg(wm, "SmCaptionFont", ncm.lfSmCaptionFont);
                ncm.lfMenuFont = FaceFromReg(wm, "MenuFont", ncm.lfMenuFont);
                ncm.lfMessageFont = FaceFromReg(wm, "MessageFont", ncm.lfMessageFont);
                ncm.lfStatusFont = FaceFromReg(wm, "StatusFont", ncm.lfStatusFont);
                Native.SystemParametersInfo(Native.SPI_SETNONCLIENTMETRICS, (uint)ncm.cbSize, ref ncm, SPIF);
            }
            var im = new Native.ICONMETRICS { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.ICONMETRICS>() };
            if (Native.SystemParametersInfo(Native.SPI_GETICONMETRICS, (uint)im.cbSize, ref im, 0))
            {
                im.lfFont = FaceFromReg(wm, "IconFont", im.lfFont);
                Native.SystemParametersInfo(Native.SPI_SETICONMETRICS, (uint)im.cbSize, ref im, SPIF);
            }
        }
        catch (Exception ex) { Log.Error("PushNonClientFromRegistry", ex); }
        Broadcast();
    }

    private static Native.LOGFONT FaceFromReg(RegistryKey wm, string name, Native.LOGFONT fallback)
    {
        if (wm.GetValue(name) is byte[] blob && blob.Length >= FontPlan.LogFontSize)
        {
            var face = FontPlan.ReadFaceName(blob);
            if (!string.IsNullOrEmpty(face)) fallback.lfFaceName = face;
        }
        return fallback;
    }

    /// <summary>Рассылает WM_FONTCHANGE и WM_SETTINGCHANGE (Fonts/WindowMetrics), чтобы приложения перечитали шрифты.</summary>
    public static void Broadcast()
    {
        var hwndBroadcast = new IntPtr(0xFFFF);
        Native.SendMessageTimeout(hwndBroadcast, Native.WM_FONTCHANGE, IntPtr.Zero, "", Native.SMTO_ABORTIFHUNG, 3000, out _);
        foreach (var area in new[] { "Fonts", "WindowMetrics" })
            Native.SendMessageTimeout(hwndBroadcast, Native.WM_SETTINGCHANGE, IntPtr.Zero, area, Native.SMTO_ABORTIFHUNG, 3000, out _);
    }

    /// <summary>Перерегистрирует семейство Segoe UI из C:\Windows\Fonts (после починки записей реестра).</summary>
    public static void ReregisterSegoe()
    {
        try
        {
            var fontsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Fonts");
            foreach (var (_, file) in FontPlan.SegoeDefaults)
            {
                var full = Path.Combine(fontsDir, file);
                if (File.Exists(full)) Native.AddFontResource(full);
            }
        }
        catch (Exception ex) { Log.Warn("ReregisterSegoe: " + ex.Message); }
        var hwndBroadcast = new IntPtr(0xFFFF);
        Native.SendMessageTimeout(hwndBroadcast, Native.WM_FONTCHANGE, IntPtr.Zero, "", Native.SMTO_ABORTIFHUNG, 3000, out _);
    }
}

/// <summary>Аккуратный перезапуск Проводника, чтобы рабочий стол и панель задач перечитали шрифты/метрики.</summary>
internal static class ExplorerRestarter
{
    private static DateTime _last = DateTime.MinValue;
    public static event Action? ExplorerRestarted;

    public static void Restart(bool flushIconCache = false)
    {
        try
        {
            // Повторный запрос со сбросом кэша не отбрасываем: иначе новые значки не попадут в кэш.
            if (!flushIconCache && (DateTime.UtcNow - _last).TotalSeconds < 8) return;
            _last = DateTime.UtcNow;
            foreach (var p in Process.GetProcessesByName("explorer"))
            {
                try { p.Kill(); } catch { }
            }
            if (flushIconCache) FlushIconCache();
            // Windows обычно сама поднимает оболочку; на всякий случай стартуем через 800 мс, если её нет.
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(1500);
                if (!Process.GetProcessesByName("explorer").Any())
                {
                    try { Process.Start(new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true }); } catch { }
                }
                try { ExplorerRestarted?.Invoke(); } catch { }
            });
        }
        catch (Exception ex) { Log.Warn("ExplorerRestarter: " + ex.Message); }
    }

    /// <summary>Кэш значков иначе держит старую картинку и после смены IconLocation. Звать, пока Проводник уже остановлен.</summary>
    private static void FlushIconCache()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Explorer");
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir))
            {
                var name = Path.GetFileName(f);
                if (name.StartsWith("iconcache", StringComparison.OrdinalIgnoreCase) || name.StartsWith("thumbcache", StringComparison.OrdinalIgnoreCase))
                    try { File.Delete(f); } catch { }
            }
        }
        catch (Exception ex) { Log.Warn("Кэш значков: " + ex.Message); }
    }
}

/// <summary>
/// Повышенная точка «Исправить шрифты» (--repair-fonts): возвращает штатную регистрацию Segoe UI и удаляет
/// подстановку «Segoe UI», даже если снимок уже утерян. Затем перерегистрирует шрифты и рассылает уведомления.
/// </summary>
internal static class FontRepairEntry
{
    public static int Run()
    {
        try
        {
            var applier = new RegistryApplier(new Iskra.Core.Windows.SystemSnapshot());
            applier.Apply(Iskra.Core.Windows.FontPlan.BuildFontRepair());
            FontApply.ReregisterSegoe();
            FontApply.PushNonClientFromRegistry();
            FontApply.Broadcast();
            ThemeBroadcast.Notify();
            ExplorerRestarter.Restart();
            return 0;
        }
        catch (Exception ex) { Log.Error("FontRepairEntry", ex); return 1; }
    }

    /// <summary>Запускает повышенную починку шрифтов (UAC). true, если процесс успешно отработал.</summary>
    public static bool RunElevated()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppInfo.ExePath,
                Arguments = "--repair-fonts",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            var p = Process.Start(psi);
            p?.WaitForExit(120000);
            return p != null && p.ExitCode == 0;
        }
        catch (System.ComponentModel.Win32Exception) { return false; }
        catch (Exception ex) { Log.Error("RunElevated repair", ex); return false; }
    }
}

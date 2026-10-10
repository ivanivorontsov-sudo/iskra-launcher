using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>Сетка рабочего стола: живое применение через SystemParametersInfo (без перезапуска/перезагрузки).</summary>
internal static class DesktopGrid
{
    public const int DefaultTwips = -1125; // штатные 75 px

    public static int Dpi()
    {
        using var wm = Registry.CurrentUser.OpenSubKey(FontPlan.WindowMetricsKey);
        return wm?.GetValue("AppliedDPI") is int d && d >= 96 && d <= 480 ? d : 96;
    }

    public static (string? h, string? v) Read()
    {
        using var wm = Registry.CurrentUser.OpenSubKey(FontPlan.WindowMetricsKey);
        return (wm?.GetValue("IconSpacing") as string, wm?.GetValue("IconVerticalSpacing") as string);
    }

    /// <summary>Берёт значения из реестра (уже проверенные) и применяет вживую. Флаги «Упорядочить автоматически»
    /// и «Выровнять по сетке» (Bags\1\Desktop\FFlags) не трогаем — они сохраняются.</summary>
    public static void ApplyFromRegistry()
    {
        try
        {
            var (h, v) = Read();
            int hPx = int.TryParse(h, out var th) ? SystemPlan.TwipsToPx(th) : 75;
            int vPx = int.TryParse(v, out var tv) ? SystemPlan.TwipsToPx(tv) : 75;
            Apply(hPx, vPx);
        }
        catch (Exception ex) { Log.Error("Сетка рабочего стола", ex); }
    }

    public static void Apply(int hPx, int vPx)
    {
        double k = Dpi() / 96.0;
        uint f = Native.SPIF_UPDATEINIFILE | Native.SPIF_SENDCHANGE;
        Native.SystemParametersInfoPtr(Native.SPI_ICONHORIZONTALSPACING, (uint)Math.Round(hPx * k), IntPtr.Zero, f);
        Native.SystemParametersInfoPtr(Native.SPI_ICONVERTICALSPACING, (uint)Math.Round(vPx * k), IntPtr.Zero, f);
        // SPI пишет значения с учётом dpi; возвращаем «логические» твипы, чтобы реестр совпадал со снимком/планом.
        using var wm = Registry.CurrentUser.OpenSubKey(FontPlan.WindowMetricsKey, true);
        wm?.SetValue("IconSpacing", SystemPlan.PxToTwips(hPx).ToString(System.Globalization.CultureInfo.InvariantCulture));
        wm?.SetValue("IconVerticalSpacing", SystemPlan.PxToTwips(vPx).ToString(System.Globalization.CultureInfo.InvariantCulture));
        try { Native.SHChangeNotify(Native.SHCNE_ASSOCCHANGED, Native.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); } catch { }
    }
}

/// <summary>
/// «Починить рабочий стол»: сетка → исходная из снимка или штатная (75 px); значки ярлыков/папок/.url → исходные;
/// наши Shell Icons (3, 4, 29) и DefaultIcon папок → удалить; шрифт подписей → проверить; кэш значков → сбросить
/// с мягким перезапуском Проводника; шрифты Segoe UI → починить (с UAC, только если повреждены).
/// </summary>
internal static class DesktopRepair
{
    private const string OurMarker = @"\Iskra\generated\icons\";

    public static List<string> Run(SystemThemeApplier sys)
    {
        var done = new List<string>();
        var snap = sys.Snapshot;

        // 1. Сетка.
        string? Orig(string name) => snap.Registry.FirstOrDefault(r => r.Hive == RegHive.CurrentUser
            && r.Key.Equals(FontPlan.WindowMetricsKey, StringComparison.OrdinalIgnoreCase) && r.Name == name && r.ValueExisted)?.StringValue;
        int h = int.TryParse(Orig("IconSpacing"), out var oh) && !SystemPlan.IsGridValueBroken(oh.ToString()) ? SystemPlan.TwipsToPx(oh) : 75;
        int v = int.TryParse(Orig("IconVerticalSpacing"), out var ov) && !SystemPlan.IsGridValueBroken(ov.ToString()) ? SystemPlan.TwipsToPx(ov) : 75;
        DesktopGrid.Apply(h, v);
        done.Add($"сетка {h}×{v} px");

        // 2. Значки: из снимка, а ярлыки без резервной копии, указывающие на наши файлы, — к значку цели.
        if (snap.Shortcuts.Count > 0 || snap.FileIcons.Count > 0) { ShortcutIconApplier.Restore(snap); done.Add("значки ярлыков и папок"); }
        foreach (var dir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) })
        {
            try
            {
                foreach (var lnk in Directory.EnumerateFiles(dir, "*.lnk"))
                    if (ShellLink.GetIconLocation(lnk).Contains(OurMarker, StringComparison.OrdinalIgnoreCase)) ShellLink.SetIconLocation(lnk, "");
            }
            catch { }
        }

        // 3. Наши переопределения значков оболочки (стрелка ярлыка 29, папки 3/4, DefaultIcon).
        DeleteIfOurs(SystemPlan.ShellIconsKey, "29"); DeleteIfOurs(SystemPlan.ShellIconsKey, "3"); DeleteIfOurs(SystemPlan.ShellIconsKey, "4");
        DeleteIfOurs(SystemPlan.FolderIconKey, ""); DeleteIfOurs(SystemPlan.DirectoryIconKey, "");
        DeleteIfOurs(SystemPlan.ThisPcIconKey, ""); DeleteIfOurs(SystemPlan.RecycleIconKey, "empty"); DeleteIfOurs(SystemPlan.RecycleIconKey, "full");
        done.Add("значки оболочки и стрелки ярлыков");
        sys.SaveSnapshotPublic();

        // 4. Шрифт подписей значков: если такого шрифта нет — штатный Segoe UI 9 pt.
        try
        {
            using var wm = Registry.CurrentUser.OpenSubKey(FontPlan.WindowMetricsKey);
            var face = wm?.GetValue("IconFont") is byte[] b ? FontPlan.ReadFaceName(b) : "";
            if (string.IsNullOrEmpty(face) || !FontCheck.Exists(face))
            {
                FontApply.ApplyNonClient(new Iskra.Core.Settings.FontSettings { IconFont = "Segoe UI" }, 9);
                done.Add("шрифт подписей");
            }
            else FontApply.PushNonClientFromRegistry();
        }
        catch (Exception ex) { Log.Error("Шрифт подписей", ex); }

        // 5. Системный шрифт (HKLM) — только если повреждён, с одним запросом UAC.
        if (SystemThemeApplier.FontRepairNeeded() && FontRepairEntry.RunElevated()) done.Add("шрифты Segoe UI");

        // 6. Кэш значков + мягкий перезапуск Проводника.
        ShortcutIconApplier.RefreshIcons(restartExplorer: true);
        done.Add("кэш значков");
        return done;
    }

    /// <summary>Повышенная часть (--repair-desktop): починка Segoe UI.</summary>
    public static int RunElevatedPart() => FontRepairEntry.Run();

    private static void DeleteIfOurs(string key, string name)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(key, true);
            if (k?.GetValue(name) is string val && val.Contains(OurMarker, StringComparison.OrdinalIgnoreCase)
                && RegistryGuard.IsAllowed(RegHive.CurrentUser, key, name))
                k.DeleteValue(name, false);
        }
        catch (Exception ex) { Log.Warn($"{key}[{name}]: {ex.Message}"); }
    }
}

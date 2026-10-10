using System;
using System.Linq;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>Режимы для проверки на CI (windows-latest): применить тему к Windows без окна и вывести отчёт.</summary>
internal static class CiHarness
{
    // WinExe не имеет консоли — отчёт пишем в файл, CI его печатает.
    private static void Out(string line)
    {
        try { System.IO.File.AppendAllText(System.IO.Path.Combine(Paths.Root, "ci-report.txt"), line + Environment.NewLine); } catch { }
    }

    public static int ApplyTheme(string id)
    {
        try
        {
            Paths.Init();
            if (System.Windows.Application.Current == null) _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            var theme = BuiltInThemes.All.FirstOrDefault(t => t.Id == id) ?? throw new ArgumentException("нет темы " + id);
            var opts = new SystemApplyOptions
            {
                ShellIcons = true, ShapedShortcutIcons = true, HideShortcutArrows = true,
                LockScreen = false, Sounds = false, GridDensity = IconSpacing.Normal,
            };
            var sys = new SystemThemeApplier();
            var r = sys.ApplySerializedAsync(theme, opts, (1920, 1080), 90000).GetAwaiter().GetResult();
            Out($"[{id}] changed: {string.Join(", ", r.Changed)}");
            Out($"[{id}] skipped: {string.Join(", ", r.Skipped)}");
            var rep = ShortcutIconApplier.LastReport;
            Out($"[{id}] icons applied={rep.Applied} elevated={rep.Elevated} unsupported={rep.Unsupported}");
            return 0;
        }
        catch (Exception ex) { Out("CI apply failed: " + ex); Log.Error("CI", ex); return 1; }
    }

    public static int Repair()
    {
        try
        {
            Paths.Init();
            if (System.Windows.Application.Current == null) _ = new System.Windows.Application { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            Out("repair: " + string.Join(", ", DesktopRepair.Run(new SystemThemeApplier())));
            return 0;
        }
        catch (Exception ex) { Out("CI repair failed: " + ex); return 1; }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Iskra.App.Views;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// Перерисовывает значки ярлыков рабочего стола и меню «Пуск» в форме темы.
/// Исходный IconLocation каждого ярлыка сохраняется в снимок и возвращается при откате.
/// Выполняется ТОЛЬКО по явному согласию (SystemApplyOptions.ShapedShortcutIcons).
/// </summary>
internal static class ShortcutIconApplier
{
    private static IEnumerable<string> ShortcutDirs() => new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
    };

    public static int Apply(ThemeDefinition theme, SystemSnapshot snapshot, int max = 200)
    {
        int count = 0;
        var opts = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        foreach (var dir in ShortcutDirs().Where(Directory.Exists).Distinct())
        {
            IEnumerable<string> lnks;
            try { lnks = Directory.EnumerateFiles(dir, "*.lnk", opts); } catch { continue; }
            foreach (var lnk in lnks)
            {
                if (count >= max) break;
                try
                {
                    // резервная копия исходного значка (один раз)
                    if (!snapshot.Shortcuts.Any(x => string.Equals(x.LnkPath, lnk, StringComparison.OrdinalIgnoreCase)))
                        snapshot.Shortcuts.Add(new ShortcutIconBackup { LnkPath = lnk, OriginalIconLocation = ShellLink.GetIconLocation(lnk) });

                    var resolved = ShellLink.Resolve(lnk);
                    var target = resolved?.target;
                    var appIcon = IconLoader.Load(string.IsNullOrEmpty(target) ? lnk : target!);
                    if (appIcon == null) continue;
                    var key = Sanitize(Path.GetFileNameWithoutExtension(lnk)) + "_" + (uint)lnk.ToLowerInvariant().GetHashCode();
                    var ico = ShapeIconFactory.GenerateShortcutIcon(theme, appIcon, key);
                    if (ShellLink.SetIconLocation(lnk, ico + ",0")) count++;
                }
                catch (Exception ex) { Log.Warn($"Значок ярлыка {lnk}: {ex.Message}"); }
            }
        }
        if (count > 0) { snapshot.ShellIconsTouched = true; RefreshIcons(); }
        return count;
    }

    public static void Restore(SystemSnapshot snapshot)
    {
        foreach (var b in snapshot.Shortcuts)
        {
            try { if (File.Exists(b.LnkPath)) ShellLink.SetIconLocation(b.LnkPath, b.OriginalIconLocation); }
            catch (Exception ex) { Log.Warn($"Возврат значка {b.LnkPath}: {ex.Message}"); }
        }
        snapshot.Shortcuts.Clear();
        RefreshIcons();
    }

    public static void RefreshIcons()
    {
        try { Native.SHChangeNotify(Native.SHCNE_ASSOCCHANGED, Native.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); } catch { }
    }

    private static string Sanitize(string s) => string.Concat(s.Split(Path.GetInvalidFileNameChars()));
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Iskra.Core.Search;
using Microsoft.Win32;

namespace Iskra.App.Services;

/// <summary>Индексатор приложений: ярлыки меню «Пуск» (.lnk/.url) и приложения Store через AppsFolder (shell:).</summary>
internal static class AppIndexer
{
    public static List<SearchItem> IndexStartMenu()
    {
        var items = new List<SearchItem>();
        var dirs = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
        };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in dirs.Where(Directory.Exists))
        {
            foreach (var file in SafeEnumerate(dir, "*.lnk").Concat(SafeEnumerate(dir, "*.url")))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (name.Length == 0 || name.Contains("uninstall", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("удал", StringComparison.OrdinalIgnoreCase) || name.Contains("readme", StringComparison.OrdinalIgnoreCase)) continue;
                if (!seen.Add(name)) continue;
                items.Add(new SearchItem("lnk:" + file.ToLowerInvariant(), name, ItemKind.App, file, Loc("kind.App"))
                { FilePath = file });
            }
        }
        return items;
    }

    public static List<SearchItem> IndexStoreApps()
    {
        var items = new List<SearchItem>();
        try
        {
            // UWP/Store: имя (DisplayName) → AppUserModelID из AppsFolder; запуск через shell:AppsFolder\<AUMID>.
            foreach (var (name, aumid) in StoreApps.Enumerate())
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                items.Add(new SearchItem("uwp:" + aumid.ToLowerInvariant(), name, ItemKind.StoreApp, @"shell:AppsFolder\" + aumid, Loc("kind.App")));
            }
        }
        catch (Exception ex) { Log.Error("Индекс Store", ex); }
        return items;
    }

    private static IEnumerable<string> SafeEnumerate(string dir, string pattern)
    {
        try { return Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories); }
        catch { return Enumerable.Empty<string>(); }
    }

    private static string Loc(string k) => Iskra.Core.Loc.L.T(k);
}

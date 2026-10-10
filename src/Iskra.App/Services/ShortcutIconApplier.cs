using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Iskra.App.Views;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>
/// Значки рабочего стола в форме темы.
///  • .lnk (свой и общий рабочий стол, свой «Пуск») — IconLocation, исходный сохраняется в снимок;
///  • .url — IconFile/IconIndex; • папки — desktop.ini (IconResource) + атрибут ReadOnly;
///  • «Этот компьютер», корзина, папки пользователя — через реестр (SystemPlan, Shell Icons/DefaultIcon);
///  • .exe/.docx и другие обычные файлы собственного значка иметь не могут — их форма не меняется (сообщаем в отчёте).
/// Ярлыки общего рабочего стола (C:\Users\Public\Desktop) пишутся одним повышенным процессом (UAC).
/// </summary>
internal static class ShortcutIconApplier
{
    public sealed record Report(int Applied, int Elevated, int Unsupported, List<string> UnsupportedNames);

    private const string OurMarker = @"\Iskra\generated\icons\";

    private static string UserDesktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    private static string PublicDesktop => Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);

    public static Report LastReport { get; private set; } = new(0, 0, 0, new());

    public static int Apply(ThemeDefinition theme, SystemSnapshot snapshot, int max = 400)
    {
        int count = 0, elevated = 0;
        var unsupported = new List<string>();
        var pending = new List<string[]>(); // [lnk, ico] — для повышенного процесса
        var touched = new List<string>();

        IEnumerable<string> Top(string dir) { try { return Directory.Exists(dir) ? Directory.EnumerateFileSystemEntries(dir) : Array.Empty<string>(); } catch { return Array.Empty<string>(); } }

        var items = Top(UserDesktop).Concat(Top(PublicDesktop)).ToList();
        // Свой «Пуск» — только .lnk (общий «Пуск» не трогаем: сотни ярлыков SDK и т. п.).
        try
        {
            var sm = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
            if (Directory.Exists(sm))
                items.AddRange(Directory.EnumerateFiles(sm, "*.lnk", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }));
        }
        catch { }

        foreach (var item in items.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (count + elevated >= max) break;
            var name = Path.GetFileName(item);
            if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (Directory.Exists(item)) { if (ApplyFolder(theme, snapshot, item)) { count++; touched.Add(item); } continue; }
                var ext = Path.GetExtension(item).ToLowerInvariant();
                if (ext == ".lnk")
                {
                    var r = ApplyLnk(theme, snapshot, item, pending);
                    if (r == 1) { count++; touched.Add(item); }
                    continue;
                }
                if (ext == ".url") { if (ApplyUrl(theme, snapshot, item)) { count++; touched.Add(item); } continue; }
                if (item.StartsWith(UserDesktop, StringComparison.OrdinalIgnoreCase) || item.StartsWith(PublicDesktop, StringComparison.OrdinalIgnoreCase))
                    unsupported.Add(name);
            }
            catch (Exception ex) { Log.Warn($"Значок {item}: {ex.Message}"); }
        }

        if (pending.Count > 0)
        {
            elevated = RunElevatedBatch(pending);
            if (elevated > 0) touched.AddRange(pending.Select(p => p[0]));
        }

        if (count + elevated > 0) snapshot.ShellIconsTouched = true;
        LastReport = new Report(count, elevated, unsupported.Count, unsupported);
        if (unsupported.Count > 0) Log.Info("Без формы (обычные файлы не имеют своего значка): " + string.Join(", ", unsupported));
        Refresh(touched, restartExplorer: false); // перезапуск Проводника — один раз в конце применения темы
        return count + elevated;
    }

    /// <summary>0 — пропуск, 1 — применено, 2 — отложено для повышенного процесса.</summary>
    private static int ApplyLnk(ThemeDefinition theme, SystemSnapshot snapshot, string lnk, List<string[]> pending)
    {
        var backup = snapshot.Shortcuts.FirstOrDefault(x => string.Equals(x.LnkPath, lnk, StringComparison.OrdinalIgnoreCase));
        var current = ShellLink.GetIconLocation(lnk);
        if (backup == null)
        {
            // Если снимок потерян, а ярлык уже указывает на наш значок, исходным считаем «значок цели».
            var orig = current.Contains(OurMarker, StringComparison.OrdinalIgnoreCase) ? "" : current;
            backup = new ShortcutIconBackup { LnkPath = lnk, OriginalIconLocation = orig };
            snapshot.Shortcuts.Add(backup);
        }
        var target = ShellLink.Resolve(lnk)?.target;
        var src = IconLoader.LoadLarge(backup.OriginalIconLocation, string.IsNullOrEmpty(target) ? lnk : target!);
        if (src == null) return 0;
        var ico = ShapeIconFactory.GenerateShortcutIcon(theme, src, Key(lnk));
        if (ShellLink.SetIconLocation(lnk, ico + ",0") && string.Equals(ShellLink.GetIconLocation(lnk), ico + ",0", StringComparison.OrdinalIgnoreCase))
            return 1;
        pending.Add(new[] { lnk, ico + ",0" }); // нет прав (общий рабочий стол) — сделаем с UAC
        return 2;
    }

    private static bool ApplyUrl(ThemeDefinition theme, SystemSnapshot snapshot, string url)
    {
        var lines = File.ReadAllLines(url).ToList();
        string? Get(string k) => lines.FirstOrDefault(l => l.StartsWith(k + "=", StringComparison.OrdinalIgnoreCase))?.Substring(k.Length + 1);
        if (!snapshot.FileIcons.Any(f => string.Equals(f.Path, url, StringComparison.OrdinalIgnoreCase)))
        {
            var file = Get("IconFile"); var idx = Get("IconIndex");
            bool ours = file?.Contains(OurMarker, StringComparison.OrdinalIgnoreCase) == true;
            snapshot.FileIcons.Add(new FileIconBackup { Path = url, Kind = "url", Existed = file != null && !ours, Original = ours ? null : $"{file}|{idx}" });
        }
        var b = snapshot.FileIcons.First(f => string.Equals(f.Path, url, StringComparison.OrdinalIgnoreCase));
        string? srcFile = b.Existed ? b.Original?.Split('|')[0] : null;
        var src = IconLoader.LoadLarge(srcFile, url);
        if (src == null) return false;
        var ico = ShapeIconFactory.GenerateShortcutIcon(theme, src, Key(url));
        SetIni(lines, "InternetShortcut", "IconFile", ico);
        SetIni(lines, "InternetShortcut", "IconIndex", "0");
        File.WriteAllLines(url, lines);
        return true;
    }

    private static bool ApplyFolder(ThemeDefinition theme, SystemSnapshot snapshot, string dir)
    {
        var ini = Path.Combine(dir, "desktop.ini");
        if (!snapshot.FileIcons.Any(f => string.Equals(f.Path, dir, StringComparison.OrdinalIgnoreCase)))
        {
            bool existed = File.Exists(ini);
            snapshot.FileIcons.Add(new FileIconBackup
            {
                Path = dir, Kind = "folder", Existed = existed,
                Original = existed ? File.ReadAllText(ini) : null,
                Attributes = (int)File.GetAttributes(dir),
            });
        }
        var src = IconLoader.LoadLarge(null, dir);
        if (src == null) return false;
        var ico = ShapeIconFactory.GenerateShortcutIcon(theme, src, Key(dir));
        var lines = File.Exists(ini) ? File.ReadAllLines(ini).ToList() : new List<string>();
        if (File.Exists(ini)) File.SetAttributes(ini, FileAttributes.Normal);
        SetIni(lines, ".ShellClassInfo", "IconResource", ico + ",0");
        File.WriteAllLines(ini, lines, Encoding.Unicode);
        File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System);
        File.SetAttributes(dir, File.GetAttributes(dir) | FileAttributes.ReadOnly); // иначе Проводник игнорирует desktop.ini
        return true;
    }

    public static void Restore(SystemSnapshot snapshot)
    {
        var pending = new List<string[]>();
        var touched = new List<string>();
        foreach (var b in snapshot.Shortcuts)
        {
            try
            {
                if (!File.Exists(b.LnkPath)) continue;
                if (ShellLink.SetIconLocation(b.LnkPath, b.OriginalIconLocation)
                    && !ShellLink.GetIconLocation(b.LnkPath).Contains(OurMarker, StringComparison.OrdinalIgnoreCase)) touched.Add(b.LnkPath);
                else pending.Add(new[] { b.LnkPath, b.OriginalIconLocation });
            }
            catch (Exception ex) { Log.Warn($"Возврат значка {b.LnkPath}: {ex.Message}"); }
        }
        if (pending.Count > 0) RunElevatedBatch(pending);
        foreach (var f in snapshot.FileIcons)
        {
            try
            {
                if (f.Kind == "url" && File.Exists(f.Path))
                {
                    var lines = File.ReadAllLines(f.Path).ToList();
                    lines.RemoveAll(l => l.StartsWith("IconFile=", StringComparison.OrdinalIgnoreCase) || l.StartsWith("IconIndex=", StringComparison.OrdinalIgnoreCase));
                    if (f.Existed && f.Original != null)
                    {
                        var parts = f.Original.Split('|');
                        SetIni(lines, "InternetShortcut", "IconFile", parts[0]);
                        if (parts.Length > 1 && parts[1].Length > 0) SetIni(lines, "InternetShortcut", "IconIndex", parts[1]);
                    }
                    File.WriteAllLines(f.Path, lines);
                }
                else if (f.Kind == "folder" && Directory.Exists(f.Path))
                {
                    var ini = Path.Combine(f.Path, "desktop.ini");
                    if (File.Exists(ini)) File.SetAttributes(ini, FileAttributes.Normal);
                    if (f.Existed && f.Original != null) { File.WriteAllText(ini, f.Original, Encoding.Unicode); File.SetAttributes(ini, FileAttributes.Hidden | FileAttributes.System); }
                    else if (File.Exists(ini)) File.Delete(ini);
                    File.SetAttributes(f.Path, (FileAttributes)f.Attributes);
                }
                touched.Add(f.Path);
            }
            catch (Exception ex) { Log.Warn($"Возврат значка {f.Path}: {ex.Message}"); }
        }
        snapshot.Shortcuts.Clear();
        snapshot.FileIcons.Clear();
        Refresh(touched);
    }

    /// <summary>Надёжное обновление: точечный SHCNE_UPDATEITEM, общий ASSOCCHANGED, ie4uinit -show; Проводник — только если нужно.</summary>
    public static void Refresh(IEnumerable<string>? items = null, bool restartExplorer = true)
    {
        if (items != null)
            foreach (var p in items)
                try { Native.SHChangeNotify(Native.SHCNE_UPDATEITEM, Native.SHCNF_PATHW, p, IntPtr.Zero); } catch { }
        RefreshIcons(restartExplorer);
    }

    public static void RefreshIcons(bool restartExplorer = true)
    {
        try { Native.SHChangeNotify(Native.SHCNE_ASSOCCHANGED, Native.SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero); } catch { }
        try
        {
            var ie4 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "ie4uinit.exe");
            if (File.Exists(ie4)) Process.Start(new ProcessStartInfo(ie4, "-show") { UseShellExecute = false, CreateNoWindow = true })?.WaitForExit(5000);
        }
        catch { }
        // Windows 11 держит значки рабочего стола в iconcache*.db — сбрасываем его при мягком перезапуске Проводника.
        if (restartExplorer) ExplorerRestarter.Restart(flushIconCache: true);
    }

    // ---- Повышенный пакет для общего рабочего стола ----
    private static int RunElevatedBatch(List<string[]> pairs)
    {
        try
        {
            var file = Path.Combine(Paths.Root, "icon-batch.json");
            File.WriteAllText(file, JsonSerializer.Serialize(pairs));
            var p = Process.Start(new ProcessStartInfo
            {
                FileName = AppInfo.ExePath, Arguments = $"--icon-batch \"{file}\"",
                UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
            });
            p?.WaitForExit(60000);
            return p?.ExitCode is int n and >= 0 ? n : 0;
        }
        catch (System.ComponentModel.Win32Exception) { Log.Warn("Значки общего рабочего стола: отказ от UAC"); return 0; }
        catch (Exception ex) { Log.Error("icon-batch", ex); return 0; }
    }

    /// <summary>Точка входа повышенного процесса: --icon-batch файл.json. Возвращает число изменённых ярлыков.</summary>
    public static int RunBatch(string file)
    {
        int n = 0;
        try
        {
            var pairs = JsonSerializer.Deserialize<List<string[]>>(File.ReadAllText(file)) ?? new();
            foreach (var pr in pairs)
            {
                // Только ярлыки на рабочих столах — ничего другого повышенный процесс не трогает.
                if (pr.Length != 2 || !pr[0].EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) continue;
                if (!pr[0].StartsWith(PublicDesktop, StringComparison.OrdinalIgnoreCase) && !pr[0].StartsWith(UserDesktop, StringComparison.OrdinalIgnoreCase)) continue;
                if (ShellLink.SetIconLocation(pr[0], pr[1])) n++;
            }
            try { File.Delete(file); } catch { }
        }
        catch (Exception ex) { Log.Error("RunBatch", ex); }
        return n;
    }

    private static void SetIni(List<string> lines, string section, string key, string value)
    {
        int s = lines.FindIndex(l => l.Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
        if (s < 0) { lines.Add($"[{section}]"); lines.Add($"{key}={value}"); return; }
        int end = lines.FindIndex(s + 1, l => l.TrimStart().StartsWith("["));
        if (end < 0) end = lines.Count;
        for (int i = s + 1; i < end; i++)
            if (lines[i].StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) { lines[i] = $"{key}={value}"; return; }
        lines.Insert(end, $"{key}={value}");
    }

    private static string Key(string path)
    {
        var n = string.Concat(Path.GetFileNameWithoutExtension(path).Split(Path.GetInvalidFileNameChars()));
        if (n.Length > 40) n = n[..40];
        uint h = 2166136261; foreach (var c in path.ToLowerInvariant()) h = (h ^ c) * 16777619; // стабильный хэш (GetHashCode меняется между запусками)
        return n + "_" + h.ToString("x8");
    }
}

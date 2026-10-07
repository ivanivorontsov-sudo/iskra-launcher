using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Iskra.Core.Search;
using Iskra.Core.Loc;

namespace Iskra.App.Services;

/// <summary>Фоновый индекс файлов в выбранных папках. Ограничен по глубине и количеству, чтобы не тормозить.</summary>
internal static class FileIndexer
{
    public const int MaxItems = 60000;
    private static readonly string[] SkipDirs = { "node_modules", ".git", "AppData", "$Recycle.Bin", "Windows", "obj", "bin", ".cache" };
    private static readonly string[] SkipExt = { ".tmp", ".log", ".dll", ".sys", ".dat", ".pdb", ".obj", ".cache" };

    public static List<string> DefaultFolders() => new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + Path.DirectorySeparatorChar + "Downloads",
    }.Where(Directory.Exists).Distinct().ToList();

    public static List<SearchItem> Index(IEnumerable<string> folders, int maxDepth, CancellationToken ct)
    {
        var items = new List<SearchItem>();
        var kindFile = L.T("kind.File"); var kindFolder = L.T("kind.Folder");
        foreach (var root in folders.Where(Directory.Exists))
        {
            Walk(root, root, 0, maxDepth, items, kindFile, kindFolder, ct);
            if (items.Count >= MaxItems || ct.IsCancellationRequested) break;
        }
        return items;
    }

    private static void Walk(string root, string dir, int depth, int maxDepth, List<SearchItem> items, string kindFile, string kindFolder, CancellationToken ct)
    {
        if (depth > maxDepth || items.Count >= MaxItems || ct.IsCancellationRequested) return;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(dir); }
        catch { return; }
        foreach (var path in entries)
        {
            if (items.Count >= MaxItems || ct.IsCancellationRequested) return;
            var name = Path.GetFileName(path);
            if (name.StartsWith('.') || name.StartsWith('$')) continue;
            bool isDir;
            try { isDir = (File.GetAttributes(path) & FileAttributes.Directory) != 0; } catch { continue; }
            if (isDir)
            {
                if (SkipDirs.Any(d => string.Equals(d, name, StringComparison.OrdinalIgnoreCase))) continue;
                items.Add(new SearchItem("fsd:" + path.ToLowerInvariant(), name, ItemKind.Folder, path, kindFolder) { FilePath = path, Glyph = "\uE8B7" });
                Walk(root, path, depth + 1, maxDepth, items, kindFile, kindFolder, ct);
            }
            else
            {
                var ext = Path.GetExtension(name).ToLowerInvariant();
                if (SkipExt.Contains(ext)) continue;
                items.Add(new SearchItem("fsf:" + path.ToLowerInvariant(), name, ItemKind.File, path, kindFile) { FilePath = path, Glyph = "\uE7C3" });
            }
        }
    }
}

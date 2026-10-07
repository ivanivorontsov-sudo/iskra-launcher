using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Iskra.App.Services;

namespace Iskra.App.Views;

/// <summary>Извлекает иконки файлов/ярлыков/приложений через Shell и кэширует их.</summary>
internal static class IconLoader
{
    private static readonly ConcurrentDictionary<string, BitmapSource?> Cache = new();

    public static BitmapSource? Load(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        return Cache.GetOrAdd(path.ToLowerInvariant(), _ => Extract(path));
    }

    private static BitmapSource? Extract(string path)
    {
        try
        {
            string target = path;
            if (path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var res = ShellLink.Resolve(path);
                if (res is { target: { Length: > 0 } tgt } && File.Exists(tgt)) target = tgt;
            }
            if (path.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase)) return null; // Store — глиф-заглушка
            using var ico = Icon.ExtractAssociatedIcon(target);
            if (ico == null) return null;
            var src = Imaging.CreateBitmapSourceFromHIcon(ico.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        catch { return null; }
    }
}

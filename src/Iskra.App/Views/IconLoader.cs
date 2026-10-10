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
        var key = path.ToLowerInvariant();
        if (Cache.TryGetValue(key, out var cached)) return cached;

        // ConcurrentDictionary does not accept null reference values. Some shell targets
        // (notably AppsFolder placeholders and inaccessible/broken shortcuts) legitimately
        // have no extractable icon, so cache only successful extractions.
        var extracted = Extract(path);
        if (extracted != null) Cache.TryAdd(key, extracted);
        return extracted;
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

    // ---- Крупные значки (256 px) для системных .ico: иначе 32-пиксельный значок растягивается и выглядит грубо ----
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern uint PrivateExtractIcons(string file, int index, int cx, int cy, IntPtr[] icons, uint[]? ids, uint n, uint flags);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr h);

    [System.Runtime.InteropServices.ComImport, System.Runtime.InteropServices.Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),
     System.Runtime.InteropServices.InterfaceType(System.Runtime.InteropServices.ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory { [System.Runtime.InteropServices.PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)] private struct SIZE { public int cx, cy; }
    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [System.Runtime.InteropServices.In] ref Guid riid,
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Interface)] out IShellItemImageFactory item);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr h);

    /// <summary>
    /// Исходный значок в высоком разрешении. iconLocation — «путь,индекс» из ярлыка (если был задан),
    /// иначе берём значок самого файла/цели через IShellItemImageFactory (без оверлеев и без уже применённой формы).
    /// </summary>
    public static BitmapSource? LoadLarge(string? iconLocation, string fallbackPath, int size = 256)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(iconLocation))
            {
                var (file, idx) = SplitLocation(iconLocation!);
                file = Environment.ExpandEnvironmentVariables(file);
                if (File.Exists(file))
                {
                    var h = new IntPtr[1];
                    if (PrivateExtractIcons(file, idx, size, size, h, null, 1, 0) > 0 && h[0] != IntPtr.Zero)
                    {
                        try
                        {
                            var bs = Imaging.CreateBitmapSourceFromHIcon(h[0], Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                            bs.Freeze(); return bs;
                        }
                        finally { DestroyIcon(h[0]); }
                    }
                }
            }
            if (!string.IsNullOrEmpty(fallbackPath) && (File.Exists(fallbackPath) || Directory.Exists(fallbackPath)))
            {
                var iid = typeof(IShellItemImageFactory).GUID;
                SHCreateItemFromParsingName(fallbackPath, IntPtr.Zero, ref iid, out var f);
                // SIIGBF_BIGGERSIZEOK(1) | SIIGBF_ICONONLY(4)
                if (f.GetImage(new SIZE { cx = size, cy = size }, 0x1 | 0x4, out var hbm) == 0 && hbm != IntPtr.Zero)
                {
                    try
                    {
                        var bs = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        bs.Freeze(); return FixAlpha(bs);
                    }
                    finally { DeleteObject(hbm); }
                }
            }
        }
        catch { }
        return Load(fallbackPath);
    }

    public static (string file, int index) SplitLocation(string loc)
    {
        int comma = loc.LastIndexOf(',');
        if (comma > 1 && int.TryParse(loc[(comma + 1)..].Trim(), out var i)) return (loc[..comma].Trim().Trim('"'), i);
        return (loc.Trim().Trim('"'), 0);
    }

    /// <summary>HBITMAP из Shell приходит как Bgr32 с premultiplied альфой в старшем байте — переводим в Pbgra32.</summary>
    private static BitmapSource FixAlpha(BitmapSource src)
    {
        int w = src.PixelWidth, h = src.PixelHeight, stride = w * 4;
        var px = new byte[h * stride];
        src.CopyPixels(px, stride, 0);
        bool anyAlpha = false;
        for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) { anyAlpha = true; break; }
        if (!anyAlpha) for (int i = 3; i < px.Length; i += 4) px[i] = 255;
        var bs = BitmapSource.Create(w, h, 96, 96, System.Windows.Media.PixelFormats.Pbgra32, null, px, stride);
        bs.Freeze();
        return bs;
    }
}

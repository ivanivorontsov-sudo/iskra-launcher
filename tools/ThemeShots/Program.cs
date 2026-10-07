using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Iskra.App.Services;
using Iskra.Core.Themes;

namespace ThemeShots;

// Рендерит настоящие обои/карточки тем Искры в PNG (живой WPF), для экрана тем в CI.
internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var outDir = args.Length > 0 ? args[0] : "screens";
        Directory.CreateDirectory(outDir);
        var app = new Application();
        int w = 760, h = 560;
        foreach (var t in BuiltInThemes.All)
        {
            try
            {
                var dv = new DrawingVisual();
                using (var dc = dv.RenderOpen())
                    WallpaperPainter.Paint(dc, t, w, h, 0);
                var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                rtb.Render(dv);
                var enc = new PngBitmapEncoder();
                enc.Frames.Add(BitmapFrame.Create(rtb));
                using var fs = File.Create(Path.Combine(outDir, $"wallpaper-{t.Id}.png"));
                enc.Save(fs);
                Console.WriteLine($"wallpaper-{t.Id}.png");
            }
            catch (Exception ex) { Console.Error.WriteLine($"{t.Id}: {ex.Message}"); }
        }
        return 0;
    }
}

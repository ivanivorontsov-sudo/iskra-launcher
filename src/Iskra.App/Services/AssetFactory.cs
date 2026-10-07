using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Iskra.Core.Media;
using Iskra.Core.Themes;
using Iskra.Core.Windows;

namespace Iskra.App.Services;

/// <summary>Генерирует курсоры, звуки и обои по теме. Всё рисуется кодом — лицензий сторонних ресурсов нет.</summary>
internal static class AssetFactory
{
    // ---------- Звуки ----------
    public static Dictionary<string, string> GenerateSounds(ThemeDefinition t)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(Paths.Sounds, t.Id);
        Directory.CreateDirectory(dir);
        foreach (var ev in SystemPlan.SoundEvents)
        {
            var se = SoundSynth.ForWindowsEvent(ev);
            var path = Path.Combine(dir, Sanitize(ev) + ".wav");
            try { File.WriteAllBytes(path, SoundSynth.Wav(t.Sound, se)); map[ev] = path; } catch (Exception ex) { Log.Error("Звук " + ev, ex); }
        }
        return map;
    }

    public static byte[] UiSound(SoundPack pack, SoundEvent ev) => SoundSynth.Wav(pack, ev);

    private static string Sanitize(string s) => string.Concat(s.Split(Path.GetInvalidFileNameChars()));

    // ---------- Курсоры ----------
    public static (Dictionary<string, string> map, string scheme) GenerateCursors(ThemeDefinition t)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dir = Path.Combine(Paths.Cursors, t.Id);
        Directory.CreateDirectory(dir);
        var fill = Argb.Parse(t.System.CursorFill, new Argb(0xFF, 0xFF, 0xFF, 0xFF));
        var outline = Argb.Parse(t.System.CursorOutline, new Argb(0xFF, 0, 0, 0));
        var accent = Argb.Parse(t.Palette.Accent);
        int baseSize = Math.Clamp(t.System.CursorSize, 32, 96);

        void Save(string role, Func<int, byte[]> draw, int hotX, int hotY)
        {
            try
            {
                var imgs = new List<CursorImage>();
                foreach (var s in new[] { baseSize, baseSize * 3 / 2, baseSize * 2 })
                    imgs.Add(new CursorImage(s, draw(s), hotX * s / baseSize, hotY * s / baseSize));
                var path = Path.Combine(dir, role + ".cur");
                File.WriteAllBytes(path, CursorWriter.WriteCur(imgs));
                map[role] = path;
            }
            catch (Exception ex) { Log.Error("Курсор " + role, ex); }
        }

        Save("Arrow", s => CursorPainter.Arrow(s, t.System.Cursor, fill, outline), 0, 0);
        Save("Hand", s => CursorPainter.Hand(s, t.System.Cursor, fill, outline), (int)(baseSize * 0.35), 0);
        Save("IBeam", s => CursorPainter.IBeam(s, fill, outline), baseSize / 2, baseSize / 2);
        Save("Help", s => CursorPainter.Arrow(s, t.System.Cursor, fill, outline), 0, 0);
        Save("AppStarting", s => CursorPainter.Arrow(s, t.System.Cursor, fill, outline), 0, 0);
        Save("SizeAll", s => CursorPainter.SizeAll(s, fill, outline), baseSize / 2, baseSize / 2);
        Save("No", s => CursorPainter.No(s, accent, outline), baseSize / 2, baseSize / 2);
        Save("Wait", s => CursorPainter.Busy(s, accent, fill, outline), baseSize / 2, baseSize / 2);

        // Анимированные «часы» ожидания — несколько кадров.
        try
        {
            var frames = new List<byte[]>();
            for (int f = 0; f < 12; f++)
            {
                var imgs = new List<CursorImage> { new(baseSize, CursorPainter.BusyFrame(baseSize, f / 12.0, accent, fill, outline), baseSize / 2, baseSize / 2) };
                frames.Add(CursorWriter.WriteCur(imgs));
            }
            var ani = Path.Combine(dir, "Wait.ani");
            File.WriteAllBytes(ani, CursorWriter.WriteAni(frames, 5));
            map["Wait"] = ani; map["AppStarting"] = ani;
        }
        catch (Exception ex) { Log.Error("Курсор Wait.ani", ex); }

        return (map, "Искра — " + t.Name);
    }

    // ---------- Обои ----------
    public static string GenerateWallpaper(ThemeDefinition t, int w, int h, int frame = 0)
    {
        var dir = Path.Combine(Paths.Wallpapers, t.Id);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"wall_{w}x{h}{(frame > 0 ? "_" + frame : "")}.png");
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
            WallpaperPainter.Paint(dc, t, w, h, frame);
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(dv);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
        return path;
    }

    public static List<string> GenerateSlideshow(ThemeDefinition t, int w, int h, int count = 4)
    {
        var list = new List<string>();
        for (int i = 0; i < count; i++) list.Add(GenerateWallpaper(t, w, h, i + 1));
        return list;
    }
}

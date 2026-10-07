namespace Iskra.Core.Media;

/// <summary>Кадр курсора: квадрат size×size, пиксели BGRA (не premultiplied), сверху вниз.</summary>
public sealed record CursorImage(int Size, byte[] Bgra, int HotX, int HotY);

/// <summary>Запись .cur (несколько размеров) и анимированных .ani — форматы Windows, без сторонних библиотек.</summary>
public static class CursorWriter
{
    public static byte[] WriteCur(IReadOnlyList<CursorImage> images)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((short)0); w.Write((short)2); w.Write((short)images.Count);
        var blobs = images.Select(Dib).ToList();
        int offset = 6 + 16 * images.Count;
        for (int i = 0; i < images.Count; i++)
        {
            var im = images[i];
            w.Write((byte)(im.Size >= 256 ? 0 : im.Size));
            w.Write((byte)(im.Size >= 256 ? 0 : im.Size));
            w.Write((byte)0); w.Write((byte)0);
            w.Write((short)im.HotX); w.Write((short)im.HotY);
            w.Write(blobs[i].Length); w.Write(offset);
            offset += blobs[i].Length;
        }
        foreach (var b in blobs) w.Write(b);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>DIB: BITMAPINFOHEADER + XOR (32 бит, снизу вверх) + AND-маска (1 бит).</summary>
    private static byte[] Dib(CursorImage im)
    {
        int s = im.Size;
        if (im.Bgra.Length != s * s * 4) throw new ArgumentException("размер буфера не совпадает с размером курсора");
        int maskStride = ((s + 31) / 32) * 4;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(40); w.Write(s); w.Write(s * 2); w.Write((short)1); w.Write((short)32); w.Write(0);
        w.Write(s * s * 4 + maskStride * s); w.Write(0); w.Write(0); w.Write(0); w.Write(0);
        for (int y = s - 1; y >= 0; y--) w.Write(im.Bgra, y * s * 4, s * 4);
        for (int y = s - 1; y >= 0; y--)
        {
            var row = new byte[maskStride];
            for (int x = 0; x < s; x++)
                if (im.Bgra[(y * s + x) * 4 + 3] == 0) row[x / 8] |= (byte)(0x80 >> (x % 8));
            w.Write(row);
        }
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Анимированный курсор RIFF/ACON. jiffies — длительность кадра в 1/60 с.</summary>
    public static byte[] WriteAni(IReadOnlyList<byte[]> curFrames, int jiffies)
    {
        using var body = new MemoryStream();
        using var bw = new BinaryWriter(body);
        bw.Write("ACON"u8);
        bw.Write("anih"u8); bw.Write(36);
        bw.Write(36); bw.Write(curFrames.Count); bw.Write(curFrames.Count); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(0); bw.Write(jiffies); bw.Write(1);
        using var fram = new MemoryStream();
        using (var fw = new BinaryWriter(fram, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            fw.Write("fram"u8);
            foreach (var f in curFrames)
            {
                fw.Write("icon"u8); fw.Write(f.Length); fw.Write(f);
                if (f.Length % 2 == 1) fw.Write((byte)0);
            }
        }
        bw.Write("LIST"u8); bw.Write((int)fram.Length); bw.Write(fram.ToArray());
        bw.Flush();
        using var outMs = new MemoryStream();
        using var ow = new BinaryWriter(outMs);
        ow.Write("RIFF"u8); ow.Write((int)body.Length); ow.Write(body.ToArray());
        ow.Flush();
        return outMs.ToArray();
    }
}

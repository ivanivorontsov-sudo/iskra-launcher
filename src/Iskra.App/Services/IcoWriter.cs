using System;
using System.Collections.Generic;
using System.IO;

namespace Iskra.App.Services;

/// <summary>Пишет .ico из набора PNG-кадров (Vista+ допускает PNG внутри ICO).</summary>
internal static class IcoWriter
{
    public static void Write(string path, IReadOnlyList<(int size, byte[] png)> frames)
    {
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((ushort)0);            // reserved
        bw.Write((ushort)1);            // type = icon
        bw.Write((ushort)frames.Count);
        int offset = 6 + 16 * frames.Count;
        foreach (var (size, png) in frames)
        {
            bw.Write((byte)(size >= 256 ? 0 : size));  // width (0 = 256)
            bw.Write((byte)(size >= 256 ? 0 : size));  // height
            bw.Write((byte)0);          // palette
            bw.Write((byte)0);          // reserved
            bw.Write((ushort)1);        // planes
            bw.Write((ushort)32);       // bpp
            bw.Write(png.Length);       // bytes
            bw.Write(offset);           // offset
            offset += png.Length;
        }
        foreach (var (_, png) in frames) bw.Write(png);
    }
}

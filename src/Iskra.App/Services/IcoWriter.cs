using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Iskra.App.Services;

/// <summary>
/// Пишет .ico. Кадры до 48 px — классический BMP с альфой: рабочий стол и Проводник Windows 11
/// часто не читают ICO, внутри которого только PNG, и оставляют штатный значок.
/// 64+ остаются PNG (Vista+), чтобы не раздувать файл.
/// </summary>
internal static class IcoWriter
{
    public static void Write(string path, IReadOnlyList<(int size, byte[] png)> frames)
    {
        var images = new List<byte[]>(frames.Count);
        foreach (var (size, png) in frames)
            images.Add(size <= 48 ? PngToIconBmp(png, size) : png);

        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((ushort)0);
        bw.Write((ushort)1);
        bw.Write((ushort)frames.Count);
        int offset = 6 + 16 * frames.Count;
        for (int i = 0; i < frames.Count; i++)
        {
            int size = frames[i].size;
            var image = images[i];
            bool bmp = size <= 48;
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)(size >= 256 ? 0 : size));
            bw.Write((byte)0);
            bw.Write((byte)0);
            bw.Write((ushort)1);
            bw.Write((ushort)(bmp ? 32 : 32));
            bw.Write(image.Length);
            bw.Write(offset);
            offset += image.Length;
        }
        foreach (var image in images) bw.Write(image);
    }

    /// <summary>BITMAPINFOHEADER + XOR (снизу вверх, BGRA) + AND-маска прозрачности. Высота в заголовке удвоена.</summary>
    private static byte[] PngToIconBmp(byte[] png, int size)
    {
        var decoder = new PngBitmapDecoder(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames[0];
        var converted = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight;
        int stride = w * 4;
        var pixels = new byte[stride * h];
        converted.CopyPixels(pixels, stride, 0);

        int maskStride = ((w + 31) / 32) * 4;
        int xorSize = stride * h;
        int andSize = maskStride * h;
        var dib = new byte[40 + xorSize + andSize];
        void U32(int o, uint v) { dib[o] = (byte)v; dib[o + 1] = (byte)(v >> 8); dib[o + 2] = (byte)(v >> 16); dib[o + 3] = (byte)(v >> 24); }
        U32(0, 40);
        U32(4, (uint)w);
        U32(8, (uint)(h * 2));
        dib[12] = 1; dib[13] = 0; // planes
        dib[14] = 32; dib[15] = 0; // bpp
        int andOffset = 40 + xorSize;
        for (int y = 0; y < h; y++)
        {
            int sourceRow = (h - 1 - y) * stride;
            Buffer.BlockCopy(pixels, sourceRow, dib, 40 + y * stride, stride);

            // В классическом ICO бит AND = 1 означает «прозрачно».
            // Одного альфа-канала недостаточно для части путей отрисовки оболочки Windows:
            // без маски прозрачные области исходного PNG становятся чёрными прямоугольниками.
            int maskRow = andOffset + y * maskStride;
            for (int x = 0; x < w; x++)
            {
                if (pixels[sourceRow + x * 4 + 3] == 0)
                    dib[maskRow + x / 8] |= (byte)(0x80 >> (x & 7));
            }
        }
        return dib;
    }
}

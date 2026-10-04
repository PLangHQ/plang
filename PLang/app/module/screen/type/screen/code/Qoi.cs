using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace app.module.screen.type.screen.code;

/// <summary>
/// QOI, the "Quite OK Image" format: lossless, fast, simple to decode anywhere — how PlangOS's screen sends what
/// isn't video, and how the host reads it. The bytes are BGRA; QOI doesn't care which channel is which, so they
/// come out in the same order they went in.
/// </summary>
internal readonly ref struct Qoi(ReadOnlySpan<byte> pixels, int width, int height)
{
    private readonly ReadOnlySpan<byte> pixels = pixels;

    /// <summary>The most a picture this size can take: every pixel spelled out, plus header and end.</summary>
    internal static int MaxSize(int width, int height) => 14 + width * height * 5 + 8;

    /// <summary>Writes the picture into <paramref name="output"/>; returns how many bytes it took.
    /// Pixels are read as 32-bit words; a run of the same pixel — most of a web page — is found with
    /// one vectorized search, not pixel by pixel.</summary>
    internal int Into(Span<byte> output)
    {
        "qoif"u8.CopyTo(output);
        BinaryPrimitives.WriteUInt32BigEndian(output[4..], (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(output[8..], (uint)height);
        output[12] = 4;
        output[13] = 0;
        var o = 14;
        var all = MemoryMarshal.Cast<byte, uint>(pixels);
        Span<uint> index = stackalloc uint[64];
        index.Clear();
        var previous = 0xFF000000u;   // r 0, g 0, b 0, a 255 (byte 0 is r, byte 3 is a)
        byte pr = 0, pg = 0, pb = 0, pa = 255;
        for (var i = 0; i < all.Length; i++)
        {
            var pixel = all[i];
            if (pixel == previous)
            {
                // the run: as far as the same pixel goes, in pieces of up to 62 — a short one (text)
                // counted here, a long one (a background) with vectors
                var run = 1;
                while (run < 8 && i + run < all.Length && all[i + run] == previous) run++;
                if (run == 8)
                {
                    var other = all[(i + 8)..].IndexOfAnyExcept(previous);
                    run = other < 0 ? all.Length - i : 8 + other;
                }
                i += run - 1;
                for (; run > 62; run -= 62) output[o++] = 0xC0 | 61;
                output[o++] = (byte)(0xC0 | (run - 1));
                continue;
            }
            byte r = unchecked((byte)pixel), g = unchecked((byte)(pixel >> 8)), b = unchecked((byte)(pixel >> 16)), a = (byte)(pixel >> 24);
            var h = (r * 3 + g * 5 + b * 7 + a * 11) % 64;
            if (index[h] == pixel)
                output[o++] = (byte)h;
            else
            {
                index[h] = pixel;
                if (a == pa)
                {
                    int vr = unchecked((sbyte)(byte)(r - pr)), vg = unchecked((sbyte)(byte)(g - pg)), vb = unchecked((sbyte)(byte)(b - pb));
                    int vgr = vr - vg, vgb = vb - vg;
                    if (vr is > -3 and < 2 && vg is > -3 and < 2 && vb is > -3 and < 2)
                        output[o++] = (byte)(0x40 | ((vr + 2) << 4) | ((vg + 2) << 2) | (vb + 2));
                    else if (vgr is > -9 and < 8 && vg is > -33 and < 32 && vgb is > -9 and < 8)
                    {
                        output[o++] = (byte)(0x80 | (vg + 32));
                        output[o++] = (byte)(((vgr + 8) << 4) | (vgb + 8));
                    }
                    else { output[o++] = 0xFE; output[o++] = r; output[o++] = g; output[o++] = b; }
                }
                else { output[o++] = 0xFF; output[o++] = r; output[o++] = g; output[o++] = b; output[o++] = a; }
            }
            pr = r; pg = g; pb = b; pa = a;
            previous = pixel;
        }
        for (var i = 0; i < 7; i++) output[o++] = 0;
        output[o++] = 1;
        return o;
    }

    /// <summary>A QOI picture's pixels into <paramref name="output"/> (BGRA, as they went in); false when
    /// <paramref name="qoi"/> isn't one or holds fewer pixels than <paramref name="output"/> needs.</summary>
    internal static bool Decode(ReadOnlySpan<byte> qoi, Span<byte> output)
    {
        if (qoi.Length < 22 || !qoi[..4].SequenceEqual("qoif"u8)) return false;
        Span<byte> index = stackalloc byte[64 * 4];
        index.Clear();
        byte c0 = 0, c1 = 0, c2 = 0, c3 = 255;
        int p = 14, run = 0;
        var end = qoi.Length - 8;
        for (var o = 0; o < output.Length; o += 4)
        {
            if (run > 0) run--;
            else if (p < end)
            {
                int b = qoi[p++];
                if (b == 0xFE) { c0 = qoi[p++]; c1 = qoi[p++]; c2 = qoi[p++]; }
                else if (b == 0xFF) { c0 = qoi[p++]; c1 = qoi[p++]; c2 = qoi[p++]; c3 = qoi[p++]; }
                else switch (b >> 6)
                {
                    case 0:
                        var i = (b & 63) * 4;
                        c0 = index[i]; c1 = index[i + 1]; c2 = index[i + 2]; c3 = index[i + 3];
                        break;
                    case 1:
                        c0 = unchecked((byte)(c0 + ((b >> 4) & 3) - 2));
                        c1 = unchecked((byte)(c1 + ((b >> 2) & 3) - 2));
                        c2 = unchecked((byte)(c2 + (b & 3) - 2));
                        break;
                    case 2:
                        int b2 = qoi[p++], vg = (b & 63) - 32;
                        c0 = unchecked((byte)(c0 + vg - 8 + ((b2 >> 4) & 15)));
                        c1 = unchecked((byte)(c1 + vg));
                        c2 = unchecked((byte)(c2 + vg - 8 + (b2 & 15)));
                        break;
                    case 3:
                        run = b & 63;
                        break;
                }
                var h = (c0 * 3 + c1 * 5 + c2 * 7 + c3 * 11) % 64 * 4;
                index[h] = c0; index[h + 1] = c1; index[h + 2] = c2; index[h + 3] = c3;
            }
            else return false;
            output[o] = c0; output[o + 1] = c1; output[o + 2] = c2; output[o + 3] = c3;
        }
        return true;
    }
}

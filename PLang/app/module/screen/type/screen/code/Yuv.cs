namespace app.module.screen.type.screen.code;

/// <summary>
/// A decoded picture: 8-bit Y, U and V planes, 4:2:0 (the chroma planes half the size each way), each with its stride.
/// It draws itself as BGRA at any size (nearest sample: a test host's scaling, not a player's), BT.709 video range —
/// what the Windows host's Media Foundation gives.
/// </summary>
internal sealed record Yuv(byte[] Y, byte[] U, byte[] V, int YStride, int CStride, int Width, int Height)
{
    /// <summary>The picture as <paramref name="width"/> × <paramref name="height"/> BGRA pixels into <paramref name="bgra"/>.</summary>
    internal void Into(Span<byte> bgra, int width, int height)
    {
        if (width <= 0 || height <= 0) return;
        var columns = new int[width];
        for (var x = 0; x < width; x++) columns[x] = (int)((long)x * Width / width);
        for (var y = 0; y < height; y++)
        {
            var sy = (int)((long)y * Height / height);
            int yr = sy * YStride, cr = sy / 2 * CStride;
            var o = y * width * 4;
            for (var x = 0; x < width; x++, o += 4)
            {
                var sx = columns[x];
                int c = 298 * (Y[yr + sx] - 16), u = U[cr + (sx >> 1)] - 128, v = V[cr + (sx >> 1)] - 128;
                bgra[o] = Clamp((c + 541 * u + 128) >> 8);
                bgra[o + 1] = Clamp((c - 55 * u - 136 * v + 128) >> 8);
                bgra[o + 2] = Clamp((c + 459 * v + 128) >> 8);
                bgra[o + 3] = 255;
            }
        }
    }

    private static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
}

/// <summary>A video decoder: coded pictures in, in decode order; the pictures they give out, in show order.</summary>
internal interface IDecoder : IDisposable
{
    /// <summary>One coded picture, which shows at <paramref name="time"/>; the decoded pictures ready now, each with
    /// the time of its own sample (a decoder working on several frames at once gives them later than their samples,
    /// sometimes more than one, and none for a sample it can't decode).</summary>
    List<(double time, Yuv picture)> Decode(ReadOnlyMemory<byte> sample, double time);
}

/// <summary>A host's decoding: the codecs it plays itself (what it tells PlangOS), and a fresh decoder for one of them
/// — dav1d and openh264 on the Linux host, Media Foundation on Windows.</summary>
internal interface IDecoding
{
    /// <summary>The codecs here (<c>av01</c>, <c>avc1</c>), none when this host plays no video itself.</summary>
    string[] Codecs { get; }

    /// <summary>A decoder for <paramref name="codec"/> with the stream's <paramref name="config"/> (av1C, avcC) and
    /// size, or null when this host has none for it (or it wouldn't start).</summary>
    IDecoder? Make(string codec, byte[] config, int width, int height);
}

namespace app.module.screen.type.screen.code;

/// <summary>
/// A decoded picture, 8 bits, 4:2:0 (the colour half the size each way), in one buffer: the lumas (<see cref="YStride"/>
/// a row), then the colour — as two planes, U then V (dav1d, openh264), or U and V side by side in one (NV12: Windows'
/// decoders). It draws itself as BGRA at any size (nearest sample), BT.709 video range — the numbers browsers stream
/// with. Its buffer is borrowed (<see cref="Planes"/>, <see cref="Nv12"/>) and given back when the picture is done with (<see cref="Dispose"/>):
/// sixty pictures a second, new memory each, kept the garbage collector busy — a 1080p video cost Windows' plang cores and
/// spikes to ~1 GB.
/// </summary>
internal sealed class Yuv : IDisposable
{
    private byte[]? data;
    private readonly Lender? lender;

    /// <summary>The buffer: lumas from 0, colour from <see cref="UAt"/> (and <see cref="VAt"/> unless interleaved).</summary>
    internal byte[] Data => data ?? throw new ObjectDisposedException(nameof(Yuv));
    internal int YStride { get; }
    internal int CStride { get; }
    internal int UAt { get; }
    internal int VAt { get; }
    internal bool Interleaved { get; }
    internal int Width { get; }
    internal int Height { get; }

    private Yuv(Lender? lender, byte[] data, int yStride, int cStride, int uAt, int vAt, bool interleaved, int width, int height)
        => (this.lender, this.data, YStride, CStride, UAt, VAt, Interleaved, Width, Height) = (lender, data, yStride, cStride, uAt, vAt, interleaved, width, height);

    /// <summary>A picture in planes: <paramref name="yStride"/> × <paramref name="height"/> lumas, then U and V of
    /// <paramref name="cStride"/> × half the rows each — its buffer from <paramref name="lender"/> (new without one), for
    /// the decoder to fill.</summary>
    internal static Yuv Planes(Lender? lender, int yStride, int cStride, int width, int height)
    {
        int y = yStride * height, c = cStride * ((height + 1) / 2);
        return new Yuv(lender, lender?.Lend(y + 2 * c) ?? new byte[y + 2 * c], yStride, cStride, y, y + c, false, width, height);
    }

    /// <summary>A picture as NV12: <paramref name="stride"/> × <paramref name="height"/> lumas, then half the rows of U and V
    /// side by side — its buffer from <paramref name="lender"/> (new without one), for the decoder to fill.</summary>
    internal static Yuv Nv12(Lender? lender, int stride, int width, int height)
    {
        int y = stride * height, c = stride * ((height + 1) / 2);
        return new Yuv(lender, lender?.Lend(y + c) ?? new byte[y + c], stride, stride, y, y + 1, true, width, height);
    }

    /// <summary>The picture as <paramref name="width"/> × <paramref name="height"/> BGRA pixels into <paramref name="bgra"/>:
    /// rows in bands on half the cores (the other half are PlangOS's: its VM runs on the same machine).</summary>
    internal void Into(byte[] bgra, int width, int height) => Into(bgra, width, height, Width, Height);

    /// <summary>The picture's top left <paramref name="fromWidth"/> × <paramref name="fromHeight"/> (the whole of it, or
    /// less: an encoder pads to whole blocks) as <paramref name="width"/> × <paramref name="height"/> BGRA pixels.</summary>
    internal void Into(byte[] bgra, int width, int height, int fromWidth, int fromHeight)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4) return;
        // where each column's luma and colour are, once (the colour's: a byte apart in NV12, two)
        var lumas = new int[width];
        var colours = new int[width];
        for (var x = 0; x < width; x++)
        {
            var sx = (int)((long)x * fromWidth / width);
            lumas[x] = sx;
            colours[x] = Interleaved ? (sx >> 1) * 2 : sx >> 1;
        }
        var bands = Math.Clamp(Environment.ProcessorCount / 2, 1, Math.Max(1, height / 32));
        Parallel.For(0, bands, band =>
        {
            for (var y = height * band / bands; y < height * (band + 1) / bands; y++) Row(bgra, y, width, height, fromHeight, lumas, colours);
        });
    }

    // one row: each pixel's luma and colour looked up (BT.709 video range, the products tabled once), clamped by a
    // table, and written as one 32-bit BGRA value — no multiplying, no branching per pixel
    private void Row(byte[] bgra, int y, int width, int height, int fromHeight, int[] lumas, int[] colours)
    {
        var d = Data;
        var sy = (int)((long)y * fromHeight / height);
        int yr = sy * YStride, ur = UAt + sy / 2 * CStride, vr = VAt + sy / 2 * CStride;
        var row = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(bgra.AsSpan(y * width * 4, width * 4));
        var t = Tables.Of;
        for (var x = 0; x < width; x++)
        {
            var cx = colours[x];
            int c = t.Luma[d[yr + lumas[x]]], u = d[ur + cx], v = d[vr + cx];
            row[x] = t.Clamp[(c + t.Bu[u]) >> 8] | (uint)t.Clamp[(c + t.Guv[u] + t.Gv[v]) >> 8] << 8
                     | (uint)t.Clamp[(c + t.Rv[v]) >> 8] << 16 | 0xFF000000u;
        }
    }

    // the conversion's products, once: 298 · (Y − 16) and the colour's terms, with the rounding; and a 1 KB clamp table
    // (the luma's term carries an offset, so no sum is negative: a sum below 0 lands in the table's zeros)
    private sealed class Tables
    {
        internal static readonly Tables Of = new();
        internal readonly int[] Luma = new int[256], Bu = new int[256], Guv = new int[256], Gv = new int[256], Rv = new int[256];
        internal readonly byte[] Clamp = new byte[1024];
        private const int Low = 320;   // in whole steps: the clamp table's offset

        private Tables()
        {
            for (var i = 0; i < 256; i++)
            {
                Luma[i] = 298 * (i - 16) + 128 + (Low << 8);
                Bu[i] = 541 * (i - 128);
                Guv[i] = -55 * (i - 128);
                Gv[i] = -136 * (i - 128);
                Rv[i] = 459 * (i - 128);
            }
            for (var s = 0; s < Clamp.Length; s++) Clamp[s] = (byte)Math.Clamp(s - Low, 0, 255);
        }
    }

    /// <summary>Its buffer given back (to be lent again).</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref data, null) is { } d) lender?.Back(d);
    }
}

/// <summary>
/// A decoder's picture buffers, lent and given back: a few of the stream's size kept (as many as pictures wait between
/// the decoder and the screen), the same ones over and over — no new memory per picture. A buffer of another size (the
/// stream changed size) is let go.
/// </summary>
internal sealed class Lender
{
    private readonly System.Collections.Concurrent.ConcurrentBag<byte[]> kept = new();
    private const int Keeps = 12;

    /// <summary>A buffer of exactly <paramref name="length"/> bytes: one given back, or new.</summary>
    internal byte[] Lend(int length)
    {
        while (kept.TryTake(out var b))
            if (b.Length == length) return b;   // one of another size: let go
        return new byte[length];
    }

    /// <summary>A buffer back, kept for the next picture (up to a few).</summary>
    internal void Back(byte[] buffer)
    {
        if (kept.Count < Keeps) kept.Add(buffer);
    }
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
    /// size, or null when this host has none for it; one that won't start throws why.</summary>
    IDecoder? Make(string codec, byte[] config, int width, int height);
}

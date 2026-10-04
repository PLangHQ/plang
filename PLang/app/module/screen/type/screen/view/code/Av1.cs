using System.Runtime.InteropServices;

namespace app.module.screen.type.screen.view.code;

/// <summary>
/// AV1 decoded with dav1d (on a Linux host; the library Chromium uses). Samples are MP4's AV1 samples — OBUs in the
/// low-overhead format, which dav1d reads as they are. 8-bit 4:2:0 only (what browsers stream); anything else gives
/// no picture.
/// </summary>
internal sealed class Av1 : IDecoder
{
    private const string Library = "libdav1d.so.7";
    private static readonly Lazy<bool> here = new(() => NativeLibrary.TryLoad(Library, out _));

    /// <summary>dav1d is here (the library loads).</summary>
    internal static bool Here => here.Value;

    [DllImport(Library)] private static extern void dav1d_default_settings(IntPtr settings);
    [DllImport(Library)] private static extern int dav1d_open(out IntPtr context, IntPtr settings);
    [DllImport(Library)] private static extern void dav1d_close(ref IntPtr context);
    [DllImport(Library)] private static extern IntPtr dav1d_data_create(IntPtr data, nuint size);
    [DllImport(Library)] private static extern void dav1d_data_unref(IntPtr data);
    [DllImport(Library)] private static extern int dav1d_send_data(IntPtr context, IntPtr data);
    [DllImport(Library)] private static extern int dav1d_get_picture(IntPtr context, IntPtr picture);
    [DllImport(Library)] private static extern void dav1d_picture_unref(IntPtr picture);

    private const int EAGAIN = -11;
    // Dav1dData (72 bytes in dav1d 1.x) and Dav1dPicture (~260): room to spare, zeroed before each use
    private readonly IntPtr data = Marshal.AllocHGlobal(256), picture = Marshal.AllocHGlobal(1024);
    private IntPtr context;

    // the stream's sequence header (av1C's configOBUs), given before the first sample: a decoder started at a key
    // frame later in the stream (a seek) has no other — key frames needn't repeat it
    private byte[]? sequence;

    internal Av1(byte[]? av1C = null)
    {
        // av1C: 4 bytes (marker, version, profile, level…) then the configOBUs
        if (av1C is { Length: > 4 }) sequence = av1C[4..];
        if (!Here) throw new InvalidOperationException($"{Library} isn't here: no AV1 on this host");
        var settings = Marshal.AllocHGlobal(1024);
        try
        {
            Marshal.Copy(new byte[1024], 0, settings, 1024);
            dav1d_default_settings(settings);
            // Dav1dSettings { int n_threads; … }: a few threads, frames decoded side by side (pictures come out some
            // frames after their samples; the presenter decodes that far ahead)
            Marshal.WriteInt32(settings, 0, Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
            if (dav1d_open(out context, settings) != 0) throw new InvalidOperationException("dav1d wouldn't start a decoder");
        }
        finally { Marshal.FreeHGlobal(settings); }
    }

    public List<(double time, Yuv picture)> Decode(ReadOnlyMemory<byte> sample, double time)
    {
        var got = new List<(double, Yuv)>();
        if (sequence != null)
        {
            sample = (byte[])[.. sequence, .. sample.Span];
            sequence = null;
        }
        Marshal.Copy(new byte[256], 0, data, 256);
        var bytes = dav1d_data_create(data, (nuint)sample.Length);
        if (bytes == IntPtr.Zero) return got;
        Marshal.Copy(sample.ToArray(), 0, bytes, sample.Length);
        // Dav1dData { data, sz, ref, m { timestamp, … } }: the sample's time, in microseconds, comes back on its picture
        Marshal.WriteInt64(data, 24, (long)Math.Round(time * 1e6));
        // the decoder takes the data, maybe in parts: what is ready comes out, and the rest goes in again (sz != 0)
        for (var tries = 0; tries < 64; tries++)
        {
            var sent = dav1d_send_data(context, data);
            while (Picture() is var (ok, p) && ok) { if (p is { } shown) got.Add(shown); }
            if (sent != EAGAIN || Marshal.ReadIntPtr(data, 8) == IntPtr.Zero) break;
        }
        if (Marshal.ReadIntPtr(data, 8) != IntPtr.Zero) dav1d_data_unref(data);
        return got;
    }

    // a decoded picture, if one is ready (ok), and it with its sample's time when it's one this draws (8-bit 4:2:0):
    // Dav1dPicture { seq_hdr, frame_hdr, data[3], stride[2], p { w, h, layout, bpc }, m { timestamp, … } }
    private (bool ok, (double time, Yuv yuv)? picture) Picture()
    {
        Marshal.Copy(new byte[1024], 0, picture, 1024);
        if (dav1d_get_picture(context, picture) != 0) return (false, null);
        try
        {
            int w = Marshal.ReadInt32(picture, 56), h = Marshal.ReadInt32(picture, 60);
            int layout = Marshal.ReadInt32(picture, 64), bpc = Marshal.ReadInt32(picture, 68);
            var time = Marshal.ReadInt64(picture, 72) / 1e6;
            if (layout != 1 || bpc != 8) return (true, null);   // DAV1D_PIXEL_LAYOUT_I420, 8 bits
            var yStride = (int)Marshal.ReadInt64(picture, 40);
            var cStride = (int)Marshal.ReadInt64(picture, 48);
            var Y = new byte[yStride * h];
            var U = new byte[cStride * ((h + 1) / 2)];
            var V = new byte[U.Length];
            Marshal.Copy(Marshal.ReadIntPtr(picture, 16), Y, 0, Y.Length);
            Marshal.Copy(Marshal.ReadIntPtr(picture, 24), U, 0, U.Length);
            Marshal.Copy(Marshal.ReadIntPtr(picture, 32), V, 0, V.Length);
            return (true, (time, new Yuv(Y, U, V, yStride, cStride, w, h)));
        }
        finally { dav1d_picture_unref(picture); }
    }

    public void Dispose()
    {
        if (context != IntPtr.Zero) dav1d_close(ref context);
        Marshal.FreeHGlobal(data);
        Marshal.FreeHGlobal(picture);
    }
}

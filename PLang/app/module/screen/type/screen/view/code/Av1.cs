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

    internal Av1()
    {
        if (!Here) throw new InvalidOperationException($"{Library} isn't here: no AV1 on this host");
        var settings = Marshal.AllocHGlobal(1024);
        try
        {
            Marshal.Copy(new byte[1024], 0, settings, 1024);
            dav1d_default_settings(settings);
            if (dav1d_open(out context, settings) != 0) throw new InvalidOperationException("dav1d wouldn't start a decoder");
        }
        finally { Marshal.FreeHGlobal(settings); }
    }

    public Yuv? Decode(ReadOnlyMemory<byte> sample)
    {
        Marshal.Copy(new byte[256], 0, data, 256);
        var bytes = dav1d_data_create(data, (nuint)sample.Length);
        if (bytes == IntPtr.Zero) return null;
        Marshal.Copy(sample.ToArray(), 0, bytes, sample.Length);
        // the decoder takes the data (it may take it in parts: send again after a picture is out)
        var sent = dav1d_send_data(context, data);
        var got = Picture();
        if (sent == EAGAIN)
        {
            dav1d_send_data(context, data);
            got = Picture() ?? got;
        }
        if (Marshal.ReadIntPtr(data, 8) != IntPtr.Zero) dav1d_data_unref(data);   // what it didn't take (sz != 0)
        return got;
    }

    // a decoded picture, if one is ready: Dav1dPicture { seq_hdr, frame_hdr, data[3], stride[2], p { w, h, layout, bpc } }
    private Yuv? Picture()
    {
        Marshal.Copy(new byte[1024], 0, picture, 1024);
        if (dav1d_get_picture(context, picture) != 0) return null;
        try
        {
            int w = Marshal.ReadInt32(picture, 56), h = Marshal.ReadInt32(picture, 60);
            int layout = Marshal.ReadInt32(picture, 64), bpc = Marshal.ReadInt32(picture, 68);
            if (layout != 1 || bpc != 8) return null;   // DAV1D_PIXEL_LAYOUT_I420, 8 bits
            var yStride = (int)Marshal.ReadInt64(picture, 40);
            var cStride = (int)Marshal.ReadInt64(picture, 48);
            var Y = new byte[yStride * h];
            var U = new byte[cStride * ((h + 1) / 2)];
            var V = new byte[U.Length];
            Marshal.Copy(Marshal.ReadIntPtr(picture, 16), Y, 0, Y.Length);
            Marshal.Copy(Marshal.ReadIntPtr(picture, 24), U, 0, U.Length);
            Marshal.Copy(Marshal.ReadIntPtr(picture, 32), V, 0, V.Length);
            return new Yuv(Y, U, V, yStride, cStride, w, h);
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

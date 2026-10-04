using System.Runtime.InteropServices;
using app.module.screen.type.screen.code;

namespace app.module.screen.type.screen.view.code;

/// <summary>
/// A video stream PlangOS sends (H.264, baseline, BT.709 video range), decoded on a Linux host with openh264 — the
/// library PlangOS encodes it with. A new stream number is a new stream, decoded anew. openh264's C++ interface is a
/// table of functions; they are read from it, not declared.
/// </summary>
internal sealed class Video : IDisposable
{
    private const string Library = "libopenh264.so.8";
    private static readonly Lazy<IntPtr> library = new(() => NativeLibrary.TryLoad(Library, out var h) ? h : IntPtr.Zero);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long Create(out IntPtr decoder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Destroy(IntPtr decoder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate long Initialize(IntPtr self, IntPtr param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int DecodeFrameNoDelay(IntPtr self, IntPtr source, int length, IntPtr[] planes, IntPtr info);

    private readonly IntPtr decoder;
    private readonly DecodeFrameNoDelay decode;
    private readonly IntPtr info = Marshal.AllocHGlobal(Info);
    private const int Info = 72;   // SBufferInfo

    /// <summary>The stream's number.</summary>
    internal uint Id { get; }

    /// <summary>openh264 is here (the library loads).</summary>
    internal static bool Here => library.Value != IntPtr.Zero;

    internal Video(uint id)
    {
        Id = id;
        if (!Here) throw new InvalidOperationException($"{Library} isn't here: no video on this host");
        Marshal.GetDelegateForFunctionPointer<Create>(NativeLibrary.GetExport(library.Value, "WelsCreateDecoder"))(out decoder);
        // SDecodingParam: uiTargetDqLayer 255, sVideoProperty { size 8, VIDEO_BITSTREAM_AVC }
        var param = Marshal.AllocHGlobal(32);
        try
        {
            Marshal.Copy(new byte[32], 0, param, 32);
            Marshal.WriteInt32(param, 12, 255);
            Marshal.WriteInt32(param, 24, 8);
            Marshal.WriteInt32(param, 28, 0);
            if (Slot<Initialize>(0)(decoder, param) != 0) throw new InvalidOperationException("openh264 wouldn't start a decoder");
        }
        finally { Marshal.FreeHGlobal(param); }
        decode = Slot<DecodeFrameNoDelay>(3);
    }

    private T Slot<T>(int slot) where T : Delegate
        => Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(decoder), slot * IntPtr.Size));

    /// <summary>The stream's next picture as <paramref name="width"/> × <paramref name="height"/> BGRA pixels, or null
    /// when the bytes gave none yet.</summary>
    internal byte[]? Next(ReadOnlyMemory<byte> h264, int width, int height)
    {
        if (Decode(h264) is not { } picture || picture.Width < width || picture.Height < height) return null;
        // the screen's part is the picture's top left (an encoder pads to whole blocks): drawn 1:1
        var bgra = new byte[width * height * 4];
        (picture with { Width = width, Height = height }).Into(bgra, width, height);
        return bgra;
    }

    /// <summary>One access unit (Annex B: start codes); the picture it gives now, or null.</summary>
    internal Yuv? Decode(ReadOnlyMemory<byte> h264)
    {
        Marshal.Copy(new byte[Info], 0, info, Info);
        var planes = new IntPtr[3];
        if (!MemoryMarshal.TryGetArray(h264, out var bytes)) bytes = new ArraySegment<byte>(h264.ToArray());
        var pinned = GCHandle.Alloc(bytes.Array!, GCHandleType.Pinned);
        try { decode(decoder, pinned.AddrOfPinnedObject() + bytes.Offset, bytes.Count, planes, info); }
        finally { pinned.Free(); }
        if (Marshal.ReadInt32(info, 0) != 1) return null;   // iBufferStatus: a picture is ready
        int w = Marshal.ReadInt32(info, 24), h = Marshal.ReadInt32(info, 28);
        int yStride = Marshal.ReadInt32(info, 36), cStride = Marshal.ReadInt32(info, 40);
        var Y = new byte[yStride * h];
        var U = new byte[cStride * ((h + 1) / 2)];
        var V = new byte[U.Length];
        Marshal.Copy(planes[0], Y, 0, Y.Length);
        Marshal.Copy(planes[1], U, 0, U.Length);
        Marshal.Copy(planes[2], V, 0, V.Length);
        return new Yuv(Y, U, V, yStride, cStride, w, h);
    }

    public void Dispose()
    {
        Marshal.GetDelegateForFunctionPointer<Destroy>(NativeLibrary.GetExport(library.Value, "WelsDestroyDecoder"))(decoder);
        Marshal.FreeHGlobal(info);
    }
}

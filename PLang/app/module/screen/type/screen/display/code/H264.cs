using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace app.module.screen.type.screen.display.code;

/// <summary>
/// An H.264 stream of one part of the screen — a video playing — through openh264 (in the image:
/// Chromium needs it too). Pictures go in as BGRA, the stream comes out as Annex-B bytes: the first
/// picture a key frame, the rest what changed since. Quality over size: the video is already
/// compressed once, so it is sent at a quality that adds no loss you can see (QP 14–24,
/// ~0.1 bit per pixel), the whole picture every time, no frame skipped.
/// openh264's C++ interface is a table of functions; they are read from it, not declared.
/// </summary>
internal sealed class H264 : IDisposable
{
    private const string Library = "libopenh264.so.8";
    private static readonly Lazy<IntPtr> library = new(() => NativeLibrary.TryLoad(Library, out var h) ? h : IntPtr.Zero);

    // SEncParamExt (openh264 2.6, codec_app_def.h), offsets from the header
    private const int ParamSize = 924, Layer = 32;
    private const int PictureSize = 80, InfoSize = 7192, LayerInfoSize = 56;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Create(out IntPtr encoder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Destroy(IntPtr encoder);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int WithParams(IntPtr self, IntPtr param);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Encode(IntPtr self, IntPtr picture, IntPtr info);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Option(IntPtr self, int option, ref int value);

    private readonly IntPtr encoder;
    private readonly Encode encode;
    private readonly IntPtr picture = Marshal.AllocHGlobal(PictureSize);
    private readonly IntPtr info = Marshal.AllocHGlobal(InfoSize);
    private readonly byte[] yuv;
    private readonly GCHandle pinned;
    private long frames;

    internal int Width { get; }
    internal int Height { get; }
    /// <summary>Which stream this is: the host starts a decoder when it sees a new one.</summary>
    internal uint Id { get; }

    private static uint streams;

    /// <summary>Whether H.264 can be made here (openh264 is in the image).</summary>
    internal static bool Here => library.Value != IntPtr.Zero;

    /// <summary>A stream of <paramref name="width"/> × <paramref name="height"/> pictures (both even).</summary>
    internal H264(int width, int height, int threads)
    {
        Width = width; Height = height;
        Id = ++streams;
        var create = Function<Create>(NativeLibrary.GetExport(library.Value, "WelsCreateSVCEncoder"));
        if (create(out encoder) != 0 || encoder == IntPtr.Zero) throw new InvalidOperationException("openh264: no encoder");
        encode = Method<Encode>(4);

        var param = Marshal.AllocHGlobal(ParamSize);
        try
        {
            Method<WithParams>(2)(encoder, param);   // GetDefaultParams, then what differs
            var fps = 60f;
            var bitrate = (int)Math.Min(int.MaxValue, (long)width * height * 60 / 8);   // ~0.125 bit per pixel
            Marshal.WriteInt32(param, 0, 0);            // iUsageType: CAMERA_VIDEO_REAL_TIME
            Marshal.WriteInt32(param, 4, width);
            Marshal.WriteInt32(param, 8, height);
            Marshal.WriteInt32(param, 12, bitrate);     // iTargetBitrate
            Marshal.WriteInt32(param, 16, 0);           // iRCMode: RC_QUALITY_MODE
            WriteFloat(param, 20, fps);                 // fMaxFrameRate
            Marshal.WriteInt32(param, 24, 1);           // iTemporalLayerNum
            Marshal.WriteInt32(param, 28, 1);           // iSpatialLayerNum
            Marshal.WriteInt32(param, 836, 0);          // uiIntraPeriod: a key frame only at the start
            Marshal.WriteByte(param, 860, 0);           // bEnableFrameSkip: every picture arrives
            Marshal.WriteInt32(param, 864, bitrate * 2);// iMaxBitrate
            Marshal.WriteInt32(param, 868, 24);         // iMaxQp
            Marshal.WriteInt32(param, 872, 14);         // iMinQp
            Marshal.WriteInt16(param, 892, (short)threads);   // iMultipleThreadIdc
            Marshal.WriteByte(param, 908, 0);           // bEnableDenoise
            // the cheapest search, no picture analysis: the CPU is shared with Chromium decoding the
            // video, and at this bitrate what they would save isn't seen
            Marshal.WriteInt32(param, 832, 0);          // iComplexityMode: LOW_COMPLEXITY
            Marshal.WriteByte(param, 909, 0);           // bEnableBackgroundDetection
            Marshal.WriteByte(param, 910, 0);           // bEnableAdaptiveQuant
            // the one layer
            var layer = param + Layer;
            Marshal.WriteInt32(layer, 0, width);
            Marshal.WriteInt32(layer, 4, height);
            WriteFloat(layer, 8, fps);
            Marshal.WriteInt32(layer, 12, bitrate);     // iSpatialBitrate
            Marshal.WriteInt32(layer, 16, bitrate * 2); // iMaxSpatialBitrate
            Marshal.WriteInt32(layer, 20, 66);          // uiProfileIdc: baseline, which every decoder takes
            Marshal.WriteInt32(layer, 32, 1);           // sSliceArgument.uiSliceMode: SM_FIXEDSLCNUM_SLICE
            Marshal.WriteInt32(layer, 36, threads);     // uiSliceNum: one slice per thread
            // the colours: BT.709, video range — what the host converts back with
            Marshal.WriteByte(layer, 184, 1);           // bVideoSignalTypePresent
            Marshal.WriteByte(layer, 185, 5);           // uiVideoFormat: unspecified
            Marshal.WriteByte(layer, 186, 0);           // bFullRange
            Marshal.WriteByte(layer, 187, 1);           // bColorDescriptionPresent
            Marshal.WriteByte(layer, 188, 1);           // uiColorPrimaries: BT.709
            Marshal.WriteByte(layer, 189, 1);           // uiTransferCharacteristics: BT.709
            Marshal.WriteByte(layer, 190, 1);           // uiColorMatrix: BT.709
            var quiet = 0;
            Method<Option>(7)(encoder, 25, ref quiet);  // ENCODER_OPTION_TRACE_LEVEL: WELS_LOG_QUIET
            if (Method<WithParams>(1)(encoder, param) != 0)   // InitializeExt
                throw new InvalidOperationException($"openh264: {width}x{height} refused");
        }
        finally { Marshal.FreeHGlobal(param); }

        // I420: Y, then U and V at a quarter each
        yuv = GC.AllocateUninitializedArray<byte>(width * height * 3 / 2, pinned: true);
        pinned = GCHandle.Alloc(yuv, GCHandleType.Pinned);
        var at = pinned.AddrOfPinnedObject();
        Marshal.Copy(new byte[PictureSize], 0, picture, PictureSize);
        Marshal.WriteInt32(picture, 0, 23);                  // iColorFormat: videoFormatI420
        Marshal.WriteInt32(picture, 4, width);               // iStride[0..2]
        Marshal.WriteInt32(picture, 8, width / 2);
        Marshal.WriteInt32(picture, 12, width / 2);
        Marshal.WriteIntPtr(picture, 24, at);                // pData[0..2]
        Marshal.WriteIntPtr(picture, 32, at + width * height);
        Marshal.WriteIntPtr(picture, 40, at + width * height * 5 / 4);
        Marshal.WriteInt32(picture, 56, width);
        Marshal.WriteInt32(picture, 60, height);
    }

    /// <summary>The next picture (BGRA rows of <paramref name="stride"/> bytes) into the stream; its
    /// bytes, written to <paramref name="into"/> (grown if short). Returns how many.</summary>
    internal int Next(byte[] bgra, int stride, ref byte[] into)
    {
        Yuv.From(bgra, stride, Width, Height, yuv);
        Marshal.WriteInt64(picture, 64, frames++ * 1000 / 60);   // uiTimeStamp, ms
        if (encode(encoder, picture, info) != 0) return 0;
        var total = 0;
        var layers = Marshal.ReadInt32(info, 0);
        for (var i = 0; i < layers; i++)
        {
            var layer = info + 8 + i * LayerInfoSize;
            var nals = Marshal.ReadInt32(layer, 16);
            var lengths = Marshal.ReadIntPtr(layer, 24);
            var size = 0;
            for (var n = 0; n < nals; n++) size += Marshal.ReadInt32(lengths, n * 4);
            if (total + size > into.Length) Array.Resize(ref into, Math.Max(into.Length * 2, total + size));
            Marshal.Copy(Marshal.ReadIntPtr(layer, 32), into, total, size);
            total += size;
        }
        return total;
    }

    public void Dispose()
    {
        if (encoder != IntPtr.Zero)
        {
            Method<WithParams>(3)(encoder, IntPtr.Zero);   // Uninitialize
            Function<Destroy>(NativeLibrary.GetExport(library.Value, "WelsDestroySVCEncoder"))(encoder);
        }
        if (pinned.IsAllocated) pinned.Free();
        Marshal.FreeHGlobal(picture);
        Marshal.FreeHGlobal(info);
    }

    private T Method<T>(int slot) where T : Delegate =>
        Function<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(encoder), slot * IntPtr.Size));

    private static T Function<T>(IntPtr at) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(at);

    private static void WriteFloat(IntPtr at, int offset, float value) =>
        Marshal.WriteInt32(at, offset, BitConverter.SingleToInt32Bits(value));
}

/// <summary>BGRA to I420, BT.709 video range — the host converts back with the same numbers.</summary>
internal static class Yuv
{
    private const int Step = 32;   // pixels per vector step: 4 × 8 in, 32 luma bytes out
    /// <summary>BGRA rows into I420 (<paramref name="width"/> and <paramref name="height"/> even):
    /// a luma per pixel, the colour per 2×2 block — rows in parallel bands.</summary>
    internal static void From(byte[] bgra, int stride, int width, int height, byte[] yuv)
    {
        var pairs = height / 2;
        var bands = Math.Min(Environment.ProcessorCount, Math.Max(1, pairs / 16));
        Parallel.For(0, bands, band =>
            Rows(bgra, stride, width, height, yuv, pairs * band / bands * 2, pairs * (band + 1) / bands * 2));
    }

    private static void Rows(byte[] bgra, int stride, int width, int height, byte[] yuv, int from, int to)
    {
        var y0 = 0; var u0 = width * height; var v0 = u0 + width * height / 4;
        var pixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(bgra.AsSpan());
        var words = stride / 4;
        for (var y = from; y < to; y += 2)
        {
            var top = pixels.Slice(y * words, width);
            var bottom = pixels.Slice((y + 1) * words, width);
            var yTop = yuv.AsSpan(y0 + y * width, width);
            var yBottom = yuv.AsSpan(y0 + (y + 1) * width, width);
            var u = yuv.AsSpan(u0 + y / 2 * (width / 2), width / 2);
            var v = yuv.AsSpan(v0 + y / 2 * (width / 2), width / 2);
            var x = 0;
            if (Vector256.IsHardwareAccelerated)
                for (; x + Step <= width; x += Step)
                    Block(top.Slice(x, Step), bottom.Slice(x, Step), yTop.Slice(x, Step), yBottom.Slice(x, Step), u.Slice(x / 2, Step / 2), v.Slice(x / 2, Step / 2));
            for (; x < width; x += 2)
            {
                uint a = top[x], b = top[x + 1], c = bottom[x], d = bottom[x + 1];
                yTop[x] = Luma(a); yTop[x + 1] = Luma(b);
                yBottom[x] = Luma(c); yBottom[x + 1] = Luma(d);
                // the block's colour: the sum of four, kept at 4× until the end
                int r = R(a) + R(b) + R(c) + R(d), g = G(a) + G(b) + G(c) + G(d), bl = B(a) + B(b) + B(c) + B(d);
                u[x / 2] = (byte)(((-26 * r - 87 * g + 112 * bl + 512) >> 10) + 128);
                v[x / 2] = (byte)(((112 * r - 102 * g - 10 * bl + 512) >> 10) + 128);
            }
        }
    }

    /// <summary>32 pixels of two rows at once, 8 to a vector: their lumas, and the colour of their
    /// 16 blocks. A block's colour is summed packed — blue and red side by side in one 32-bit lane
    /// (16 bits each, room for four), green likewise — then the two pixels of a pair added as one
    /// 64-bit lane's halves.</summary>
    private static void Block(ReadOnlySpan<uint> top, ReadOnlySpan<uint> bottom, Span<byte> yTop, Span<byte> yBottom, Span<byte> u, Span<byte> v)
    {
        Vector256<uint> t0 = Vector256.Create(top), t1 = Vector256.Create(top[8..]), t2 = Vector256.Create(top[16..]), t3 = Vector256.Create(top[24..]);
        Vector256<uint> b0 = Vector256.Create(bottom), b1 = Vector256.Create(bottom[8..]), b2 = Vector256.Create(bottom[16..]), b3 = Vector256.Create(bottom[24..]);
        Lumas(t0, t1, t2, t3).CopyTo(yTop);
        Lumas(b0, b1, b2, b3).CopyTo(yBottom);
        var (u0, v0) = Colours(t0, t1, b0, b1);
        var (u1, v1) = Colours(t2, t3, b2, b3);
        Vector256.Narrow(Vector256.Narrow(u0, u1), Vector256<ushort>.Zero).GetLower().CopyTo(u);
        Vector256.Narrow(Vector256.Narrow(v0, v1), Vector256<ushort>.Zero).GetLower().CopyTo(v);
    }

    private static Vector256<byte> Lumas(Vector256<uint> a, Vector256<uint> b, Vector256<uint> c, Vector256<uint> d) =>
        Vector256.Narrow(Vector256.Narrow(Luma(a), Luma(b)), Vector256.Narrow(Luma(c), Luma(d)));

    private static Vector256<uint> Luma(Vector256<uint> p)
    {
        var mask = Vector256.Create(0xFFu);
        var b = (p & mask).AsInt32();
        var g = ((p >> 8) & mask).AsInt32();
        var r = ((p >> 16) & mask).AsInt32();
        return (((r * 47 + g * 157 + b * 16 + Vector256.Create(128)) >> 8) + Vector256.Create(16)).AsUInt32();
    }

    /// <summary>The colour (U, V) of the 8 blocks of 16 pixels in two rows.</summary>
    private static (Vector256<uint> u, Vector256<uint> v) Colours(Vector256<uint> t0, Vector256<uint> t1, Vector256<uint> b0, Vector256<uint> b1)
    {
        var pair = Vector256.Create(0x00FF00FFu);
        // per pixel, top + bottom: blue | red << 16, and green | alpha << 16
        Vector256<uint> br0 = (t0 & pair) + (b0 & pair), br1 = (t1 & pair) + (b1 & pair);
        Vector256<uint> ga0 = ((t0 >> 8) & pair) + ((b0 >> 8) & pair), ga1 = ((t1 >> 8) & pair) + ((b1 >> 8) & pair);
        // per block: a pair's two lanes added
        var br = Vector256.Narrow(Pairs(br0), Pairs(br1));
        var ga = Vector256.Narrow(Pairs(ga0), Pairs(ga1));
        var low = Vector256.Create(0xFFFFu);
        var bl = (br & low).AsInt32();
        var r = (br >> 16).AsInt32();
        var g = (ga & low).AsInt32();
        var round = Vector256.Create(512);
        var middle = Vector256.Create(128);
        var u = ((r * -26 + g * -87 + bl * 112 + round) >> 10) + middle;
        var v = ((r * 112 + g * -102 + bl * -10 + round) >> 10) + middle;
        return (u.AsUInt32(), v.AsUInt32());
    }

    private static Vector256<ulong> Pairs(Vector256<uint> sums)
    {
        var wide = sums.AsUInt64();
        return (wide & Vector256.Create(0xFFFFFFFFul)) + (wide >> 32);
    }

    private static int B(uint p) => (int)(p & 0xFF);
    private static int G(uint p) => (int)((p >> 8) & 0xFF);
    private static int R(uint p) => (int)((p >> 16) & 0xFF);
    private static byte Luma(uint p) => (byte)(((47 * R(p) + 157 * G(p) + 16 * B(p) + 128) >> 8) + 16);
}

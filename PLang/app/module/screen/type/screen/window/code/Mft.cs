using System.Runtime.InteropServices;
using app.module.screen.type.screen.code;

namespace app.module.screen.type.screen.window.code;

/// <summary>
/// A page's video decoded by Windows itself: the Media Foundation transform Windows has for its format — H.264 (in
/// every Windows but the N editions), AV1 (where the AV1 Video Extension is installed) — one that runs on the CPU
/// (synchronous; no GPU). Samples go in in decode order, each with its time; pictures come out as NV12 with the time of
/// their own sample (the transform carries it), as a <see cref="Yuv"/>. The COM interfaces are tables of functions;
/// the few used are read from them.
/// </summary>
internal sealed class Mft : IDecoder
{
    private static readonly Guid DecoderCategory = new("d6c02d4b-6833-45b4-971a-05a4b04bab91"); // MFT_CATEGORY_VIDEO_DECODER
    private static readonly Guid H264Decoder = new("62CE7E72-4C71-4d20-B15D-452831A87D9D");     // CLSID_CMSH264DecoderMFT
    private static readonly Guid Interlace = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");       // MF_MT_INTERLACE_MODE
    private static readonly Guid TransformInterface = new("bf94c121-5b05-4e6f-8000-ba598961414d"); // IID_IMFTransform
    private static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");       // MF_MT_MAJOR_TYPE
    private static readonly Guid SubType = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");         // MF_MT_SUBTYPE
    private static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");       // MF_MT_FRAME_SIZE
    private static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");   // MF_MT_DEFAULT_STRIDE
    private static readonly Guid Aperture = new("d7388766-18fe-48c6-a177-ee894867c8c4");        // MF_MT_MINIMUM_DISPLAY_APERTURE
    private static readonly Guid VideoType = new("73646976-0000-0010-8000-00AA00389B71");       // MFMediaType_Video
    private static readonly Guid H264Format = new("34363248-0000-0010-8000-00AA00389B71");      // MFVideoFormat_H264
    private static readonly Guid Av1Format = new("31305641-0000-0010-8000-00AA00389B71");       // MFVideoFormat_AV1
    private static readonly Guid Nv12 = new("3231564E-0000-0010-8000-00AA00389B71");            // MFVideoFormat_NV12

    private const int NeedMoreInput = unchecked((int)0xC00D6D72), StreamChange = unchecked((int)0xC00D6D61), NotAccepting = unchecked((int)0xC00D36B5);
    // MFT_ENUM_FLAG_SYNCMFT | MFT_ENUM_FLAG_LOCALMFT | MFT_ENUM_FLAG_SORTANDFILTER: the CPU ones, the app's own too, best first
    private const int OnTheCpu = 0x01 | 0x10 | 0x40;

    // vtable slots: IUnknown 0–2; IMFAttributes 3–32; IMFMediaType / IMFSample / IMFActivate go on from 33
    private const int Release = 2;
    private const int GetUINT32 = 7, GetUINT64 = 8, GetGUID = 10, GetBlob = 15, SetUINT32 = 21, SetUINT64 = 22, SetGUID = 24;
    private const int ActivateObject = 33;
    private const int GetOutputStreamInfo = 7, GetOutputAvailableType = 14, SetInputType = 15,
        SetOutputType = 16, GetOutputCurrentType = 18, ProcessMessage = 23, ProcessInput = 24, ProcessOutput = 25;
    private const int GetSampleTime = 35, SetSampleTime = 36, ConvertToContiguousBuffer = 41, AddBuffer = 42;
    private const int Lock = 3, Unlock = 4, SetCurrentLength = 6;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Call0(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallPtr(IntPtr self, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallPtrIn(IntPtr self, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallGuidU32(IntPtr self, in Guid key, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallGuidU64(IntPtr self, in Guid key, long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallGuidGuid(IntPtr self, in Guid key, in Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetGuidOut(IntPtr self, in Guid key, out Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU32Out(IntPtr self, in Guid key, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU64Out(IntPtr self, in Guid key, out long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBlobOut(IntPtr self, in Guid key, byte[] buffer, int size, out int got);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Activate(IntPtr self, in Guid iid, out IntPtr instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StreamType(IntPtr self, int stream, IntPtr type, int flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int AvailableType(IntPtr self, int stream, int index, out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CurrentType(IntPtr self, int stream, out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StreamInfo(IntPtr self, int stream, out OutputInfo info);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Message(IntPtr self, int message, IntPtr param);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Input(IntPtr self, int stream, IntPtr sample, int flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Output(IntPtr self, int flags, int count, IntPtr buffers, out int status);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetTime(IntPtr self, long time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetTime(IntPtr self, out long time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockBuffer(IntPtr self, out IntPtr data, out int max, out int current);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetLength(IntPtr self, int length);

    [StructLayout(LayoutKind.Sequential)] private struct OutputInfo { public int Flags, Size, Alignment; }
    [StructLayout(LayoutKind.Sequential)] private struct TypeInfo { public Guid Major, Sub; }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, int model);
    [DllImport("ole32.dll")] private static extern void CoTaskMemFree(IntPtr memory);
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid clsid, IntPtr outer, int context, in Guid iid, out IntPtr instance);
    [DllImport("mfplat.dll")] private static extern int MFStartup(int version, int flags);
    [DllImport("mfplat.dll")] private static extern int MFTEnumEx(Guid category, int flags, in TypeInfo input, IntPtr output, out IntPtr activates, out int count);
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfplat.dll")] private static extern int MFCreateSample(out IntPtr sample);
    [DllImport("mfplat.dll")] private static extern int MFCreateMemoryBuffer(int length, out IntPtr buffer);

    private static readonly Lazy<bool> started = new(() => MFStartup(0x00020070, 0) >= 0);

    private readonly IntPtr transform;
    private readonly IntPtr output = Marshal.AllocHGlobal(32);   // MFT_OUTPUT_DATA_BUFFER
    private readonly Avcc? avcc;
    private byte[]? first;   // before the first sample: H.264's parameter sets, AV1's sequence header (av1C's configOBUs)
    private int frameWidth, frameHeight, stride, showWidth, showHeight;
    private IntPtr outSample;
    private int outSize;

    /// <summary>The input format of <paramref name="codec"/> (as PlangOS names it), or null for one this doesn't know.</summary>
    private static Guid? Format(string codec) => codec switch
    {
        "avc1" or "avc3" => H264Format,
        "av01" => Av1Format,
        _ => null,
    };

    /// <summary>Windows has a decoder for <paramref name="codec"/> that runs on the CPU.</summary>
    internal static bool Here(string codec)
    {
        if (Format(codec) is not { } format || !Start()) return false;
        var found = Find(format);
        if (found == IntPtr.Zero) return false;
        Let(found);
        return true;
    }

    /// <summary>A decoder for <paramref name="codec"/> with the stream's <paramref name="config"/> (avcC, av1C) and
    /// size. Throws when Windows has none (or it won't take the stream: AV1 of 10 bits gives no NV12, say).</summary>
    internal Mft(string codec, byte[] config, int width, int height)
    {
        if (Format(codec) is not { } format) throw new InvalidOperationException($"no decoder for {codec}");
        if (!Start()) throw new InvalidOperationException("Media Foundation doesn't start");
        if (format == H264Format)
            // Windows' own H.264 decoder, by name: the one the pixel path's H.264 has always used on this host
            Check(CoCreateInstance(H264Decoder, IntPtr.Zero, 1, TransformInterface, out transform), "no H.264 decoder");
        else
        {
            var found = Find(format);
            if (found == IntPtr.Zero) throw new InvalidOperationException($"Windows has no decoder for {codec}");
            try { Check(Fn<Activate>(found, ActivateObject)(found, TransformInterface, out transform), $"{codec} decoder"); }
            finally { Let(found); }
        }

        try
        {
            if (format == H264Format) { avcc = new Avcc(config); first = avcc.Parameters; }
            else if (config.Length > 4) first = config[4..];   // av1C: 4 bytes, then the configOBUs

            Check(MFCreateMediaType(out var input), "media type");
            try
            {
                Fn<CallGuidGuid>(input, SetGUID)(input, MajorType, VideoType);
                Fn<CallGuidGuid>(input, SetGUID)(input, SubType, format);
                if (width > 0 && height > 0) Fn<CallGuidU64>(input, SetUINT64)(input, FrameSize, ((long)width << 32) | (uint)height);
                // progressive or interlaced, as the stream says (television is often interlaced)
                Fn<CallGuidU32>(input, SetUINT32)(input, Interlace, 7);   // MFVideoInterlace_MixedInterlaceOrProgressive
                Check(Fn<StreamType>(transform, SetInputType)(transform, 0, input, 0), $"{codec} in");
            }
            finally { Let(input); }
            Typed();
            Fn<Message>(transform, ProcessMessage)(transform, 0x10000000, IntPtr.Zero);   // NOTIFY_BEGIN_STREAMING
            Fn<Message>(transform, ProcessMessage)(transform, 0x10000003, IntPtr.Zero);   // NOTIFY_START_OF_STREAM
        }
        catch
        {
            Dispose();   // the transform let go of: it won't take this stream
            throw;
        }
    }

    // this thread in COM's multithreaded room (or already in one), Media Foundation started (once)
    private static bool Start()
    {
        CoInitializeEx(IntPtr.Zero, 0);
        return started.Value;
    }

    // the best CPU decoder for the format, as an activation object (to let go of), or zero
    private static IntPtr Find(Guid format)
    {
        var wanted = new TypeInfo { Major = VideoType, Sub = format };
        if (MFTEnumEx(DecoderCategory, OnTheCpu, wanted, IntPtr.Zero, out var list, out var count) < 0 || list == IntPtr.Zero) return IntPtr.Zero;
        try
        {
            var best = count > 0 ? Marshal.ReadIntPtr(list) : IntPtr.Zero;
            for (var i = 1; i < count; i++) Let(Marshal.ReadIntPtr(list, i * IntPtr.Size));
            return best;
        }
        finally { CoTaskMemFree(list); }
    }

    public List<(double time, Yuv picture)> Decode(ReadOnlyMemory<byte> sample, double time)
    {
        var bytes = avcc != null ? avcc.AnnexB(sample.Span, first)
            : first != null ? [.. first, .. sample.Span] : sample.ToArray();
        first = null;
        var got = new List<(double, Yuv)>();
        Check(MFCreateMemoryBuffer(bytes.Length, out var buffer), "buffer");
        Check(MFCreateSample(out var input), "sample");
        try
        {
            Fn<LockBuffer>(buffer, Lock)(buffer, out var data, out _, out _);
            Marshal.Copy(bytes, 0, data, bytes.Length);
            Fn<Call0>(buffer, Unlock)(buffer);
            Fn<SetLength>(buffer, SetCurrentLength)(buffer, bytes.Length);
            Fn<CallPtrIn>(input, AddBuffer)(input, buffer);
            Fn<SetTime>(input, SetSampleTime)(input, (long)Math.Round(time * 1e7));   // 100 ns units
            var hr = Fn<Input>(transform, ProcessInput)(transform, 0, input, 0);
            if (hr == NotAccepting)
            {
                Drain(got);
                hr = Fn<Input>(transform, ProcessInput)(transform, 0, input, 0);
            }
            Check(hr, "input");
            Drain(got);
        }
        finally
        {
            Let(input);
            Let(buffer);
        }
        return got;
    }

    // every picture the decoder has ready, each with its sample's time
    private void Drain(List<(double, Yuv)> got)
    {
        while (true)
        {
            Check(Fn<StreamInfo>(transform, GetOutputStreamInfo)(transform, 0, out var info), "output info");
            // the decoder gives its own samples (MFT_OUTPUT_STREAM_PROVIDES_SAMPLES), or takes ours: one, kept
            var ours = (info.Flags & 0x100) == 0;
            var sample = ours ? Out(Math.Max(info.Size, stride * frameHeight * 3 / 2)) : IntPtr.Zero;
            Marshal.Copy(new byte[32], 0, output, 32);
            Marshal.WriteIntPtr(output, 8, sample);
            var hr = Fn<Output>(transform, ProcessOutput)(transform, 0, 1, output, out _);
            var events = Marshal.ReadIntPtr(output, 24);
            if (events != IntPtr.Zero) Let(events);
            if (!ours) sample = Marshal.ReadIntPtr(output, 8);
            try
            {
                if (hr == NeedMoreInput) return;
                if (hr == StreamChange) { Typed(); continue; }
                Check(hr, "output");
                if (sample == IntPtr.Zero || Fn<GetTime>(sample, GetSampleTime)(sample, out var at) < 0) continue;
                if (Fn<CallPtr>(sample, ConvertToContiguousBuffer)(sample, out var whole) < 0) continue;
                try
                {
                    Fn<LockBuffer>(whole, Lock)(whole, out var data, out _, out var length);
                    try { if (Picture(data, length) is { } picture) got.Add((at / 1e7, picture)); }
                    finally { Fn<Call0>(whole, Unlock)(whole); }
                }
                finally { Let(whole); }
            }
            finally
            {
                if (!ours && sample != IntPtr.Zero) Let(sample);
            }
        }
    }

    // NV12 — the lumas, then U and V interleaved per 2×2 block, rows of the frame's (padded) height — as the picture
    // it shows: the planes apart, cut to what is shown
    private Yuv? Picture(IntPtr data, int length)
    {
        if (length < stride * frameHeight * 3 / 2 || showWidth <= 0 || showHeight <= 0) return null;
        var Y = new byte[stride * showHeight];
        Marshal.Copy(data, Y, 0, Y.Length);
        int cWidth = (showWidth + 1) / 2, cHeight = (showHeight + 1) / 2;
        var row = new byte[cWidth * 2];
        var U = new byte[cWidth * cHeight];
        var V = new byte[U.Length];
        var colour = data + stride * frameHeight;
        for (var y = 0; y < cHeight; y++)
        {
            Marshal.Copy(colour + y * stride, row, 0, row.Length);
            for (var x = 0; x < cWidth; x++) { U[y * cWidth + x] = row[2 * x]; V[y * cWidth + x] = row[2 * x + 1]; }
        }
        return new Yuv(Y, U, V, stride, cWidth, showWidth, showHeight);
    }

    /// <summary>The output sample, with a buffer of at least <paramref name="size"/> bytes.</summary>
    private IntPtr Out(int size)
    {
        if (outSample != IntPtr.Zero && outSize >= size) return outSample;
        if (outSample != IntPtr.Zero) Let(outSample);
        Check(MFCreateSample(out outSample), "sample");
        Check(MFCreateMemoryBuffer(size, out var buffer), "buffer");
        Fn<CallPtrIn>(outSample, AddBuffer)(outSample, buffer);
        Let(buffer);   // the sample holds it
        outSize = size;
        return outSample;
    }

    /// <summary>The pictures come out as NV12: the first such type the decoder offers; the frame's size and row
    /// length, and the part of it that is shown (the display aperture: H.264's 1080 of 1088 rows).</summary>
    private void Typed()
    {
        for (var i = 0; ; i++)
        {
            Check(Fn<AvailableType>(transform, GetOutputAvailableType)(transform, 0, i, out var type), "no NV12 out");
            try
            {
                if (Fn<GetGuidOut>(type, GetGUID)(type, SubType, out var format) < 0 || format != Nv12) continue;
                Check(Fn<StreamType>(transform, SetOutputType)(transform, 0, type, 0), "NV12 out");
                break;
            }
            finally { Let(type); }
        }
        Check(Fn<CurrentType>(transform, GetOutputCurrentType)(transform, 0, out var current), "output type");
        try
        {
            if (Fn<GetU64Out>(current, GetUINT64)(current, FrameSize, out var size) >= 0)
            {
                frameWidth = (int)(size >> 32);
                frameHeight = (int)(size & 0xFFFFFFFF);
            }
            stride = Fn<GetU32Out>(current, GetUINT32)(current, DefaultStride, out var s) >= 0 && s > 0 ? s : frameWidth;
            (showWidth, showHeight) = (frameWidth, frameHeight);
            // MFVideoArea { MFOffset x { u16 fract; i16 value }, MFOffset y, SIZE { i32 cx, cy } }: from the corner
            var area = new byte[16];
            if (Fn<GetBlobOut>(current, GetBlob)(current, Aperture, area, area.Length, out var got) >= 0 && got >= 16
                && BitConverter.ToInt16(area, 2) == 0 && BitConverter.ToInt16(area, 6) == 0)
                (showWidth, showHeight) = (Math.Min(frameWidth, BitConverter.ToInt32(area, 8)), Math.Min(frameHeight, BitConverter.ToInt32(area, 12)));
        }
        finally { Let(current); }
    }

    public void Dispose()
    {
        if (outSample != IntPtr.Zero) Let(outSample);
        if (transform != IntPtr.Zero) Let(transform);
        Marshal.FreeHGlobal(output);
    }

    // a function of the table, made callable once and kept (it is called for every picture)
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<(IntPtr, Type), Delegate> functions = new();

    private static T Fn<T>(IntPtr self, int slot) where T : Delegate =>
        (T)functions.GetOrAdd((Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), slot * IntPtr.Size), typeof(T)),
            key => Marshal.GetDelegateForFunctionPointer<T>(key.Item1));

    private static void Let(IntPtr self) => Fn<Call0>(self, Release)(self);

    private static void Check(int hr, string what)
    {
        if (hr < 0) throw new InvalidOperationException($"video: {what} (0x{hr:X8})");
    }
}

using System.Runtime.InteropServices;

namespace app.module.screen.code;

/// <summary>
/// A video from PlangOS's screen — one H.264 stream (baseline, BT.709 video range) — decoded with
/// Windows' own decoder (Media Foundation's H.264 transform: in every Windows but the N editions,
/// on the CPU when there is no GPU). Its pictures come out as BGRA of the part of the screen it
/// plays in. The COM interfaces are tables of functions; the few used are read from them.
/// </summary>
internal sealed class Video : IDisposable
{
    private static readonly Guid DecoderClass = new("62CE7E72-4C71-4d20-B15D-452831A87D9D");   // CLSID_CMSH264DecoderMFT
    private static readonly Guid TransformInterface = new("bf94c121-5b05-4e6f-8000-ba598961414d"); // IID_IMFTransform
    private static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");      // MF_MT_MAJOR_TYPE
    private static readonly Guid SubType = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");        // MF_MT_SUBTYPE
    private static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");      // MF_MT_FRAME_SIZE
    private static readonly Guid FrameRate = new("c459a2e8-3d2c-4e44-b132-fee5156c7bb0");      // MF_MT_FRAME_RATE
    private static readonly Guid Interlace = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");      // MF_MT_INTERLACE_MODE
    private static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");  // MF_MT_DEFAULT_STRIDE
    private static readonly Guid LowLatency = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");     // MF_LOW_LATENCY
    private static readonly Guid VideoType = new("73646976-0000-0010-8000-00AA00389B71");         // MFMediaType_Video
    private static readonly Guid H264Format = new("34363248-0000-0010-8000-00AA00389B71");     // MFVideoFormat_H264
    private static readonly Guid Nv12 = new("3231564E-0000-0010-8000-00AA00389B71");           // MFVideoFormat_NV12

    private const int NeedMoreInput = unchecked((int)0xC00D6D72), StreamChange = unchecked((int)0xC00D6D61), NotAccepting = unchecked((int)0xC00D36B5);

    // vtable slots: IUnknown 0–2; IMFAttributes 3–32; IMFMediaType/IMFSample go on from 33
    private const int Release = 2;
    private const int GetUINT32 = 7, GetUINT64 = 8, GetGUID = 10, SetUINT32 = 21, SetUINT64 = 22, SetGUID = 24;
    private const int GetOutputStreamInfo = 7, GetAttributes = 8, GetOutputAvailableType = 14, SetInputType = 15,
        SetOutputType = 16, GetOutputCurrentType = 18, ProcessMessage = 23, ProcessInput = 24, ProcessOutput = 25;
    private const int SetSampleTime = 36, ConvertToContiguousBuffer = 41, AddBuffer = 42;
    private const int Lock = 3, Unlock = 4, SetCurrentLength = 6;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Call0(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallPtr(IntPtr self, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallGuidU32(IntPtr self, in Guid key, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallGuidU64(IntPtr self, in Guid key, long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallGuidGuid(IntPtr self, in Guid key, in Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetGuidOut(IntPtr self, in Guid key, out Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU32Out(IntPtr self, in Guid key, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU64Out(IntPtr self, in Guid key, out long value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StreamType(IntPtr self, int stream, IntPtr type, int flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int AvailableType(IntPtr self, int stream, int index, out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CurrentType(IntPtr self, int stream, out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int StreamInfo(IntPtr self, int stream, out OutputInfo info);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Message(IntPtr self, int message, IntPtr param);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Input(IntPtr self, int stream, IntPtr sample, int flags);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Output(IntPtr self, int flags, int count, IntPtr buffers, out int status);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetTime(IntPtr self, long time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockBuffer(IntPtr self, out IntPtr data, out int max, out int current);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetLength(IntPtr self, int length);

    [StructLayout(LayoutKind.Sequential)] private struct OutputInfo { public int Flags, Size, Alignment; }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, int model);
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid clsid, IntPtr outer, int context, in Guid iid, out IntPtr instance);
    [DllImport("mfplat.dll")] private static extern int MFStartup(int version, int flags);
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfplat.dll")] private static extern int MFCreateSample(out IntPtr sample);
    [DllImport("mfplat.dll")] private static extern int MFCreateMemoryBuffer(int length, out IntPtr buffer);

    private static readonly Lazy<bool> started = new(() => MFStartup(0x00020070, 0) >= 0);

    private readonly IntPtr transform;
    private readonly IntPtr output = Marshal.AllocHGlobal(32);   // MFT_OUTPUT_DATA_BUFFER
    private int frameWidth, frameHeight, stride;
    private long time;
    private bool shown;
    // kept from picture to picture: made again only when the size changes
    private IntPtr outSample;
    private int outSize;
    private byte[] nv12 = [], bgra = [];
    private static readonly byte[] zeros = new byte[32];

    /// <summary>Which stream of PlangOS's this decodes.</summary>
    internal uint Id { get; }

    /// <summary>A decoder for stream <paramref name="id"/>, pictures of <paramref name="width"/> ×
    /// <paramref name="height"/>. Throws when Windows has no H.264 decoder.</summary>
    internal Video(uint id, int width, int height)
    {
        Id = id;
        CoInitializeEx(IntPtr.Zero, 0);   // this thread in COM's multithreaded room (or already in one)
        if (!started.Value) throw new InvalidOperationException("Media Foundation doesn't start");
        Check(CoCreateInstance(DecoderClass, IntPtr.Zero, 1, TransformInterface, out transform), "no H.264 decoder");

        // each picture out as soon as it is in: no waiting for more
        if (Fn<CallPtr>(transform, GetAttributes)(transform, out var attributes) >= 0 && attributes != IntPtr.Zero)
        {
            Fn<CallGuidU32>(attributes, SetUINT32)(attributes, LowLatency, 1);
            Let(attributes);
        }

        Check(MFCreateMediaType(out var input), "media type");
        try
        {
            Fn<CallGuidGuid>(input, SetGUID)(input, MajorType, VideoType);
            Fn<CallGuidGuid>(input, SetGUID)(input, SubType, H264Format);
            Fn<CallGuidU64>(input, SetUINT64)(input, FrameSize, ((long)width << 32) | (uint)height);
            Fn<CallGuidU64>(input, SetUINT64)(input, FrameRate, (60L << 32) | 1);
            Fn<CallGuidU32>(input, SetUINT32)(input, Interlace, 2);   // progressive
            Check(Fn<StreamType>(transform, SetInputType)(transform, 0, input, 0), "H.264 in");
        }
        finally { Let(input); }
        Typed();
        Fn<Message>(transform, ProcessMessage)(transform, 0x10000000, IntPtr.Zero);   // NOTIFY_BEGIN_STREAMING
        Fn<Message>(transform, ProcessMessage)(transform, 0x10000003, IntPtr.Zero);   // NOTIFY_START_OF_STREAM
    }

    /// <summary>The next of its bytes (Annex B); the newest picture that came out of them, as BGRA
    /// <paramref name="width"/> × <paramref name="height"/> — or null when none did (yet), or when it
    /// isn't to be <paramref name="shown"/> (a newer one waits: this one is only decoded, which the
    /// ones after it need). The picture is this decoder's own array, the same each time: copy it out
    /// before the next.</summary>
    internal byte[]? Decode(ReadOnlySpan<byte> h264, int width, int height, bool shown)
    {
        this.shown = shown;
        Check(MFCreateMemoryBuffer(h264.Length, out var buffer), "buffer");
        Check(MFCreateSample(out var sample), "sample");
        byte[]? picture = null;
        try
        {
            Fn<LockBuffer>(buffer, Lock)(buffer, out var data, out _, out _);
            Marshal.Copy(h264.ToArray(), 0, data, h264.Length);
            Fn<Call0>(buffer, Unlock)(buffer);
            Fn<SetLength>(buffer, SetCurrentLength)(buffer, h264.Length);
            Fn<CallPtrIn>(sample, AddBuffer)(sample, buffer);
            Fn<SetTime>(sample, SetSampleTime)(sample, time);
            time += 166_667;   // 100 ns units: a sixtieth of a second
            var hr = Fn<Input>(transform, ProcessInput)(transform, 0, sample, 0);
            if (hr == NotAccepting)
            {
                picture = Drain(width, height) ?? picture;
                hr = Fn<Input>(transform, ProcessInput)(transform, 0, sample, 0);
            }
            Check(hr, "H.264 input");
            picture = Drain(width, height) ?? picture;
        }
        finally
        {
            Let(sample);
            Let(buffer);
        }
        return picture;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallPtrIn(IntPtr self, IntPtr value);

    /// <summary>Every picture the decoder has ready; the last, as BGRA.</summary>
    private byte[]? Drain(int width, int height)
    {
        byte[]? picture = null;
        while (true)
        {
            Check(Fn<StreamInfo>(transform, GetOutputStreamInfo)(transform, 0, out var info), "output info");
            // the decoder gives its own samples (MFT_OUTPUT_STREAM_PROVIDES_SAMPLES), or takes ours:
            // one, made once for the size, kept (not 60 new ones a second)
            var ours = (info.Flags & 0x100) == 0;
            var sample = IntPtr.Zero;
            if (ours) sample = Out(Math.Max(info.Size, stride * frameHeight * 3 / 2));
            Marshal.Copy(zeros, 0, output, 32);
            Marshal.WriteIntPtr(output, 8, sample);
            var hr = Fn<Output>(transform, ProcessOutput)(transform, 0, 1, output, out _);
            var events = Marshal.ReadIntPtr(output, 24);
            if (events != IntPtr.Zero) Let(events);
            if (!ours) sample = Marshal.ReadIntPtr(output, 8);   // the decoder's: let go of below
            try
            {
                if (hr == NeedMoreInput) return picture;
                if (hr == StreamChange) { Typed(); continue; }
                Check(hr, "H.264 output");
                if (!shown || sample == IntPtr.Zero) continue;
                if (Fn<CallPtr>(sample, ConvertToContiguousBuffer)(sample, out var whole) < 0) continue;
                try
                {
                    Fn<LockBuffer>(whole, Lock)(whole, out var data, out _, out var length);
                    try { picture = Bgra(data, length, width, height); }
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

    /// <summary>The pictures come out as NV12: the first such type the decoder offers, and the size
    /// and row length of what it gives.</summary>
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
        }
        finally { Let(current); }
    }

    /// <summary>NV12 (a luma per pixel; the colour, U and V interleaved, per 2×2 block) to BGRA,
    /// BT.709 video range — the numbers PlangOS encoded with. Rows in parallel bands.</summary>
    private byte[]? Bgra(IntPtr data, int length, int width, int height)
    {
        var size = stride * frameHeight * 3 / 2;
        if (frameWidth < width || frameHeight < height || length < size) return null;
        if (nv12.Length != size) nv12 = new byte[size];
        Marshal.Copy(data, nv12, 0, size);
        if (bgra.Length != width * height * 4) bgra = new byte[width * height * 4];
        var (yuv, rgb, uv, s) = (nv12, bgra, stride * frameHeight, stride);
        // half the cores: the other half are PlangOS's (its VM runs on this machine's cores too)
        var bands = Math.Clamp(Environment.ProcessorCount / 2, 1, Math.Max(1, height / 32));
        Parallel.For(0, bands, band =>
        {
            var nv12 = yuv;
            var bgra = rgb;
            for (var y = height * band / bands; y < height * (band + 1) / bands; y++)
            {
                var o = y * width * 4;
                var row = y * s;
                var colour = uv + y / 2 * s;
                for (var x = 0; x < width; x++, o += 4)
                {
                    int c = 298 * (nv12[row + x] - 16);
                    int u = nv12[colour + (x & ~1)] - 128, v = nv12[colour + (x & ~1) + 1] - 128;
                    bgra[o] = Clamp((c + 541 * u + 128) >> 8);
                    bgra[o + 1] = Clamp((c - 55 * u - 136 * v + 128) >> 8);
                    bgra[o + 2] = Clamp((c + 459 * v + 128) >> 8);
                    bgra[o + 3] = 255;
                }
            }
        });
        return bgra;
    }

    private static byte Clamp(int v) => (byte)(v < 0 ? 0 : v > 255 ? 255 : v);

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
        if (hr < 0) throw new InvalidOperationException($"H.264: {what} (0x{hr:X8})");
    }
}

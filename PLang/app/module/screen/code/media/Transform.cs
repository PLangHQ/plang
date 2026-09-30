using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace app.module.screen.code.media;

/// <summary>
/// A Media Foundation transform (Windows): samples in, samples out — a decoder found by the kind of
/// video it takes (Windows' own, or an extension's: H.264, VP9, AV1), or the video processor (NV12 to
/// BGRA at another size). The COM interfaces are tables of functions; the few used are read from
/// them. Runs on the thread that made it (COM's multithreaded room). Its output samples are its own:
/// taken, then given back (<see cref="Recycle"/>) and used again — no picture's memory made per frame.
/// </summary>
internal sealed class Transform : IDisposable
{
    internal static readonly Guid MajorType = new("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
    internal static readonly Guid SubType = new("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
    internal static readonly Guid FrameSize = new("1652c33d-d6b2-4012-b834-72030849a37d");
    internal static readonly Guid Interlace = new("e2724bb8-e676-4806-b4b2-a8d6efb44ccd");
    internal static readonly Guid DefaultStride = new("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");
    internal static readonly Guid PixelAspect = new("c6376a1e-8d0a-4027-be45-6d9a0ad39bb6");
    internal static readonly Guid SampleSize = new("dad3ab78-1990-408b-bce2-eba673dacc10");
    internal static readonly Guid DisplayAperture = new("d7388766-18fe-48c6-a177-ee894867c8c4");
    internal static readonly Guid GeometricAperture = new("66758743-7e5f-400d-980a-aa8596c85696");
    internal static readonly Guid PanScanAperture = new("79614dde-9187-48fb-b8c7-4d52689de649");
    internal static readonly Guid LowLatency = new("9c27891a-ed7a-40e1-88e8-b22727a024ee");
    internal static readonly Guid Video = new("73646976-0000-0010-8000-00AA00389B71");
    internal static readonly Guid H264 = new("34363248-0000-0010-8000-00AA00389B71");
    internal static readonly Guid Vp9 = new("30395056-0000-0010-8000-00AA00389B71");
    internal static readonly Guid Av1 = new("31305641-0000-0010-8000-00AA00389B71");
    internal static readonly Guid Nv12 = new("3231564E-0000-0010-8000-00AA00389B71");
    internal static readonly Guid Rgb32 = new("00000016-0000-0010-8000-00AA00389B71");
    private static readonly Guid Decoders = new("d6c02d4b-6833-45b4-971a-05a4b04bab91");      // MFT_CATEGORY_VIDEO_DECODER
    private static readonly Guid Processor = new("88753B26-5B24-49BD-B2E7-0C445C78C982");     // CLSID_VideoProcessorMFT
    private static readonly Guid TransformInterface = new("bf94c121-5b05-4e6f-8000-ba598961414d");
    private static readonly Guid ProcessorControl = new("A3F675D5-6119-4f7f-A100-1D8B280F0EFB"); // IMFVideoProcessorControl

    internal const int NeedMoreInput = unchecked((int)0xC00D6D72), StreamChange = unchecked((int)0xC00D6D61), NotAccepting = unchecked((int)0xC00D36B5);

    // MFTEnumEx: synchronous (answers at once), in this process, best first — not hardware ones
    // (asynchronous: they'd need unlocking and a D3D device)
    private const int SyncMft = 0x01, LocalMft = 0x10, SortAndFilter = 0x40;

    // vtable slots: IUnknown 0–2; IMFAttributes 3–32; IMFSample 33+; IMFTransform; IMFMediaBuffer; IMFActivate 33+
    private const int QueryInterface = 0, Release = 2;
    private const int GetUINT32 = 7, GetUINT64 = 8, GetGUID = 10, DeleteItem = 19, SetUINT32 = 21, SetUINT64 = 22, SetGUID = 24, CopyAllItems = 32;
    private const int GetOutputStreamInfo = 7, GetAttributes = 8, GetOutputAvailableType = 14, SetInputType = 15,
        SetOutputType = 16, GetOutputCurrentType = 18, ProcessMessage = 23, ProcessInput = 24, ProcessOutput = 25;
    private const int GetSampleTime = 35, SetSampleTime = 36, SetSampleDuration = 38, ConvertToContiguousBuffer = 41, AddBuffer = 42;
    private const int Lock = 3, Unlock = 4, SetCurrentLength = 6;
    private const int ActivateObject = 33;
    private const int SetSourceRectangle = 4;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Call0(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallPtr(IntPtr self, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallPtrIn(IntPtr self, IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Query(IntPtr self, in Guid iid, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int CallGuid(IntPtr self, in Guid key);
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
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetTime(IntPtr self, out long time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetTime(IntPtr self, long time);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockBuffer(IntPtr self, out IntPtr data, out int max, out int current);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetLength(IntPtr self, int length);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Activate(IntPtr self, in Guid iid, out IntPtr result);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetRect(IntPtr self, in Rect rect);

    [StructLayout(LayoutKind.Sequential)] private struct OutputInfo { public int Flags, Size, Alignment; }
    [StructLayout(LayoutKind.Sequential)] private struct TypeInfo { public Guid Major, Sub; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }

    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, int model);
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(in Guid clsid, IntPtr outer, int context, in Guid iid, out IntPtr instance);
    [DllImport("ole32.dll")] private static extern void CoTaskMemFree(IntPtr p);
    [DllImport("mfplat.dll")] private static extern int MFStartup(int version, int flags);
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfplat.dll")] private static extern int MFCreateSample(out IntPtr sample);
    [DllImport("mfplat.dll")] private static extern int MFCreateMemoryBuffer(int length, out IntPtr buffer);
    [DllImport("mfplat.dll")] private static extern int MFCreateAlignedMemoryBuffer(int length, int alignment, out IntPtr buffer);
    [DllImport("mfplat.dll")] private static extern int MFTEnumEx(Guid category, int flags, in TypeInfo input, IntPtr output, out IntPtr activates, out int count);

    private static readonly Lazy<bool> started = new(() => MFStartup(0x00020070, 0) >= 0);
    private static readonly ConcurrentDictionary<(IntPtr, Type), Delegate> functions = new();
    private static readonly byte[] Zeros = new byte[32];

    private readonly IntPtr mft;
    private readonly IntPtr output = Marshal.AllocHGlobal(32);   // MFT_OUTPUT_DATA_BUFFER
    private readonly Func<IntPtr, bool>? wanted;                  // which output type (a decoder's: NV12)
    private readonly Stack<(IntPtr sample, int size)> spare = new();
    private readonly Dictionary<IntPtr, int> sizes = new();
    private IntPtr current;      // the output type now (refreshed when it changes)
    private int outSize;
    private bool drained;

    private Transform(IntPtr mft, Func<IntPtr, bool>? wanted)
    {
        this.mft = mft;
        this.wanted = wanted;
    }

    /// <summary>Windows' decoder for <paramref name="codec"/> (<see cref="H264"/>, <see cref="Vp9"/>,
    /// <see cref="Av1"/>) pictures of <paramref name="width"/> × <paramref name="height"/>, giving NV12 —
    /// each one Windows offers tried in turn — or null when none takes it (an extension not installed,
    /// an N edition). <paramref name="lowLatency"/>: each picture out as soon as it is in (for one that
    /// shows at once; a player decoding ahead wants the display order kept instead).</summary>
    internal static Transform? Decoder(Guid codec, int width, int height, bool lowLatency)
    {
        Start();
        if (MFTEnumEx(Decoders, SyncMft | LocalMft | SortAndFilter, new TypeInfo { Major = Video, Sub = codec }, IntPtr.Zero, out var list, out var count) < 0)
            return null;
        Transform? found = null;
        try
        {
            for (var i = 0; i < count; i++)
            {
                var activate = Marshal.ReadIntPtr(list, i * IntPtr.Size);
                if (found == null && Fn<Activate>(activate, ActivateObject)(activate, TransformInterface, out var mft) >= 0)
                    found = Set(new Transform(mft, type => Fn<GetGuidOut>(type, GetGUID)(type, SubType, out var s) >= 0 && s == Nv12),
                        codec, width, height, lowLatency);
                Let(activate);
            }
        }
        finally { CoTaskMemFree(list); }
        return found;
    }

    /// <summary>A decoder set up for the stream, or null (it is let go of) when it won't take it.</summary>
    private static Transform? Set(Transform decoder, Guid codec, int width, int height, bool lowLatency)
    {
        try
        {
            if (lowLatency && Fn<CallPtr>(decoder.mft, GetAttributes)(decoder.mft, out var attributes) >= 0 && attributes != IntPtr.Zero)
            {
                Fn<CallGuidU32>(attributes, SetUINT32)(attributes, LowLatency, 1);
                Let(attributes);
            }
            var input = Type(codec, width, height);
            try { Check(Fn<StreamType>(decoder.mft, SetInputType)(decoder.mft, 0, input, 0), "decoder input"); }
            finally { Let(input); }
            decoder.Typed();
            decoder.Begin();
            return decoder;
        }
        catch (InvalidOperationException)
        {
            decoder.Dispose();
            return null;
        }
    }

    /// <summary>Windows' video processor: <paramref name="from"/> (a decoder's output type, NV12),
    /// cropped to <paramref name="width"/> × <paramref name="height"/>, into BGRA of
    /// <paramref name="toWidth"/> × <paramref name="toHeight"/>, top row first. Null when it isn't there
    /// or won't. The types it gets are copies of the decoder's, made plain: progressive (else it would
    /// deinterlace, holding pictures back), no apertures carried over.</summary>
    internal static Transform? Scaler(IntPtr from, int width, int height, int toWidth, int toHeight)
    {
        Start();
        if (CoCreateInstance(Processor, IntPtr.Zero, 1, TransformInterface, out var mft) < 0) return null;
        var scaler = new Transform(mft, null);
        IntPtr input = IntPtr.Zero, to = IntPtr.Zero;
        try
        {
            input = Copy(from);
            Fn<CallGuidU32>(input, SetUINT32)(input, Interlace, 2);   // progressive
            Check(Fn<StreamType>(mft, SetInputType)(mft, 0, input, 0), "processor input");
            to = Copy(input);
            foreach (var aperture in new[] { DisplayAperture, GeometricAperture, PanScanAperture, SampleSize })
                Fn<CallGuid>(to, DeleteItem)(to, aperture);
            Fn<CallGuidGuid>(to, SetGUID)(to, SubType, Rgb32);
            Fn<CallGuidU64>(to, SetUINT64)(to, FrameSize, ((long)toWidth << 32) | (uint)toHeight);
            Fn<CallGuidU64>(to, SetUINT64)(to, PixelAspect, (1L << 32) | 1);
            Fn<CallGuidU32>(to, SetUINT32)(to, DefaultStride, toWidth * 4);   // positive: top row first
            Check(Fn<StreamType>(mft, SetOutputType)(mft, 0, to, 0), "processor output");
            // the decoder's frame is padded (1080 → 1088): only the picture is scaled
            var (aw, ah) = Size(from);
            if (Fn<Query>(mft, QueryInterface)(mft, ProcessorControl, out var control) >= 0)
            {
                Fn<SetRect>(control, SetSourceRectangle)(control, new Rect { Right = Math.Min(width, aw), Bottom = Math.Min(height, ah) });
                Let(control);
            }
            scaler.Begin();
            return scaler;
        }
        catch (InvalidOperationException)
        {
            scaler.Dispose();
            return null;
        }
        finally
        {
            if (input != IntPtr.Zero) Let(input);
            if (to != IntPtr.Zero) Let(to);
        }
    }

    /// <summary>A new media type with everything <paramref name="type"/> has.</summary>
    private static IntPtr Copy(IntPtr type)
    {
        Check(MFCreateMediaType(out var copy), "media type");
        Fn<CallPtrIn>(type, CopyAllItems)(type, copy);
        return copy;
    }

    /// <summary>The type its output has now (a decoder's NV12: size, row length) — its own: not let go of.</summary>
    internal IntPtr OutputType()
    {
        if (current == IntPtr.Zero) Check(Fn<CurrentType>(mft, GetOutputCurrentType)(mft, 0, out current), "output type");
        return current;
    }

    /// <summary>A size of <paramref name="type"/>: its frame size.</summary>
    internal static (int width, int height) Size(IntPtr type)
        => Fn<GetU64Out>(type, GetUINT64)(type, FrameSize, out var s) >= 0 ? ((int)(s >> 32), (int)(s & 0xFFFFFFFF)) : (0, 0);

    /// <summary>The row length of <paramref name="type"/>'s pictures.</summary>
    internal static int Stride(IntPtr type, int width)
        => Fn<GetU32Out>(type, GetUINT32)(type, DefaultStride, out var s) >= 0 && s > 0 ? s : width;

    /// <summary>Bytes as a sample at <paramref name="time"/> (seconds): copied once, into the buffer
    /// the decoder reads (it may keep it past this call).</summary>
    internal static IntPtr Sample(ReadOnlyMemory<byte> bytes, double time, double duration)
    {
        Check(MFCreateMemoryBuffer(Math.Max(1, bytes.Length), out var buffer), "buffer");
        Check(MFCreateSample(out var sample), "sample");
        Fn<LockBuffer>(buffer, Lock)(buffer, out var data, out _, out _);
        if (MemoryMarshal.TryGetArray(bytes, out var array)) Marshal.Copy(array.Array!, array.Offset, data, array.Count);
        else Marshal.Copy(bytes.ToArray(), 0, data, bytes.Length);
        Fn<Call0>(buffer, Unlock)(buffer);
        Fn<SetLength>(buffer, SetCurrentLength)(buffer, bytes.Length);
        Fn<CallPtrIn>(sample, AddBuffer)(sample, buffer);
        Let(buffer);
        Fn<SetTime>(sample, SetSampleTime)(sample, (long)(time * 1e7));
        Fn<SetTime>(sample, SetSampleDuration)(sample, (long)(duration * 1e7));
        return sample;
    }

    /// <summary>A sample's time, in seconds (NaN when it has none).</summary>
    internal static double Time(IntPtr sample) => Fn<GetTime>(sample, GetSampleTime)(sample, out var t) >= 0 ? t / 1e7 : double.NaN;

    /// <summary>A sample's bytes, locked: where they are and how many; <see cref="Unlocked"/> with the
    /// buffer returned when done. Zero when it has none.</summary>
    internal static IntPtr Locked(IntPtr sample, out IntPtr data, out int length)
    {
        data = IntPtr.Zero; length = 0;
        if (Fn<CallPtr>(sample, ConvertToContiguousBuffer)(sample, out var buffer) < 0) return IntPtr.Zero;
        if (Fn<LockBuffer>(buffer, Lock)(buffer, out data, out _, out length) >= 0) return buffer;
        Let(buffer);
        return IntPtr.Zero;
    }

    internal static void Unlocked(IntPtr buffer)
    {
        Fn<Call0>(buffer, Unlock)(buffer);
        Let(buffer);
    }

    /// <summary>A sample in; false when the transform has output to give first.</summary>
    internal bool Feed(IntPtr sample)
    {
        if (drained)
        {
            Fn<Message>(mft, ProcessMessage)(mft, 0x10000003, IntPtr.Zero);   // NOTIFY_START_OF_STREAM again
            drained = false;
        }
        var hr = Fn<Input>(mft, ProcessInput)(mft, 0, sample, 0);
        if (hr == NotAccepting) return false;
        Check(hr, "input");
        return true;
    }

    /// <summary>The next sample out, or none (it needs more in). The caller gives it back
    /// (<see cref="Recycle"/>) when done: the samples, and their pictures' memory, are used again.</summary>
    internal IntPtr Take()
    {
        while (true)
        {
            Check(Fn<StreamInfo>(mft, GetOutputStreamInfo)(mft, 0, out var info), "output info");
            var ours = (info.Flags & 0x100) == 0;   // not MFT_OUTPUT_STREAM_PROVIDES_SAMPLES
            var sample = IntPtr.Zero;
            if (ours) sample = Spare(Math.Max(info.Size, outSize), info.Alignment);
            Marshal.Copy(Zeros, 0, output, 32);
            Marshal.WriteIntPtr(output, 8, sample);
            var hr = Fn<Output>(mft, ProcessOutput)(mft, 0, 1, output, out _);
            var events = Marshal.ReadIntPtr(output, 24);
            if (events != IntPtr.Zero) Let(events);
            if (!ours) sample = Marshal.ReadIntPtr(output, 8);
            if (hr >= 0) return sample;
            if (sample != IntPtr.Zero) Recycle(sample);
            if (hr == NeedMoreInput) return IntPtr.Zero;
            if (hr == StreamChange && wanted != null) { Typed(); continue; }
            Check(hr, "output");
        }
    }

    /// <summary>A sample of ours with room for <paramref name="size"/> bytes: a spare one, or new.</summary>
    private IntPtr Spare(int size, int alignment)
    {
        while (spare.TryPop(out var s))
        {
            if (s.size >= size) return s.sample;
            sizes.Remove(s.sample);
            Let(s.sample);
        }
        Check(MFCreateSample(out var sample), "sample");
        var buffer = IntPtr.Zero;
        Check(alignment > 1 ? MFCreateAlignedMemoryBuffer(size, alignment - 1, out buffer) : MFCreateMemoryBuffer(size, out buffer), "buffer");
        Fn<CallPtrIn>(sample, AddBuffer)(sample, buffer);
        Let(buffer);
        sizes[sample] = size;
        return sample;
    }

    /// <summary>A sample from <see cref="Take"/> given back: kept for the next (a few), or let go.</summary>
    internal void Recycle(IntPtr sample)
    {
        if (sizes.TryGetValue(sample, out var size) && spare.Count < 8) spare.Push((sample, size));
        else
        {
            sizes.Remove(sample);
            Let(sample);
        }
    }

    /// <summary>Everything in it is dropped (a seek).</summary>
    internal void Flush() => Fn<Message>(mft, ProcessMessage)(mft, 0x00000000, IntPtr.Zero);   // MFT_MESSAGE_COMMAND_FLUSH

    /// <summary>No more input for now: what it holds back (reordered pictures) comes out with Take.
    /// Feeding again starts a stream anew.</summary>
    internal void Drain()
    {
        Fn<Message>(mft, ProcessMessage)(mft, 0x00000001, IntPtr.Zero);   // MFT_MESSAGE_COMMAND_DRAIN
        drained = true;
    }

    /// <summary>The first output type it offers that is <see cref="wanted"/>, set.</summary>
    private void Typed()
    {
        if (current != IntPtr.Zero) { Let(current); current = IntPtr.Zero; }
        for (var i = 0; ; i++)
        {
            Check(Fn<AvailableType>(mft, GetOutputAvailableType)(mft, 0, i, out var type), "no output type");
            try
            {
                if (!wanted!(type)) continue;
                Check(Fn<StreamType>(mft, SetOutputType)(mft, 0, type, 0), "output type");
                var (w, h) = Size(type);
                outSize = Stride(type, w) * h * 3 / 2;
                return;
            }
            finally { Let(type); }
        }
    }

    private void Begin()
    {
        Fn<Message>(mft, ProcessMessage)(mft, 0x10000000, IntPtr.Zero);   // NOTIFY_BEGIN_STREAMING
        Fn<Message>(mft, ProcessMessage)(mft, 0x10000003, IntPtr.Zero);   // NOTIFY_START_OF_STREAM
    }

    private static IntPtr Type(Guid subtype, int width, int height)
    {
        Check(MFCreateMediaType(out var type), "media type");
        Fn<CallGuidGuid>(type, SetGUID)(type, MajorType, Video);
        Fn<CallGuidGuid>(type, SetGUID)(type, SubType, subtype);
        Fn<CallGuidU64>(type, SetUINT64)(type, FrameSize, ((long)width << 32) | (uint)height);
        Fn<CallGuidU64>(type, SetUINT64)(type, PixelAspect, (1L << 32) | 1);
        Fn<CallGuidU32>(type, SetUINT32)(type, Interlace, 2);   // progressive
        return type;
    }

    private static void Start()
    {
        CoInitializeEx(IntPtr.Zero, 0);   // this thread in COM's multithreaded room (or already in one)
        if (!started.Value) throw new InvalidOperationException("Media Foundation doesn't start");
    }

    /// <summary>A COM object let go of.</summary>
    internal static void Let(IntPtr self) => Fn<Call0>(self, Release)(self);

    public void Dispose()
    {
        while (spare.TryPop(out var s)) Let(s.sample);
        if (current != IntPtr.Zero) Let(current);
        Let(mft);
        Marshal.FreeHGlobal(output);
    }

    private static T Fn<T>(IntPtr self, int slot) where T : Delegate =>
        (T)functions.GetOrAdd((Marshal.ReadIntPtr(Marshal.ReadIntPtr(self), slot * IntPtr.Size), typeof(T)),
            key => Marshal.GetDelegateForFunctionPointer<T>(key.Item1));

    private static void Check(int hr, string what)
    {
        if (hr < 0) throw new InvalidOperationException($"Media Foundation: {what} (0x{hr:X8})");
    }
}

using System.Buffers.Binary;

namespace app.module.screen.type.screen.display.code;

/// <summary>
/// A part of the screen playing a video: its pictures go to the host as an H.264 stream, not as
/// lossless ones (a video barely compresses losslessly — 1080p at 60 frames would be ~230 MB/s
/// through the pipe; as H.264 a few MB/s). Only its place: what is around it stays lossless.
/// Under the display's gate it only composes its place (what is above it — a menu, the taskbar —
/// is in it too) into a free buffer; its own thread encodes and sends, so the display, and Chromium
/// waiting for its frame to be taken, don't wait for the encoder. When the encoder is behind, the
/// newest picture replaces the one waiting.
/// </summary>
internal sealed class Video : IDisposable
{
    private const long Quiet = 500;   // ms without a change: it has stopped (paused, ended) — not a busy moment
    private static bool broken;       // openh264 refused, or the host can't decode: the lossless way stays

    private readonly Frame frame;
    private readonly H264 stream;
    // three buffers: one composed into (under the gate), one waiting, one being encoded
    private readonly byte[][] buffers;
    private int free = 0, waiting = 1, encoding = 2;
    private bool offered;
    private readonly Lock swap = new();
    private readonly SemaphoreSlim ready = new(0);
    private readonly Thread encoder;
    private volatile bool stopping;
    private byte[] bytes = new byte[1 << 20];
    private bool changed;
    private long last = Environment.TickCount64;

    internal Rect Rect { get; }

    /// <summary>Nothing changed in it for a while: a paused or ended video.</summary>
    internal bool Still => Environment.TickCount64 - last > Quiet;

    /// <summary>A video at <paramref name="place"/> (made even: H.264's colour is per 2×2 block; a
    /// column or row left over stays lossless), or none when H.264 can't be made here.</summary>
    internal static Video? At(Rect place, Frame frame)
    {
        if (broken || !H264.Here) return null;
        try { return new Video(Place(place), frame); }
        catch (Exception) { broken = true; return null; }
    }

    /// <summary>No more videos: the host can't show them.</summary>
    internal static void Off() => broken = true;

    /// <summary>Where a video playing in <paramref name="r"/> is streamed: its even part.</summary>
    internal static Rect Place(Rect r) => r with { Width = r.Width & ~1, Height = r.Height & ~1 };

    private Video(Rect rect, Frame frame)
    {
        Rect = rect;
        this.frame = frame;
        stream = new H264(rect.Width, rect.Height, Math.Clamp(Environment.ProcessorCount / 2, 1, 4));
        buffers = [.. Enumerable.Range(0, 3).Select(_ => GC.AllocateUninitializedArray<byte>(rect.Width * rect.Height * 4, pinned: true))];
        encoder = new Thread(Encode) { IsBackground = true, Name = "screen video" };
        encoder.Start();
    }

    /// <summary>Something in its place changed: a picture goes with the next frame.</summary>
    internal void Changed()
    {
        changed = true;
        last = Environment.TickCount64;
    }

    /// <summary>Under the gate: its place composed, if it changed, and offered to the encoder. (Not
    /// kept as the screen as sent: nothing is compared there while it plays, and when it stops its
    /// place is sent whole.)</summary>
    internal void Next(Display display)
    {
        if (!changed) return;
        changed = false;
        var pixels = buffers[free];
        var row = Rect.Width * 4;
        for (var y = 0; y < Rect.Height; y++)
            display.Draw(Rect.Y + y, Rect.X, pixels.AsSpan(y * row, row));
        lock (swap)
        {
            (free, waiting) = (waiting, free);
            if (offered) return;   // the encoder hasn't taken the last one: this one replaced it
            offered = true;
        }
        ready.Release();
    }

    /// <summary>The encoder's thread: the newest picture into the stream, sent as a frame of its own
    /// — "h264" [u32 stream] then the bytes.</summary>
    private void Encode()
    {
        while (true)
        {
            ready.Wait();
            if (stopping) return;
            lock (swap)
            {
                (waiting, encoding) = (encoding, waiting);
                offered = false;
            }
            var n = stream.Next(buffers[encoding], Rect.Width * 4, ref bytes);
            if (n == 0 || stopping) continue;
            var message = new byte[8 + n];
            "h264"u8.CopyTo(message);
            BinaryPrimitives.WriteUInt32LittleEndian(message.AsSpan(4), stream.Id);
            bytes.AsSpan(0, n).CopyTo(message.AsSpan(8));
            frame.Alone(Region.Encoded(Rect, message, message.Length));
        }
    }

    /// <summary>Its stream ends: once this returns, nothing more of it is sent (a picture being
    /// encoded is dropped), so what is sent in its place next comes after all of it.</summary>
    public void Dispose()
    {
        stopping = true;
        ready.Release();
        encoder.Join();
        stream.Dispose();
        ready.Dispose();
    }
}

/// <summary>
/// Watches what is presented for a video: the same big rectangle changing again and again — ten
/// times, each within a quarter second of the last (10 a second or more: a busy machine's video is
/// still one, and a slow one is the most in need of it). Where a video played in the last ten
/// seconds, three times is enough: a pause, an ad, a buffering moment ends a stream, and the video
/// must not stay lossless after it (lossless it is heavy, heavy it stays slow, slow it wouldn't be
/// seen as a video again).
/// </summary>
internal sealed class Motion
{
    private const int Pictures = 10, Again = 3, MinWidth = 200, MinHeight = 120;
    private const long Gap = 250;       // ms: more between two changes and it isn't playing
    private const long Remember = 10_000;   // ms a video's place is remembered after its stream ended

    private Rect seen, played;
    private int count;
    private long last, playedAt = long.MinValue / 2;

    /// <summary>A rectangle presented; true when it has become a video's place (and again every
    /// few changes it goes on being one).</summary>
    internal bool Playing(Rect r)
    {
        if (r.Width < MinWidth || r.Height < MinHeight) return false;
        var now = Environment.TickCount64;
        count = r == seen && now - last < Gap ? count + 1 : 1;
        seen = r;
        last = now;
        if (count < (r == played && now - playedAt < Remember ? Again : Pictures)) return false;
        count = 0;
        played = r;
        playedAt = now;
        return true;
    }
}

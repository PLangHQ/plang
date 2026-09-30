using System.Buffers;
using System.Buffers.Binary;

namespace app.module.screen.code.wayland;

/// <summary>
/// What the host sees: the screen as last sent, the regions of the frame being built, and the
/// messages out — [u32 length][u8 kind][payload], little-endian:
///   1 a frame: [u16 count] then per rectangle [i32 x][i32 y][u32 w][u32 h][u32 n][n bytes QOI of BGRA]
///   7 a frame that opens with a move (a window dragged): [i32 x][i32 y][u32 w][u32 h][i32 dx][i32 dy],
///     the host's pixels there move by (dx, dy); then as 1
///   2 the pointer to show: its CSS name
///   3 [u64 t]: the stamp of the input a frame answers (the host times input → picture)
///   4 [u64 t]: the same stamp, at once (the pipe's round trip)
///   5 copied text, for the host's clipboard
/// Its big buffers are made once, when the screen opens, and kept: a frame allocates next to
/// nothing, so the garbage collector has nothing big to collect while the screen runs.
/// Also answers the clients' frame callbacks that had nothing new to show, once per 16 ms tick.
/// </summary>
internal sealed class Frame
{
    private readonly Display display;
    private readonly Stream? output;
    private readonly byte[] screen;     // what the host shows now
    private readonly byte[] composing;  // the rectangle being composed
    private readonly Encoders encoders = new(Math.Min(8, Environment.ProcessorCount));
    private readonly List<Region> pending = new();
    private readonly List<WlCallback> later = new();
    private (Rect from, Point by)? moved;   // the move that opens the frame being built
    private (ulong t, long at)? stamp;

    internal Frame(Display display, Stream? output)
    {
        this.display = display;
        // written in pieces (header, then each region), sent at once
        this.output = output == null ? null : new BufferedStream(output, 1 << 20);
        var bytes = display.Size.Width * display.Size.Height * 4;
        screen = GC.AllocateArray<byte>(bytes, pinned: true);
        composing = GC.AllocateUninitializedArray<byte>(bytes, pinned: true);
    }

    /// <summary>Composes the screen inside <paramref name="area"/> — everything, bottom to top — and
    /// encodes the rows that changed, for the next frame.</summary>
    internal void Present(Rect area)
    {
        var r = area.Clip(new Rect(0, 0, display.Size.Width, display.Size.Height));
        if (r.Empty) return;
        var row = r.Width * 4;
        int first = -1, last = -1;   // the rows that changed: only they are sent
        for (var y = r.Y; y < r.Bottom; y++)
        {
            var line = composing.AsSpan((y - r.Y) * row, row);
            display.Draw(y, r.X, line);   // writes all of it: the bottom window is copied in, not blended over a cleared line
            var shown = screen.AsSpan((y * display.Size.Width + r.X) * 4, row);
            if (shown.SequenceEqual(line)) continue;
            line.CopyTo(shown);
            if (first < 0) first = y;
            last = y;
        }
        if (first < 0) return;
        var changed = new Region(new Rect(r.X, first, r.Width, last - first + 1),
            composing.AsMemory((first - r.Y) * row, (last - first + 1) * row));
        // encoded now (the composing buffer is reused by the next present), in bands at once
        var bands = changed.Bands(changed.Big ? encoders.Count : 1);
        encoders.Encode(bands);
        pending.AddRange(bands);
    }

    /// <summary>Where something was and is, composed again and sent.</summary>
    internal void Redraw(Rect was, Rect now)
    {
        if (!was.Empty && was.Overlaps(now)) Present(was.Merge(now));
        else
        {
            Present(was);
            Present(now);
        }
        Send();
    }

    /// <summary>The frame built so far — its move, if a window moved, then its regions — as one
    /// message, so the host shows all of it at once; then the stamp of the input it answers.</summary>
    internal void Send()
    {
        if (pending.Count == 0 && moved == null) return;
        if (output != null)
        {
            var size = 1 + (moved == null ? 0 : 24) + 2 + pending.Sum(region => region.Size);
            Span<byte> head = stackalloc byte[31];
            BinaryPrimitives.WriteInt32LittleEndian(head, size);
            head[4] = moved == null ? (byte)1 : (byte)7;
            var at = 5;
            if (moved is var (from, by))
            {
                BinaryPrimitives.WriteInt32LittleEndian(head[5..], from.X);
                BinaryPrimitives.WriteInt32LittleEndian(head[9..], from.Y);
                BinaryPrimitives.WriteInt32LittleEndian(head[13..], from.Width);
                BinaryPrimitives.WriteInt32LittleEndian(head[17..], from.Height);
                BinaryPrimitives.WriteInt32LittleEndian(head[21..], by.X);
                BinaryPrimitives.WriteInt32LittleEndian(head[25..], by.Y);
                at = 29;
            }
            BinaryPrimitives.WriteUInt16LittleEndian(head[at..], (ushort)pending.Count);
            try
            {
                output.Write(head[..(at + 2)]);
                foreach (var region in pending) region.WriteTo(output);
                output.Flush();
            }
            catch (IOException) { /* the host went away */ }
        }
        foreach (var region in pending) region.Done();
        pending.Clear();
        moved = null;
        // only when the frame came soon after the input: a later one is something else changing
        if (stamp is { } s && Environment.TickCount64 - s.at < 300) Stamp(3, s.t);
        stamp = null;
    }

    /// <summary>
    /// What is in <paramref name="from"/> moves by <paramref name="by"/> (a window dragged): the host
    /// moves the pixels it already has, and so does the screen as sent, so what follows is compared
    /// against the moved picture. The move opens the next frame (message 7: the move, then the
    /// regions that frame puts right), so the host never shows the moved pixels without them.
    /// </summary>
    internal void Move(Rect from, Point by)
    {
        Send();   // what came before goes first
        // what is on the screen and lands on the screen
        var screenRect = new Rect(0, 0, display.Size.Width, display.Size.Height);
        var source = from.Clip(screenRect).Clip(screenRect.Moved(new Point(-by.X, -by.Y)));
        if (source.Empty) return;
        var bytes = source.Width * 4;
        for (var i = 0; i < source.Height; i++)
        {
            var row = by.Y > 0 ? source.Bottom - 1 - i : source.Y + i;
            screen.AsSpan((row * display.Size.Width + source.X) * 4, bytes)
                .CopyTo(screen.AsSpan(((row + by.Y) * display.Size.Width + source.X + by.X) * 4, bytes));
        }
        moved = (source, by);
    }

    /// <summary>An input's stamp: echoed at once, and after the next frame.</summary>
    internal void Echo(ulong t)
    {
        Stamp(4, t);
        stamp = (t, Environment.TickCount64);
    }

    private void Stamp(byte kind, ulong t)
    {
        Span<byte> bytes = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(bytes, t);
        Message(kind, bytes);
    }

    internal void Cursor(string name) => Message(2, System.Text.Encoding.UTF8.GetBytes(name));
    internal void Clipboard(string text) => Message(5, System.Text.Encoding.UTF8.GetBytes(text));

    private void Message(byte kind, ReadOnlySpan<byte> payload)
    {
        if (output == null) return;
        Span<byte> head = stackalloc byte[5];
        BinaryPrimitives.WriteInt32LittleEndian(head, payload.Length + 1);
        head[4] = kind;
        try
        {
            output.Write(head);
            output.Write(payload);
            output.Flush();
        }
        catch (IOException) { /* the host went away */ }
    }

    /// <summary>Callbacks of commits with nothing new: answered on the next tick.</summary>
    internal void Later(IEnumerable<WlCallback> callbacks) => later.AddRange(callbacks);

    internal void Tick()
    {
        if (later.Count == 0) return;
        var time = display.Time();
        foreach (var callback in later) callback.Done(time);
        later.Clear();
    }
}

/// <summary>
/// A part of the screen that changed: its rectangle and its pixels (BGRA, a slice of the frame's
/// composing buffer until encoded). It cuts itself into bands, encodes itself (QOI, into a buffer
/// borrowed from the shared pool), writes itself into a frame message —
/// [i32 x][i32 y][u32 w][u32 h][u32 n][n bytes QOI] — and gives the buffer back when sent.
/// </summary>
internal sealed class Region(Rect rect, ReadOnlyMemory<byte> pixels)
{
    private byte[]? qoi;
    private int length;

    /// <summary>Worth encoding in parallel bands.</summary>
    internal bool Big => rect.Width * rect.Height >= 256 * 256;

    /// <summary>Its bytes in a frame message.</summary>
    internal int Size => 20 + length;

    /// <summary>This region as up to <paramref name="count"/> horizontal bands.</summary>
    internal Region[] Bands(int count)
    {
        var rows = (rect.Height + count - 1) / count;
        var row = rect.Width * 4;
        var bands = new Region[(rect.Height + rows - 1) / rows];
        for (var i = 0; i < bands.Length; i++)
        {
            var top = i * rows;
            var h = Math.Min(rows, rect.Height - top);
            bands[i] = new Region(new Rect(rect.X, rect.Y + top, rect.Width, h), pixels.Slice(top * row, h * row));
        }
        return bands;
    }

    internal void Encode()
    {
        qoi = ArrayPool<byte>.Shared.Rent(Qoi.MaxSize(rect.Width, rect.Height));
        length = new Qoi(pixels.Span, rect.Width, rect.Height).Into(qoi);
    }

    internal void WriteTo(Stream message)
    {
        Span<byte> head = stackalloc byte[20];
        BinaryPrimitives.WriteInt32LittleEndian(head, rect.X);
        BinaryPrimitives.WriteInt32LittleEndian(head[4..], rect.Y);
        BinaryPrimitives.WriteInt32LittleEndian(head[8..], rect.Width);
        BinaryPrimitives.WriteInt32LittleEndian(head[12..], rect.Height);
        BinaryPrimitives.WriteInt32LittleEndian(head[16..], length);
        message.Write(head);
        message.Write(qoi.AsSpan(0, length));
    }

    /// <summary>Sent: its buffer goes back to the pool.</summary>
    internal void Done()
    {
        if (qoi != null) ArrayPool<byte>.Shared.Return(qoi);
        qoi = null;
    }
}

/// <summary>
/// Threads that encode bands side by side, made once and kept (a thread per band per frame would
/// be made and thrown away 60 times a second). Their own threads, not the pool's: encoding runs
/// under the display's gate, and pool threads may be waiting on that gate.
/// </summary>
internal sealed class Encoders
{
    private readonly SemaphoreSlim work = new(0);
    private readonly Lock gate = new();
    private Region[] bands = [];
    private int next;
    private CountdownEvent? done;

    internal int Count { get; }

    internal Encoders(int count)
    {
        Count = count;
        for (var i = 1; i < count; i++)
            new Thread(Run) { IsBackground = true, Name = "screen encoder" }.Start();
    }

    /// <summary>Encodes <paramref name="all"/>: this thread takes a share too, then waits for the rest.</summary>
    internal void Encode(Region[] all)
    {
        if (all.Length == 1) { all[0].Encode(); return; }
        lock (gate)
        {
            bands = all;
            next = 0;
            done = new CountdownEvent(all.Length);
        }
        work.Release(Math.Min(all.Length - 1, Count - 1));
        Take();
        done.Wait();
    }

    private void Run()
    {
        while (true)
        {
            work.Wait();
            Take();
        }
    }

    /// <summary>Encodes bands until none are left.</summary>
    private void Take()
    {
        while (true)
        {
            Region band;
            CountdownEvent counter;
            lock (gate)
            {
                if (done == null || next >= bands.Length) return;
                band = bands[next++];
                counter = done;
            }
            band.Encode();
            counter.Signal();
        }
    }
}

/// <summary>
/// QOI, the "Quite OK Image" format: lossless, fast, simple to decode anywhere. The bytes are BGRA;
/// QOI doesn't care which channel is which, so they come out in the same order they went in.
/// </summary>
internal readonly ref struct Qoi(ReadOnlySpan<byte> pixels, int width, int height)
{
    private readonly ReadOnlySpan<byte> pixels = pixels;

    /// <summary>The most a picture this size can take: every pixel spelled out, plus header and end.</summary>
    internal static int MaxSize(int width, int height) => 14 + width * height * 5 + 8;

    /// <summary>Writes the picture into <paramref name="output"/>; returns how many bytes it took.
    /// Pixels are read as 32-bit words; a run of the same pixel — most of a web page — is found with
    /// one vectorized search, not pixel by pixel.</summary>
    internal int Into(Span<byte> output)
    {
        "qoif"u8.CopyTo(output);
        BinaryPrimitives.WriteUInt32BigEndian(output[4..], (uint)width);
        BinaryPrimitives.WriteUInt32BigEndian(output[8..], (uint)height);
        output[12] = 4;
        output[13] = 0;
        var o = 14;
        var all = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(pixels);
        Span<uint> index = stackalloc uint[64];
        index.Clear();
        var previous = 0xFF000000u;   // r 0, g 0, b 0, a 255 (byte 0 is r, byte 3 is a)
        byte pr = 0, pg = 0, pb = 0, pa = 255;
        for (var i = 0; i < all.Length; i++)
        {
            var pixel = all[i];
            if (pixel == previous)
            {
                // the run: as far as the same pixel goes, in pieces of up to 62 — a short one (text)
                // counted here, a long one (a background) with vectors
                var run = 1;
                while (run < 8 && i + run < all.Length && all[i + run] == previous) run++;
                if (run == 8)
                {
                    var other = all[(i + 8)..].IndexOfAnyExcept(previous);
                    run = other < 0 ? all.Length - i : 8 + other;
                }
                i += run - 1;
                for (; run > 62; run -= 62) output[o++] = 0xC0 | 61;
                output[o++] = (byte)(0xC0 | (run - 1));
                continue;
            }
            byte r = unchecked((byte)pixel), g = unchecked((byte)(pixel >> 8)), b = unchecked((byte)(pixel >> 16)), a = (byte)(pixel >> 24);
            var h = (r * 3 + g * 5 + b * 7 + a * 11) % 64;
            if (index[h] == pixel)
                output[o++] = (byte)h;
            else
            {
                index[h] = pixel;
                if (a == pa)
                {
                    int vr = unchecked((sbyte)(byte)(r - pr)), vg = unchecked((sbyte)(byte)(g - pg)), vb = unchecked((sbyte)(byte)(b - pb));
                    int vgr = vr - vg, vgb = vb - vg;
                    if (vr is > -3 and < 2 && vg is > -3 and < 2 && vb is > -3 and < 2)
                        output[o++] = (byte)(0x40 | ((vr + 2) << 4) | ((vg + 2) << 2) | (vb + 2));
                    else if (vgr is > -9 and < 8 && vg is > -33 and < 32 && vgb is > -9 and < 8)
                    {
                        output[o++] = (byte)(0x80 | (vg + 32));
                        output[o++] = (byte)(((vgr + 8) << 4) | (vgb + 8));
                    }
                    else { output[o++] = 0xFE; output[o++] = r; output[o++] = g; output[o++] = b; }
                }
                else { output[o++] = 0xFF; output[o++] = r; output[o++] = g; output[o++] = b; output[o++] = a; }
            }
            pr = r; pg = g; pb = b; pa = a;
            previous = pixel;
        }
        for (var i = 0; i < 7; i++) output[o++] = 0;
        output[o++] = 1;
        return o;
    }
}

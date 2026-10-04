using System.Buffers.Binary;
using System.Text.Json;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace app.module.browser.type.video;

/// <summary>
/// A video a page plays that the host plays itself (pass-through): the page's stand-in gives its chunks (fragmented
/// MP4, <see cref="code.Mp4"/>), its clock and its place; they go to the host beside the screen's frames, little-endian:
///   10 it starts (again, when the player changes quality): [u32 id][4 codec, ASCII][u16 width][u16 height][config]
///   11 a sample: [u32 id][f64 time it shows][f64 time it is decoded][f64 duration][u8 key frame][coded picture]
///   12 its clock and place: [u32 id][f64 time][u8 playing][f32 rate][i32 x][i32 y][i32 w][i32 h][u8 shown]
///      [u8 r][u8 g][u8 b] — on the screen; the host shows the picture for <c>time</c> there, where the page shows
///      the key colour (r, g, b), under what the page draws over it (the player's controls, captions)
///   13 it ends: [u32 id]
/// </summary>
internal sealed class @this(uint id, Action<byte, byte[]> send)
{
    private readonly code.Mp4 _mp4 = new();
    private bool _started;

    internal uint Id { get; } = id;

    // what the player appended and isn't read yet: a player may cut a segment anywhere (a moof here, its mdat in the
    // next append, or a box in two)
    private byte[] _pending = [];
    private int _sent;   // samples of the moof being read sent already (its mdat still coming)
    private double _offset, _first = double.NaN, _last = double.NaN;

    /// <summary>A chunk the player appended: appended to what came before, then each whole box read — an init
    /// segment (ftyp, moov) starts (or restarts) the stream; a moof with the mdat after it, its samples to the host.
    /// <paramref name="offset"/> is the source buffer's timestampOffset.</summary>
    internal void Chunk(byte[] data, double offset)
    {
        // an append that starts a segment (or an init segment: a quality change) starts afresh: a player may leave a
        // segment half appended and start another — what was half read is dropped. (An mdat's next bytes beginning
        // with a segment's first box is too unlikely to matter.)
        if (data.Length >= 8 && System.Text.Encoding.ASCII.GetString(data, 4, 4) is "ftyp" or "moov" or "moof" or "styp" or "sidx" or "emsg")
            (_pending, _sent) = ([], 0);
        var bytes = _pending.Length == 0 ? data : [.. _pending, .. data];
        var at = 0;
        while (Whole(bytes, at, out var type, out var end))
        {
            if (type is "ftyp" or "moov")
            {
                // the init segment is ftyp then moov: read once moov is whole too
                var moov = type == "moov" ? (at, end) : Whole(bytes, end, out var next, out var moovEnd) && next == "moov" ? (at, moovEnd) : (-1, -1);
                if (moov.Item1 < 0) break;
                Init(bytes[moov.Item1..moov.Item2]);
                at = moov.Item2;
            }
            else if (type == "moof")
            {
                // a moof's samples are in the mdat after it, which a player may append bit by bit as it downloads:
                // each sample goes as soon as its bytes are here, the rest when they come
                if (!Whole(bytes, end, out var next, out var mdatEnd))
                {
                    _sent += Media(bytes.AsMemory(at), offset, _sent);
                    break;
                }
                if (next == "mdat") Media(bytes.AsMemory(at, mdatEnd - at), offset, _sent);
                _sent = 0;
                at = next == "mdat" ? mdatEnd : end;
            }
            else at = end;   // anything else (sidx, styp, emsg) isn't needed
        }
        _pending = bytes[at..];
        // what waits must be a box beginning: a type of letters and a size a segment could have — otherwise the
        // reading lost its place, and waits for the next segment's start instead of forever
        if (_pending.Length >= 8 && !Plausible(_pending)) (_pending, _sent) = ([], 0);
    }

    private static bool Plausible(byte[] box)
    {
        var size = BinaryPrimitives.ReadUInt32BigEndian(box);
        if (size != 1 && (size < 8 || size > 256 << 20)) return false;
        for (var i = 4; i < 8; i++)
            if (box[i] is not ((>= (byte)'a' and <= (byte)'z') or (>= (byte)'A' and <= (byte)'Z') or (>= (byte)'0' and <= (byte)'9') or (byte)' '))
                return false;
        return true;
    }

    /// <summary>What it holds now: bytes waiting for the rest of their box, samples of that box sent.</summary>
    internal string Holding => $"offset {_offset:F3}, samples sent so far {_first:F3}…{_last:F3}; {_pending.Length} B waiting{(_pending.Length >= 8 ? " (" + System.Text.Encoding.ASCII.GetString(_pending, 4, 4) + " of " + BinaryPrimitives.ReadUInt32BigEndian(_pending) + " B)" : "")}, {_sent} of its samples sent";

    /// <summary>The player aborted its append (SourceBuffer.abort): what was half appended is dropped.</summary>
    internal void Abort() => (_pending, _sent) = ([], 0);

    // a whole box starts at at: its type and where it ends
    private static bool Whole(byte[] bytes, int at, out string type, out int end)
    {
        type = ""; end = 0;
        if (at + 8 > bytes.Length) return false;
        long size = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(at));
        if (size == 1)
        {
            if (at + 16 > bytes.Length) return false;
            size = (long)BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(at + 8));
        }
        if (size < 8 || at + size > bytes.Length) return false;
        type = System.Text.Encoding.ASCII.GetString(bytes, at + 4, 4);
        end = (int)(at + size);
        return true;
    }

    // an init segment: the stream starts (again) with its codec, size and configuration
    private void Init(byte[] data)
    {
        if (!_mp4.Init(data)) return;
        _started = true;
        var codec = System.Text.Encoding.ASCII.GetBytes(_mp4.Codec.PadRight(4)[..4]);
        var m = new byte[12 + _mp4.Config.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(m, Id);
        codec.CopyTo(m, 4);
        BinaryPrimitives.WriteUInt16LittleEndian(m.AsSpan(8), (ushort)_mp4.Width);
        BinaryPrimitives.WriteUInt16LittleEndian(m.AsSpan(10), (ushort)_mp4.Height);
        _mp4.Config.CopyTo(m, 12);
        send(10, m);
    }

    // a moof and its mdat, or as much of the mdat as is here: each whole sample after the first <paramref name="skip"/>
    // to the host; how many it sent
    private int Media(ReadOnlyMemory<byte> data, double offset, int skip)
    {
        if (!_started) return 0;
        var samples = _mp4.Samples(data);
        _offset = offset;
        foreach (var s in samples.Skip(skip))
        {
            if (double.IsNaN(_first)) _first = s.Time + offset;
            _last = s.Time + offset;
            var m = new byte[29 + s.Bytes.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(m, Id);
            BinaryPrimitives.WriteDoubleLittleEndian(m.AsSpan(4), s.Time + offset);
            BinaryPrimitives.WriteDoubleLittleEndian(m.AsSpan(12), s.Decode + offset);
            BinaryPrimitives.WriteDoubleLittleEndian(m.AsSpan(20), s.Duration);
            m[28] = s.Key ? (byte)1 : (byte)0;
            s.Bytes.Span.CopyTo(m.AsSpan(29));
            send(11, m);
        }
        return Math.Max(0, samples.Count - skip);
    }

    /// <summary>Its clock and place: the page's <paramref name="clock"/> ({time, playing, rate, x, y, w, h, shown, key}),
    /// its place moved by where the page is on the screen (<paramref name="page"/>; none: not shown).</summary>
    internal void Clock(JsonElement clock, Rect? page)
    {
        var m = new byte[37];
        BinaryPrimitives.WriteUInt32LittleEndian(m, Id);
        BinaryPrimitives.WriteDoubleLittleEndian(m.AsSpan(4), Number(clock, "time"));
        m[12] = clock.TryGetProperty("playing", out var p) && p.ValueKind == JsonValueKind.True ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteSingleLittleEndian(m.AsSpan(13), (float)Number(clock, "rate", 1));
        var at = page ?? default;
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(17), at.X + (int)Number(clock, "x"));
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(21), at.Y + (int)Number(clock, "y"));
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(25), (int)Number(clock, "w"));
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(29), (int)Number(clock, "h"));
        m[33] = page != null && clock.TryGetProperty("shown", out var s) && s.ValueKind == JsonValueKind.True ? (byte)1 : (byte)0;
        var (r, g, b) = Key(clock.TryGetProperty("key", out var k) ? k.GetString() : null);
        (m[34], m[35], m[36]) = (r, g, b);
        send(12, m);
    }

    /// <summary>It ends: the host lets go of it.</summary>
    internal void End()
    {
        var m = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(m, Id);
        send(13, m);
    }

    private static double Number(JsonElement e, string name, double otherwise = 0)
        => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : otherwise;

    // "rgb(1, 2, 3)" → (1, 2, 3)
    private static (byte, byte, byte) Key(string? css)
    {
        var parts = (css ?? "").Split(['(', ')', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 4 && byte.TryParse(parts[1], out var r) && byte.TryParse(parts[2], out var g) && byte.TryParse(parts[3], out var b)
            ? (r, g, b) : ((byte)1, (byte)2, (byte)3);
    }
}

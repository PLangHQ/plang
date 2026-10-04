using System.Buffers.Binary;
using System.Text.Json;
using Display = app.module.screen.type.screen.display.code.Display;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace app.module.browser.type.video;

/// <summary>
/// A video a page plays that the host plays itself (pass-through): the page's stand-in gives its chunks (fragmented
/// MP4, <see cref="code.Mp4"/>), its clock and its place; they go to the host beside the screen's frames, little-endian:
///   10 it starts (again, when the player changes quality): [u32 id][4 codec, ASCII][u16 width][u16 height][config]
///   11 a sample: [u32 id][f64 time][f64 duration][u8 key frame][coded picture]
///   12 its clock and place: [u32 id][f64 time][u8 playing][f32 rate][i32 x][i32 y][i32 w][i32 h][u8 shown]
///      [u8 r][u8 g][u8 b] — on the screen; the host shows the picture for <c>time</c> there, where the page shows
///      the key colour (r, g, b), under what the page draws over it (the player's controls, captions)
///   13 it ends: [u32 id]
/// </summary>
internal sealed class @this(uint id, Display display)
{
    private readonly code.Mp4 _mp4 = new();
    private bool _started;

    internal uint Id { get; } = id;

    /// <summary>A chunk the player appended: an init segment starts (or restarts) the stream, a media segment's
    /// samples go to the host. <paramref name="offset"/> is the source buffer's timestampOffset.</summary>
    internal void Chunk(byte[] data, double offset)
    {
        if (Starts(data))
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
            display.Frame.Media(10, m);
            return;
        }
        if (!_started) return;
        foreach (var s in _mp4.Samples(data))
        {
            var m = new byte[21 + s.Bytes.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(m, Id);
            BinaryPrimitives.WriteDoubleLittleEndian(m.AsSpan(4), s.Time + offset);
            BinaryPrimitives.WriteDoubleLittleEndian(m.AsSpan(12), s.Duration);
            m[20] = s.Key ? (byte)1 : (byte)0;
            s.Bytes.Span.CopyTo(m.AsSpan(21));
            display.Frame.Media(11, m);
        }
    }

    // an init segment: it opens with ftyp (or goes straight to moov)
    private static bool Starts(byte[] data)
        => data.Length >= 8 && (data.AsSpan(4, 4).SequenceEqual("ftyp"u8) || data.AsSpan(4, 4).SequenceEqual("moov"u8));

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
        display.Frame.Media(12, m);
    }

    /// <summary>It ends: the host lets go of it.</summary>
    internal void End()
    {
        var m = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(m, Id);
        display.Frame.Media(13, m);
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

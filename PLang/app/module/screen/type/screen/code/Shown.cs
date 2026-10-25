using System.Buffers;
using System.Buffers.Binary;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace app.module.screen.type.screen.code;

/// <summary>
/// What the host shows of PlangOS's screen: its pixels (BGRA), kept up to date by the frame messages PlangOS sends —
/// [1][u16 count] then per rectangle [i32 x][i32 y][u32 w][u32 h][u32 n][n bytes]: QOI, or a video's picture ("h264"
/// [u32 stream] then H.264, decoded by its video); or [7] a move ([i32 x][i32 y][u32 w][u32 h][i32 dx][i32 dy], the
/// pixels the host has move by (dx, dy)) and then the same. A frame goes in whole, under its lock: no one reading the
/// pixels sees half of it (moved pixels without what the move uncovered). A rectangle whose pixels would take more
/// than <c>screen.setting.max</c> is refused before anything is decoded, whatever its message says it holds.
/// </summary>
internal sealed class Shown
{
    private readonly Func<uint, ReadOnlyMemory<byte>, int, int, byte[]?>? _video;

    /// <summary>A blank screen <paramref name="width"/>×<paramref name="height"/>; a video's pictures decoded by
    /// <paramref name="video"/>.</summary>
    internal Shown(int width, int height, Func<uint, ReadOnlyMemory<byte>, int, int, byte[]?>? video = null)
        : this(new Bitmap(width, height), video) { }

    /// <summary>A screen showing <paramref name="bitmap"/>, a picture presented whole: the screen is its size.</summary>
    internal Shown(Bitmap bitmap, Func<uint, ReadOnlyMemory<byte>, int, int, byte[]?>? video = null)
    {
        Bitmap = bitmap;
        _video = video;
    }

    /// <summary>The picture. Its pixels are read under <see cref="Lock"/>.</summary>
    internal Bitmap Bitmap { get; }

    internal Lock Lock { get; } = new();

    // one rectangle of a frame as it arrived: its place and its bytes (QOI, or "h264" [u32 stream] and a video's picture)
    private readonly record struct Part(int X, int Y, int W, int H, ReadOnlyMemory<byte> Bytes)
    {
        internal bool IsVideo => Bytes.Length > 8 && Bytes.Span[..4].SequenceEqual("h264"u8);
    }

    /// <summary>A frame message (1 or 7) applied, each rectangle at most <paramref name="max"/>; the area it changed,
    /// or none (not a frame, or nothing in it). A rectangle past the max is an AppException carrying BadFrame.</summary>
    internal Rect? Apply(byte[] m, global::app.type.item.size.@this max)
    {
        if (m.Length < 3 || (m[0] != 1 && m[0] != 7)) return null;
        var at = m[0] == 7 ? 25 : 1;
        if (m.Length < at + 2) return null;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(m.AsSpan(at));
        var parts = new Part[count];
        at += 2;
        for (var i = 0; i < count; i++)
        {
            if (at + 20 > m.Length) return null;
            int x = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(at)), y = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(at + 4));
            int w = (int)BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(at + 8)), h = (int)BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(at + 12));
            var n = (int)BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(at + 16));
            if (at + 20 + n > m.Length) return null;
            parts[i] = new Part(x, y, w, h, m.AsMemory(at + 20, n));
            at += 20 + n;
        }
        return Put(parts, max, m[0] == 7 ? m : null);
    }

    /// <summary>A text frame applied — <c>{"rects":[[x,y,w,h,"&lt;qoi base64&gt;"],…]}</c> (one frame, a big change in
    /// bands) or <c>{"rect":[x,y,w,h],"qoi":"…"}</c> (one rectangle) — each rectangle at most <paramref name="max"/>; the
    /// area it changed, or none. A frame that can't be read (a rectangle past the max, one whose QOI isn't base64, a
    /// line that isn't such a frame) is an AppException carrying BadFrame, the frame refused whole.</summary>
    internal Rect? Apply(string line, global::app.type.item.size.@this max)
    {
        Part[] parts;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(line);
            var root = doc.RootElement;
            parts = root.TryGetProperty("rects", out var list)
                ? list.EnumerateArray().Select(e => new Part(e[0].GetInt32(), e[1].GetInt32(), e[2].GetInt32(), e[3].GetInt32(),
                    Convert.FromBase64String(e[4].GetString()!))).ToArray()
                : root.TryGetProperty("rect", out var r) && root.TryGetProperty("qoi", out var q)
                    ? [new Part(r[0].GetInt32(), r[1].GetInt32(), r[2].GetInt32(), r[3].GetInt32(), Convert.FromBase64String(q.GetString()!))]
                    : [];
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException
                                       or IndexOutOfRangeException or KeyNotFoundException or ArgumentNullException)
        {
            var error = new global::app.error.@this($"Not an image: the frame line can't be read: {ex.Message}", "BadFrame", 400);
            error.list.Add(global::app.error.@this.FromException(ex));
            throw new global::app.error.AppException(error, ex);
        }
        return Put(parts, max);
    }

    // The one door for a frame's rectangles, whichever form they came in: every one bounded by the max first (refused
    // before anything is decoded), then decoded off the lock — a video's picture in order, the lossless ones on all
    // cores — then the move (a kind-7 frame's) and the rectangles cut to the screen under the lock, so no one reading
    // the pixels sees half of it.
    private Rect? Put(Part[] parts, global::app.type.item.size.@this max, byte[]? move = null)
    {
        foreach (var part in parts) Bound(part, max);
        // the frame's rectangles together fit the max too, not each alone: a frame of many is no larger than one
        var pixels = parts.Sum(part => (long)part.W * part.H);
        if (!Bitmap.Fits(pixels, max))
            throw new global::app.error.AppException(new global::app.error.@this(
                $"Not an image: a frame's {parts.Length} rectangles claim {pixels} pixels together, more than screen.setting.max ({max}) holds; raise it with --screen.setting.max=1GiB",
                "BadFrame", 400));
        var decoded = new byte[]?[parts.Length];
        var rented = new bool[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            if (parts[i].IsVideo)
                decoded[i] = _video?.Invoke(BinaryPrimitives.ReadUInt32LittleEndian(parts[i].Bytes.Span[4..]), parts[i].Bytes[8..], parts[i].W, parts[i].H);
        Parallel.For(0, parts.Length, i =>
        {
            if (parts[i].IsVideo) return;
            if (Decoded(parts[i].Bytes.Span, parts[i].W, parts[i].H) is { } pixels) { decoded[i] = pixels; rented[i] = true; }
        });
        var changed = default(Rect);
        lock (Lock)
        {
            if (move != null && Move(move) is { } moved) changed = changed.Merge(moved);
            for (var i = 0; i < parts.Length; i++)
                if (decoded[i] is { } px && Copy(parts[i].X, parts[i].Y, parts[i].W, parts[i].H, px) is { } area)
                    changed = changed.Merge(area);
        }
        for (var i = 0; i < parts.Length; i++)
            if (rented[i]) ArrayPool<byte>.Shared.Return(decoded[i]!);
        return changed.Empty ? null : changed;
    }

    /// <summary>A picture the screen's size put in whole, at 0,0.</summary>
    internal void Put(Bitmap picture)
    {
        lock (Lock) picture.Pixels.AsSpan(0, Math.Min(picture.Pixels.Length, Bitmap.Pixels.Length)).CopyTo(Bitmap.Pixels);
    }

    // a rectangle's pixels may take at most the max, by the picture's own rule, and a QOI one no more than its bytes can
    // hold (a video's picture is its decoder's to judge)
    private static void Bound(Part part, global::app.type.item.size.@this max)
    {
        var why = !Bitmap.Fits(part.W, part.H, max)
            ? $"a rectangle claims {part.W}×{part.H}, more pixels than screen.setting.max ({max}) holds; raise it with --screen.setting.max=1GiB"
            : !part.IsVideo && !Bitmap.Encodes(part.W, part.H, part.Bytes.Length)
                ? $"a rectangle claims {part.W}×{part.H}, more pixels than its {part.Bytes.Length} bytes of QOI can hold"
                : null;
        if (why != null)
            throw new global::app.error.AppException(new global::app.error.@this("Not an image: " + why, "BadFrame", 400));
    }

    // a QOI rectangle's pixels, in a rented buffer the caller gives back; null when it isn't QOI of that size
    private static byte[]? Decoded(ReadOnlySpan<byte> qoi, int w, int h)
    {
        var bytes = w * h * 4;
        var pixels = ArrayPool<byte>.Shared.Rent(bytes);
        if (Qoi.Decode(qoi, pixels.AsSpan(0, bytes))) return pixels;
        ArrayPool<byte>.Shared.Return(pixels);
        return null;
    }

    // the move: what is on the screen both where it is and where it lands, row by row in the order that doesn't
    // overwrite what is still to be moved (within a row the copy is overlap-safe)
    private Rect? Move(byte[] m)
    {
        int x = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(1)), y = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(5));
        int w = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(9)), h = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(13));
        int dx = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(17)), dy = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(21));
        var left = Math.Max(x, Math.Max(0, -dx));
        var top = Math.Max(y, Math.Max(0, -dy));
        var right = Math.Min(x + w, Math.Min(Bitmap.Width, Bitmap.Width - dx));
        var bottom = Math.Min(y + h, Math.Min(Bitmap.Height, Bitmap.Height - dy));
        if (right <= left || bottom <= top) return null;
        var bytes = (right - left) * 4;
        for (var i = 0; i < bottom - top; i++)
        {
            var row = dy > 0 ? bottom - 1 - i : top + i;
            Bitmap.Pixels.AsSpan((row * Bitmap.Width + left) * 4, bytes).CopyTo(Bitmap.Pixels.AsSpan(((row + dy) * Bitmap.Width + left + dx) * 4, bytes));
        }
        return new Rect(left + dx, top + dy, right - left, bottom - top);
    }

    // a rectangle's pixels in, cut to the screen
    private Rect? Copy(int x, int y, int w, int h, byte[] rect)
    {
        var rows = Math.Min(h, Bitmap.Height - y);
        var cols = Math.Min(w, Bitmap.Width - x);
        if (rows <= 0 || cols <= 0 || x < 0 || y < 0) return null;
        for (var row = 0; row < rows; row++)
            rect.AsSpan(row * w * 4, cols * 4).CopyTo(Bitmap.Pixels.AsSpan(((y + row) * Bitmap.Width + x) * 4));
        return new Rect(x, y, cols, rows);
    }
}

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

    /// <summary>A frame message (1 or 7) applied, each rectangle at most <paramref name="max"/>; the area it changed,
    /// or none (not a frame, or nothing in it). A rectangle past the max is an AppException carrying BadFrame.</summary>
    internal Rect? Apply(byte[] m, global::app.type.item.size.@this max)
    {
        if (m.Length < 3 || (m[0] != 1 && m[0] != 7)) return null;
        var at = m[0] == 7 ? 25 : 1;
        if (m.Length < at + 2) return null;
        var count = BinaryPrimitives.ReadUInt16LittleEndian(m.AsSpan(at));
        var parts = new (int x, int y, int w, int h, int at, int n)[count];
        at += 2;
        for (var i = 0; i < count; i++)
        {
            if (at + 20 > m.Length) return null;
            int x = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(at)), y = BinaryPrimitives.ReadInt32LittleEndian(m.AsSpan(at + 4));
            int w = (int)BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(at + 8)), h = (int)BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(at + 12));
            var n = (int)BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(at + 16));
            if (at + 20 + n > m.Length) return null;
            Bound(w, h, max);
            parts[i] = (x, y, w, h, at + 20, n);
            at += 20 + n;
        }
        // each rectangle decoded off the lock: a video's picture in order, the lossless ones on all cores
        var decoded = new byte[]?[count];
        var rented = new bool[count];
        for (var i = 0; i < count; i++)
            if (IsVideo(m, parts[i].at, parts[i].n))
                decoded[i] = _video?.Invoke(BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(parts[i].at + 4)),
                    m.AsMemory(parts[i].at + 8, parts[i].n - 8), parts[i].w, parts[i].h);
        Parallel.For(0, count, i =>
        {
            if (IsVideo(m, parts[i].at, parts[i].n)) return;
            if (Decoded(m.AsSpan(parts[i].at, parts[i].n), parts[i].w, parts[i].h) is { } pixels) { decoded[i] = pixels; rented[i] = true; }
        });
        var changed = default(Rect);
        lock (Lock)
        {
            if (m[0] == 7 && Move(m) is { } moved) changed = changed.Merge(moved);
            for (var i = 0; i < count; i++)
                if (decoded[i] is { } px && Copy(parts[i].x, parts[i].y, parts[i].w, parts[i].h, px) is { } area)
                    changed = changed.Merge(area);
        }
        for (var i = 0; i < count; i++)
            if (rented[i]) ArrayPool<byte>.Shared.Return(decoded[i]!);
        return changed.Empty ? null : changed;
    }

    /// <summary>One rectangle as it arrives — its place and its QOI — put in: refused past <paramref name="max"/>
    /// (an AppException carrying BadFrame) before it is decoded, then decoded off the lock and cut to the screen under
    /// it. The area it changed, or none.</summary>
    internal Rect? Put(int x, int y, int w, int h, ReadOnlySpan<byte> qoi, global::app.type.item.size.@this max)
    {
        Bound(w, h, max);
        if (Decoded(qoi, w, h) is not { } pixels) return null;
        try
        {
            lock (Lock) return Copy(x, y, w, h, pixels);
        }
        finally { ArrayPool<byte>.Shared.Return(pixels); }
    }

    /// <summary>A picture the screen's size put in whole, at 0,0.</summary>
    internal void Put(Bitmap picture)
    {
        lock (Lock) picture.Pixels.AsSpan(0, Math.Min(picture.Pixels.Length, Bitmap.Pixels.Length)).CopyTo(Bitmap.Pixels);
    }

    // a rectangle's pixels may take at most the max, by the picture's own rule
    private static void Bound(int w, int h, global::app.type.item.size.@this max)
    {
        if (!Bitmap.Fits(w, h, max))
            throw new global::app.error.AppException(new global::app.error.@this(
                $"Not an image: a rectangle claims {w}×{h}, more pixels than screen.setting.max ({max}) holds; raise it with --screen.setting.max=1GiB",
                "BadFrame", 400));
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

    private static bool IsVideo(byte[] m, int at, int n) => n > 8 && m.AsSpan(at, 4).SequenceEqual("h264"u8);

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

using System.Buffers;
using System.Buffers.Binary;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace app.module.screen.type.screen.code;

/// <summary>
/// What the host shows of PlangOS's screen: its pixels (BGRA), kept up to date by the frame messages PlangOS sends —
/// [1][u16 count] then per rectangle [i32 x][i32 y][u32 w][u32 h][u32 n][n bytes]: QOI, or a video's picture ("h264"
/// [u32 stream] then H.264, decoded by <paramref name="video"/>); or [7] a move ([i32 x][i32 y][u32 w][u32 h][i32 dx]
/// [i32 dy], the pixels the host has move by (dx, dy)) and then the same. A frame goes in whole, under its lock: no
/// one reading the pixels sees half of it (moved pixels without what the move uncovered).
/// </summary>
internal sealed class Shown(int width, int height, Func<uint, ReadOnlyMemory<byte>, int, int, byte[]?>? video = null)
{
    internal int Width { get; } = width;
    internal int Height { get; } = height;

    /// <summary>The picture: <see cref="Width"/> × <see cref="Height"/> BGRA pixels. Read under <see cref="Lock"/>.</summary>
    internal byte[] Pixels { get; } = GC.AllocateArray<byte>(width * height * 4, pinned: true);

    internal Lock Lock { get; } = new();

    /// <summary>A frame message (1 or 7) applied; the area it changed, or none (not a frame, or nothing in it).</summary>
    internal Rect? Apply(byte[] m)
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
            parts[i] = (x, y, w, h, at + 20, n);
            at += 20 + n;
        }
        // each rectangle decoded off the lock: a video's picture in order, the lossless ones on all cores
        var decoded = new byte[]?[count];
        var rented = new bool[count];
        for (var i = 0; i < count; i++)
            if (IsVideo(m, parts[i].at, parts[i].n))
                decoded[i] = video?.Invoke(BinaryPrimitives.ReadUInt32LittleEndian(m.AsSpan(parts[i].at + 4)),
                    m.AsMemory(parts[i].at + 8, parts[i].n - 8), parts[i].w, parts[i].h);
        Parallel.For(0, count, i =>
        {
            if (IsVideo(m, parts[i].at, parts[i].n)) return;
            var bytes = parts[i].w * parts[i].h * 4;
            var pixels = ArrayPool<byte>.Shared.Rent(bytes);
            if (Qoi.Decode(m.AsSpan(parts[i].at, parts[i].n), pixels.AsSpan(0, bytes))) { decoded[i] = pixels; rented[i] = true; }
            else ArrayPool<byte>.Shared.Return(pixels);
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
        var right = Math.Min(x + w, Math.Min(Width, Width - dx));
        var bottom = Math.Min(y + h, Math.Min(Height, Height - dy));
        if (right <= left || bottom <= top) return null;
        var bytes = (right - left) * 4;
        for (var i = 0; i < bottom - top; i++)
        {
            var row = dy > 0 ? bottom - 1 - i : top + i;
            Pixels.AsSpan((row * Width + left) * 4, bytes).CopyTo(Pixels.AsSpan(((row + dy) * Width + left + dx) * 4, bytes));
        }
        return new Rect(left + dx, top + dy, right - left, bottom - top);
    }

    // a rectangle's pixels in, cut to the screen
    private Rect? Copy(int x, int y, int w, int h, byte[] rect)
    {
        var rows = Math.Min(h, Height - y);
        var cols = Math.Min(w, Width - x);
        if (rows <= 0 || cols <= 0 || x < 0 || y < 0) return null;
        for (var row = 0; row < rows; row++)
            rect.AsSpan(row * w * 4, cols * 4).CopyTo(Pixels.AsSpan(((y + row) * Width + x) * 4));
        return new Rect(x, y, cols, rows);
    }
}

namespace app.module.screen.type.screen.display.code;

internal readonly record struct Point(int X, int Y)
{
    public static Point operator +(Point a, Point b) => new(a.X + b.X, a.Y + b.Y);
    public static Point operator -(Point a, Point b) => new(a.X - b.X, a.Y - b.Y);
}

/// <summary>A rectangle on the screen (or in a surface).</summary>
internal readonly record struct Rect(int X, int Y, int Width, int Height)
{
    internal static Rect At(Point at, int width, int height) => new(at.X, at.Y, width, height);
    internal Point Corner => new(X, Y);
    internal int Right => X + Width;
    internal int Bottom => Y + Height;
    internal bool Empty => Width <= 0 || Height <= 0;
    internal bool Contains(int x, int y) => x >= X && y >= Y && x < Right && y < Bottom;
    internal bool Overlaps(Rect o) => !Empty && !o.Empty && X < o.Right && o.X < Right && Y < o.Bottom && o.Y < Bottom;
    internal Rect Moved(Point by) => this with { X = X + by.X, Y = Y + by.Y };
    internal Rect Grown(int by) => new(X - by, Y - by, Width + 2 * by, Height + 2 * by);

    /// <summary>The smallest rectangle holding both (an empty one adds nothing).</summary>
    internal Rect Merge(Rect o)
    {
        if (Empty) return o;
        if (o.Empty) return this;
        int x = Math.Min(X, o.X), y = Math.Min(Y, o.Y);
        return new Rect(x, y, Math.Max(Right, o.Right) - x, Math.Max(Bottom, o.Bottom) - y);
    }

    /// <summary>What is left of this rectangle without <paramref name="o"/>: up to four rectangles
    /// (above, below, left, right of it).</summary>
    internal IEnumerable<Rect> Without(Rect o)
    {
        if (!Overlaps(o)) { yield return this; yield break; }
        if (o.Y > Y) yield return new Rect(X, Y, Width, o.Y - Y);
        if (o.Bottom < Bottom) yield return new Rect(X, o.Bottom, Width, Bottom - o.Bottom);
        int top = Math.Max(Y, o.Y), bottom = Math.Min(Bottom, o.Bottom);
        if (o.X > X) yield return new Rect(X, top, o.X - X, bottom - top);
        if (o.Right < Right) yield return new Rect(o.Right, top, Right - o.Right, bottom - top);
    }

    internal Rect Clip(Rect to)
    {
        int x = Math.Max(X, to.X), y = Math.Max(Y, to.Y);
        return new Rect(x, y, Math.Max(0, Math.Min(Right, to.Right) - x), Math.Max(0, Math.Min(Bottom, to.Bottom) - y));
    }
}

/// <summary>
/// Pixels (BGRA, premultiplied — as Wayland clients give them) and where they are. A picture draws
/// itself over a row of the screen: opaque ones copy, others blend (Chromium's shadows, rounded
/// corners, the title bars).
/// </summary>
internal sealed class Picture(Rect rect, byte[] pixels, bool opaque)
{
    internal static readonly Picture None = new(default, [], true);

    internal Rect Rect { get; private set; } = rect;
    internal byte[] Pixels { get; } = pixels;
    internal bool Opaque { get; } = opaque;
    internal bool Empty => Pixels.Length == 0;

    internal void Place(Point at) => Rect = Rect with { X = at.X, Y = at.Y };

    /// <summary>This picture as the bottom of <paramref name="line"/> (the screen's row
    /// <paramref name="y"/> from column <paramref name="x0"/>): its pixels copied in — over nothing, a
    /// premultiplied pixel is itself, so there is nothing to blend — and the rest of the line cleared.</summary>
    internal void Base(int y, int x0, Span<byte> line)
    {
        var r = Rect;
        int sx = Math.Max(x0, r.X), ex = Math.Min(x0 + line.Length / 4, r.Right);
        var src = ((y - r.Y) * r.Width + (sx - r.X)) * 4;
        var n = (ex - sx) * 4;
        if (Empty || y < r.Y || y >= r.Bottom || ex <= sx || src + n > Pixels.Length)
        {
            line.Clear();
            return;
        }
        line[..((sx - x0) * 4)].Clear();
        Pixels.AsSpan(src, n).CopyTo(line.Slice((sx - x0) * 4, n));
        line[((ex - x0) * 4)..].Clear();
    }

    /// <summary>This picture over <paramref name="line"/>: the screen's row <paramref name="y"/>
    /// from column <paramref name="x0"/>.</summary>
    internal void Draw(int y, int x0, Span<byte> line)
    {
        var r = Rect;
        if (Empty || y < r.Y || y >= r.Bottom) return;
        var x1 = x0 + line.Length / 4;
        int sx = Math.Max(x0, r.X), ex = Math.Min(x1, r.Right);
        if (ex <= sx) return;
        var src = ((y - r.Y) * r.Width + (sx - r.X)) * 4;
        var n = (ex - sx) * 4;
        if (src + n > Pixels.Length) return;
        var from = Pixels.AsSpan(src, n);
        var to = line.Slice((sx - x0) * 4, n);
        if (Opaque)
        {
            from.CopyTo(to);
            return;
        }
        // by runs: a stretch of opaque pixels (most of a page) copied at once, a clear one skipped, only the pixels
        // between them (shadows, rounded corners, anti-aliased edges) blended one by one
        var source = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(from);
        var target = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(to);
        for (var i = 0; i < source.Length;)
        {
            var run = Run(source[i..], Solid);
            if (run > 0)
            {
                source.Slice(i, run).CopyTo(target.Slice(i, run));
                i += run;
                continue;
            }
            run = Run(source[i..], 0);
            if (run > 0)
            {
                i += run;
                continue;
            }
            var at = i * 4;
            var keep = 255 - from[at + 3];
            for (var c = 0; c < 4; c++)
                to[at + c] = (byte)Math.Min(255, from[at + c] + to[at + c] * keep / 255);
            i++;
        }
    }

    // a pixel's alpha, as a little-endian uint of BGRA: its top byte
    private const uint Solid = 0xFF000000u;

    /// <summary>How many pixels from the start have the alpha <paramref name="alpha"/> (<see cref="Solid"/> or 0) —
    /// compared a vector at a time.</summary>
    private static int Run(ReadOnlySpan<uint> pixels, uint alpha)
    {
        var i = 0;
        if (System.Numerics.Vector.IsHardwareAccelerated && pixels.Length >= System.Numerics.Vector<uint>.Count)
        {
            var mask = new System.Numerics.Vector<uint>(Solid);
            var want = new System.Numerics.Vector<uint>(alpha);
            var vectors = System.Runtime.InteropServices.MemoryMarshal.Cast<uint, System.Numerics.Vector<uint>>(pixels);
            var v = 0;
            while (v < vectors.Length && System.Numerics.Vector.EqualsAll(vectors[v] & mask, want)) v++;
            i = v * System.Numerics.Vector<uint>.Count;
        }
        while (i < pixels.Length && (pixels[i] & Solid) == alpha) i++;
        return i;
    }
}

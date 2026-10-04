namespace app.module.screen.type.screen.display.code;

/// <summary>
/// What changed in an area just composed, against what the host shows: runs of changed rows (a few unchanged rows
/// between two changes stay in one run), each only as wide as its changes — a scrollbar's thumb and a page's new strip
/// are two small rectangles, not the page between them. Each run's pixels are packed in place in the composed rows
/// (its own width a row), and the host's picture is brought up to date.
/// </summary>
internal static class Damage
{
    private const int Gap = 16;   // unchanged rows that still join two changes: fewer, smaller messages

    /// <summary>The changed rectangles of <paramref name="r"/> and where each one's packed pixels start in
    /// <paramref name="composed"/> (the area's rows, <c>r.Width</c> pixels each); <paramref name="screen"/>
    /// (<paramref name="width"/> pixels a row) takes the changes. <paramref name="all"/>: every row is sent whole (what
    /// the host shows there isn't exact).</summary>
    internal static List<(Rect Rect, int At)> Find(Rect r, Span<byte> composed, Span<byte> screen, int width, bool all)
    {
        var found = new List<(Rect, int)>();
        var row = r.Width * 4;
        int start = -1, last = -1, left = 0, right = 0;
        for (var y = r.Y; y < r.Bottom; y++)
        {
            var line = composed.Slice((y - r.Y) * row, row);
            var shown = screen.Slice((y * width + r.X) * 4, row);
            int from, to;
            if (all) (from, to) = (0, r.Width);
            else
            {
                var same = line.CommonPrefixLength(shown);
                if (same == row)
                {
                    if (start >= 0 && y - last > Gap) { found.Add(Pack(r, composed, start, last, left, right)); start = -1; }
                    continue;
                }
                from = same / 4;
                to = r.Width - SameAtEnd(line, shown) / 4;
            }
            line[(from * 4)..(to * 4)].CopyTo(shown[(from * 4)..]);
            if (start < 0) (start, left, right) = (y, from, to);
            else (left, right) = (Math.Min(left, from), Math.Max(right, to));
            last = y;
        }
        if (start >= 0) found.Add(Pack(r, composed, start, last, left, right));
        return found;
    }

    // the run's columns, packed in place from its first row: a packed row never starts after the row it comes from
    private static (Rect, int) Pack(Rect r, Span<byte> composed, int start, int last, int left, int right)
    {
        var row = r.Width * 4;
        var packed = (right - left) * 4;
        var at = (start - r.Y) * row;
        if (packed != row)
            for (var y = start; y <= last; y++)
                composed.Slice((y - r.Y) * row + left * 4, packed).CopyTo(composed.Slice(at + (y - start) * packed, packed));
        return (new Rect(r.X + left, start, right - left, last - start + 1), at);
    }

    /// <summary>How many bytes at the end of the two are the same, a whole pixel at a time.</summary>
    internal static int SameAtEnd(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        var i = a.Length;
        while (i >= 64 && a.Slice(i - 64, 64).SequenceEqual(b.Slice(i - 64, 64))) i -= 64;
        while (i >= 4 && a.Slice(i - 4, 4).SequenceEqual(b.Slice(i - 4, 4))) i -= 4;
        return a.Length - i;
    }
}

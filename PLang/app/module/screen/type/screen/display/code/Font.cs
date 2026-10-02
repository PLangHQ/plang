using System.Buffers.Binary;

namespace app.module.screen.type.screen.display.code;

/// <summary>
/// A TrueType font, read from its bytes: each character's outline turned into pixels (how much of
/// each pixel it covers). No hinting — the curves are cut into lines and the lines' areas summed,
/// as fontdue/font-rs do. Enough for the title bars plang-screen draws; the pages are Chromium's.
/// </summary>
internal sealed class Font
{
    private readonly byte[] data;
    private readonly Dictionary<string, int> tables = new();
    private readonly int unitsPerEm, locFormat, glyphCount, hMetrics;
    private readonly short ascender, descender;
    private readonly int cmap;       // the character map subtable used
    private readonly int cmapFormat;
    private readonly Dictionary<(char, float), Glyph> glyphs = new();

    /// <summary>A character in pixels: <see cref="Cover"/> is Width × Height, one byte per pixel;
    /// Left is from the pen, Top is above the baseline.</summary>
    internal sealed record Glyph(int Width, int Height, int Left, int Top, float Advance, byte[] Cover);

    private Font(byte[] data)
    {
        this.data = data;
        int count = U16(4);
        for (var i = 0; i < count; i++)
        {
            var at = 12 + 16 * i;
            tables[System.Text.Encoding.ASCII.GetString(data, at, 4)] = (int)U32(at + 8);
        }
        var head = tables["head"];
        unitsPerEm = U16(head + 18);
        locFormat = S16(head + 50);
        glyphCount = U16(tables["maxp"] + 4);
        var hhea = tables["hhea"];
        ascender = S16(hhea + 4);
        descender = S16(hhea + 6);
        hMetrics = U16(hhea + 34);
        // Unicode, full range (format 12) if there is one, else the basic plane (format 4)
        var cm = tables["cmap"];
        for (var i = 0; i < U16(cm + 2); i++)
        {
            int platform = U16(cm + 4 + 8 * i), encoding = U16(cm + 6 + 8 * i);
            var sub = cm + (int)U32(cm + 8 + 8 * i);
            var format = U16(sub);
            if ((platform == 3 && encoding == 10 && format == 12) || (cmap == 0 && ((platform == 3 && encoding == 1) || platform == 0) && format is 4 or 12))
            {
                cmap = sub;
                cmapFormat = format;
            }
        }
    }

    /// <summary>The font in <paramref name="bytes"/>, or null if it isn't a TrueType font this reads.</summary>
    internal static Font? From(byte[]? bytes)
    {
        if (bytes == null || bytes.Length < 12) return null;
        try
        {
            var font = new Font(bytes);
            return font.cmap == 0 || !font.tables.ContainsKey("glyf") ? null : font;
        }
        catch (Exception ex) when (ex is KeyNotFoundException or IndexOutOfRangeException or ArgumentOutOfRangeException) { return null; }
    }

    /// <summary>Above and below the baseline for text <paramref name="px"/> high (below is negative).</summary>
    internal (float ascent, float descent) Line(float px) => (ascender * px / unitsPerEm, descender * px / unitsPerEm);

    internal float Advance(char c, float px) => AdvanceOf(GlyphIndex(c)) * px / unitsPerEm;

    internal Glyph Render(char c, float px)
    {
        if (glyphs.TryGetValue((c, px), out var known)) return known;
        var index = GlyphIndex(c);
        var scale = px / unitsPerEm;
        var contours = new List<List<(float x, float y, bool on)>>();
        Outline(index, contours, 1, 0, 0, 1, 0, 0, 0);
        var advance = AdvanceOf(index) * scale;
        var points = contours.SelectMany(c => c).ToList();
        if (points.Count == 0)
            return glyphs[(c, px)] = new Glyph(0, 0, 0, 0, advance, []);
        var left = (int)Math.Floor(points.Min(p => p.x) * scale);
        var right = (int)Math.Ceiling(points.Max(p => p.x) * scale);
        var bottom = (int)Math.Floor(points.Min(p => p.y) * scale);
        var top = (int)Math.Ceiling(points.Max(p => p.y) * scale);
        var raster = new Raster(right - left + 2, top - bottom + 2);
        foreach (var contour in contours)
        {
            // pixels: x from the left edge, y down from the top
            var pts = contour.Select(p => (x: p.x * scale - left, y: top - p.y * scale, p.on)).ToList();
            raster.Contour(pts);
        }
        return glyphs[(c, px)] = new Glyph(raster.Width, raster.Height, left, top, advance, raster.Cover());
    }

    // ---- the font's tables ------------------------------------------------------------------------

    private int U16(int at) => BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(at));
    private short S16(int at) => BinaryPrimitives.ReadInt16BigEndian(data.AsSpan(at));
    private uint U32(int at) => BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(at));

    private int AdvanceOf(int glyph)
    {
        var hmtx = tables["hmtx"];
        return U16(hmtx + 4 * Math.Min(glyph, hMetrics - 1));
    }

    private int GlyphIndex(char c)
    {
        int code = c;
        if (cmapFormat == 12)
        {
            var groups = (int)U32(cmap + 12);
            for (var i = 0; i < groups; i++)
            {
                var g = cmap + 16 + 12 * i;
                if (code >= U32(g) && code <= U32(g + 4)) return (int)(U32(g + 8) + (uint)code - U32(g));
            }
            return 0;
        }
        var segments = U16(cmap + 6) / 2;
        var ends = cmap + 14;
        var starts = ends + 2 * segments + 2;
        var deltas = starts + 2 * segments;
        var offsets = deltas + 2 * segments;
        for (var i = 0; i < segments; i++)
        {
            if (U16(ends + 2 * i) < code) continue;
            var start = U16(starts + 2 * i);
            if (start > code) return 0;
            var delta = S16(deltas + 2 * i);
            var offset = U16(offsets + 2 * i);
            if (offset == 0) return (code + delta) & 0xffff;
            var g = U16(offsets + 2 * i + offset + 2 * (code - start));
            return g == 0 ? 0 : (g + delta) & 0xffff;
        }
        return 0;
    }

    private (int at, int length) GlyphData(int glyph)
    {
        if (glyph >= glyphCount) return (0, 0);
        var loca = tables["loca"];
        int from, to;
        if (locFormat == 0) { from = U16(loca + 2 * glyph) * 2; to = U16(loca + 2 * glyph + 2) * 2; }
        else { from = (int)U32(loca + 4 * glyph); to = (int)U32(loca + 4 * glyph + 4); }
        return (tables["glyf"] + from, to - from);
    }

    /// <summary>Glyph <paramref name="glyph"/>'s contours in font units, through the transform
    /// (a b c d) + (dx dy) — composite glyphs place their parts that way.</summary>
    private void Outline(int glyph, List<List<(float, float, bool)>> into, float a, float b, float c, float d, float dx, float dy, int depth)
    {
        var (at, length) = GlyphData(glyph);
        if (length == 0 || depth > 8) return;
        int contours = S16(at);
        if (contours < 0)
        {
            Composite(at + 10, into, a, b, c, d, dx, dy, depth);
            return;
        }
        var endPoints = new int[contours];
        for (var i = 0; i < contours; i++) endPoints[i] = U16(at + 10 + 2 * i);
        var count = contours == 0 ? 0 : endPoints[^1] + 1;
        var p = at + 10 + 2 * contours;
        p += 2 + U16(p);   // instructions
        var flags = new byte[count];
        for (var i = 0; i < count;)
        {
            var f = data[p++];
            flags[i++] = f;
            if ((f & 8) != 0)
                for (var r = data[p++]; r > 0 && i < count; r--) flags[i++] = f;
        }
        var xs = new int[count];
        var ys = new int[count];
        for (int i = 0, v = 0; i < count; i++)
        {
            var f = flags[i];
            if ((f & 2) != 0) { v += (f & 16) != 0 ? data[p] : -data[p]; p++; }
            else if ((f & 16) == 0) { v += S16(p); p += 2; }
            xs[i] = v;
        }
        for (int i = 0, v = 0; i < count; i++)
        {
            var f = flags[i];
            if ((f & 4) != 0) { v += (f & 32) != 0 ? data[p] : -data[p]; p++; }
            else if ((f & 32) == 0) { v += S16(p); p += 2; }
            ys[i] = v;
        }
        var first = 0;
        foreach (var end in endPoints)
        {
            var contour = new List<(float, float, bool)>();
            for (var i = first; i <= end; i++)
                contour.Add((a * xs[i] + c * ys[i] + dx, b * xs[i] + d * ys[i] + dy, (flags[i] & 1) != 0));
            into.Add(contour);
            first = end + 1;
        }
    }

    private void Composite(int p, List<List<(float, float, bool)>> into, float a, float b, float c, float d, float dx, float dy, int depth)
    {
        while (true)
        {
            int flags = U16(p), part = U16(p + 2);
            p += 4;
            float ox, oy;
            if ((flags & 1) != 0) { ox = S16(p); oy = S16(p + 2); p += 4; }
            else { ox = unchecked((sbyte)data[p]); oy = unchecked((sbyte)data[p + 1]); p += 2; }
            float pa = 1, pb = 0, pc = 0, pd = 1;
            static float F2Dot14(short v) => v / 16384f;
            if ((flags & 8) != 0) { pa = pd = F2Dot14(S16(p)); p += 2; }
            else if ((flags & 0x40) != 0) { pa = F2Dot14(S16(p)); pd = F2Dot14(S16(p + 2)); p += 4; }
            else if ((flags & 0x80) != 0) { pa = F2Dot14(S16(p)); pb = F2Dot14(S16(p + 2)); pc = F2Dot14(S16(p + 4)); pd = F2Dot14(S16(p + 6)); p += 8; }
            if ((flags & 2) == 0) { ox = 0; oy = 0; }   // matched points: rare, not needed here
            // the part's transform, then the parent's
            Outline(part, into,
                a * pa + c * pb, b * pa + d * pb,
                a * pc + c * pd, b * pc + d * pd,
                a * ox + c * oy + dx, b * ox + d * oy + dy, depth + 1);
            if ((flags & 0x20) == 0) break;
        }
    }

    /// <summary>Coverage by signed area: each line adds how much of each pixel lies to its right;
    /// summing along a row gives how much of each pixel is inside.</summary>
    private sealed class Raster(int width, int height)
    {
        internal int Width { get; } = width;
        internal int Height { get; } = height;
        private readonly float[] area = new float[width * height + 4];

        internal void Contour(List<(float x, float y, bool on)> pts)
        {
            if (pts.Count < 2) return;
            // start on a point that is on the curve (or between two that aren't)
            var start = pts.FindIndex(p => p.on);
            (float x, float y) first = start >= 0 ? (pts[start].x, pts[start].y)
                : ((pts[0].x + pts[1].x) / 2, (pts[0].y + pts[1].y) / 2);
            if (start < 0) start = 0;
            var pen = first;
            (float x, float y)? control = null;
            for (var k = 1; k <= pts.Count; k++)
            {
                var p = pts[(start + k) % pts.Count];
                if (p.on)
                {
                    if (control is { } q) Curve(pen, q, (p.x, p.y)); else Line(pen, (p.x, p.y));
                    pen = (p.x, p.y);
                    control = null;
                }
                else if (control is { } q)
                {
                    var mid = ((q.x + p.x) / 2, (q.y + p.y) / 2);   // two off-curve points: one on between
                    Curve(pen, q, mid);
                    pen = mid;
                    control = (p.x, p.y);
                }
                else control = (p.x, p.y);
            }
            if (control is { } last) Curve(pen, last, first); else Line(pen, first);
        }

        private void Curve((float x, float y) p0, (float x, float y) p1, (float x, float y) p2)
        {
            var devx = p0.x - 2 * p1.x + p2.x;
            var devy = p0.y - 2 * p1.y + p2.y;
            var dev = devx * devx + devy * devy;
            if (dev < 0.333f) { Line(p0, p2); return; }
            var n = 1 + (int)Math.Floor(Math.Sqrt(Math.Sqrt(3 * dev)));
            var prev = p0;
            for (var i = 1; i <= n; i++)
            {
                var t = (float)i / n;
                var u = 1 - t;
                var next = (u * u * p0.x + 2 * u * t * p1.x + t * t * p2.x, u * u * p0.y + 2 * u * t * p1.y + t * t * p2.y);
                Line(prev, next);
                prev = next;
            }
        }

        private void Line((float x, float y) p0, (float x, float y) p1)
        {
            if (p0.y == p1.y) return;
            var dir = 1f;
            if (p0.y > p1.y) { (p0, p1) = (p1, p0); dir = -1f; }
            var dxdy = (p1.x - p0.x) / (p1.y - p0.y);
            var x = p0.x;
            if (p0.y < 0) x -= p0.y * dxdy;
            var yEnd = Math.Min(Height, (int)Math.Ceiling(p1.y));
            for (var y = Math.Max(0, (int)p0.y); y < yEnd; y++)
            {
                var row = y * Width;
                var dy = Math.Min(y + 1, p1.y) - Math.Max(y, p0.y);
                var xnext = x + dxdy * dy;
                var d = dy * dir;
                var (x0, x1) = x < xnext ? (x, xnext) : (xnext, x);
                var x0floor = (float)Math.Floor(x0);
                var x0i = (int)x0floor;
                var x1ceil = (float)Math.Ceiling(x1);
                var x1i = (int)x1ceil;
                if (x0i < 0 || row + x1i >= area.Length) { x = xnext; continue; }
                if (x1i <= x0i + 1)
                {
                    var xmf = 0.5f * (x + xnext) - x0floor;
                    area[row + x0i] += d - d * xmf;
                    area[row + x0i + 1] += d * xmf;
                }
                else
                {
                    var s = 1 / (x1 - x0);
                    var x0f = x0 - x0floor;
                    var a0 = 0.5f * s * (1 - x0f) * (1 - x0f);
                    var x1f = x1 - x1ceil + 1;
                    var am = 0.5f * s * x1f * x1f;
                    area[row + x0i] += d * a0;
                    if (x1i == x0i + 2) area[row + x0i + 1] += d * (1 - a0 - am);
                    else
                    {
                        var a1 = s * (1.5f - x0f);
                        area[row + x0i + 1] += d * (a1 - a0);
                        for (var xi = x0i + 2; xi < x1i - 1; xi++) area[row + xi] += d * s;
                        var a2 = a1 + (x1i - x0i - 3) * s;
                        area[row + x1i - 1] += d * (1 - a2 - am);
                    }
                    area[row + x1i] += d * am;
                }
                x = xnext;
            }
        }

        internal byte[] Cover()
        {
            var cover = new byte[Width * Height];
            var sum = 0f;
            for (var i = 0; i < cover.Length; i++)
            {
                sum += area[i];
                cover[i] = (byte)Math.Min(255, (int)(Math.Abs(sum) * 255 + 0.5f));
            }
            return cover;
        }
    }
}

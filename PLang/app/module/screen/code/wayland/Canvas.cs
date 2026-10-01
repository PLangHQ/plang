namespace app.module.screen.code.wayland;

/// <summary>A color, premultiplied BGRA (as the screen's pixels are).</summary>
internal readonly record struct Color(float B, float G, float R, float A)
{
    internal static Color Rgba(byte r, byte g, byte b, float a) => new(b * a, g * a, r * a, 255 * a);

    // the desktop's palette (desktop.html): deep blue glass, light text, the orange accent
    internal static readonly Color BarActive = Rgba(0x22, 0x2e, 0x42, 1);
    internal static readonly Color BarInactive = Rgba(0x14, 0x1b, 0x27, 1);
    // a title bar's rim — its top and sides, where one window's header ends and the next one's begins: clear on the
    // window in use, faint on the others
    internal static readonly Color RimActive = Rgba(0x6a, 0x7f, 0x9e, 1);
    internal static readonly Color RimInactive = Rgba(0x2e, 0x3a, 0x4e, 1);
    internal static readonly Color Ink = Rgba(0xee, 0xf2, 0xf7, 1);
    internal static readonly Color InkInactive = Rgba(0x8d, 0x9b, 0xb0, 1);
    internal static readonly Color Hover = Rgba(0xff, 0xff, 0xff, 0.10f);
    internal static readonly Color CloseHover = Rgba(0xe5, 0x48, 0x4d, 1);
    internal static readonly Color Field = Rgba(0x0f, 0x16, 0x21, 0.98f);
    internal static readonly Color Edge = Rgba(0x2a, 0x36, 0x4a, 1);
    internal static readonly Color Accent = Rgba(0xf2, 0xa3, 0x3a, 1);
    internal static readonly Color Selection = Rgba(0x3a, 0x6e, 0xd8, 0.85f);
    internal static readonly Color Shadow = Rgba(0, 0, 0, 0.35f);
    internal static readonly Color Rule = Rgba(0xff, 0xff, 0xff, 0.10f);
}

/// <summary>
/// Pixels plang-screen draws itself: the title bars, the address field, the window menu. Shapes
/// are smooth (each pixel covered by how much of the shape is on it); text is the font's.
/// </summary>
internal sealed class Canvas
{
    internal const float TextSize = 13f;
    private readonly byte[] px;
    internal int Width { get; }
    internal int Height { get; }

    internal Canvas(int width, int height)
    {
        Width = Math.Max(1, width);
        Height = Math.Max(1, height);
        px = new byte[Width * Height * 4];
    }

    /// <summary><paramref name="c"/> over the pixel at (x, y), <paramref name="cover"/> of it (0..1).</summary>
    private void Blend(int x, int y, Color c, float cover)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height || cover <= 0) return;
        cover = Math.Min(cover, 1);
        var i = (y * Width + x) * 4;
        var keep = 1 - c.A / 255 * cover;
        px[i] = Channel(c.B * cover + px[i] * keep);
        px[i + 1] = Channel(c.G * cover + px[i + 1] * keep);
        px[i + 2] = Channel(c.R * cover + px[i + 2] * keep);
        px[i + 3] = Channel(c.A * cover + px[i + 3] * keep);
    }

    private static byte Channel(float v) => (byte)Math.Clamp((int)MathF.Round(v), 0, 255);

    internal void Fill(Rect r, Color c)
    {
        for (var y = Math.Max(0, r.Y); y < Math.Min(Height, r.Bottom); y++)
            for (var x = Math.Max(0, r.X); x < Math.Min(Width, r.Right); x++) Blend(x, y, c, 1);
    }

    /// <summary>A filled rectangle with round corners (only the top ones, for a title bar).</summary>
    internal void Round(Rect r, float radius, Color c, bool topOnly = false)
    {
        for (var y = Math.Max(0, r.Y); y < Math.Min(Height, r.Bottom); y++)
            for (var x = Math.Max(0, r.X); x < Math.Min(Width, r.Right); x++)
            {
                float px0 = x + 0.5f, py0 = y + 0.5f;
                var cx = Math.Clamp(px0, r.X + radius, r.Right - radius);
                var cy = topOnly ? Math.Max(py0, r.Y + radius) : Math.Clamp(py0, r.Y + radius, r.Bottom - radius);
                var d = MathF.Sqrt((px0 - cx) * (px0 - cx) + (py0 - cy) * (py0 - cy));
                Blend(x, y, c, Math.Clamp(radius - d + 0.5f, 0, 1));
            }
    }

    /// <summary>A line from (x0, y0) to (x1, y1). 1 px lines are crisp on pixel centers (.5).</summary>
    internal void Line(float x0, float y0, float x1, float y1, float width, Color c)
    {
        float dx = x1 - x0, dy = y1 - y0, length2 = Math.Max(dx * dx + dy * dy, 1e-6f);
        for (var y = (int)MathF.Floor(Math.Min(y0, y1) - width); y <= (int)MathF.Ceiling(Math.Max(y0, y1) + width); y++)
            for (var x = (int)MathF.Floor(Math.Min(x0, x1) - width); x <= (int)MathF.Ceiling(Math.Max(x0, x1) + width); x++)
            {
                float px0 = x + 0.5f, py0 = y + 0.5f;
                var t = Math.Clamp(((px0 - x0) * dx + (py0 - y0) * dy) / length2, 0, 1);
                var d = MathF.Sqrt((px0 - x0 - t * dx) * (px0 - x0 - t * dx) + (py0 - y0 - t * dy) * (py0 - y0 - t * dy));
                Blend(x, y, c, Math.Clamp(width / 2 - d + 0.5f, 0, 1));
            }
    }

    /// <summary>An ellipse's outline around (cx, cy); a circle when rx = ry.</summary>
    internal void Ring(float cx, float cy, float rx, float ry, float width, Color c)
    {
        for (var y = (int)MathF.Floor(cy - ry - width); y <= (int)MathF.Ceiling(cy + ry + width); y++)
            for (var x = (int)MathF.Floor(cx - rx - width); x <= (int)MathF.Ceiling(cx + rx + width); x++)
            {
                float ex = x + 0.5f - cx, ey = y + 0.5f - cy;
                // distance to the outline, first order: (k - 1) / |∇k|, k the ellipse's own radius
                var k = Math.Max(MathF.Sqrt(ex * ex / (rx * rx) + ey * ey / (ry * ry)), 1e-3f);
                var grad = MathF.Sqrt(ex * ex / (rx * rx * rx * rx) + ey * ey / (ry * ry * ry * ry)) / k;
                var d = (k - 1) / Math.Max(grad, 1e-3f);
                Blend(x, y, c, Math.Clamp(width / 2 - Math.Abs(d) + 0.5f, 0, 1));
            }
    }

    internal void Outline(float left, float top, float w, float h, Color c)
    {
        Line(left, top, left + w, top, 1, c);
        Line(left + w, top, left + w, top + h, 1, c);
        Line(left + w, top + h, left, top + h, 1, c);
        Line(left, top + h, left, top, 1, c);
    }

    /// <summary>Text from <paramref name="x"/>, on the line whose middle is <paramref name="mid"/>, cut
    /// with … before <paramref name="maxX"/>. Returns where each character starts, and where the last ends.</summary>
    internal List<float> Text(Font? font, string text, float x, float mid, float maxX, Color c)
    {
        var starts = new List<float>();
        if (font == null) { starts.Add(x); return starts; }
        var (ascent, descent) = font.Line(TextSize);
        var baseline = MathF.Round(mid + (ascent + descent) / 2);
        var width = text.Sum(ch => font.Advance(ch, TextSize));
        var ellipsis = font.Advance('…', TextSize);
        var cut = x + width > maxX;
        var pen = x;
        foreach (var ch in text)
        {
            var advance = font.Advance(ch, TextSize);
            if (cut && pen + advance > maxX - ellipsis)
            {
                Glyph(font, '…', pen, baseline, c);
                starts.Add(pen);
                return starts;
            }
            starts.Add(pen);
            Glyph(font, ch, pen, baseline, c);
            pen += advance;
        }
        starts.Add(pen);
        return starts;
    }

    /// <summary>How wide <paramref name="text"/> is.</summary>
    internal static float Measure(Font? font, string text) => font == null ? 0 : text.Sum(ch => font.Advance(ch, TextSize));

    private void Glyph(Font font, char ch, float pen, float baseline, Color c)
    {
        var g = font.Render(ch, TextSize);
        var left = (int)MathF.Round(pen) + g.Left;
        var top = (int)baseline - g.Top;
        for (var gy = 0; gy < g.Height; gy++)
            for (var gx = 0; gx < g.Width; gx++)
                Blend(left + gx, top + gy, c, g.Cover[gy * g.Width + gx] / 255f);
    }

    /// <summary>What was drawn, as a picture at <paramref name="at"/>.</summary>
    internal Picture Picture(Point at) => new(Rect.At(at, Width, Height), px, opaque: false);
}

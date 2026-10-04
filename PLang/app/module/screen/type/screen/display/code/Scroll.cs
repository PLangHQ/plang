using System.Numerics;
using System.Runtime.InteropServices;

namespace app.module.screen.type.screen.display.code;

/// <summary>
/// A page scrolled: most of a rectangle's new rows are rows the host already shows, a few rows up or down. Found by
/// each row's hash — the rows that are in one place only vote for how far they moved — then the longest band of rows
/// that moved that far. The host moves that band itself (a move message), and only what is new is sent: a scrolling
/// page isn't a video, and isn't sent again whole. A wrong guess costs bytes, never pixels: what follows the move is
/// compared row by row and put right.
/// </summary>
internal static class Scroll
{
    private const int MinWidth = 200, MinHeight = 120;   // what a video would be: smaller areas aren't worth it
    private const int Votes = 8;                         // rows agreeing on how far it moved

    /// <summary>In <paramref name="r"/> (its rows composed into <paramref name="composed"/>, a row after another), the
    /// rows that moved by the same <c>dy</c> from what <paramref name="screen"/> (the host's picture,
    /// <paramref name="width"/> pixels a row) holds — the band where they are now (only the columns that changed: a
    /// sidebar that stays isn't part of it) and how far they moved — or none.</summary>
    internal static (int Left, int Right, int Top, int Bottom, int Dy)? Find(Rect r, ReadOnlySpan<byte> composed, ReadOnlySpan<byte> screen, int width)
    {
        if (r.Width < MinWidth || r.Height < MinHeight) return null;
        var h = r.Height;
        var row = r.Width * 4;
        // the columns anything changed in: a page's column scrolls, what stays beside it (a sidebar, a margin) doesn't
        int left = r.Width, right = 0;
        for (var i = 0; i < h; i++)
        {
            var line = composed.Slice(i * row, row);
            var shown = screen.Slice(((r.Y + i) * width + r.X) * 4, row);
            var same = line.CommonPrefixLength(shown);
            if (same == row) continue;
            left = Math.Min(left, same / 4);
            right = Math.Max(right, r.Width - Damage.SameAtEnd(line, shown) / 4);
        }
        if (right - left < MinWidth) return null;
        var now = new ulong[h];
        var was = new ulong[h];
        var unchanged = 0;
        for (var i = 0; i < h; i++)
        {
            now[i] = Hash(composed.Slice(i * row + left * 4, (right - left) * 4));
            was[i] = Hash(screen.Slice(((r.Y + i) * width + r.X + left) * 4, (right - left) * 4));
            if (now[i] == was[i]) unchanged++;
        }
        if (unchanged > h * 9 / 10) return null;   // nothing (much) moved

        // where each row the host shows is — only rows that are in one place say anything (a blank row is everywhere)
        var place = new Dictionary<ulong, int>(h);
        for (var i = 0; i < h; i++)
            place[was[i]] = place.ContainsKey(was[i]) ? -1 : i;
        var votes = new Dictionary<int, int>();
        for (var i = 0; i < h; i++)
            if (place.TryGetValue(now[i], out var j) && j >= 0 && j != i)
                votes[i - j] = votes.GetValueOrDefault(i - j) + 1;
        if (votes.Count == 0) return null;
        var (dy, count) = votes.MaxBy(v => v.Value);
        if (count < Votes) return null;

        // the longest run of rows that moved by dy
        int best = 0, top = 0, run = 0;
        for (var i = 0; i < h; i++)
        {
            var from = i - dy;
            run = from >= 0 && from < h && now[i] == was[from] ? run + 1 : 0;
            if (run > best) { best = run; top = i - run + 1; }
        }
        if (best < Math.Max(Votes * 4, h / 4)) return null;
        return (r.X + left, r.X + right, r.Y + top, r.Y + top + best, dy);
    }

    /// <summary>A row's hash: its pixels as 64-bit words, mixed in four lanes side by side (the multiplies wrap: the
    /// project checks arithmetic).</summary>
    internal static ulong Hash(ReadOnlySpan<byte> row)
    {
        unchecked
        {
            var words = MemoryMarshal.Cast<byte, ulong>(row);
            ulong a = 0x9E3779B97F4A7C15, b = 0xC2B2AE3D27D4EB4F, c = 0x165667B19E3779F9, d = 0x27D4EB2F165667C5;
            var i = 0;
            for (; i + 4 <= words.Length; i += 4)
            {
                a = BitOperations.RotateLeft((a ^ words[i]) * 0x9E3779B97F4A7C15, 31);
                b = BitOperations.RotateLeft((b ^ words[i + 1]) * 0x9E3779B97F4A7C15, 31);
                c = BitOperations.RotateLeft((c ^ words[i + 2]) * 0x9E3779B97F4A7C15, 31);
                d = BitOperations.RotateLeft((d ^ words[i + 3]) * 0x9E3779B97F4A7C15, 31);
            }
            for (; i < words.Length; i++) a = BitOperations.RotateLeft((a ^ words[i]) * 0x9E3779B97F4A7C15, 31);
            for (var t = words.Length * 8; t < row.Length; t++) b = BitOperations.RotateLeft((b ^ row[t]) * 0x9E3779B97F4A7C15, 31);
            return (a ^ BitOperations.RotateLeft(b, 17) ^ BitOperations.RotateLeft(c, 29) ^ BitOperations.RotateLeft(d, 43)) * 0xC2B2AE3D27D4EB4F;
        }
    }
}

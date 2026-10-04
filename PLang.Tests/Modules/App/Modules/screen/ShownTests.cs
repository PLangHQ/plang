using System.Buffers.Binary;
using Qoi = app.module.screen.type.screen.code.Qoi;
using Rect = app.module.screen.type.screen.display.code.Rect;
using Shown = app.module.screen.type.screen.code.Shown;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// The host's picture of PlangOS's screen, kept by the frame messages PlangOS sends: a rectangle put where it says, a
/// move that shifts the pixels the host has and the strip that fills what it uncovered — the same picture as PlangOS's.
/// </summary>
public class ShownTests
{
    private const int W = 64, H = 48;

    private static byte[] Random(int bytes, int seed)
    {
        var b = new byte[bytes];
        new Random(seed).NextBytes(b);
        return b;
    }

    // a frame message: [kind][move?][u16 count] then each rectangle's head and its QOI
    private static byte[] Frame((int x, int y, int w, int h, int dx, int dy)? move, params (Rect r, byte[] px)[] rects)
    {
        using var m = new MemoryStream();
        m.WriteByte(move == null ? (byte)1 : (byte)7);
        var i32 = new byte[4];
        void Int(int v) { BinaryPrimitives.WriteInt32LittleEndian(i32, v); m.Write(i32); }
        if (move is var (x, y, w, h, dx, dy)) { Int(x); Int(y); Int(w); Int(h); Int(dx); Int(dy); }
        m.Write(BitConverter.GetBytes((ushort)rects.Length));
        foreach (var (r, px) in rects)
        {
            var q = new byte[Qoi.MaxSize(r.Width, r.Height)];
            var n = new Qoi(px, r.Width, r.Height).Into(q);
            Int(r.X); Int(r.Y); Int(r.Width); Int(r.Height); Int(n);
            m.Write(q, 0, n);
        }
        return m.ToArray();
    }

    private static byte[] Rows(byte[] screen, Rect r)
    {
        var out_ = new byte[r.Width * r.Height * 4];
        for (var y = 0; y < r.Height; y++) screen.AsSpan(((r.Y + y) * W + r.X) * 4, r.Width * 4).CopyTo(out_.AsSpan(y * r.Width * 4));
        return out_;
    }

    [Test]
    public async Task AFullPicture_ThenAScroll_IsPlangOSsPicture()
    {
        var shown = new Shown(W, H);
        var screen = Random(W * H * 4, 1);
        await Assert.That(shown.Apply(Frame(null, (new Rect(0, 0, W, H), screen)))).IsEqualTo(new Rect(0, 0, W, H));
        await Assert.That(shown.Pixels.SequenceEqual(screen)).IsTrue();

        // PlangOS scrolls rows 8.. up by 6 and sends the strip at the bottom
        var scrolled = (byte[])screen.Clone();
        for (var y = 8; y < H - 6; y++) screen.AsSpan(((y + 6) * W) * 4, W * 4).CopyTo(scrolled.AsSpan(y * W * 4));
        var strip = new Rect(0, H - 6, W, 6);
        var fresh = Random(W * 6 * 4, 2);
        for (var y = 0; y < 6; y++) fresh.AsSpan(y * W * 4, W * 4).CopyTo(scrolled.AsSpan((H - 6 + y) * W * 4));
        shown.Apply(Frame((0, 14, W, H - 14, 0, -6), (strip, fresh)));
        await Assert.That(shown.Pixels.SequenceEqual(scrolled)).IsTrue();
    }

    [Test]
    public async Task ARectangle_GoesWhereItSays_AndNothingElseChanges()
    {
        var shown = new Shown(W, H);
        var screen = Random(W * H * 4, 3);
        shown.Apply(Frame(null, (new Rect(0, 0, W, H), screen)));
        var r = new Rect(10, 5, 20, 7);
        var px = Random(r.Width * r.Height * 4, 4);
        await Assert.That(shown.Apply(Frame(null, (r, px)))).IsEqualTo(r);
        await Assert.That(Rows(shown.Pixels, r).SequenceEqual(px)).IsTrue();
        await Assert.That(Rows(shown.Pixels, new Rect(0, 0, W, 5)).SequenceEqual(Rows(screen, new Rect(0, 0, W, 5)))).IsTrue();
    }

    [Test]
    public async Task NotAFrame_OrCutOff_ChangesNothing()
    {
        var shown = new Shown(W, H);
        var frame = Frame(null, (new Rect(0, 0, 8, 8), Random(8 * 8 * 4, 5)));
        await Assert.That(shown.Apply([2, (byte)'x'])).IsNull();
        await Assert.That(shown.Apply(frame[..(frame.Length / 2)])).IsNull();
        await Assert.That(shown.Pixels.All(b => b == 0)).IsTrue();
    }
}

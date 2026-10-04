using app.module.screen.type.screen.code;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// A decoded picture draws itself the same whatever its layout — planes apart (dav1d, openh264) or U and V side by side
/// (NV12: Windows' decoders) — and gives its borrowed buffer back when done.
/// </summary>
public class YuvTests
{
    private const int W = 32, H = 18, Stride = 48;

    [Test]
    public async Task ThePictureInPlanes_AndAsNv12_DrawTheSamePixels()
    {
        var random = new Random(3);
        var lumas = new byte[Stride * H];
        random.NextBytes(lumas);
        var u = new byte[(W / 2) * (H / 2)];
        var v = new byte[u.Length];
        random.NextBytes(u);
        random.NextBytes(v);

        using var planes = Yuv.Planes(null, Stride, W / 2, W, H);
        lumas.CopyTo(planes.Data, 0);
        u.CopyTo(planes.Data, planes.UAt);
        v.CopyTo(planes.Data, planes.VAt);

        using var nv12 = Yuv.Nv12(null, Stride, W, H);
        lumas.CopyTo(nv12.Data, 0);
        for (var y = 0; y < H / 2; y++)
            for (var x = 0; x < W / 2; x++)
            {
                nv12.Data[nv12.UAt + y * Stride + 2 * x] = u[y * (W / 2) + x];
                nv12.Data[nv12.UAt + y * Stride + 2 * x + 1] = v[y * (W / 2) + x];
            }

        // drawn at a size of its own (scaled), as at a video's place
        foreach (var (w, h) in new[] { (W, H), (20, 11), (50, 29) })
        {
            var a = new byte[w * h * 4];
            var b = new byte[w * h * 4];
            planes.Into(a, w, h);
            nv12.Into(b, w, h);
            await Assert.That(a.SequenceEqual(b)).IsTrue().Because($"{w}×{h}");
        }
    }

    // every luma and colour value gives the BT.709 video-range pixel (the tables are the formula, exactly)
    [Test]
    public async Task EachPixel_IsTheBt709Formula()
    {
        static byte Clamp(int v) => (byte)Math.Clamp(v, 0, 255);
        using var picture = Yuv.Planes(null, 256, 128, 256, 2);
        for (var i = 0; i < 256; i++) picture.Data[i] = picture.Data[256 + i] = (byte)i;   // every luma, two rows
        var random = new Random(5);
        var u = new byte[128];
        var v = new byte[128];
        random.NextBytes(u);
        random.NextBytes(v);
        u.CopyTo(picture.Data, picture.UAt);
        v.CopyTo(picture.Data, picture.VAt);
        var bgra = new byte[256 * 2 * 4];
        picture.Into(bgra, 256, 2);
        for (var x = 0; x < 256; x++)
        {
            int c = 298 * (x - 16), cu = u[x / 2] - 128, cv = v[x / 2] - 128;
            byte[] want = [Clamp((c + 541 * cu + 128) >> 8), Clamp((c - 55 * cu - 136 * cv + 128) >> 8), Clamp((c + 459 * cv + 128) >> 8), 255];
            await Assert.That(bgra.AsSpan(x * 4, 4).ToArray()).IsEquivalentTo(want).Because($"Y {x}, U {u[x / 2]}, V {v[x / 2]}");
        }
    }

    // a picture done with gives its buffer back to its decoder's lender, once, and the next picture gets that buffer
    [Test]
    public async Task APictureDoneWith_GivesItsBufferBack_ForTheNext()
    {
        var lender = new Lender();
        var picture = Yuv.Nv12(lender, Stride, W, H);
        var buffer = picture.Data;
        picture.Dispose();
        picture.Dispose();   // twice: still given back once
        await Assert.That(() => picture.Data).Throws<ObjectDisposedException>();
        using var next = Yuv.Nv12(lender, Stride, W, H);
        await Assert.That(ReferenceEquals(next.Data, buffer)).IsTrue();
        using var another = Yuv.Nv12(lender, Stride, W, H);
        await Assert.That(ReferenceEquals(another.Data, buffer)).IsFalse().Because("one buffer is lent to one picture at a time");
    }
}

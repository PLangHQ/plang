using Picture = app.module.screen.type.screen.display.code.Picture;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// A picture drawn over a row of the screen: opaque pixels copied, clear ones skipped, the rest blended (premultiplied
/// over: <c>out = src + dst·(255−a)/255</c>) — the same pixels however the picture's runs fall and wherever it is clipped.
/// </summary>
public class PictureTests
{
    // The blend, one pixel at a time: what Draw must give
    private static byte[] Reference(byte[] pixels, Rect r, int y, int x0, byte[] line)
    {
        var to = (byte[])line.Clone();
        if (y < r.Y || y >= r.Bottom) return to;
        int sx = Math.Max(x0, r.X), ex = Math.Min(x0 + line.Length / 4, r.Right);
        for (var x = sx; x < ex; x++)
        {
            var s = ((y - r.Y) * r.Width + (x - r.X)) * 4;
            var d = (x - x0) * 4;
            var a = pixels[s + 3];
            if (a == 255) { Array.Copy(pixels, s, to, d, 4); continue; }
            if (a == 0) continue;
            for (var c = 0; c < 4; c++) to[d + c] = (byte)Math.Min(255, pixels[s + c] + to[d + c] * (255 - a) / 255);
        }
        return to;
    }

    // a picture of runs: long opaque stretches, clear ones, and single edge pixels of any alpha
    private static byte[] Runs(int width, int height, int seed)
    {
        var random = new Random(seed);
        var pixels = new byte[width * height * 4];
        var i = 0;
        while (i < pixels.Length)
        {
            var kind = random.Next(4);
            var run = kind switch { 0 => random.Next(1, 200), 1 => random.Next(1, 40), _ => random.Next(1, 4) };
            for (var k = 0; k < run && i < pixels.Length; k++, i += 4)
            {
                byte a = kind switch { 0 => 255, 1 => 0, _ => (byte)random.Next(1, 255) };
                // premultiplied: no channel above alpha
                for (var c = 0; c < 3; c++) pixels[i + c] = (byte)random.Next(0, a + 1);
                pixels[i + 3] = a;
            }
        }
        return pixels;
    }

    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    public async Task DrawingAPictureOfRuns_GivesThePixelByPixelBlend(int seed)
    {
        const int width = 333, height = 7;
        var pixels = Runs(width, height, seed);
        var random = new Random(seed + 100);
        foreach (var (at, x0, length) in new[] { (5, 0, 400), (0, 0, 333), (-20, 0, 300), (50, 60, 200), (10, 12, 37) })
        {
            var rect = new Rect(at, 2, width, height);
            for (var y = 0; y < 10; y++)
            {
                var line = new byte[length * 4];
                random.NextBytes(line);
                var expected = Reference(pixels, rect, y, x0, line);
                new Picture(rect, pixels, opaque: false).Draw(y, x0, line);
                await Assert.That(line.SequenceEqual(expected)).IsTrue().Because($"at {at}, x0 {x0}, row {y}");
            }
        }
    }

    [Test]
    public async Task AnOpaquePicture_IsCopied()
    {
        var pixels = Runs(64, 2, 9);
        var line = new byte[100 * 4];
        new Picture(new Rect(10, 0, 64, 2), pixels, opaque: true).Draw(1, 0, line);
        await Assert.That(line.AsSpan(40, 64 * 4).SequenceEqual(pixels.AsSpan(64 * 4, 64 * 4))).IsTrue();
    }
}

using Qoi = app.module.screen.type.screen.code.Qoi;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// QOI, both ends: what PlangOS's screen encodes, the host decodes to the same pixels — runs, small and big changes,
/// alpha, a long background — and a cut-off or foreign picture is refused, never half drawn.
/// </summary>
public class QoiTests
{
    private static byte[] Picture(int width, int height, int seed)
    {
        var random = new Random(seed);
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            switch (random.Next(5))
            {
                case 0 when i >= 4: Array.Copy(pixels, i - 4, pixels, i, 4); break;   // a run
                case 1 when i >= 4:                                                    // a small change
                    for (var c = 0; c < 3; c++) pixels[i + c] = (byte)(pixels[i - 4 + c] + random.Next(-2, 2));
                    pixels[i + 3] = pixels[i - 1];
                    break;
                default: random.NextBytes(pixels.AsSpan(i, 4)); break;
            }
        }
        // a long background: the same pixel for a whole row and more
        if (height >= 3) pixels.AsSpan(width * 4, width * 8).Fill(0x7F);
        return pixels;
    }

    [Test]
    [Arguments(1, 1, 1)]
    [Arguments(300, 200, 2)]
    [Arguments(1920, 40, 3)]
    public async Task WhatIsEncoded_DecodesToTheSamePixels(int width, int height, int seed)
    {
        var pixels = Picture(width, height, seed);
        var encoded = new byte[Qoi.MaxSize(width, height)];
        var length = new Qoi(pixels, width, height).Into(encoded);
        var decoded = new byte[pixels.Length];
        await Assert.That(Qoi.Decode(encoded.AsSpan(0, length), decoded)).IsTrue();
        await Assert.That(decoded.SequenceEqual(pixels)).IsTrue();
    }

    [Test]
    public async Task ACutOffOrForeignPicture_IsRefused()
    {
        var pixels = Picture(64, 64, 4);
        var encoded = new byte[Qoi.MaxSize(64, 64)];
        var length = new Qoi(pixels, 64, 64).Into(encoded);
        await Assert.That(Qoi.Decode(encoded.AsSpan(0, length / 2), new byte[pixels.Length])).IsFalse();
        await Assert.That(Qoi.Decode("not a picture at all, no"u8, new byte[16])).IsFalse();
    }
}

namespace PLang.Tests.Runtime.App.IdentityLeak;

/// Security probe (fast-apps full pass, packages): SixLabors.ImageSharp 2.1.13 is read on any bytes a program reads as an
/// image (the image type's ProbeDimensions: Image.Identify). GHSA-wmxv-xphr-5c9g: a 24-byte BigTIFF with a huge IFD entry
/// count keeps the reading thread busy for seconds. Safe = a header read of 24 bytes answers fast.
public class ImageSharpReachProbeTests
{
    private static byte[] BigTiff()
    {
        var b = new byte[24];
        b[0] = (byte)'I'; b[1] = (byte)'I'; b[2] = 0x2B; b[4] = 8;
        b[8] = 16;                                   // first IFD at 16
        for (var i = 16; i < 24; i++) b[i] = 0x7F;   // entry count: huge
        b[23] = 0x00;
        return b;
    }

    [Test]
    public async Task Identify_OnA24ByteBigTiff_AnswersQuickly()
    {
        var bytes = BigTiff();
        // the read runs on a thread of its own so a read that never ends fails the test, not the run (it stays pinned
        // until the process ends)
        var read = System.Threading.Tasks.Task.Run(() =>
        {
            try { SixLabors.ImageSharp.Image.Identify(bytes); } catch (System.Exception) { /* a refusal is fine */ }
        });
        var answered = read.Wait(3000);
        await Assert.That(answered).IsTrue().Because("24 bytes held the reader past 3 s");
    }
}

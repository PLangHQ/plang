using Mp4 = app.module.browser.type.video.code.Mp4;

namespace PLang.Tests.App.actions.browser;

/// <summary>
/// The fragmented MP4 a page's player feeds Media Source, read as a decoder needs it — captured from YouTube (Big Buck
/// Bunny 60 fps, AV1): the init segment's codec, configuration and size; a media segment's samples, in order, each a
/// slice of the segment, starting at a key frame, a 60th of a second apart, together all of its picture data.
/// </summary>
public class Mp4Tests
{
    private static byte[] Fixture(string name)
        => File.ReadAllBytes(Path.Combine(global::PLang.Tests.Shared.Fixture.Root(), "PLang.Tests", "Modules", "App", "Modules", "browser", "fixtures", name));

    [Test]
    public async Task TheInitSegment_SaysTheCodec_ItsConfig_AndItsSize()
    {
        var mp4 = new Mp4();
        await Assert.That(mp4.Init(Fixture("av1-init.mp4"))).IsTrue();
        await Assert.That(mp4.Codec).IsEqualTo("av01");
        await Assert.That(mp4.Config.Length).IsGreaterThan(3);
        await Assert.That(mp4.Config[0] & 0x80).IsEqualTo(0x80).Because("an av1C record starts with its marker bit");
        await Assert.That(mp4.Width).IsGreaterThan(0);
        await Assert.That(mp4.Height).IsGreaterThan(0);
        await Assert.That(mp4.Timescale).IsGreaterThan(0u);
    }

    [Test]
    public async Task AMediaSegment_IsItsPictures_InOrder_FromAKeyFrame()
    {
        var mp4 = new Mp4();
        mp4.Init(Fixture("av1-init.mp4"));
        var segment = Fixture("av1-segment.m4s");
        var samples = mp4.Samples(segment);
        await Assert.That(samples.Count).IsGreaterThan(10);
        await Assert.That(samples[0].Key).IsTrue();
        for (var i = 1; i < samples.Count; i++)
        {
            await Assert.That(samples[i].Time).IsGreaterThan(samples[i - 1].Time);
            await Assert.That(Math.Abs(samples[i].Duration - 1 / 60.0)).IsLessThan(0.002).Because("60 frames a second");
        }
        // the samples are the mdat's payload, back to back, nothing missed
        var total = samples.Sum(s => s.Bytes.Length);
        await Assert.That(total).IsGreaterThan(segment.Length * 9 / 10);
        await Assert.That(total).IsLessThanOrEqualTo(segment.Length);
    }

    [Test]
    public async Task NotMp4_IsNoVideo()
    {
        var mp4 = new Mp4();
        await Assert.That(mp4.Init(new byte[64])).IsFalse();
        await Assert.That(mp4.Samples(new byte[64])).IsEmpty();
    }
}

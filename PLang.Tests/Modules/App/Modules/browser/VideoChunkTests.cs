using Mp4 = app.module.browser.type.video.code.Mp4;

namespace PLang.Tests.App.actions.browser;

/// <summary>
/// A player may append its MP4 cut anywhere — a moof in one append, its mdat in the next, a box in two. However it is
/// cut, the samples read are the same as from the whole segment: nothing lost where a cut fell.
/// </summary>
public class VideoChunkTests
{
    private static byte[] Fixture(string name)
        => File.ReadAllBytes(Path.Combine(global::PLang.Tests.Shared.Fixture.Root(), "PLang.Tests", "Modules", "App", "Modules", "browser", "fixtures", name));

    [Test]
    [Arguments(1000)]
    [Arguments(7919)]
    [Arguments(65536)]
    public async Task ASegmentCutAnywhere_GivesTheSameSamples(int cut)
    {
        var init = Fixture("av1-init.mp4");
        var segment = Fixture("av1-segment.m4s");
        var whole = new Mp4();
        whole.Init(init);
        var expected = whole.Samples(segment).Select(s => (s.Time, s.Bytes.Length)).ToList();

        // what it sends the host: each sample (11) as [u32 id][f64 time][f64 decode][f64 duration][u8 key][bytes]
        var sent = new List<(double, int)>();
        var stream = new global::app.module.browser.type.video.@this(1, (kind, m) =>
        {
            if (kind == 11) sent.Add((BitConverter.ToDouble(m, 4), m.Length - 29));
        });
        stream.Chunk(init, 0);
        for (var at = 0; at < segment.Length; at += cut)
            stream.Chunk(segment[at..Math.Min(segment.Length, at + cut)], 0);
        await Assert.That(sent).IsEquivalentTo(expected);
    }

    // a player leaves a segment half appended and appends one from its start (the same again, after a seek): the
    // reading finds its place again — the second whole
    [Test]
    public async Task ASegmentAgain_AfterHalfOfOne_IsReadWhole()
    {
        var init = Fixture("av1-init.mp4");
        var segment = Fixture("av1-segment.m4s");
        var whole = new Mp4();
        whole.Init(init);
        var times = new List<double>();
        var stream = new global::app.module.browser.type.video.@this(1, (kind, m) => { if (kind == 11) times.Add(BitConverter.ToDouble(m, 4)); });
        stream.Chunk(init, 0);
        stream.Chunk(segment[..(segment.Length / 3)], 0);
        var half = times.Count;
        stream.Chunk(segment, 0);
        await Assert.That(times.Count - half).IsEqualTo(whole.Samples(segment).Count);
    }

    // a quality change: half a segment, then a new init segment and a whole one — of the half, what had come whole is
    // sent (as it came), the rest dropped; the new one read whole
    [Test]
    public async Task ANewInitSegment_AfterHalfASegment_StartsAfresh()
    {
        var init = Fixture("av1-init.mp4");
        var segment = Fixture("av1-segment.m4s");
        var starts = 0;
        var samples = 0;
        var stream = new global::app.module.browser.type.video.@this(1, (kind, _) => { if (kind == 10) starts++; if (kind == 11) samples++; });
        stream.Chunk(init, 0);
        stream.Chunk(segment[..(segment.Length / 2)], 0);
        stream.Chunk(init, 0);
        stream.Chunk(segment, 0);
        var whole = new Mp4();
        whole.Init(init);
        await Assert.That(starts).IsEqualTo(2);
        await Assert.That(samples).IsEqualTo(whole.Samples(segment[..(segment.Length / 2)]).Count + whole.Samples(segment).Count);
    }
}

using Av1 = app.module.screen.type.screen.view.code.Av1;
using Mp4 = app.module.browser.type.video.code.Mp4;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// A page's video the host plays itself: what PlangOS reads out of the page's MP4 (the browser's side) decodes on the
/// host (the view's side) into pictures of the stream's size — on a capture from YouTube, AV1. Needs dav1d (in the
/// PlangOS image; put on LD_LIBRARY_PATH): without it the test says so, it doesn't pass quietly.
/// </summary>
public class PassedVideoTests
{
    private static byte[] Fixture(string name)
        => File.ReadAllBytes(Path.Combine(global::PLang.Tests.Shared.Fixture.Root(), "PLang.Tests", "Modules", "App", "Modules", "browser", "fixtures", name));

    [Test]
    public async Task TheSamplesPlangOSReads_DecodeOnTheHost_ToPicturesOfTheStreamsSize()
    {
        if (!Av1.Here)
        {
            Skip.Test("dav1d isn't on the library path (LD_LIBRARY_PATH=<dir with libdav1d.so.7>)");
            return;
        }
        var mp4 = new Mp4();
        await Assert.That(mp4.Init(Fixture("av1-init.mp4"))).IsTrue();
        using var av1 = new Av1();
        var levels = new List<int>();
        foreach (var sample in mp4.Samples(Fixture("av1-segment.m4s")))
            if (av1.Decode(sample.Bytes) is { } picture)
            {
                await Assert.That((picture.Width, picture.Height)).IsEqualTo((mp4.Width, mp4.Height));
                levels.Add(picture.Y.Take(picture.YStride * picture.Height).Distinct().Count());
            }
        await Assert.That(levels.Count).IsGreaterThan(10);
        // the film opens on black and fades in: its pictures become pictures
        await Assert.That(levels.Max()).IsGreaterThan(16).Because("levels per picture: " + string.Join(",", levels));
    }
}

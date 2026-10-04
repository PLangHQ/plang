using System.Buffers.Binary;
using app.module.screen.type.screen.code;
using Rect = app.module.screen.type.screen.display.code.Rect;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// A page's video the host plays itself, whatever decodes it (each host hands in its own): its picture is drawn where
/// the page shows the key colour, at its place — over the whole screen (the Linux view) or over just the part a window
/// paints (Windows), the same pixels either way — and each new picture says where, so a window repaints just there.
/// </summary>
public class MediaTests
{
    private const int W = 64, H = 48;
    private static readonly (byte r, byte g, byte b) Key = (1, 2, 3);
    private static readonly Rect Place = new(8, 8, 32, 24);

    // a decoder that gives one grey picture per sample, with the sample's time
    private sealed class Grey : IDecoder
    {
        public List<(double time, Yuv picture)> Decode(ReadOnlyMemory<byte> sample, double time)
            => [(time, new Yuv(Enumerable.Repeat((byte)200, 16 * 16).ToArray(), Enumerable.Repeat((byte)128, 8 * 8).ToArray(),
                Enumerable.Repeat((byte)128, 8 * 8).ToArray(), 16, 8, 16, 16))];

        public void Dispose() { }
    }

    private sealed class Stand : IDecoding
    {
        public int Made;
        public string[] Codecs => ["test"];
        public IDecoder? Make(string codec, byte[] config, int width, int height)
        {
            Made++;
            return codec == "test" ? new Grey() : codec == "bad!" ? throw new InvalidOperationException("the decoder refused the stream") : null;
        }
    }

    private static byte[] Start(byte id = 1, string codec = "test") => [10, id, 0, 0, 0, .. System.Text.Encoding.ASCII.GetBytes(codec), 16, 0, 16, 0];

    private static byte[] Sample(byte id = 1)
    {
        var m = new byte[31];
        m[0] = 11; m[1] = id;
        m[29] = 1;   // a key frame, at time 0
        return m;
    }

    private static byte[] Clock(byte id = 1, (byte r, byte g, byte b)? key = null)
    {
        var m = new byte[38];
        m[0] = 12; m[1] = id;
        m[34] = 1;
        (m[35], m[36], m[37]) = key ?? Key;
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(18), Place.X);
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(22), Place.Y);
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(26), Place.Width);
        BinaryPrimitives.WriteInt32LittleEndian(m.AsSpan(30), Place.Height);
        return m;
    }

    // the page: the key colour where the video shows, but a control drawn over it at (20, 20)
    private static byte[] Screen()
    {
        var px = new byte[W * H * 4];
        for (var i = 0; i < px.Length; i += 4) (px[i], px[i + 1], px[i + 2], px[i + 3]) = (Key.b, Key.g, Key.r, 255);
        var c = (20 * W + 20) * 4;
        (px[c], px[c + 1], px[c + 2]) = (250, 250, 250);
        return px;
    }

    private static async Task<Rect> Playing(Media media, (byte r, byte g, byte b)? key = null)
    {
        var presented = new TaskCompletionSource<Rect>(TaskCreationOptions.RunContinuationsAsynchronously);
        media.Presented += place => presented.TrySetResult(place);
        media.Take(Start());
        media.Take(Sample());
        media.Take(Clock(key: key));
        return await presented.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    // a player's controls over a magenta key: half-seen black shows half the video, an opaque control stays the page's
    [Test]
    public async Task UnderAHalfSeenOverlay_TheVideoShowsThrough_AsMuchAsTheOverlayLets()
    {
        using var media = new Media(new Stand());
        await Playing(media, (255, 0, 255));
        var area = new Rect(10, 10, 3, 1);
        // BGRA: magenta; magenta under 50% black (128, 0, 128); an opaque grey control
        byte[] px = [255, 0, 255, 255, 128, 0, 128, 255, 90, 90, 90, 255];
        media.Draw(px, area);
        // the grey picture (Y 200, no colour) is ~214 in each channel
        var full = px[0];
        await Assert.That((int)full).IsBetween(205, 225);
        await Assert.That((int)px[4]).IsEqualTo((128 * full + 127) / 255).Because("half the video under the half-seen black");
        await Assert.That(px.AsSpan(8, 3).ToArray()).IsEquivalentTo(new byte[] { 90, 90, 90 });
    }

    // a video in a window behind another (none of its place shows) isn't decoded; it is again when it shows
    [Test]
    public async Task AVideoNoneOfWhichShows_IsNotDecoded_UntilItShows()
    {
        var stand = new Stand();
        using var media = new Media(stand);
        var showing = false;
        media.Seen = (_, _) => showing;
        media.Take(Start());
        media.Take(Sample());
        media.Take(Clock());
        await Task.Delay(300);
        await Assert.That(stand.Made).IsEqualTo(0);
        await Assert.That(media.Numbers).Contains("0 decoded");
        showing = true;
        for (var i = 0; i < 50 && stand.Made == 0; i++) await Task.Delay(20);
        await Assert.That(stand.Made).IsEqualTo(1);
    }

    // before its first picture, the video's place is black, not the key colour
    [Test]
    public async Task BeforeItsFirstPicture_ItsPlaceIsBlack()
    {
        using var media = new Media(new Stand());
        media.Take(Start(codec: "none"));   // no decoder: no picture ever
        media.Take(Clock(key: (255, 0, 255)));
        byte[] px = [255, 0, 255, 255];
        media.Draw(px, new Rect(10, 10, 1, 1));
        await Assert.That(px).IsEquivalentTo(new byte[] { 0, 0, 0, 255 });
    }

    [Test]
    public async Task ANewPicture_SaysItsPlace_AndShowsThere()
    {
        using var media = new Media(new Stand());
        var place = await Playing(media);
        await Assert.That(place).IsEqualTo(Place);
        await Assert.That(media.Places).IsEquivalentTo(new[] { Place });
        await Assert.That(media.Codecs).IsEquivalentTo(new[] { "test" });
    }

    // a decoder that won't start: why is in the numbers, and it isn't asked for again sixty times a second
    [Test]
    public async Task ADecoderThatWontStart_IsSaid_AndNotRetriedEveryTick()
    {
        var stand = new Stand();
        using var media = new Media(stand);
        media.Take(Start(codec: "bad!"));
        media.Take(Sample());
        media.Take(Clock());
        await Task.Delay(500);   // ~30 presenter ticks
        await Assert.That(media.Numbers).Contains("the decoder refused the stream");
        await Assert.That(stand.Made).IsEqualTo(1);
    }

    // two windows' videos may overlap on the screen: both play (a page that goes away is ended by PlangOS, not guessed)
    [Test]
    public async Task TwoVideosAtOverlappingPlaces_BothPlay()
    {
        using var media = new Media(new Stand());
        await Playing(media);
        media.Take(Start(2));
        media.Take(Sample(2));
        media.Take(Clock(2));
        await Assert.That(media.Numbers).Contains("stream 1:");
        await Assert.That(media.Numbers).Contains("stream 2:");
    }

    [Test]
    public async Task DrawnOverAnArea_IsThatPartOfItDrawnOverTheWholeScreen()
    {
        using var media = new Media(new Stand());
        await Playing(media);
        var whole = Screen();
        media.Draw(whole, new Rect(0, 0, W, H));

        var area = new Rect(20, 10, 30, 30);   // part in the place, part out of it
        var screen = Screen();
        var part = new byte[area.Width * area.Height * 4];
        for (var y = 0; y < area.Height; y++)
            Buffer.BlockCopy(screen, ((area.Y + y) * W + area.X) * 4, part, y * area.Width * 4, area.Width * 4);
        media.Draw(part, area);

        for (var y = 0; y < area.Height; y++)
            await Assert.That(part.AsSpan(y * area.Width * 4, area.Width * 4).SequenceEqual(whole.AsSpan(((area.Y + y) * W + area.X) * 4, area.Width * 4)))
                .IsTrue().Because($"row {area.Y + y}");
        // the video where the key colour was, the control over it kept, the key colour outside the place untouched
        var inside = ((12 - area.Y) * area.Width + (22 - area.X)) * 4;
        await Assert.That(part[inside]).IsNotEqualTo(Key.b);
        var control = ((20 - area.Y) * area.Width + 0) * 4;
        await Assert.That(part[control]).IsEqualTo((byte)250);
        var outside = ((36 - area.Y) * area.Width + (45 - area.X)) * 4;
        await Assert.That(part[outside]).IsEqualTo(Key.b);
    }
}

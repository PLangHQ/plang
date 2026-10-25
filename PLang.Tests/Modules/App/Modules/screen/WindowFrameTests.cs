using System.Buffers.Binary;
using Qoi = app.module.screen.type.screen.code.Qoi;
using Rect = app.module.screen.type.screen.display.code.Rect;
using Shown = app.module.screen.type.screen.code.Shown;
using View = app.module.screen.type.screen.view.@this;

namespace PLang.Tests.App.actions.screen;

/// <summary>
/// A frame's rectangle is bounded by screen.setting.max before anything is decoded, whatever its message says it
/// holds: through the shared screen every host applies frames with — a binary frame (kinds 1 and 7, the Linux view's
/// path and the window's) and a rectangle as a text frame hands it in (the window's).
/// </summary>
public class WindowFrameTests : System.IAsyncDisposable
{
    private const int W = 32, H = 24;

    private readonly global::app.@this _app = new global::app.@this("/windowframetests", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _app.DisposeAsync();

    private global::app.actor.context.@this Context => _app.actor.list.User.Context;

    private async Task<global::app.type.item.size.@this> Max()
        => (await Context.Setting.Get<global::app.module.screen.setting.@this>()).Max;

    // [1][u16 1] then one rectangle's head claiming w×h, and its bytes
    private static byte[] Frame(int w, int h, byte[] bytes)
    {
        var m = new byte[1 + 2 + 20 + bytes.Length];
        m[0] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(m.AsSpan(1), 1);
        BinaryPrimitives.WriteUInt32LittleEndian(m.AsSpan(11), (uint)w);
        BinaryPrimitives.WriteUInt32LittleEndian(m.AsSpan(15), (uint)h);
        BinaryPrimitives.WriteUInt32LittleEndian(m.AsSpan(19), (uint)bytes.Length);
        bytes.CopyTo(m, 23);
        return m;
    }

    private static byte[] Qoied(int w, int h, byte seed)
    {
        var px = Enumerable.Range(0, w * h * 4).Select(i => (byte)(i * seed)).ToArray();
        var q = new byte[Qoi.MaxSize(w, h)];
        return q[..new Qoi(px, w, h).Into(q)];
    }

    [Test]
    public async Task ABinaryFrameClaimingMoreThanTheMax_IsBadFrame_BeforeAnyDecode()
    {
        var refused = await Assert.That(async () => new Shown(W, H).Apply(Frame(50000, 50000, [1, 2, 3, 4, 5]), await Max()))
            .Throws<global::app.error.AppException>();
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
        await Assert.That(refused.Error.Message).Contains("--screen.setting.max=");
    }

    [Test]
    public async Task ATextFramesRectangleClaimingMoreThanTheMax_IsBadFrame_BeforeAnyDecode()
    {
        var max = await Max();
        var refused = await Assert.That(() => new Shown(W, H).Apply("{\"rect\":[0,0,50000,50000],\"qoi\":\"AAAA\"}", max))
            .Throws<global::app.error.AppException>();
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
    }

    [Test]
    public async Task ARectangleWithinTheMax_GoesIn()
    {
        var shown = new Shown(W, H);
        var max = await Max();
        var put = shown.Apply("{\"rect\":[2,3,8,4],\"qoi\":\"" + Convert.ToBase64String(Qoied(8, 4, 7)) + "\"}", max);
        await Assert.That(put).IsEqualTo(new Rect(2, 3, 8, 4));
        await Assert.That(shown.Apply(Frame(4, 4, Qoied(4, 4, 3)), max)).IsEqualTo(new Rect(0, 0, 4, 4));
    }

    // a max past what an array holds (16 GiB) still bounds a rectangle by what its pixels can be held in: 30000×30000
    // is 3.6 GB of pixels, more than one array takes — BadFrame, never an overflow
    [Test]
    public async Task ARectanglePastWhatAnArrayHolds_IsBadFrame_UnderAHugeMax()
    {
        await (await new global::app.type.item.variable.parser.@this("%!screen.setting.max%", Context.App.type.list).Variable.Single()
            .Set(Context.Ok(Context.Text("16 GiB")), Context)).IsSuccess();
        var max = await Max();
        var refused = await Assert.That(() => new Shown(W, H).Apply(Frame(30000, 30000, [1, 2, 3, 4, 5]), max))
            .Throws<global::app.error.AppException>();
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
    }

    // a text frame's rectangles — {"rects":[[x,y,w,h,"<qoi base64>"],…]} — through the same door: one past the max, or
    // one whose QOI isn't base64, is the frame's BadFrame, answered whole (never a throw out of a parallel decode)
    [Test]
    [Arguments("{\"rects\":[[0,0,4,4,\"cW9pZg==\"],[0,0,50000,50000,\"AAAA\"]]}")]
    [Arguments("{\"rects\":[[0,0,4,4,\"not base64!\"]]}")]
    [Arguments("{\"rect\":[0,0,4,4],\"qoi\":\"not base64!\"}")]
    [Arguments("{\"rects\":[[0,0]]}")]
    [Arguments("{\"rects\":[[0,0,4,4,null]]}")]
    public async Task ATextFrameWithABadRectangle_IsBadFrame(string line)
    {
        var max = await Max();
        var refused = await Assert.That(() => new Shown(W, H).Apply(line, max)).Throws<global::app.error.AppException>();
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
    }

    [Test]
    public async Task ATextFrame_GoesIn()
    {
        var max = await Max();
        var qoi = Convert.ToBase64String(Qoied(4, 4, 3));
        var shown = new Shown(W, H);
        var changed = shown.Apply("{\"rects\":[[0,0,4,4,\"" + qoi + "\"],[8,8,4,4,\"" + qoi + "\"]]}", max);
        await Assert.That(changed).IsEqualTo(new Rect(0, 0, 12, 12));
        await Assert.That(shown.Apply("{\"rect\":[20,10,4,4],\"qoi\":\"" + qoi + "\"}", max)).IsEqualTo(new Rect(20, 10, 4, 4));
    }

    // the Linux view's own path: a frame past the max is refused on its thread, told on the actor's error channel, and
    // changes nothing; one within it shows
    [Test]
    public async Task TheView_ReportsAFramePastTheMax_AndShowsTheNext()
    {
        var errors = (global::app.channel.type.stream.@this)Context.Actor.Channel.CreateMemoryChannel(global::app.channel.list.@this.Error);
        var view = (View)(await View.Start("frames", W, H, null, null, Context).Value())!;
        var max = await Max();
        await Assert.That(view.Show(Frame(50000, 50000, [1, 2, 3, 4, 5]), max)).IsTrue();
        await Assert.That(view.Show(Frame(4, 4, Qoied(4, 4, 3)), max)).IsTrue();
        global::app.module.screen.type.screen.code.Bitmap? read = null;
        for (var i = 0; i < 100 && read?.Pixels.Take(4 * 4).Any(b => b != 0) != true; i++)
        {
            await Task.Delay(20);
            read = new global::app.module.screen.type.screen.code.Bitmap(view.Picture.Bytes, max);
        }
        await Assert.That(read!.Pixels.Take(4 * 4).Any(b => b != 0)).IsTrue().Because("the frame after the refused one shows");
        var told = "";
        for (var i = 0; i < 100 && !told.Contains("screen.setting.max"); i++)
        {
            await Task.Delay(20);
            told = System.Text.Encoding.UTF8.GetString(((MemoryStream)errors.Stream).ToArray());
        }
        await Assert.That(told).Contains("screen.setting.max").Because("the refused frame is told where the app's errors go");
        view.Close();
    }
}

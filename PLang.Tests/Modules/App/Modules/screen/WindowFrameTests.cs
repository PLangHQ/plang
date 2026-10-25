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
        var refused = await Assert.That(() => new Shown(W, H).Put(0, 0, 50000, 50000, [1, 2, 3, 4, 5], max))
            .Throws<global::app.error.AppException>();
        await Assert.That(refused!.Error.Key).IsEqualTo("BadFrame");
    }

    [Test]
    public async Task ARectangleWithinTheMax_GoesIn()
    {
        var shown = new Shown(W, H);
        var max = await Max();
        var put = shown.Put(2, 3, 8, 4, Qoied(8, 4, 7), max);
        await Assert.That(put).IsEqualTo(new Rect(2, 3, 8, 4));
        await Assert.That(shown.Apply(Frame(4, 4, Qoied(4, 4, 3)), max)).IsEqualTo(new Rect(0, 0, 4, 4));
    }

    // the Linux view's own path: a frame past the max is refused on its thread and changes nothing; one within it shows
    [Test]
    public async Task TheView_SkipsAFramePastTheMax_AndShowsTheNext()
    {
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
        view.Close();
    }
}

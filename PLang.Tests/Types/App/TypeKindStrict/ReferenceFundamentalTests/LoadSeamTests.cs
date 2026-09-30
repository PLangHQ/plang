using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Text;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.TypeKindStrict.ReferenceFundamentalTests;

// The materialize seam: a path-backed image is lazy through set + navigation, and a
// sync writer sees only what it holds. The pull is `data.Value()`, or `Open` when a
// stream channel is about to send it — the format never loads. A load/strict failure
// rides ONTO the data binding (data.Fail), it does not throw.
public class LoadSeamTests
{
    private global::app.@this _app = null!;

    private global::app.actor.context.@this Ctx => _app.actor.list.User.Context;
    private global::app.type.kind.@this Plang => Ctx.Format("application/plang");

    [Before(Test)]
    public void Setup()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-load-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        _app = new global::app.@this(root).Testing();
    }

    [After(Test)]
    public void Cleanup() => _app.DisposeAsync().AsTask().GetAwaiter().GetResult();

    // A full 1x1 PNG (ImageSharp DetectFormat identifies it as png).
    private static readonly byte[] Png1x1 =
    {
        0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
        0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
        0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
        0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
        0x42,0x60,0x82
    };

    private image PathBackedPng(string name)
    {
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(_app.AbsolutePath, name), Png1x1);
        return new image(global::app.type.item.path.@this.Resolve(
            System.IO.Path.Combine(_app.AbsolutePath, name), _app.actor.list.User.Context), _app.actor.list.User.Context);
    }

    [Test] public async Task Value_MaterializesPathBackedImage_SyncBytesThenReal()
    {
        var img = PathBackedPng("a.png");
        await Assert.That(img.Bytes.Length).IsEqualTo(0); // lazy — nothing read yet

        var data = _app.actor.list.User.Context.Ok(img);
        await data.Value();                 // the async pull

        await data.IsSuccess();
        await Assert.That(img.Bytes).IsEquivalentTo(Png1x1); // sync view now real
    }

    [Test] public async Task WrittenThroughStreamChannel_NestedImage_WritesItsPath_Unread()
    {
        // The channel opens only the value it writes; an image nested inside it is written unread — as where it
        // is, never empty bytes.
        var img = PathBackedPng("nested.png");
        var dict = new System.Collections.Generic.Dictionary<string, object?> { ["avatar"] = img };
        await using var channel = PlangChannel();

        var result = await channel.Write(_app.actor.list.User.Context.Ok(dict));
        await result.IsSuccess();

        var json = Written(channel);
        await Assert.That(img.RawBytes).IsNull();
        await Assert.That(json).Contains("nested.png");
        await Assert.That(json).DoesNotContain("iVBOR");
    }

    [Test] public async Task Value_StrictMismatch_FailsOntoBinding_NoThrow()
    {
        var img = PathBackedPng("shot.png");
        img.RequireStrictKind("gif"); // png content behind strict gif

        var data = _app.actor.list.User.Context.Ok(img);
        await data.Value();             // fails onto the binding, does not throw

        await data.IsFailure();
        await Assert.That(data.Error!.Key).IsEqualTo("StrictKindMismatch");
    }

    [Test] public async Task Value_NoLazyContent_IsNoOp()
    {
        // A bytes-backed image and a scalar graph carry nothing lazy — materializing succeeds.
        var bytesBacked = Data.Ok(new image(Png1x1, "image/png"));
        await bytesBacked.Value();
        await bytesBacked.IsSuccess();

        var scalar = _app.actor.list.User.Context.Ok(new System.Collections.Generic.Dictionary<string, object?> { ["n"] = 42L, ["s"] = "x" });
        await scalar.Value();
        await scalar.IsSuccess();
    }

    // A plang-formatted stream channel over memory — the door a value leaves the program through.
    private static global::app.channel.type.stream.@this PlangChannel() =>
        new("out", new System.IO.MemoryStream()) { Mime = "application/plang" };

    private static string Written(global::app.channel.type.stream.@this channel) =>
        Encoding.UTF8.GetString(((System.IO.MemoryStream)channel.Stream).ToArray());

    [Test] public async Task WrittenThroughStreamChannel_PathBackedImage_EmitsRealBytes()
    {
        // The channel opens the value at the last moment: the image's bytes are read, then the format writes them.
        var img = PathBackedPng("out.png");
        await using var channel = PlangChannel();

        var result = await channel.Write(_app.actor.list.User.Context.Ok(img));
        await result.IsSuccess();

        // PNG base64 starts with "iVBOR"; an unopened image would emit "" instead.
        await Assert.That(Written(channel)).Contains("iVBOR");
    }

    [Test] public async Task EncodedWithoutChannel_PathBackedImage_NeverLoads()
    {
        // A format writes what the value holds; only a channel opens it.
        var img = PathBackedPng("unopened.png");
        using var ms = new System.IO.MemoryStream();

        var result = await Plang.Encode(ms, _app.actor.list.User.Context.Ok(img), Ctx);
        await result.IsSuccess();

        await Assert.That(img.RawBytes).IsNull();
        await Assert.That(Encoding.UTF8.GetString(ms.ToArray())).DoesNotContain("iVBOR");
    }

    // A store keeps the reference, a dump does no I/O — neither opens the value.
    [Test] public async Task StoreAndDebug_PathBackedImage_NeverLoad()
    {
        var img = PathBackedPng("kept.png");
        using var ms = new System.IO.MemoryStream();

        var stored = await Plang.Encode(ms, _app.actor.list.User.Context.Ok(img), Ctx, global::app.View.Store);
        await stored.IsSuccess();
        var dumped = await img.Debug(Ctx);

        await Assert.That(img.RawBytes).IsNull();
        await Assert.That(Encoding.UTF8.GetString(ms.ToArray())).DoesNotContain("iVBOR");
        await Assert.That(dumped).DoesNotContain("iVBOR");
    }

    [Test] public async Task WrittenThroughStreamChannel_StrictMismatch_FailsCleanly_BeforeStreamWrite()
    {
        var img = PathBackedPng("bad.png");
        img.RequireStrictKind("gif");
        await using var channel = PlangChannel();

        var result = await channel.Write(_app.actor.list.User.Context.Ok(img));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("StrictKindMismatch");
        await Assert.That(channel.Stream.Length).IsEqualTo(0); // nothing written
    }
}

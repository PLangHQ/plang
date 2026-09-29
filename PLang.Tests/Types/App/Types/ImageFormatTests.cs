namespace PLang.Tests.App.Types;

// A type that reads a format writes it: an image value is its bytes.
public class ImageFormatTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52];

    [Test] public async Task AnImageSavedToPng_IsItsBytes()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-img-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        try
        {
            await using var app = new global::app.@this(root).Testing();
            var ctx = app.actor.list.User.Context;
            var target = global::app.type.item.path.@this.Resolve(System.IO.Path.Combine(root, "photo.png"), ctx)!;
            var saved = await target.Save(ctx.Ok(new global::app.type.item.image.@this(Png, "image/png", "png")), ctx);
            await saved.IsSuccess();
            await Assert.That(await System.IO.File.ReadAllBytesAsync(System.IO.Path.Combine(root, "photo.png"))).IsEquivalentTo(Png);
        }
        finally { System.IO.Directory.Delete(root, recursive: true); }
    }

    [Test] public async Task AnImageChannelWrite_WritesTheBytes()
    {
        await using var app = new global::app.@this("/test", autoWireConsoleChannels: false).Testing();
        var capture = new System.IO.MemoryStream();
        var channel = new StreamChannel("img", capture, ChannelDirection.Output, ownsStream: false) { Mime = "image/png" };
        app.actor.list.User.Channel.Register(channel);

        var written = await channel.Write(app.actor.list.User.Context.Ok(new global::app.type.item.image.@this(Png, "image/png", "png")));
        await written.IsSuccess();
        await Assert.That(capture.ToArray()).IsEquivalentTo(Png);
    }

    [Test] public async Task AnImageFormat_RefusesAValueThatIsNoImage()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var result = await app.type.list.Stamp("image/png", ctx).kind.Encode(new System.IO.MemoryStream(), ctx.Ok("not an image"), ctx);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NoEncoder");
    }
}

using base64 = global::app.type.item.base64.@this;
using text = global::app.type.item.text.@this;
using binary = global::app.type.item.binary.@this;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Types;

// base64 — a string-backed encoded value. `as base64` (courier) ENCODES lazily; a DECLARED
// base64 payload validates (Parse). data-url mime rides as the Kind tail token; the payload is
// the value. byte face (RawBytes / Clr byte[]) = the decoded bytes.
public class Base64Tests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this(
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-base64-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static readonly byte[] PngBytes = new byte[]
    { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52 };

    [Test] public async Task Registered_AsAType()
        => await Assert.That(app.type.list["base64"]).IsNotNull();

    // --- Parse: the validate door (reader / born path) ---

    [Test] public async Task Parse_ValidPayload_KeepsIt()
        => await Assert.That(base64.Parse("SGVsbG8=").ToString()).IsEqualTo("SGVsbG8=");

    [Test] public async Task Parse_InvalidPayload_Throws()
        => await Assert.That(() => base64.Parse("not!base64!")).Throws<System.FormatException>();

    [Test] public async Task Parse_DataUrl_MimeTailIsKind_PayloadIsValue()
    {
        var b64 = base64.Parse("data:image/gif;base64,R0lGODdh");
        await Assert.That(b64.ToString()).IsEqualTo("R0lGODdh");
        await Assert.That(b64.Type.kind.Name).IsEqualTo("gif");
        await Assert.That(b64.Type.Name).IsEqualTo("base64");   // OWN name, never {image,gif}
    }

    [Test] public async Task Parse_DataUrl_NotBase64Encoded_Throws()
        => await Assert.That(() => base64.Parse("data:text/plain,hello")).Throws<System.FormatException>();

    // --- Courier: ENCODE ---

    [Test] public async Task AsBase64_EncodesText_Lazily()
    {
        var given = app.actor.list.User.Context.Ok(new text("hello"));
        var b64 = base64.Create(new text("hello"), given.Type, given);
        var ready = await b64!.Value(app.actor.list.User.Context.Ok(b64));
        await Assert.That(ready.ToString()).IsEqualTo("aGVsbG8=");     // base64("hello"), NOT validate
    }

    [Test] public async Task AsBase64_EncodesImageBytes_NotDoubleEncoded()
    {
        var img = image.FromBytes(PngBytes);
        var given = app.actor.list.User.Context.Ok(img!);
        var b64 = base64.Create(img, given.Type, given);
        var ready = await b64!.Value(app.actor.list.User.Context.Ok(b64));
        // base64 of the RAW png bytes — not base64 of image's own base64 json render
        await Assert.That(ready.ToString()).IsEqualTo(System.Convert.ToBase64String(PngBytes));
    }

    // A base64 written in place in a template encodes its source there — the template writes it through its Output,
    // not its Value door (educator: "pcm:%b64%" wrote "pcm:").
    [Test] public async Task AsBase64_InATemplate_WritesItsPayload()
    {
        var ctx = app.actor.list.User.Context;
        var given = ctx.Ok(new text("hello"));
        await ctx.Variable.Set("b64", base64.Create(new text("hello"), given.Type, given));
        var line = new global::app.data.@this("line", new text("pcm:%b64%", "plang"), context: ctx);
        await Assert.That((await line.Value())?.ToString()).IsEqualTo("pcm:aGVsbG8=");
    }

    // --- byte face + cross-type ---

    [Test] public async Task Base64_ToBinary_Decodes()
    {
        var b64 = new base64(System.Convert.ToBase64String(PngBytes));
        var bin = binary.Create(b64);                                 // binary's existing base64 core
        await Assert.That(bin).IsNotNull();
        await Assert.That(bin!.Value).IsEquivalentTo(PngBytes);
    }

    [Test] public async Task Base64_ToImage_SniffsMime()
    {
        var b64 = new base64(System.Convert.ToBase64String(PngBytes));
        var img = image.Create(b64);
        await Assert.That(((image)img!).Mime).IsEqualTo("image/png");
    }

    [Test] public async Task Clr_ByteTarget_DecodesBytes()
    {
        var b64 = new base64(System.Convert.ToBase64String(PngBytes));
        await Assert.That(b64.Clr<byte[]>()).IsEquivalentTo(PngBytes);
    }
}

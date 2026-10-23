using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// Stage 1 (item-doors, decisions 867 + 880): a value made from bytes holds its REGISTERED
/// kind — not a fresh kind built from a name. The value is born through the app's type
/// door (<c>app.type.list["image"].Create(bytes, ctx)</c>), not via <c>new</c>, since no
/// value is made without its app (880).
///
/// <para>Red today: <c>type.Create(bytes, ctx)</c> routes through image's <c>Create</c>
/// → <c>FromBytes</c> → <c>new image(bytes, mime, kind)</c> (<c>image/this.cs:185–190</c>),
/// whose kind is built inline as <c>new kind.@this(kind)</c> (<c>:189</c>). The bare kind
/// has no Mime, so <c>Type.kind.Mime</c> is empty — the registered <c>image.kind.png</c>'s
/// <c>image/png</c> never reaches the value.</para>
/// </summary>
public class ImageKindKnowsItsMimeTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task ImageFromBytes_Kind_CarriesItsRegisteredMime()
    {
        var ctx = app.actor.list.System.Context;
        var data = await app.type.list["image"].Create(PngHeader, ctx);
        var img = (image)await data.Value();

        await Assert.That(img.Type.kind).IsNotNull();
        await Assert.That(img.Type.kind.Mime).Contains("image/png");
    }
}

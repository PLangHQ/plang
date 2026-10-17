using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Types;

/// <summary>
/// Stage 1 (item-doors): a value made from bytes holds its REGISTERED kind — not a fresh
/// kind built from a name. An image constructed from bytes should carry its kind's facts
/// (its MIME, its extension). Today the image makes its kind inline from a name
/// (<c>type/item/image/this.cs:189</c>: <c>new kind.@this(kind)</c>), so the kind arrives
/// bare — its <c>Mime</c> list is empty. Red today for exactly that reason.
/// </summary>
public class ImageKindKnowsItsMimeTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task ImageFromBytes_Kind_CarriesItsRegisteredMime()
    {
        var img = new image(PngHeader, "image/png", "png");
        await Assert.That(img.Type.kind).IsNotNull();
        await Assert.That(img.Type.kind.Mime).Contains("image/png");
    }
}

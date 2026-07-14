using image = global::app.type.item.image.@this;
using base64 = global::app.type.item.base64.@this;

namespace PLang.Tests.App.Types;

// image meets only BYTES now — FromBytes(byte[]) sniffs the mime off the magic bytes.
// The string forms moved off image: a data-url / bare base64 is a `base64` value whose
// decoded bytes reach image via the Create base64 arm; a file path loads through the path verbs.

public class ImageParseTests
{
    private static readonly byte[] PngBytes = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    };
    private static readonly byte[] JpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 };

    [Test] public async Task DataUrl_AsBase64_ThenImage_SniffsMimeFromBytes()
    {
        // A data-url is a base64 value (kind off the mime header); its decoded bytes build the
        // image through Create's base64 arm — mime sniffed off the magic bytes (content truth).
        var dataUrl = "data:image/png;base64," + System.Convert.ToBase64String(PngBytes);
        var img = image.Create(base64.Parse(dataUrl));
        await Assert.That(img).IsNotNull();
        await Assert.That(((image)img!).Mime).IsEqualTo("image/png");
    }

    [Test] public async Task RawBase64_AsBase64_ThenImage_DetectsAsImage()
    {
        var b64 = new base64(System.Convert.ToBase64String(PngBytes));
        var img = image.Create(b64);
        await Assert.That(img).IsNotNull();
        await Assert.That(((image)img!).Mime).IsEqualTo("image/png");
    }

    [Test] public async Task FromBytes_PngMagicBytes_PicksImagePng()
    {
        var img = image.FromBytes(PngBytes);
        await Assert.That(img!.Mime).IsEqualTo("image/png");
    }

    [Test] public async Task FromBytes_JpegMagicBytes_PicksImageJpeg()
    {
        var img = image.FromBytes(JpegBytes);
        await Assert.That(img!.Mime).IsEqualTo("image/jpeg");
    }

    [Test] public async Task FromBytes_NonImageBytes_ReturnsNull_NoThrow()
    {
        await Assert.That(image.FromBytes(new byte[] { 0x00, 0x01, 0x02, 0x03 })).IsNull();
    }
}

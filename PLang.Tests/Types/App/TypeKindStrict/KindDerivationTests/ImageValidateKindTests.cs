using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using ImageType = global::app.type.item.image.@this;

namespace PLang.Tests.App.TypeKindStrict.KindDerivationTests;

public class ImageValidateKindTests
{
    // Minimal 1×1 transparent GIF (smallest legal GIF87a payload).
    private static readonly byte[] GifBytes = new byte[]
    {
        0x47, 0x49, 0x46, 0x38, 0x37, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00,
        0x00, 0x00, 0x00, 0x00, 0xFF, 0xFF, 0xFF, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B
    };

    // Minimal 1×1 PNG.
    private static readonly byte[] PngBytes = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D,
        0x49, 0x48, 0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00,
        0x0D, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x62, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49,
        0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    };

    [Test] public async Task ValidateKind_GifBytesMatchingGif_OkTrue()
    {
        var img = new ImageType(GifBytes, "image/gif");
        var (ok, actual) = img.ValidateKind(GifBytes, "gif");
        await Assert.That(ok).IsTrue();
        await Assert.That(actual).IsNull();
    }

    [Test] public async Task ValidateKind_PngBytesMatchingGif_OkFalse_ActualIsPng()
    {
        var img = new ImageType(PngBytes, "image/png");
        var (ok, actual) = img.ValidateKind(PngBytes, "gif");
        await Assert.That(ok).IsFalse();
        await Assert.That(actual).IsEqualTo("png");
    }

    [Test] public async Task ValidateKind_GarbageBytes_OkFalse()
    {
        var garbage = new byte[] { 0, 1, 2, 3, 4, 5 };
        var img = new ImageType(garbage, "application/octet-stream");
        var (ok, actual) = img.ValidateKind(garbage, "gif");
        await Assert.That(ok).IsFalse();
        await Assert.That(actual).IsNull();
    }

    [Test] public async Task ValidateKind_EmptyBytes_OkFalse()
    {
        var empty = System.Array.Empty<byte>();
        var img = new ImageType(empty, "application/octet-stream");
        var (ok, actual) = img.ValidateKind(empty, "gif");
        await Assert.That(ok).IsFalse();
        await Assert.That(actual).IsNull();
    }
}

using image = global::app.type.image.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// image.Resolve(string) — path / data-url / base64 disambiguation.
// image.Resolve(byte[]) — direct construction with mime sniffed from magic bytes.
// Sync Resolve handles in-memory forms only; file/http paths require async (ResolveAsync).

public class ImageParseTests
{
    private static readonly byte[] PngBytes = new byte[]
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52
    };
    private static readonly byte[] JpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00 };

    private static global::app.@this NewApp()
        => TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-imgparse-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task Resolve_FilePath_Constructs_FromFileBytes()
    {
        await using var app = NewApp();
        System.IO.Directory.CreateDirectory(app.AbsolutePath);
        var rel = "img-parse-" + System.Guid.NewGuid().ToString("N")[..8] + ".png";
        var abs = System.IO.Path.Combine(app.AbsolutePath, rel);
        System.IO.File.WriteAllBytes(abs, PngBytes);
        try
        {
            var img = await image.ResolveAsync(abs, app.User.Context);
            await Assert.That(img).IsNotNull();
            await Assert.That(img!.Mime).IsEqualTo("image/png");
            await Assert.That(img.Bytes.Length).IsEqualTo(PngBytes.Length);
        }
        finally { try { System.IO.File.Delete(abs); } catch { } }
    }

    [Test] public async Task Resolve_DataUrl_PicksMimeFromHeader()
    {
        await using var app = NewApp();
        var b64 = System.Convert.ToBase64String(PngBytes);
        var dataUrl = "data:image/png;base64," + b64;
        var img = image.Resolve(dataUrl, app.User.Context);
        await Assert.That(img!.Mime).IsEqualTo("image/png");
    }

    [Test] public async Task Resolve_RawBase64String_DetectsAsImage()
    {
        await using var app = NewApp();
        var b64 = System.Convert.ToBase64String(PngBytes);
        var img = image.Resolve(b64, app.User.Context);
        await Assert.That(img).IsNotNull();
        await Assert.That(img!.Mime).IsEqualTo("image/png");
    }

    // image.ResolveAsync's http branch is latent (no shipping handler binds an
    // image from a URL string). Deferral tracked in Documentation/v0.2/todos.md
    // "image.@this HTTP fetch via ResolveAsync". Real test + mock HTTP server
    // land when a handler that consumes Data<image> from a URL ships.

    [Test] public async Task Resolve_ByteArray_PngMagicBytes_PicksImagePng()
    {
        var img = image.FromBytes(PngBytes);
        await Assert.That(img!.Mime).IsEqualTo("image/png");
    }

    [Test] public async Task Resolve_ByteArray_JpegMagicBytes_PicksImageJpeg()
    {
        var img = image.FromBytes(JpegBytes);
        await Assert.That(img!.Mime).IsEqualTo("image/jpeg");
    }

    [Test] public async Task Resolve_GarbageString_ReturnsNull_NoThrow()
    {
        await using var app = NewApp();
        await Assert.That(image.Resolve("not an image", app.User.Context)).IsNull();
        await Assert.That(image.Resolve("", app.User.Context)).IsNull();
    }
}

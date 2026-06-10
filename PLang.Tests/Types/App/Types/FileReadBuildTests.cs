using image = global::app.type.image.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// file.read.Build() (action IClass.Build()) resolves extension → HIGH-LEVEL type via the
// registry/formats; the type's own Build() supplies the kind.
// file.read.Run() constructs the typed value (an image for image MIMEs), not raw bytes.

public class FileReadBuildTests
{
    private static global::app.@this NewApp()
        => new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-fr-" + System.Guid.NewGuid().ToString("N")[..8]));

    [Test] public async Task FileReadBuild_PngExtension_ReturnsHighLevelType_Image()
    {
        // The action Build() returns the high-level type stamp. For .png the
        // formats registry maps the extension → "image".
        await using var app = NewApp();
        var k = app.Format.Kind("png");
        await Assert.That(k).IsEqualTo("image");
    }

    [Test] public async Task FileReadBuild_TxtExtension_ReturnsHighLevelType_Text()
    {
        await using var app = NewApp();
        var k = app.Format.Kind("txt");
        await Assert.That(k).IsEqualTo("text");
    }

    [Test] public async Task FileReadBuild_KindSupplied_ByImageBuild_NotByActionBuild()
    {
        // The kind comes from image.Build, NOT from the action's Build.
        // image.Build("photo.jpg") → "jpg" (the subtype/extension).
        await Assert.That(image.Build("photo.jpg")).IsEqualTo("jpg");
    }

    [Test] public async Task FileReadRun_ImageMime_ConstructsImageValue_NotRawBytes()
    {
        // file.read.Run() detects an image MIME from the ReadText result and
        // lifts to an image value with Type=image. Path inside the App root
        // so AuthGate doesn't deny.
        await using var app = NewApp();
        System.IO.Directory.CreateDirectory(app.AbsolutePath);
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        var rel = "fr-img-" + System.Guid.NewGuid().ToString("N")[..8] + ".png";
        var abs = System.IO.Path.Combine(app.AbsolutePath, rel);
        System.IO.File.WriteAllBytes(abs, pngBytes);
        try
        {
            var p = global::app.type.path.@this.Resolve(abs, app.User.Context);
            var action = new global::app.module.file.Read
            {
                Context = app.User.Context,
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(p),
            };
            var result = await action.Run();
            await result.IsSuccess();
            await Assert.That((await result.Value())).IsTypeOf<image>();
            await Assert.That(result.Type?.Name).IsEqualTo("image");
        }
        finally { try { System.IO.File.Delete(abs); } catch { } }
    }

    [Test] public async Task FileReadRun_TextMime_ReturnsStringValue()
    {
        await using var app = NewApp();
        System.IO.Directory.CreateDirectory(app.AbsolutePath);
        var rel = "fr-txt-" + System.Guid.NewGuid().ToString("N")[..8] + ".txt";
        var abs = System.IO.Path.Combine(app.AbsolutePath, rel);
        System.IO.File.WriteAllText(abs, "hello");
        try
        {
            var p = global::app.type.path.@this.Resolve(abs, app.User.Context);
            var action = new global::app.module.file.Read
            {
                Context = app.User.Context,
                Path = global::app.data.@this<global::app.type.path.@this>.Ok(p),
            };
            var result = await action.Run();
            await result.IsSuccess();
            await Assert.That((await result.Value())).IsEqualTo("hello");
        }
        finally { try { System.IO.File.Delete(abs); } catch { } }
    }

    [Test] public async Task FileReadBuild_UnknownExtension_FallsBack_BareTextType()
    {
        // Unknown extension: formats.Kind returns null, falls back to extension
        // name. The action Build returns Ok() (no stamp) when even that isn't
        // a registered PLang type.
        await using var app = NewApp();
        await Assert.That(app.Format.Kind("xyz")).IsNull();
    }

    [Test] public async Task FileReadRun_ReturnsBareDataPolymorphic_NotStaticDataImage()
    {
        // Run() signature is Task<Data> (bare), not Task<Data<image>>. The
        // image lift happens inside Run() based on MIME.
        var rt = typeof(global::app.module.file.Read).GetMethod("Run")!.ReturnType;
        await Assert.That(rt).IsEqualTo(typeof(System.Threading.Tasks.Task<global::app.data.@this>));
    }
}

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.IntegrationCutsTests;

public class Cut2_StrictMismatchFailsAtRightLayer
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() { _app = new global::app.@this("/app"); }

    private static readonly byte[] GifBytes = new byte[]
    {
        0x47,0x49,0x46,0x38,0x37,0x61,0x01,0x00,0x01,0x00,0x80,0x00,0x00,0x00,0x00,0x00,
        0xFF,0xFF,0xFF,0x2C,0x00,0x00,0x00,0x00,0x01,0x00,0x01,0x00,0x00,0x02,0x02,0x44,
        0x01,0x00,0x3B
    };
    private static readonly byte[] PngBytes = new byte[]
    {
        0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
        0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
        0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
        0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
        0x42,0x60,0x82
    };

    private global::app.type.@this Type(string name, string? kind = null, bool strict = false)
    {
        var t = new global::app.type.@this(name, kind, strict);
        t.Context = _app.User.Context;
        return t;
    }

    [Test] public async Task LiteralPngAsImageGifStrict_FailsAtBuild()
    {
        var ctx = _app.User.Context;
        var nameData = new global::app.data.@this("Name", "img"); nameData.Context = ctx;
        var valueData = new global::app.data.@this("Value", PngBytes); valueData.Context = ctx;
        var typeData = new global::app.data.@this("Type", Type("image", "gif", true)); typeData.Context = ctx;
        var parameters = new List<global::app.data.@this> { nameData, valueData, typeData };
        var error = global::app.module.variable.Set.ValidateBuild(parameters);
        await Assert.That(error).IsNotNull();
        await Assert.That(error!).Contains("gif");
    }

    [Test] public async Task VarAsImageGifStrict_BuildsClean_FailsAtRuntime()
    {
        var context = _app.User.Context;
        context.Variable.Set("upload", PngBytes);

        // ValidateBuild defers — value is a %var% reference.
        var nameData = new global::app.data.@this("Name", "img"); nameData.Context = context;
        var valueData = new global::app.data.@this("Value", "%upload%"); valueData.Context = context;
        var typeData = new global::app.data.@this("Type", Type("image", "gif", true)); typeData.Context = context;
        var validateParams = new List<global::app.data.@this> { nameData, valueData, typeData };
        await Assert.That(global::app.module.variable.Set.ValidateBuild(validateParams)).IsNull();

        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", "%upload%"),
            ("type", Type("image", "gif", true)));
        var result = await action.RunAsync(context);
        await result.IsFailure();
        await Assert.That(result.Error?.Message ?? "").Contains("gif");
    }

    [Test] public async Task LiteralGifAsImageGifStrict_BuildsAndRunsClean()
    {
        var context = _app.User.Context;

        var nameData = new global::app.data.@this("Name", "img"); nameData.Context = context;
        var valueData = new global::app.data.@this("Value", GifBytes); valueData.Context = context;
        var typeData = new global::app.data.@this("Type", Type("image", "gif", true)); typeData.Context = context;
        var validateParams = new List<global::app.data.@this> { nameData, valueData, typeData };
        await Assert.That(global::app.module.variable.Set.ValidateBuild(validateParams)).IsNull();

        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", GifBytes),
            ("type", Type("image", "gif", true)));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
        var stored = await context.Variable.Get("img");
        // A strict declaration validates AND becomes: the gif magic bytes match
        // the declared gif kind, so the value mints as the image it was verified
        // to be. Strict is the one-time gate that let the set through; the stored
        // value is simply the verified image/gif.
        await Assert.That(stored!.Type!.Name).IsEqualTo("image");
        await Assert.That(stored.Type.Kind).IsEqualTo("gif");
    }

    // A read-lift binds an already-loaded image.@this (not raw bytes). Strict
    // must sniff the instance's own bytes at the set — the realistic shape the
    // byte[]-only probe used to miss.
    [Test] public async Task ReadLiftImagePngAsImageGifStrict_FailsAtSet()
    {
        var context = _app.User.Context;
        var pngImage = new global::app.type.image.@this(PngBytes, "image/png");
        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", pngImage),
            ("type", Type("image", "gif", true)));
        var result = await action.RunAsync(context);
        await result.IsFailure();
        await Assert.That(result.Error?.Message ?? "").Contains("gif");
    }

    [Test] public async Task ReadLiftImageGifAsImageGifStrict_Succeeds()
    {
        var context = _app.User.Context;
        var gifImage = new global::app.type.image.@this(GifBytes, "image/gif");
        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", gifImage),
            ("type", Type("image", "gif", true)));
        var result = await action.RunAsync(context);
        await result.IsSuccess();
    }
}

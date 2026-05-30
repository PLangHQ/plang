using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

public class StrictRunTests
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

    [Test] public async Task Run_StrictImageGifWithRuntimeVarResolvingToPng_ThrowsTypedError()
    {
        var context = _app.User.Context;
        context.Variable.Set("upload", PngBytes);
        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", "%upload%"),
            ("type", new global::app.type.@this("image", "gif", true)));
        var result = await action.RunAsync(context);
        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error?.Message ?? "").Contains("gif");
    }

    [Test] public async Task Run_StrictImageGifWithRuntimeVarResolvingToGif_Mints()
    {
        var context = _app.User.Context;
        context.Variable.Set("upload", GifBytes);
        var action = TestAction.Create("variable", "set",
            ("name", "%img%"),
            ("value", "%upload%"),
            ("type", new global::app.type.@this("image", "gif", true)));
        var result = await action.RunAsync(context);
        await Assert.That(result.Success).IsTrue();
        var stored = context.Variable.Get("img");
        await Assert.That(stored!.Type!.Name).IsEqualTo("image");
        await Assert.That(stored.Type.Kind).IsEqualTo("gif");
    }

    [Test] public async Task Run_NotStrict_StampsKindFromBuildHook_NoValidation()
    {
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%x%"),
            ("value", "readme.md"),
            ("type", new global::app.type.@this("text")));
        var result = await action.RunAsync(context);
        await Assert.That(result.Success).IsTrue();
        var stored = context.Variable.Get("x");
        await Assert.That(stored!.Type!.Name).IsEqualTo("text");
        await Assert.That(stored.Type.Kind).IsEqualTo("md");
    }
}

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.TypeKindStrict.SetAndStrictTests;

public class StrictValidateBuildTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup() { _app = TestApp.Create("/app"); }

    // Minimal 1×1 GIF87a + PNG byte fixtures.
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

    private List<global::app.data.@this> Params(object value, global::app.type.@this typeEntity)
    {
        var ctx = _app.User.Context;
        var nameData = new global::app.data.@this("Name", "x", context: ctx);
        var valueData = new global::app.data.@this("Value", value, context: ctx);
        var typeData = new global::app.data.@this("Type", typeEntity, context: ctx);
        return new() { nameData, valueData, typeData };
    }

    [Test] public async Task ValidateBuild_StrictImageGifWithGifLiteral_ReturnsNull()
    {
        var result = global::app.module.variable.Set.ValidateBuild(
            Params(GifBytes, Type("image", "gif", true)));
        await Assert.That(result).IsNull();
    }

    [Test] public async Task ValidateBuild_StrictImageGifWithPngLiteral_ReturnsError()
    {
        var result = global::app.module.variable.Set.ValidateBuild(
            Params(PngBytes, Type("image", "gif", true)));
        await Assert.That(result).IsNotNull();
        await Assert.That(result!).Contains("gif");
        await Assert.That(result!.ToLowerInvariant()).Contains("png");
    }

    [Test] public async Task ValidateBuild_StrictImageGifWithVarRef_ReturnsNull_DefersToRuntime()
    {
        var result = global::app.module.variable.Set.ValidateBuild(
            Params("%upload%", Type("image", "gif", true)));
        await Assert.That(result).IsNull();
    }

    [Test] public async Task ValidateBuild_StrictTextMdWithLiteral_ReturnsNull()
    {
        var result = global::app.module.variable.Set.ValidateBuild(
            Params("hello", Type("text", "md", true)));
        await Assert.That(result).IsNull();
    }

    [Test] public async Task ValidateBuild_NotStrict_DoesNotValidate_EvenOnMismatch()
    {
        var result = global::app.module.variable.Set.ValidateBuild(
            Params(PngBytes, Type("image", "gif")));
        await Assert.That(result).IsNull();
    }

    [Test] public async Task ValidateBuild_StrictWithNoKind_ReturnsNull()
    {
        var result = global::app.module.variable.Set.ValidateBuild(
            Params(GifBytes, Type("image", null, true)));
        await Assert.That(result).IsNull();
    }
}

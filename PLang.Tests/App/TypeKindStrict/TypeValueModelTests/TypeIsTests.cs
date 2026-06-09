using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;
using image = global::app.type.image.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// type.@this.Is — "does this type stand in for X" (same type, or composes it as
// a facet). image has-a path, so image Is path; variable.set keeps an image
// bound to a path slot as-is rather than downgrading it.
public class TypeIsTests
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task Is_SameName_True()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Type["image"].Is(app.Type["image"])).IsTrue();
    }

    [Test] public async Task Is_Facet_ImageIsPath()
    {
        await using var app = new PLangEngine("/test");
        // image declares static Type = ["image","path"] — it has-a path.
        await Assert.That(app.Type["image"].Is(app.Type["path"])).IsTrue();
    }

    [Test] public async Task Is_NonFacet_ImageIsNotText()
    {
        await using var app = new PLangEngine("/test");
        // image has a Mime string but does NOT declare text as a facet.
        await Assert.That(app.Type["image"].Is(app.Type["text"])).IsFalse();
    }

    [Test] public async Task Is_NotSymmetric_PathIsNotImage()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Type["path"].Is(app.Type["image"])).IsFalse();
    }

    [Test] public async Task Set_ImageBoundToPathSlot_KeptAsImage_NotDowngraded()
    {
        await using var app = new PLangEngine("/test");
        var ctx = app.User.Context;
        var img = new image(PngHeader, "image/png");

        // Declared type=path, but the value is already an image (which has-a
        // path). It must stay an image — not be converted/downgraded to path.
        var action = TestAction.Create("variable", "set",
            ("name", "%p%"),
            ("value", img),
            ("type", new global::app.type.@this("path")));
        var result = await action.RunAsync(ctx);
        await result.IsSuccess();

        var stored = ctx.Variable.Get("p");
        await Assert.That(stored!.Type!.Name).IsEqualTo("image");
        await Assert.That((await stored.Value()) is image).IsTrue();
    }
}

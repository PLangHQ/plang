using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.TypeKindStrict.TypeValueModelTests;

// type.@this.Is — "does this type stand in for X" (same type, or composes it as
// a facet). image has-a path, so image Is path; variable.set keeps an image
// bound to a path slot as-is rather than downgrading it.
public class TypeIsTests
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task Is_SameName_True()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Type["image"].Is(app.Type["image"])).IsTrue();
    }

    [Test] public async Task Is_ImageBornFromPath_IsPath()
    {
        await using var app = TestApp.Create("/test");
        var ctx = app.User.Context;
        // Composition is the VALUE's type history: an image born from a path carries a "path"
        // entry, so the value answers `is path`. (A bare type entity does NOT — no history.)
        var path = new global::app.type.item.path.file.@this("/test/photo.png", ctx);
        var img = new image(new byte[] { 1, 2, 3 }, path);
        await Assert.That(img.Is(app.Type["path"])).IsTrue();
        await Assert.That(app.Type["image"].Is(app.Type["path"])).IsFalse();   // bare type: no history
    }

    [Test] public async Task Is_NonFacet_ImageIsNotText()
    {
        await using var app = TestApp.Create("/test");
        // image has a Mime string but does NOT declare text as a facet.
        await Assert.That(app.Type["image"].Is(app.Type["text"])).IsFalse();
    }

    [Test] public async Task Is_NotSymmetric_PathIsNotImage()
    {
        await using var app = TestApp.Create("/test");
        await Assert.That(app.Type["path"].Is(app.Type["image"])).IsFalse();
    }

    [Test] public async Task Set_ImageBoundToPathSlot_KeptAsImage_NotDowngraded()
    {
        await using var app = TestApp.Create("/test");
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

        var stored = await ctx.Variable.Get("p");
        await Assert.That(stored!.Type!.Name).IsEqualTo("image");
        await Assert.That((await stored.Value()) is image).IsTrue();
    }
}

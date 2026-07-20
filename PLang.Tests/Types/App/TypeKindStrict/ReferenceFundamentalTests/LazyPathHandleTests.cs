using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.TypeKindStrict.ReferenceFundamentalTests;

// A reference fundamental declared from a path is a LAZY handle: variable.set
// mints the typed value with .Path set and reads nothing; content materializes
// from the path on first (async) access, through the actor permission gate.
public class LazyPathHandleTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-lazy-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        _app = TestApp.Create(root);
    }

    [After(Test)]
    public void Cleanup() => _app.DisposeAsync().AsTask().GetAwaiter().GetResult();

    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    // A full 1x1 PNG (ImageSharp DetectFormat identifies it as png).
    private static readonly byte[] Png1x1 =
    {
        0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
        0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
        0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
        0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
        0x42,0x60,0x82
    };

    [Test] public async Task SetAsImage_MintsPathBackedHandle_NoReadAtSet()
    {
        // The file does NOT exist. If `set` read it, this would error — it
        // doesn't, because a path-backed handle reads nothing at the set.
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%pic%"),
            ("value", "ghost.jpg"),
            ("type", new global::app.type.@this("image")));
        var result = await action.Run(context);
        await result.IsSuccess();

        var stored = await context.Variable.Get("pic");
        await Assert.That((await stored!.Value()) is image).IsTrue();
        var img = (image)(await stored.Value())!;
        await Assert.That(img.Path).IsNotNull();
        await Assert.That(img.Path!.FileName).IsEqualTo("ghost.jpg");
        // Nothing loaded — Bytes is empty until first async access.
        await Assert.That(img.Bytes.Length).IsEqualTo(0);
    }

    [Test] public async Task BytesAsync_FirstAccess_LoadsThroughPath()
    {
        var context = _app.User.Context;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(_app.AbsolutePath, "real.png"), PngHeader);

        var img = new image(global::app.type.item.path.@this.Resolve(
            System.IO.Path.Combine(_app.AbsolutePath, "real.png"), context));
        await Assert.That(img.Bytes.Length).IsEqualTo(0); // not loaded yet

        await Data.Ok(img).Value();   // the async pull, through the path
        // Cached — the sync view now reflects the loaded bytes.
        await Assert.That(img.Bytes).IsEquivalentTo(PngHeader);
    }

    [Test] public async Task Materialize_MissingFile_FailsOntoBinding_NotAtConstruction()
    {
        var context = _app.User.Context;
        // Construction performs no I/O even for a missing file.
        var img = new image(global::app.type.item.path.@this.Resolve(
            System.IO.Path.Combine(_app.AbsolutePath, "missing.png"), context));
        await Assert.That(img.Path).IsNotNull();

        // The read failure rides onto the binding at first content access — no throw.
        var data = Data.Ok(img);
        await data.Value();
        await data.IsFailure();
    }

    [Test] public async Task Materialize_StrictKindMismatch_FailsOntoBinding_NotAtConstruction()
    {
        var context = _app.User.Context;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(_app.AbsolutePath, "shot.png"), Png1x1);

        // Path-backed handle declared `as image/gif strict`: nothing read at
        // construction, so no error yet — the strict requirement is imprinted.
        var img = new image(global::app.type.item.path.@this.Resolve(
            System.IO.Path.Combine(_app.AbsolutePath, "shot.png"), context));
        img.RequireStrictKind("gif");

        // The mismatch (png content behind a strict gif) surfaces at byte-load, onto the binding.
        var data = Data.Ok(img);
        await data.Value();
        await data.IsFailure();
        await Assert.That(data.Error!.Key).IsEqualTo("StrictKindMismatch");
    }

    [Test] public async Task BytesBacked_Image_Unchanged_NoPathRead()
    {
        // The bytes-backed path is untouched: content is already in hand.
        var img = new image(PngHeader, "image/png");
        await Assert.That(img.Path).IsNull();
        await Data.Ok(img).Value();
        await Assert.That(img.Bytes).IsEquivalentTo(PngHeader);
    }
}

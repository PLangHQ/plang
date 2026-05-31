using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using image = global::app.type.image.@this;

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
        _app = new global::app.@this(root);
    }

    [After(Test)]
    public void Cleanup() => _app.DisposeAsync().AsTask().GetAwaiter().GetResult();

    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task SetAsImage_MintsPathBackedHandle_NoReadAtSet()
    {
        // The file does NOT exist. If `set` read it, this would error — it
        // doesn't, because a path-backed handle reads nothing at the set.
        var context = _app.User.Context;
        var action = TestAction.Create("variable", "set",
            ("name", "%pic%"),
            ("value", "ghost.jpg"),
            ("type", new global::app.type.@this("image")));
        var result = await action.RunAsync(context);
        await result.IsSuccess();

        var stored = context.Variable.Get("pic");
        await Assert.That(stored!.Value is image).IsTrue();
        var img = (image)stored.Value!;
        await Assert.That(img.Path).IsNotNull();
        await Assert.That(img.Path!.FileName).IsEqualTo("ghost.jpg");
        // Nothing loaded — Bytes is empty until first async access.
        await Assert.That(img.Bytes.Length).IsEqualTo(0);
    }

    [Test] public async Task BytesAsync_FirstAccess_LoadsThroughPath()
    {
        var context = _app.User.Context;
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(_app.AbsolutePath, "real.png"), PngHeader);

        var img = new image(global::app.type.path.@this.Resolve(
            System.IO.Path.Combine(_app.AbsolutePath, "real.png"), context));
        await Assert.That(img.Bytes.Length).IsEqualTo(0); // not loaded yet

        var loaded = await img.BytesAsync();
        await Assert.That(loaded).IsEquivalentTo(PngHeader);
        // Cached — the sync view now reflects the loaded bytes.
        await Assert.That(img.Bytes).IsEquivalentTo(PngHeader);
    }

    [Test] public async Task BytesAsync_MissingFile_ErrorsAtFirstAccess_NotAtConstruction()
    {
        var context = _app.User.Context;
        // Construction performs no I/O even for a missing file.
        var img = new image(global::app.type.path.@this.Resolve(
            System.IO.Path.Combine(_app.AbsolutePath, "missing.png"), context));
        await Assert.That(img.Path).IsNotNull();

        // The read failure surfaces here, at first content access.
        await Assert.That(async () => await img.BytesAsync()).Throws<System.Exception>();
    }

    [Test] public async Task BytesBacked_Image_Unchanged_NoPathRead()
    {
        // The bytes-backed path is untouched: content is already in hand.
        var img = new image(PngHeader, "image/png");
        await Assert.That(img.Path).IsNull();
        await Assert.That(await img.BytesAsync()).IsEquivalentTo(PngHeader);
    }
}

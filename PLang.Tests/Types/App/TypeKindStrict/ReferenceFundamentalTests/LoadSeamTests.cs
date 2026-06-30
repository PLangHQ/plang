using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using System.Text;
using image = global::app.type.image.@this;

namespace PLang.Tests.App.TypeKindStrict.ReferenceFundamentalTests;

// The load seam: a path-backed image is lazy through set + navigation, but a
// SYNC consumer (the serializer renderers, below the STJ converter wall) sees
// only empty bytes until something pulls the content into memory. Data.Load()
// is that async pull, run at the serialize chokepoint. These tests drive the
// real consumer-facing flow the goal tests punt on — without Load() the output
// is empty and strict never throws.
public class LoadSeamTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-load-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(root);
        _app = new global::app.@this(root);
    }

    [After(Test)]
    public void Cleanup() => _app.DisposeAsync().AsTask().GetAwaiter().GetResult();

    // A full 1x1 PNG (ImageSharp DetectFormat identifies it as png).
    private static readonly byte[] Png1x1 =
    {
        0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A,0x00,0x00,0x00,0x0D,0x49,0x48,0x44,0x52,
        0x00,0x00,0x00,0x01,0x00,0x00,0x00,0x01,0x08,0x06,0x00,0x00,0x00,0x1F,0x15,0xC4,
        0x89,0x00,0x00,0x00,0x0D,0x49,0x44,0x41,0x54,0x78,0x9C,0x62,0x00,0x01,0x00,0x00,
        0x05,0x00,0x01,0x0D,0x0A,0x2D,0xB4,0x00,0x00,0x00,0x00,0x49,0x45,0x4E,0x44,0xAE,
        0x42,0x60,0x82
    };

    private image PathBackedPng(string name)
    {
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(_app.AbsolutePath, name), Png1x1);
        return new image(global::app.type.path.@this.Resolve(
            System.IO.Path.Combine(_app.AbsolutePath, name), _app.User.Context));
    }

    [Test] public async Task Load_MaterializesPathBackedImage_SyncBytesThenReal()
    {
        var img = PathBackedPng("a.png");
        await Assert.That(img.Bytes.Length).IsEqualTo(0); // lazy — nothing read yet

        var error = await Data.Ok(img).Load();

        await Assert.That(error).IsNull();
        await Assert.That(img.Bytes).IsEquivalentTo(Png1x1); // sync view now real
    }

    [Test] public async Task Load_WalksNestedImage_InsideDictionary()
    {
        var img = PathBackedPng("nested.png");
        var dict = new System.Collections.Generic.Dictionary<string, object?> { ["avatar"] = img };

        var error = await _app.User.Context.Ok(dict).Load();

        await Assert.That(error).IsNull();
        await Assert.That(img.Bytes).IsEquivalentTo(Png1x1);
    }

    [Test] public async Task Load_StrictMismatch_ReturnsDedicatedError_NoThrow()
    {
        var img = PathBackedPng("shot.png");
        img.RequireStrictKind("gif"); // png content behind strict gif

        var error = await Data.Ok(img).Load();

        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Error).IsNotNull();
        await Assert.That(error.Error!.Key).IsEqualTo("StrictKindMismatch");
    }

    [Test] public async Task Load_NoLazyContent_IsNoOp()
    {
        // A bytes-backed image and a scalar graph carry nothing lazy.
        var bytesBacked = new image(Png1x1, "image/png");
        await Assert.That((await Data.Ok(bytesBacked).Load())).IsNull();

        var scalar = new System.Collections.Generic.Dictionary<string, object?> { ["n"] = 42L, ["s"] = "x" };
        await Assert.That((await _app.User.Context.Ok(scalar).Load())).IsNull();
    }

    [Test] public async Task Serialize_PathBackedImage_EmitsRealBytes_NotEmpty()
    {
        // The chokepoint: serialization runs Load() above the STJ wall, so the
        // sync renderer reads real bytes. Without the fix the base64 is "".
        var img = PathBackedPng("out.png");
        using var ms = new System.IO.MemoryStream();

        var result = await new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext).SerializeAsync(ms, Data.Ok(img));
        await result.IsSuccess();

        var json = Encoding.UTF8.GetString(ms.ToArray());
        // PNG base64 starts with "iVBOR"; an unloaded image would emit "" instead.
        await Assert.That(json).Contains("iVBOR");
    }

    [Test] public async Task Serialize_StrictMismatch_FailsCleanly_BeforeStreamWrite()
    {
        var img = PathBackedPng("bad.png");
        img.RequireStrictKind("gif");
        using var ms = new System.IO.MemoryStream();

        var result = await new global::app.channel.serializer.plang.@this(global::PLang.Tests.TestApp.SharedContext).SerializeAsync(ms, Data.Ok(img));

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("StrictKindMismatch");
        await Assert.That(ms.Length).IsEqualTo(0); // nothing written
    }
}

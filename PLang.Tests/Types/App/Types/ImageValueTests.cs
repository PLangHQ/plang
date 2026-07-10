using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// app/type/image/this.cs — Bytes, Mime, Path (type path, nullable), lazy Width/Height,
// IBooleanResolvable = bytes.Length>0. Composition over union: an image carries a Path
// facet rather than being typed as path|image.

public class ImageValueTests
{
    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task Image_FromBytesAndMime_StoresBoth()
    {
        var img = new image(PngHeader, "image/png");
        await Assert.That(img.Bytes).IsEqualTo(PngHeader);
        await Assert.That(img.Mime).IsEqualTo("image/png");
    }

    [Test] public async Task Image_PathFacet_IsTypePath_NullableProperty()
    {
        var prop = typeof(image).GetProperty("Path");
        await Assert.That(prop).IsNotNull();
        var nullableInfo = new System.Reflection.NullabilityInfoContext().Create(prop!);
        await Assert.That(nullableInfo.ReadState).IsEqualTo(System.Reflection.NullabilityState.Nullable);
        await Assert.That(prop!.PropertyType).IsEqualTo(typeof(global::app.type.item.path.@this));
    }

    [Test] public async Task Image_Base64Constructed_PathIsNull()
    {
        var img = new image(PngHeader, "image/png");
        await Assert.That(img.Path).IsNull();
    }

    [Test] public async Task Image_FromFileRead_PathReferencesSourceFile()
    {
        await using var app = TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-img-" + System.Guid.NewGuid().ToString("N")[..8]));
        var raw = System.IO.Path.Combine(app.AbsolutePath, "photo.png");
        var p = global::app.type.item.path.@this.Resolve(raw, app.User.Context);
        var img = new image(PngHeader, "image/png", p);
        await Assert.That(img.Path).IsNotNull();
        await Assert.That(img.Path!.Raw).IsEqualTo(p.Raw);
    }

    [Test] public async Task Image_WidthHeight_LazyEvaluation()
    {
        // Width/Height computed on first access via SixLabors.ImageSharp.
        // Empty/garbage bytes degrade to (0, 0) without throwing.
        var img = new image(System.Array.Empty<byte>(), "image/png");
        await Assert.That(img.Width).IsEqualTo(0);
        await Assert.That(img.Height).IsEqualTo(0);
    }

    [Test] public async Task Image_IBooleanResolvable_NonEmptyBytes_Truthy()
        => await Assert.That(await new image(PngHeader, "image/png").AsBooleanAsync()).IsTrue();

    [Test] public async Task Image_IBooleanResolvable_EmptyBytes_Falsy()
        => await Assert.That(await new image(System.Array.Empty<byte>(), "image/png").AsBooleanAsync()).IsFalse();

    [Test] public async Task Image_PlangTypeAttribute_Registered()
    {
        var types = new global::app.type.list.@this();
        await Assert.That(types.ResolveType("image")).IsEqualTo(typeof(image));
    }

    [Test] public async Task Image_DoesNotUnionWithPath_RoutingKeyAlwaysImage()
    {
        // image is a value (: item.@this), NOT a path; it carries Path as a property.
        // The point: image never unions with path — its routing key stays "image".
        await Assert.That(typeof(image).BaseType).IsEqualTo(typeof(global::app.type.item.@this));
        await Assert.That(typeof(global::app.type.item.path.@this).IsAssignableFrom(typeof(image))).IsFalse();
    }
}

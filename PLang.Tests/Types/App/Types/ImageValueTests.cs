using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// app/type/image/this.cs — Bytes, Mime, Path (type path, nullable), lazy Width/Height,
// IBooleanResolvable = bytes.Length>0. Composition over union: an image carries a Path
// facet rather than being typed as path|image.

public class ImageValueTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private static readonly byte[] PngHeader = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task Image_FromBytesWithKind_MimeComesFromTheKind()
    {
        var img = app.actor.list.User.Context.Image(PngHeader, "png");
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
        var img = app.actor.list.User.Context.Image(PngHeader, "image/png");
        await Assert.That(img.Path).IsNull();
    }

    [Test] public async Task Image_FromFileRead_PathReferencesSourceFile()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-img-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var raw = System.IO.Path.Combine(app.filesystem.root.Absolute, "photo.png");
        var p = global::app.type.item.path.@this.Resolve(raw, app.actor.list.User.Context);
        var img = app.actor.list.User.Context.Image(p, PngHeader);
        await Assert.That(img.Path).IsNotNull();
        await Assert.That(img.Path!.Raw).IsEqualTo(p.Raw);
    }

    [Test] public async Task Image_PathBorn_JpegExtension_KindIsCanonicalJpg()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-img-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var p = global::app.type.item.path.@this.Resolve("photo.jpeg", app.actor.list.User.Context);
        var img = app.actor.list.User.Context.Image(p);
        await Assert.That(img.Mime).IsEqualTo("image/jpeg");
        await Assert.That(app.actor.list.User.Context.Ok(img).Type.kind.Name).IsEqualTo("jpg");
    }

    [Test] public async Task Image_FromSniffedJpegBytes_KindIsCanonicalJpg()
    {
        var img = image.FromBytes(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, app.actor.list.User.Context)!;
        await Assert.That(app.actor.list.User.Context.Ok(img).Type.kind.Name).IsEqualTo("jpg");
    }

    [Test] public async Task Image_IBooleanResolvable_NonEmptyBytes_Truthy()
        => await Assert.That(await app.actor.list.User.Context.Image(PngHeader, "image/png").AsBooleanAsync(app.actor.list.User.Context)).IsTrue();

    [Test] public async Task Image_IBooleanResolvable_EmptyBytes_Falsy()
        => await Assert.That(await app.actor.list.User.Context.Image(System.Array.Empty<byte>(), "image/png").AsBooleanAsync(app.actor.list.User.Context)).IsFalse();

    [Test] public async Task Image_PlangTypeAttribute_Registered()
    {
        var types = new global::app.origin.code().type;
        await Assert.That(types.Clr("image")).IsEqualTo(typeof(image));
    }

    [Test] public async Task Image_DoesNotUnionWithPath_RoutingKeyAlwaysImage()
    {
        // image is a value (: item.@this), NOT a path; it carries Path as a property.
        // The point: image never unions with path — its routing key stays "image".
        await Assert.That(typeof(image).BaseType).IsEqualTo(typeof(global::app.type.item.@this));
        await Assert.That(typeof(global::app.type.item.path.@this).IsAssignableFrom(typeof(image))).IsFalse();
    }
}

using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Serialization.IntegrationCuts;

// plang-types — Integration cut 3: composition over union (%photo.Path.Exists%).
// A file-backed image is one `image` whose value carries a `path` facet. Member access
// navigates via the typed-property catalog; the routing key stays `image`; no `path|image`
// union exists anywhere on the wire or in the registry.

public class PlangTypesCut3_CompositionNavigationTests
{
    private static readonly byte[] PngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Test] public async Task ImageFromFile_PathFacet_IsTypePath_NavigationWorks()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut3a-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        var p = global::app.type.item.path.@this.Resolve("/srv/photo.png", app.actor.list.User.Context);
        var img = app.actor.list.User.Context.Image(p!, PngBytes);

        // The Path property is typed as path.@this (composition, not a
        // string passthrough); reflection confirms the declared type.
        var pathProp = typeof(image).GetProperty("Path");
        await Assert.That(pathProp!.PropertyType).IsEqualTo(typeof(global::app.type.item.path.@this));
        await Assert.That(img.Path).IsNotNull();
    }

    [Test] public async Task ImageFromFile_PathExists_TrueForPresentFile()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut3b-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        System.IO.Directory.CreateDirectory(app.filesystem.root.Absolute);
        var abs = System.IO.Path.Combine(app.filesystem.root.Absolute, "present.png");
        System.IO.File.WriteAllBytes(abs, PngBytes);
        try
        {
            var p = global::app.type.item.path.@this.Resolve(abs, app.actor.list.User.Context) as global::app.type.item.path.file.@this;
            await Assert.That(p).IsNotNull();
            var img = app.actor.list.User.Context.Image(p!, PngBytes);
            // image.Path.Exists navigates through the path facet — the file
            // exists, so Exists is true.
            await Assert.That(await (await img.Path!.Exists(app.actor.list.User.Context)).ToBooleanAsync()).IsTrue();
        }
        finally { try { System.IO.File.Delete(abs); } catch { } }
    }

    [Test] public async Task ImageFromFile_PathExists_FalseForMissingFile()
    {
        await using var app = new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut3c-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();
        System.IO.Directory.CreateDirectory(app.filesystem.root.Absolute);
        var abs = System.IO.Path.Combine(app.filesystem.root.Absolute, "missing-" + System.Guid.NewGuid().ToString("N")[..8] + ".png");
        // Don't create the file.
        var p = global::app.type.item.path.@this.Resolve(abs, app.actor.list.User.Context) as global::app.type.item.path.file.@this;
        var img = app.actor.list.User.Context.Image(p!, PngBytes);
        await Assert.That(await (await img.Path!.Exists(app.actor.list.User.Context)).ToBooleanAsync()).IsFalse();
    }

    [Test] public async Task ImageFromBase64_PathIsNull()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var img = app.actor.list.User.Context.Image(PngBytes, "image/png");
        await Assert.That(img.Path).IsNull();
    }

    [Test] public async Task RoutingKey_StaysImage_NoPathImageUnion_AnywhereInRegistry()
    {
        var types = new global::app.origin.code(global::app.Wiring.none).type;
        // image and path are distinct registered types.
        await Assert.That(types.Clr("image")).IsEqualTo(typeof(image));
        await Assert.That(types.Clr("path")).IsNotEqualTo(typeof(image));
        // No "path|image" or similar union name exists anywhere in the registry.
        await Assert.That(types.Clr("path|image")).IsNull();
        await Assert.That(types.Clr("image|path")).IsNull();
        // image's CLR type doesn't inherit from path — no union via inheritance.
        await Assert.That(typeof(global::app.type.item.path.@this).IsAssignableFrom(typeof(image))).IsFalse();
    }

    [Test] public async Task CatalogRendering_ImagePathProperty_HasTypePathAnnotation()
    {
        var types = new global::app.origin.code(global::app.Wiring.none).type;
        var imageEntry = types["image"];
        await Assert.That(imageEntry).IsNotNull();
        await Assert.That(imageEntry!.Property).IsNotNull();
        var pathProp = imageEntry.Property!.FirstOrDefault(p => p.Name == "path");
        await Assert.That(pathProp).IsNotNull();
        await Assert.That(pathProp!.Type.ToString()).IsEqualTo("path");
    }
}

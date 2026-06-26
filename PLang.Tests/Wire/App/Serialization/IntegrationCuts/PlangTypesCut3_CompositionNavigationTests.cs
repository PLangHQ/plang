using image = global::app.type.image.@this;

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
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut3a-" + System.Guid.NewGuid().ToString("N")[..8]));
        var p = global::app.type.path.@this.Resolve("/srv/photo.png", app.User.Context);
        var img = new image(PngBytes, "image/png", p);

        // The Path property is typed as path.@this (composition, not a
        // string passthrough); reflection confirms the declared type.
        var pathProp = typeof(image).GetProperty("Path");
        await Assert.That(pathProp!.PropertyType).IsEqualTo(typeof(global::app.type.path.@this));
        await Assert.That(img.Path).IsNotNull();
    }

    [Test] public async Task ImageFromFile_PathExists_TrueForPresentFile()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut3b-" + System.Guid.NewGuid().ToString("N")[..8]));
        System.IO.Directory.CreateDirectory(app.AbsolutePath);
        var abs = System.IO.Path.Combine(app.AbsolutePath, "present.png");
        System.IO.File.WriteAllBytes(abs, PngBytes);
        try
        {
            var p = global::app.type.path.@this.Resolve(abs, app.User.Context) as global::app.type.path.file.@this;
            await Assert.That(p).IsNotNull();
            var img = new image(PngBytes, "image/png", p);
            // image.Path.Exists navigates through the path facet — the file
            // exists, so Exists is true.
            await Assert.That(((global::app.type.path.file.@this)img.Path!).Exists).IsTrue();
        }
        finally { try { System.IO.File.Delete(abs); } catch { } }
    }

    [Test] public async Task ImageFromFile_PathExists_FalseForMissingFile()
    {
        await using var app = global::PLang.Tests.TestApp.Create(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-cut3c-" + System.Guid.NewGuid().ToString("N")[..8]));
        System.IO.Directory.CreateDirectory(app.AbsolutePath);
        var abs = System.IO.Path.Combine(app.AbsolutePath, "missing-" + System.Guid.NewGuid().ToString("N")[..8] + ".png");
        // Don't create the file.
        var p = global::app.type.path.@this.Resolve(abs, app.User.Context) as global::app.type.path.file.@this;
        var img = new image(PngBytes, "image/png", p);
        await Assert.That(((global::app.type.path.file.@this)img.Path!).Exists).IsFalse();
    }

    [Test] public async Task ImageFromBase64_PathIsNull_NoCrashOnNavigation()
    {
        var img = new image(PngBytes, "image/png");
        await Assert.That(img.Path).IsNull();
        // Reading Width/Height on a base64-only image — no crash, just (0,0)
        // for non-decoding bytes (the 8-byte signature isn't a full image).
        var _ = img.Width;
        var __ = img.Height;
        await Assert.That(true).IsTrue();
    }

    [Test] public async Task RoutingKey_StaysImage_NoPathImageUnion_AnywhereInRegistry()
    {
        var types = new global::app.type.catalog.@this();
        // image and path are distinct registered types.
        await Assert.That(types.ResolveType("image")).IsEqualTo(typeof(image));
        await Assert.That(types.ResolveType("path")).IsNotEqualTo(typeof(image));
        // No "path|image" or similar union name exists anywhere in the registry.
        await Assert.That(types.ResolveType("path|image")).IsNull();
        await Assert.That(types.ResolveType("image|path")).IsNull();
        // image's CLR type doesn't inherit from path — no union via inheritance.
        await Assert.That(typeof(global::app.type.path.@this).IsAssignableFrom(typeof(image))).IsFalse();
    }

    [Test] public async Task CatalogRendering_ImagePathProperty_HasTypePathAnnotation()
    {
        var types = new global::app.type.catalog.@this();
        var entries = types.BuildTypeEntries(null);
        var imageEntry = entries.FirstOrDefault(e => e.Name == "image");
        await Assert.That(imageEntry).IsNotNull();
        await Assert.That(imageEntry!.Properties).IsNotNull();
        var pathProp = imageEntry.Properties!.FirstOrDefault(p => p.Name == "path");
        await Assert.That(pathProp).IsNotNull();
        await Assert.That(pathProp!.TypeName).IsEqualTo("path");
    }
}

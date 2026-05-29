namespace PLang.Tests.App.Serialization.IntegrationCuts;

// plang-types — Integration cut 3: composition over union (%photo.Path.Exists%).
// A file-backed image is one `image` whose value carries a `path` facet. Member access
// navigates via the typed-property catalog; the routing key stays `image`; no `path|image`
// union exists anywhere on the wire or in the registry.

public class PlangTypesCut3_CompositionNavigationTests
{
    [Test] public async Task ImageFromFile_PathFacet_IsTypePath_NavigationWorks()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ImageFromFile_PathExists_TrueForPresentFile()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ImageFromFile_PathExists_FalseForMissingFile()
        => throw new global::System.NotImplementedException();

    [Test] public async Task ImageFromBase64_PathIsNull_NoCrashOnNavigation()
        => throw new global::System.NotImplementedException();

    [Test] public async Task RoutingKey_StaysImage_NoPathImageUnion_AnywhereInRegistry()
        => throw new global::System.NotImplementedException();

    [Test] public async Task CatalogRendering_ImagePathProperty_HasTypePathAnnotation()
        => throw new global::System.NotImplementedException();
}

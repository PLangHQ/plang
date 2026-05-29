namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// app/types/image/this.cs — Bytes, Mime, Path (type path, nullable), lazy Width/Height,
// IBooleanResolvable = bytes.Length>0. Composition over union: an image carries a Path
// facet rather than being typed as path|image.

public class ImageValueTests
{
    [Test] public async Task Image_FromBytesAndMime_StoresBoth()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_PathFacet_IsTypePath_NullableProperty()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_Base64Constructed_PathIsNull()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_FromFileRead_PathReferencesSourceFile()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_WidthHeight_LazyEvaluation()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_IBooleanResolvable_NonEmptyBytes_Truthy()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_IBooleanResolvable_EmptyBytes_Falsy()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_PlangTypeAttribute_Registered()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Image_DoesNotUnionWithPath_RoutingKeyAlwaysImage()
        => throw new global::System.NotImplementedException();
}

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// file.read.Build() (action IClass.Build()) resolves extension → HIGH-LEVEL type via the
// registry/formats; the type's own Build() supplies the kind.
// file.read.Run() constructs the typed value (an image for image MIMEs), not raw bytes.

public class FileReadBuildTests
{
    [Test] public async Task FileReadBuild_PngExtension_ReturnsHighLevelType_Image()
        => throw new global::System.NotImplementedException();

    [Test] public async Task FileReadBuild_TxtExtension_ReturnsHighLevelType_Text()
        => throw new global::System.NotImplementedException();

    [Test] public async Task FileReadBuild_KindSupplied_ByImageBuild_NotByActionBuild()
        => throw new global::System.NotImplementedException();

    [Test] public async Task FileReadRun_ImageMime_ConstructsImageValue_NotRawBytes()
        => throw new global::System.NotImplementedException();

    [Test] public async Task FileReadRun_TextMime_ReturnsStringValue()
        => throw new global::System.NotImplementedException();

    [Test] public async Task FileReadBuild_UnknownExtension_FallsBack_BareTextType()
        => throw new global::System.NotImplementedException();

    [Test] public async Task FileReadRun_ReturnsBareDataPolymorphic_NotStaticDataImage()
        => throw new global::System.NotImplementedException();
}

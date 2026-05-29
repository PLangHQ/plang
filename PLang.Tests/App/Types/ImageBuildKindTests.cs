namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// image.Build(value) → kind. Extension-driven: "a.jpg"→"jpg", "a.PNG"→"png", "a.gif"→"gif".
// Kind is the extension (no dot), case-folded.

public class ImageBuildKindTests
{
    [Test] public async Task Build_JpgExtension_ReturnsJpg()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_PngExtension_ReturnsPng()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_GifExtension_ReturnsGif()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_UpperCaseExtension_NormalizesToLower()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_DataUrl_PicksKindFromMimeSubtype()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_NoExtension_ReturnsNull()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Build_NullValue_ReturnsNull()
        => throw new global::System.NotImplementedException();
}

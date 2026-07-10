using image = global::app.type.item.image.@this;

namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// image.Build(value) → kind. Extension-driven: "a.jpg"→"jpg", "a.PNG"→"png", "a.gif"→"gif".
// Kind is the extension (no dot), case-folded.

public class ImageBuildKindTests
{
    [Test] public async Task Build_JpgExtension_ReturnsJpg()
        => await Assert.That(image.Build("photo.jpg")).IsEqualTo("jpg");

    [Test] public async Task Build_PngExtension_ReturnsPng()
        => await Assert.That(image.Build("a.png")).IsEqualTo("png");

    [Test] public async Task Build_GifExtension_ReturnsGif()
        => await Assert.That(image.Build("a.gif")).IsEqualTo("gif");

    [Test] public async Task Build_UpperCaseExtension_NormalizesToLower()
        => await Assert.That(image.Build("a.PNG")).IsEqualTo("png");

    [Test] public async Task Build_DataUrl_PicksKindFromMimeSubtype()
        => await Assert.That(image.Build("data:image/gif;base64,XYZ")).IsEqualTo("gif");

    [Test] public async Task Build_NoExtension_ReturnsNull()
        => await Assert.That(image.Build("noext")).IsNull();

    [Test] public async Task Build_NullValue_ReturnsNull()
        => await Assert.That(image.Build(null)).IsNull();
}

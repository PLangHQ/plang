namespace PLang.Tests.App.Types;

// plang-types — Stage 5
// image.Resolve(string) — path / data-url / base64 disambiguation.
// image.Resolve(byte[]) — direct construction with mime sniffed from magic bytes.

public class ImageParseTests
{
    [Test] public async Task Resolve_FilePath_Constructs_FromFileBytes()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_DataUrl_PicksMimeFromHeader()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_RawBase64String_DetectsAsImage()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_HttpUrl_FetchesAndConstructs()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_ByteArray_PngMagicBytes_PicksImagePng()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_ByteArray_JpegMagicBytes_PicksImageJpeg()
        => throw new global::System.NotImplementedException();

    [Test] public async Task Resolve_GarbageString_ReturnsNull_NoThrow()
        => throw new global::System.NotImplementedException();
}

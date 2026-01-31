using PLang.Utils;

namespace PLang.Tests.Utils;

public class MimeTypeHelperTests
{
    // Audio types
    [Test]
    [Arguments(".mp3", "audio/mpeg")]
    [Arguments(".wav", "audio/wav")]
    [Arguments(".ogg", "audio/ogg")]
    [Arguments(".m4a", "audio/mp4")]
    [Arguments(".aac", "audio/aac")]
    [Arguments(".midi", "audio/midi")]
    [Arguments(".mid", "audio/midi")]
    [Arguments(".flac", "audio/flac")]
    [Arguments(".weba", "audio/webm")]
    public async Task GetWebMimeType_AudioFiles_ReturnsCorrectMimeType(string extension, string expectedMime)
    {
        var result = MimeTypeHelper.GetWebMimeType($"file{extension}");
        await Assert.That(result).IsEqualTo(expectedMime);
    }

    // Video types
    [Test]
    [Arguments(".mp4", "video/mp4")]
    [Arguments(".avi", "video/x-msvideo")]
    [Arguments(".mpeg", "video/mpeg")]
    [Arguments(".ogv", "video/ogg")]
    [Arguments(".webm", "video/webm")]
    [Arguments(".3gp", "video/3gpp")]
    [Arguments(".3g2", "video/3gpp2")]
    [Arguments(".mkv", "video/x-matroska")]
    public async Task GetWebMimeType_VideoFiles_ReturnsCorrectMimeType(string extension, string expectedMime)
    {
        var result = MimeTypeHelper.GetWebMimeType($"file{extension}");
        await Assert.That(result).IsEqualTo(expectedMime);
    }

    // Image types
    [Test]
    [Arguments(".jpeg", "image/jpeg")]
    [Arguments(".jpg", "image/jpeg")]
    [Arguments(".png", "image/png")]
    [Arguments(".gif", "image/gif")]
    [Arguments(".bmp", "image/bmp")]
    [Arguments(".svg", "image/svg+xml")]
    [Arguments(".webp", "image/webp")]
    [Arguments(".ico", "image/vnd.microsoft.icon")]
    [Arguments(".tif", "image/tiff")]
    [Arguments(".tiff", "image/tiff")]
    public async Task GetWebMimeType_ImageFiles_ReturnsCorrectMimeType(string extension, string expectedMime)
    {
        var result = MimeTypeHelper.GetWebMimeType($"file{extension}");
        await Assert.That(result).IsEqualTo(expectedMime);
    }

    // Text types
    [Test]
    [Arguments(".txt", "text/plain")]
    [Arguments(".html", "text/html")]
    [Arguments(".htm", "text/html")]
    [Arguments(".css", "text/css")]
    [Arguments(".csv", "text/csv")]
    public async Task GetWebMimeType_TextFiles_ReturnsCorrectMimeType(string extension, string expectedMime)
    {
        var result = MimeTypeHelper.GetWebMimeType($"file{extension}");
        await Assert.That(result).IsEqualTo(expectedMime);
    }

    // Application types
    [Test]
    [Arguments(".json", "application/json")]
    [Arguments(".pdf", "application/pdf")]
    [Arguments(".doc", "application/msword")]
    [Arguments(".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [Arguments(".xls", "application/vnd.ms-excel")]
    [Arguments(".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [Arguments(".ppt", "application/vnd.ms-powerpoint")]
    [Arguments(".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    [Arguments(".xml", "application/xml")]
    [Arguments(".zip", "application/zip")]
    [Arguments(".tar", "application/x-tar")]
    [Arguments(".rar", "application/vnd.rar")]
    [Arguments(".7z", "application/x-7z-compressed")]
    [Arguments(".js", "application/javascript")]
    [Arguments(".php", "application/x-httpd-php")]
    [Arguments(".bin", "application/octet-stream")]
    public async Task GetWebMimeType_ApplicationFiles_ReturnsCorrectMimeType(string extension, string expectedMime)
    {
        var result = MimeTypeHelper.GetWebMimeType($"file{extension}");
        await Assert.That(result).IsEqualTo(expectedMime);
    }

    [Test]
    public async Task GetWebMimeType_UnknownExtension_ReturnsNull()
    {
        var result = MimeTypeHelper.GetWebMimeType("file.unknown");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetWebMimeType_NoExtension_ReturnsNull()
    {
        var result = MimeTypeHelper.GetWebMimeType("filename");
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task GetWebMimeType_CaseInsensitive_ReturnsCorrectMimeType()
    {
        var lowerResult = MimeTypeHelper.GetWebMimeType("file.jpg");
        var upperResult = MimeTypeHelper.GetWebMimeType("file.JPG");
        var mixedResult = MimeTypeHelper.GetWebMimeType("file.JpG");

        await Assert.That(lowerResult).IsEqualTo("image/jpeg");
        await Assert.That(upperResult).IsEqualTo("image/jpeg");
        await Assert.That(mixedResult).IsEqualTo("image/jpeg");
    }

    [Test]
    public async Task GetWebMimeType_WithPath_ReturnsCorrectMimeType()
    {
        var result = MimeTypeHelper.GetWebMimeType("/path/to/file.pdf");
        await Assert.That(result).IsEqualTo("application/pdf");
    }

    [Test]
    public async Task GetWebMimeType_MultipleDotsInFilename_ReturnsCorrectMimeType()
    {
        var result = MimeTypeHelper.GetWebMimeType("file.name.with.dots.png");
        await Assert.That(result).IsEqualTo("image/png");
    }

    // GetMimeType tests (wrapper with default)
    [Test]
    public async Task GetMimeType_KnownExtension_ReturnsCorrectMimeType()
    {
        var result = MimeTypeHelper.GetMimeType("file.pdf");
        await Assert.That(result).IsEqualTo("application/pdf");
    }

    [Test]
    public async Task GetMimeType_UnknownExtension_ReturnsOctetStream()
    {
        var result = MimeTypeHelper.GetMimeType("file.unknown");
        await Assert.That(result).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task GetMimeType_NoExtension_ReturnsOctetStream()
    {
        var result = MimeTypeHelper.GetMimeType("filename");
        await Assert.That(result).IsEqualTo("application/octet-stream");
    }
}

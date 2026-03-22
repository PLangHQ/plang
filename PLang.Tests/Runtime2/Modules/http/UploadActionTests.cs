using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the upload action handler — content resolution, multipart, base64, forced content types.
/// </summary>
public class UploadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_ul_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _engine.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    [Test]
    public async Task Upload_FilePath_SendsBinaryStreamContent()
    {
        // File path content → StreamContent with application/octet-stream
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Upload_DictionaryContent_SendsMultipartFormData()
    {
        // Dictionary with @file refs → MultipartFormDataContent with file streams
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Upload_AsBase64_DecodesAndSendsBinary()
    {
        // As=Base64, base64 string → decoded bytes as StreamContent
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Upload_AsFile_ForcesFileEvenForAmbiguous()
    {
        // As=File forces StreamContent regardless of content shape (e.g., dict that looks like form data)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Upload_AsText_ForcesStringBody()
    {
        // As=Text forces StringContent even if content looks like a valid file path
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Upload_OnProgress_CallsGoalWithTransferProgress()
    {
        // OnProgress set → goal called every 500ms with TransferProgress during upload
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Upload_ErrorStatusCode_ReturnsDataFromError()
    {
        // Server returns 500 → Data.FromError with status code, reason phrase, and response body
        Assert.Fail("Not implemented");
    }
}

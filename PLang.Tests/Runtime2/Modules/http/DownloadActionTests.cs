using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the download action handler — file saving, existence checks, error handling.
/// </summary>
public class DownloadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_dl_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
    }

    [After(Test)]
    public void Cleanup()
    {
        try
        {
            _engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    [Test]
    public async Task Download_HappyPath_SavesFileAndReturnsPath()
    {
        // Downloads response body to correct path via IPLangFileSystem, returns Data.Ok(filePath)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Download_FileExistsError_ReturnsDataFail()
    {
        // IfExists=Error (default), file already exists → Data.Fail
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Download_FileExistsOverwrite_ReplacesFile()
    {
        // IfExists=Overwrite, file already exists → downloads and replaces content
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Download_FileExistsSkip_ReturnsPathNoDownload()
    {
        // IfExists=Skip, file already exists → returns path immediately, no HTTP call made
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Download_CreatesParentDirectories()
    {
        // SaveTo path with missing parent dirs → directories created automatically
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Download_ErrorStatusCode_ReturnsFailNoFile()
    {
        // 404 response → Data.Fail with status code, no file created on disk
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Download_OnProgress_CallsGoalWithTransferProgress()
    {
        // OnProgress set → goal called every 500ms with TransferProgress (BytesTransferred, TotalBytes, Percentage)
        Assert.Fail("Not implemented");
    }

    [Test]
    public async Task Download_OnProgress_NullTotalBytes_WhenNoContentLength()
    {
        // Server sends no Content-Length header → TransferProgress.TotalBytes is null, Percentage is null
        Assert.Fail("Not implemented");
    }
}

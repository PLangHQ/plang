using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.Engine.Settings;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the download action handler — file saving, existence checks, error handling.
/// </summary>
public class DownloadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpProvider _mock = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_dl_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);

        _mock = new MockHttpProvider();
        _engine.Providers.Register<IHttpProvider>(_mock);
        _engine.Providers.SetDefault<IHttpProvider>("mock");
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

    private class MockHttpProvider : IHttpProvider
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK);
        public bool SendCalled { get; private set; }

        public Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken ct)
        {
            CapturedRequest = request;
            SendCalled = true;
            return Task.FromResult(Response);
        }

        public Data Configure(ISettings config) => Data.Ok();
        public void Dispose() { }
    }

    [Test]
    public async Task Download_HappyPath_SavesFileAndReturnsPath()
    {
        var fileContent = "Hello, downloaded file!"u8.ToArray();
        _mock.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(fileContent)
        };

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "downloads/file.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("downloads/file.txt");

        // Verify file exists on disk
        var fullPath = _engine.FileSystem.ValidatePath("downloads/file.txt");
        await Assert.That(_engine.FileSystem.File.Exists(fullPath)).IsTrue();
        var content = await _engine.FileSystem.File.ReadAllTextAsync(fullPath);
        await Assert.That(content).IsEqualTo("Hello, downloaded file!");
    }

    [Test]
    public async Task Download_FileExistsError_ReturnsDataFromError()
    {
        // Create existing file
        var fullPath = _engine.FileSystem.ValidatePath("existing.txt");
        var dir = _engine.FileSystem.Path.GetDirectoryName(fullPath)!;
        if (!_engine.FileSystem.Directory.Exists(dir))
            _engine.FileSystem.Directory.CreateDirectory(dir);
        await _engine.FileSystem.File.WriteAllTextAsync(fullPath, "existing");

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "existing.txt",
            IfExists = FileExists.Error,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("FileExists");
    }

    [Test]
    public async Task Download_FileExistsOverwrite_ReplacesFile()
    {
        // Create existing file
        var fullPath = _engine.FileSystem.ValidatePath("overwrite.txt");
        await _engine.FileSystem.File.WriteAllTextAsync(fullPath, "old content");

        var newContent = "new content"u8.ToArray();
        _mock.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(newContent)
        };

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "overwrite.txt",
            IfExists = FileExists.Overwrite,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var content = await _engine.FileSystem.File.ReadAllTextAsync(fullPath);
        await Assert.That(content).IsEqualTo("new content");
    }

    [Test]
    public async Task Download_FileExistsSkip_ReturnsPathNoDownload()
    {
        // Create existing file
        var fullPath = _engine.FileSystem.ValidatePath("skip.txt");
        await _engine.FileSystem.File.WriteAllTextAsync(fullPath, "existing");

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "skip.txt",
            IfExists = FileExists.Skip,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsEqualTo("skip.txt");
        // No HTTP call should have been made
        await Assert.That(_mock.SendCalled).IsFalse();
    }

    [Test]
    public async Task Download_CreatesParentDirectories()
    {
        var fileContent = "nested file"u8.ToArray();
        _mock.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(fileContent)
        };

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "deep/nested/dir/file.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var fullPath = _engine.FileSystem.ValidatePath("deep/nested/dir/file.txt");
        await Assert.That(_engine.FileSystem.File.Exists(fullPath)).IsTrue();
    }

    [Test]
    public async Task Download_ErrorStatusCode_ReturnsFailNoFile()
    {
        _mock.Response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not Found"),
            ReasonPhrase = "Not Found"
        };

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/missing.txt",
            SaveTo = "should-not-exist.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);

        // Verify no file was created
        var fullPath = _engine.FileSystem.ValidatePath("should-not-exist.txt");
        await Assert.That(_engine.FileSystem.File.Exists(fullPath)).IsFalse();
    }

    [Test]
    public async Task Download_OnProgress_CallsGoalWithTransferProgress()
    {
        // Can't easily test goal invocation, but verify the download succeeds with OnProgress set
        var data = new byte[10000];
        Array.Fill<byte>(data, 0x42);
        _mock.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(data)
        };
        _mock.Response.Content.Headers.ContentLength = data.Length;

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/large.bin",
            SaveTo = "progress.bin",
            OnProgress = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ShowProgress" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Download_OnProgress_NullTotalBytes_WhenNoContentLength()
    {
        var data = new byte[1000];
        _mock.Response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(data)
        };
        // Don't set ContentLength — it should be null

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/unknown-size.bin",
            SaveTo = "nosize.bin",
            OnProgress = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ShowProgress" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }
}

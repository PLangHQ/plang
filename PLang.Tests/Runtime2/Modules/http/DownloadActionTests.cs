using System.Net;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

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
        public download? CapturedDownload { get; private set; }
        public bool DownloadCalled { get; private set; }
        public Func<download, Task<Data>>? OnDownload { get; set; }

        public async Task<Data> SendAsync(request action) => Data.Ok();
        public async Task<Data> DownloadAsync(download action)
        {
            CapturedDownload = action;
            DownloadCalled = true;
            if (OnDownload != null) return await OnDownload(action);
            return Data.Ok(action.SaveTo);
        }
        public async Task<Data> UploadAsync(upload action) => Data.Ok();
        public Data Configure(configure action) => Data.Ok();
        public void Dispose() { }
    }

    [Test]
    public async Task Download_HappyPath_ProviderReceivesAction()
    {
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
        await Assert.That(_mock.CapturedDownload!.Url).IsEqualTo("https://example.com/file.txt");
    }

    [Test]
    public async Task Download_FileExistsError_ProviderReturnsError()
    {
        _mock.OnDownload = async action =>
            Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "File already exists", "FileExists", 409));

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
    public async Task Download_FileExistsOverwrite_PassedToProvider()
    {
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
        await Assert.That(_mock.CapturedDownload!.IfExists).IsEqualTo(FileExists.Overwrite);
    }

    [Test]
    public async Task Download_FileExistsSkip_PassedToProvider()
    {
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
        await Assert.That(_mock.CapturedDownload!.IfExists).IsEqualTo(FileExists.Skip);
    }

    [Test]
    public async Task Download_CreatesParentDirectories_ProviderHandles()
    {
        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "deep/nested/dir/file.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Download_ErrorStatusCode_ProviderReturnsError()
    {
        _mock.OnDownload = async action =>
            Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "404 Not Found", "HttpError", 404));

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
    }

    [Test]
    public async Task Download_OnProgress_PassedToProvider()
    {
        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/large.bin",
            SaveTo = "progress.bin",
            OnProgress = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ShowProgress" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedDownload!.OnProgress).IsNotNull();
        await Assert.That(_mock.CapturedDownload!.OnProgress!.Name).IsEqualTo("ShowProgress");
    }

    [Test]
    public async Task Download_OnProgress_NullTotalBytes_ProviderHandles()
    {
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

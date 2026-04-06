using System.Net;
using System.Text;
using App.Engine.Context;
using App.Engine.Variables;
using App.modules.http;
using App.modules.http.providers;
using PLangEngine = App.Engine.@this;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests download action with real DefaultHttpProvider + mock HTTP transport.
/// </summary>
public class DownloadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_dl_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);

        _handler = new MockHttpMessageHandler();
        var provider = new DefaultHttpProvider(_handler) { Name = "test" };
        _engine.Providers.Register<IHttpProvider>(provider);
        _engine.Providers.SetDefault<IHttpProvider>("test");
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

    private class MockHttpMessageHandler : System.Net.Http.HttpMessageHandler
    {
        public Func<System.Net.Http.HttpRequestMessage, Task<System.Net.Http.HttpResponseMessage>>? Handler { get; set; }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Handler != null) return Handler(request);
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("file content", Encoding.UTF8, "text/plain")
            });
        }
    }

    [Test]
    public async Task Download_HappyPath_SavesFile()
    {
        _handler.Handler = _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("downloaded data", Encoding.UTF8, "text/plain")
        });

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "file.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var savedPath = System.IO.Path.Combine(_tempDir, "file.txt");
        await Assert.That(System.IO.File.Exists(savedPath)).IsTrue();
        var content = await System.IO.File.ReadAllTextAsync(savedPath);
        await Assert.That(content).IsEqualTo("downloaded data");
    }

    [Test]
    public async Task Download_FileExistsError_ReturnsError()
    {
        var existingPath = System.IO.Path.Combine(_tempDir, "existing.txt");
        await System.IO.File.WriteAllTextAsync(existingPath, "old content");

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
    public async Task Download_FileExistsSkip_ReturnsSavePath()
    {
        var existingPath = System.IO.Path.Combine(_tempDir, "skip.txt");
        await System.IO.File.WriteAllTextAsync(existingPath, "keep this");

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
        var content = await System.IO.File.ReadAllTextAsync(existingPath);
        await Assert.That(content).IsEqualTo("keep this");
    }

    [Test]
    public async Task Download_FileExistsOverwrite_ReplacesFile()
    {
        var existingPath = System.IO.Path.Combine(_tempDir, "overwrite.txt");
        await System.IO.File.WriteAllTextAsync(existingPath, "old");

        _handler.Handler = _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("new", Encoding.UTF8, "text/plain")
        });

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
        var content = await System.IO.File.ReadAllTextAsync(existingPath);
        await Assert.That(content).IsEqualTo("new");
    }

    [Test]
    public async Task Download_CreatesParentDirectories()
    {
        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/file.txt",
            SaveTo = "deep/nested/file.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var savedPath = System.IO.Path.Combine(_tempDir, "deep", "nested", "file.txt");
        await Assert.That(System.IO.File.Exists(savedPath)).IsTrue();
    }

    [Test]
    public async Task Download_404_ReturnsHttpError()
    {
        _handler.Handler = _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new System.Net.Http.StringContent("Not Found")
        });

        var action = new download
        {
            Context = Ctx,
            Url = "https://example.com/missing.txt",
            SaveTo = "nope.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }
}

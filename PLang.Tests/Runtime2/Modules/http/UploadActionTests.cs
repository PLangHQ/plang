using System.Net;
using System.Text;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;
using HttpMethod = PLang.Runtime2.modules.http.HttpMethod;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests upload action with real DefaultHttpProvider + mock HTTP transport.
/// </summary>
public class UploadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_ul_" + Guid.NewGuid().ToString("N")[..8]);
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
        public System.Net.Http.HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(
            System.Net.Http.HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (Handler != null) return Handler(request);
            return Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new System.Net.Http.StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            });
        }
    }

    [Test]
    public async Task Upload_TextContent_SendsStringContent()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "Hello upload",
            As = ContentAs.Text,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_handler.LastRequest!.Method).IsEqualTo(System.Net.Http.HttpMethod.Post);
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).IsEqualTo("Hello upload");
    }

    [Test]
    public async Task Upload_FileContent_SendsBytes()
    {
        var filePath = System.IO.Path.Combine(_tempDir, "upload.txt");
        await System.IO.File.WriteAllTextAsync(filePath, "file data");

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "upload.txt",
            As = ContentAs.File,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var body = await _handler.LastRequest!.Content!.ReadAsByteArrayAsync();
        await Assert.That(Encoding.UTF8.GetString(body)).IsEqualTo("file data");
    }

    [Test]
    public async Task Upload_Base64Content_DecodesAndSends()
    {
        var original = "hello base64";
        var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(original));

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = b64,
            As = ContentAs.Base64,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var body = await _handler.LastRequest!.Content!.ReadAsByteArrayAsync();
        await Assert.That(Encoding.UTF8.GetString(body)).IsEqualTo(original);
    }

    [Test]
    public async Task Upload_AutoDetectFile_WhenFileExists()
    {
        var filePath = System.IO.Path.Combine(_tempDir, "auto.txt");
        await System.IO.File.WriteAllTextAsync(filePath, "auto content");

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "auto.txt",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Upload_AutoDetectString_WhenNoFile()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "just a string, not a file path",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).IsEqualTo("just a string, not a file path");
    }

    [Test]
    public async Task Upload_CustomMethod_UsedCorrectly()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "data",
            Method = HttpMethod.PUT,
            As = ContentAs.Text,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_handler.LastRequest!.Method).IsEqualTo(System.Net.Http.HttpMethod.Put);
    }

    [Test]
    public async Task Upload_ResponseParsed_AsJson()
    {
        _handler.Handler = _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("{\"id\":42}", Encoding.UTF8, "application/json")
        });

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "data",
            As = ContentAs.Text,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Properties["StatusCode"]!.Value).IsEqualTo(200);
    }
}

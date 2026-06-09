using System.Net;
using System.Text;
using app.actor.context;
using app.variable;
using app.module.http;
using app.module.http.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests download action with real Default + mock HTTP transport.
/// </summary>
public class DownloadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_dl_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);

        _handler = new MockHttpMessageHandler();
        var provider = new Default(_handler) { Name = (global::app.type.text.@this)"test" };
        _app.Code.Register<IHttp>(provider);
        _app.Code.SetDefault<IHttp>("test");
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::app.actor.context.@this Ctx => _app.System.Context;

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
    public async Task Download_HappyPath_ReturnsBytes()
    {
        _handler.Handler = _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.StringContent("downloaded data", Encoding.UTF8, "text/plain")
        });

        var action = new download
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://example.com/file.txt",
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsSuccess();
        var bytes = (await result.Value()) as byte[];
        await Assert.That(bytes).IsNotNull();
        await Assert.That(Encoding.UTF8.GetString(bytes!)).IsEqualTo("downloaded data");
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
            Url = (global::app.type.text.@this)"https://example.com/missing.txt",
            Unsigned = (global::app.type.@bool.@this)true
        };

        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }
}

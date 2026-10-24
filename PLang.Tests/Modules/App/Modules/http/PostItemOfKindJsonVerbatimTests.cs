using System.Net;
using System.Net.Http;
using System.Text;
using app.module.code;
using app.module.http;
using app.module.http.code;
using PLangEngine = global::app.@this;
using HttpMethod = global::app.module.http.HttpMethod;

namespace PLang.Tests.App.Modules.http;

// Stage 4 (kind-is-type, decision 792): a body of `{item, kind: json}` holding raw bytes is sent
// verbatim, unread — the body stays an item until something reads it.
public class PostItemOfKindJsonVerbatimTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_post_json_verbatim_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir, global::PLang.Tests.Parent.Actor);

        _handler = new MockHandler();
        var provider = new Default(_handler) { Name = "test" };
        _app.Code.Register<IHttp>(provider);
        _app.Code.SetDefault<IHttp>("test");
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _app.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
        }
        catch { }
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    private class MockHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json") });
        }
    }

    [Test]
    public async Task PostItemOfKindJson_IsSentVerbatim_Unread()
    {
        const string raw = "{\"deep\": \"m-1\", \"fixed\": \"pcm\"}";
        var body = new global::app.data.@this("", raw, new global::app.type.@this("item", "json"), context: Ctx);
        var action = new request(Ctx)
        {
            Url = (global::app.type.item.text.@this)"https://api.example.com/speech",
            Method = (global::app.type.item.choice.@this<global::app.module.http.HttpMethod>)HttpMethod.POST,
            Body = body,
            Unsigned = (global::app.type.item.@bool.@this)true,
        };
        var result = await new global::app.goal.step.action.@this(action, Ctx).Start(Ctx);

        await result.IsSuccess();
        await Assert.That(await _handler.LastRequest!.Content!.ReadAsStringAsync()).IsEqualTo(raw);
        // The body stayed an item (unread) — the ride didn't force a parse.
        await Assert.That(body.Type.Name).IsEqualTo("item");
        await Assert.That(body.Type.kind.Name).IsEqualTo("json");
    }
}

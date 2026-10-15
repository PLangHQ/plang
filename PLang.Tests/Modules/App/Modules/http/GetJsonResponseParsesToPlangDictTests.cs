using System.Net;
using System.Net.Http;
using System.Text;
using app.module.code;
using app.module.http;
using app.module.http.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.http;

// Stage 4 (kind-is-type, decision 792): a json response's value is a plang dict after first touch;
// `.name` reads "Alice" through plang navigation — no JsonSerializer.Serialize dance.
public class GetJsonResponseParsesToPlangDictTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_get_json_dict_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);

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
        public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Handler { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            => Handler != null ? Handler(request) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json") });
    }

    [Test]
    public async Task GetJsonResponse_ValueIsAPlangDict_NavigableThroughPlang()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"name\":\"Alice\"}", Encoding.UTF8, "application/json")
        });

        var action = new request(Ctx)
        {
            Url = (global::app.type.item.text.@this)"https://api.example.com/users/1",
            Unsigned = (global::app.type.item.@bool.@this)true,
        };
        var result = await new global::app.goal.step.action.@this(action, Ctx).Start(Ctx);

        await result.IsSuccess();
        // Touch the value — parse fires once; after the touch the result's plang type
        // narrows to dict (decision 787: a value's type is what it is now).
        await result.Value();
        await Assert.That(result.Type.Name).IsEqualTo("dict");
        // And navigate: .name reads "Alice" through plang.
        var name = await result.Get("name");
        await Assert.That((await name.Value())?.ToString()).IsEqualTo("Alice");
    }
}

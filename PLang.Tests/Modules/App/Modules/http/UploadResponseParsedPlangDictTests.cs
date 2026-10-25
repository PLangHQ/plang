using System.Net;
using System.Net.Http;
using System.Text;
using app.module.code;
using app.module.http;
using app.module.http.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.http;

// Stage 4 (kind-is-type, decision 792): an upload's json response value is a plang dict —
// `.id` reads 42 (a plang number) through plang navigation; no JsonSerializer.Serialize dance.
public class UploadResponseParsedPlangDictTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_upload_json_dict_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir, global::PLang.Tests.Parent.Actor).Testing();

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
            { Content = new StringContent("{}", Encoding.UTF8, "application/json") });
    }

    [Test]
    public async Task UploadJsonResponse_ValueIsAPlangDict_IdReadsAsAPlangNumber()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"id\":42}", Encoding.UTF8, "application/json")
        });

        var action = new upload(Ctx)
        {
            Url = (global::app.type.item.text.@this)"https://api.example.com/upload",
            Content = new global::app.data.@this("", "data", context: Ctx),
            As = (global::app.type.item.choice.@this<global::app.module.http.ContentAs>)ContentAs.Text,
            Unsigned = (global::app.type.item.@bool.@this)true,
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        // A use opens the courier-rested json (805): navigation parses once, .id reads as a plang number,
        // and the value narrows to a plang dict (787).
        var id = await result.Get("id");
        var materialized = await id.Value();
        await Assert.That(materialized).IsTypeOf<global::app.type.item.number.@this>();
        await Assert.That(materialized!.ToString()).IsEqualTo("42");
        await Assert.That(result.Type.Name).IsEqualTo("dict");
    }
}

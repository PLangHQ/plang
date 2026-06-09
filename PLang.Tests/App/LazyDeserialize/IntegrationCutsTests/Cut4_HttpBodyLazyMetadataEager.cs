using System.Net;
using System.Net.Http;
using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.module.http;
using app.module.http.code;

namespace PLang.Tests.App.LazyDeserialize.IntegrationCutsTests;

// Cut 4 — `get http` against a json endpoint. Status reads via properties
// (eager); the body materialises only on navigation (lazy). And
// `http.response.@this` is gone — the result is plain Data (Decision 6).
public class Cut4_HttpBodyLazyMetadataEager
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public System.Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, System.Threading.CancellationToken ct)
            => Task.FromResult(Respond!(req));
    }

    private static async Task<(global::app.@this App, global::app.data.@this Result)> Get()
    {
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-cut4-" + System.Guid.NewGuid().ToString("N")[..8]));
        var handler = new StubHandler
        {
            Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"name\":\"Ada\"}", Encoding.UTF8, "application/json") }
        };
        app.Code.Register<IHttp>(new Default(handler) { Name = (global::app.type.text.@this)"test" });
        app.Code.SetDefault<IHttp>("test");
        var action = new request { Context = app.User.Context, Url = (global::app.type.text.@this)"https://x/y", Unsigned = (global::app.type.@bool.@this)true };
        return (app, await action.Run());
    }

    [Test] public async Task Cut4_StatusRead_DoesNotMaterialiseBody()
    {
        var (app, r) = await Get();
        await using (app)
        {
            await Assert.That((await r.GetChild("!StatusCode").Value())?.ToString()).IsEqualTo("200");
            await Assert.That(r.MaterializeCount).IsEqualTo(0); // body stayed raw
        }
    }

    [Test] public async Task Cut4_FieldRead_MaterialisesBody()
    {
        var (app, r) = await Get();
        await using (app)
        {
            await Assert.That((await r.GetChild("name").Value())?.ToString()).IsEqualTo("Ada"); // navigate materializes
            await Assert.That(r.MaterializeCount).IsEqualTo(1);
        }
    }

    [Test] public async Task Cut4_HttpResponseTypeIsGone()
        => await Assert.That(typeof(request).Assembly.GetType("app.http.response.@this")).IsNull();
}

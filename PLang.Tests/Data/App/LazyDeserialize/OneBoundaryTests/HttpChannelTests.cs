using System.Net;
using System.Net.Http;
using System.Text;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.module.http;
using app.module.http.code;
using httpchannel = global::app.channel.type.http.@this;

namespace PLang.Tests.App.LazyDeserialize.OneBoundaryTests;

// The http channel — bidirectional: write the request, read the response.
// Body becomes the lazy value (type/kind from Content-Type); status,
// headers, duration become Data properties. `http.response.@this` deletes
// (Decision 6).
public class HttpChannelTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public System.Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, System.Threading.CancellationToken ct)
            => Task.FromResult(Respond!(req));
    }

    private static global::app.@this NewApp(out StubHandler handler)
    {
        var app = new global::app.@this(System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "plang-httpchan-" + System.Guid.NewGuid().ToString("N")[..8]));
        handler = new StubHandler();
        var provider = new Default(handler) { Name = "test" };
        app.Code.Register<IHttp>(provider);
        app.Code.SetDefault<IHttp>("test");
        return app;
    }

    private static async Task<global::app.data.@this> Get(global::app.@this app, StubHandler handler, string contentType, string body)
    {
        handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(body, Encoding.UTF8, contentType) };
        var action = new request { Context = app.User.Context, Url = (global::app.type.text.@this)"https://x/y", Unsigned = (global::app.type.@bool.@this)true };
        return await action.Run();
    }

    [Test] public async Task HttpChannel_IsBidirectional()
    {
        await using var app = NewApp(out _);
        var ch = new httpchannel("application/json", Encoding.UTF8.GetBytes("{}"), app.User.Context);
        await Assert.That(ch.Direction).IsEqualTo(global::app.channel.ChannelDirection.Bidirectional);
    }

    [Test] public async Task HttpGet_OpensHttpChannel_StopsContentTypeDeserialize()
    {
        await using var app = NewApp(out var handler);
        var r = await Get(app, handler, "application/json", "{\"a\":1}");
        await r.IsSuccess();
        await Assert.That(r.MaterializeCount()).IsEqualTo(0);            // not deserialized at read
        await Assert.That(r.Peek()).IsEqualTo((object)"{\"a\":1}"); // raw body held
    }

    // Independent #12 — strict deletion probe by absolute name.
    [Test] public async Task HttpResponse_TypeDeleted_ByAbsoluteName()
        => await Assert.That(typeof(request).Assembly.GetType("app.http.response.@this")).IsNull();

    // Independent #13 — http.request's Run signature is Task<Data>, not the type.
    [Test] public async Task HttpGet_Run_ReturnTypeIsData_NotHttpResponse()
    {
        var ret = typeof(request).GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, System.Type.EmptyTypes)!.ReturnType;
        await Assert.That(ret).IsEqualTo(typeof(Task<global::app.data.@this>));
    }

    [Test] public async Task HttpResponse_BodyIsLazyValue_StatusHeadersDurationAreProperties()
    {
        await using var app = NewApp(out var handler);
        var r = await Get(app, handler, "application/json", "{\"a\":1}");
        await r.IsSuccess();
        await Assert.That(r.HasRaw).IsTrue();                                   // body is lazy
        await Assert.That(r.Properties.ContainsKey("StatusCode")).IsTrue();      // metadata = properties
        await Assert.That(r.Properties.ContainsKey("Headers")).IsTrue();
        await Assert.That(r.Properties.ContainsKey("Duration")).IsTrue();
    }

    // Independent #19 — a property (status) read never materializes the body.
    [Test] public async Task HttpStatusRead_DoesNotMaterialiseBody()
    {
        await using var app = NewApp(out var handler);
        var r = await Get(app, handler, "application/json", "{\"a\":1}");
        await Assert.That((await (await r.GetChild("!StatusCode")).Value())?.ToString()).IsEqualTo("200");
        await Assert.That(r.MaterializeCount()).IsEqualTo(0); // body untouched
    }
}

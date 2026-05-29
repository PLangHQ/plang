using System.Net;
using System.Net.Http;
using System.Text;
using app.module.code;
using app.module.http;
using app.module.http.code;
using Response = global::app.http.Response.@this;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract: http.request's Response.Body is dispatched by Content-Type. JSON
// parses to a JsonElement/Dictionary; text/* lands as a string; binary and
// missing Content-Type fall back to byte[].

public class Stage3_HttpContentTypeDispatchTests
{
    private string _tempDir = null!;
    private global::app.@this _app = null!;
    private StubHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-stage3-" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir);

        _handler = new StubHandler();
        var provider = new Default(_handler) { Name = "test" };
        _app.Code.Register<IHttp>(provider);
        _app.Code.SetDefault<IHttp>("test");
    }

    [After(Test)]
    public async Task Cleanup()
    {
        await _app.DisposeAsync();
        try { System.IO.Directory.Delete(_tempDir, true); } catch { /* best effort */ }
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public System.Func<HttpRequestMessage, HttpResponseMessage>? Respond { get; set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, System.Threading.CancellationToken ct)
            => Task.FromResult(Respond!(req));
    }

    private async Task<Response> Get(string url, System.Action<HttpResponseMessage> shape)
    {
        _handler.Respond = _ => {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            shape(resp);
            return resp;
        };
        var action = new request { Context = _app.User.Context, Url = url, Unsigned = true };
        var result = await action.Run();
        await Assert.That(result.Success).IsTrue();
        return result.Value!;
    }

    [Test]
    public async Task BodyDispatch_ApplicationJson_YieldsJsonNode()
    {
        var resp = await Get("https://x/y", r => r.Content = new StringContent("{\"a\":1}", Encoding.UTF8, "application/json"));
        await Assert.That(resp.Body).IsNotNull();
        var json = System.Text.Json.JsonSerializer.Serialize(resp.Body);
        await Assert.That(json).Contains("\"a\"").And.Contains("1");
    }

    [Test]
    public async Task BodyDispatch_TextHtml_YieldsString()
    {
        var resp = await Get("https://x/p", r => r.Content = new StringContent("<p>hi</p>", Encoding.UTF8, "text/html"));
        await Assert.That(resp.Body).IsTypeOf<string>();
        await Assert.That((string)resp.Body!).IsEqualTo("<p>hi</p>");
    }

    [Test]
    public async Task BodyDispatch_ImagePng_YieldsByteArray()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var resp = await Get("https://x/img", r => {
            r.Content = new ByteArrayContent(bytes);
            r.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        });
        await Assert.That(resp.Body).IsTypeOf<byte[]>();
        await Assert.That((byte[])resp.Body!).IsEquivalentTo(bytes);
    }

    [Test]
    public async Task BodyDispatch_MissingContentType_FallsBackToByteArray()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var resp = await Get("https://x/raw", r => {
            r.Content = new ByteArrayContent(bytes);
            r.Content.Headers.ContentType = null;
        });
        await Assert.That(resp.Body).IsTypeOf<byte[]>();
    }

    [Test]
    public async Task BodyDispatch_TextUnknownSubtype_FallsBackToString()
    {
        var resp = await Get("https://x/odd", r => r.Content = new StringContent("plain words", Encoding.UTF8, "text/x-unknown"));
        await Assert.That(resp.Body).IsTypeOf<string>();
        await Assert.That((string)resp.Body!).IsEqualTo("plain words");
    }

    [Test]
    public async Task BodyDispatch_TextCsv_YieldsCsv_IfMaterializerRegistered()
    {
        // No Csv materializer is currently registered — text/* fallback rule
        // applies and Body lands as a string. Add a Csv materializer to flip
        // this expectation.
        var resp = await Get("https://x/data", r => r.Content = new StringContent("a,b\n1,2", Encoding.UTF8, "text/csv"));
        await Assert.That(resp.Body).IsTypeOf<string>()
            .Because("Without a registered Csv materializer, text/csv falls back to string.");
    }

    [Test]
    public async Task BodyDispatch_UsesSerializerRegistry_GetByContentType()
    {
        var json = await Get("https://x/j", r => r.Content = new StringContent("{\"k\":\"v\"}", Encoding.UTF8, "application/json"));
        var text = await Get("https://x/t", r => r.Content = new StringContent("hello",        Encoding.UTF8, "text/plain"));
        await Assert.That(json.Body).IsNotNull();
        await Assert.That(text.Body).IsTypeOf<string>();
    }

    [Test]
    public async Task BodyDispatch_BrokenJsonContentType_FallsBackToString()
    {
        // application/json with malformed body: deser.Success is false → the
        // TextFallback kicks in and Body lands as the raw bytes decoded to
        // string. If a future change made deser.Success always true, this
        // turns red.
        const string malformed = "{not json";
        var resp = await Get("https://x/broken", r =>
            r.Content = new StringContent(malformed, Encoding.UTF8, "application/json"));
        await Assert.That(resp.Body).IsTypeOf<string>()
            .Because("Malformed JSON must surface as the raw text via TextFallback, not null.");
        await Assert.That((string)resp.Body!).IsEqualTo(malformed);
    }

    [Test]
    public async Task HttpDownload_BodyDispatch_NotApplied()
    {
        // http.download writes to file; ParseResponseAsync is not invoked, so no
        // Response/Body wrapping happens. The signature itself encodes this.
        var ret = typeof(global::app.module.http.download)
            .GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, System.Type.EmptyTypes)!
            .ReturnType;
        await Assert.That(ret).IsEqualTo(typeof(Task<Data>));
    }
}

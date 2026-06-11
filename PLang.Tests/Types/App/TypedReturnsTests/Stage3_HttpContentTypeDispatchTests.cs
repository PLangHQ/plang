using System.Net;
using System.Net.Http;
using System.Text;
using app.module.code;
using app.module.http;
using app.module.http.code;
using data = global::app.data.@this;

namespace PLang.Tests.App.TypedReturnsTests;

// Contract (post-dissolve, lazy): http.request returns plain Data. The body is
// the LAZY value, stamped {type, kind} from Content-Type — `%resp%` (scalar) is
// the raw payload; navigation/`.Value` materializes it through the reader.
// status/headers/duration ride as Properties (read with `!`). http.download is
// untouched (saves to disk).
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

    private async Task<data> Get(string url, System.Action<HttpResponseMessage> shape)
    {
        _handler.Respond = _ => {
            var resp = new HttpResponseMessage(HttpStatusCode.OK);
            shape(resp);
            return resp;
        };
        var action = new request { Context = _app.User.Context, Url = (global::app.type.text.@this)url, Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();
        await result.IsSuccess();
        return result;
    }

    // json body stamps {object, json}; untouched it's the raw string, navigated
    // it materializes and a key resolves.
    [Test]
    public async Task Body_ApplicationJson_StampsObjectJson_LazyThenNavigates()
    {
        var resp = await Get("https://x/y", r => r.Content = new StringContent("{\"a\":1}", Encoding.UTF8, "application/json"));
        await Assert.That(resp.Type.Name).IsEqualTo("item");
        await Assert.That(resp.Peek()).IsEqualTo((object)"{\"a\":1}"); // untouched = raw
        await Assert.That((await (await resp.GetChild("a")).Value())?.ToString()).IsEqualTo("1"); // navigate materializes
    }

    [Test]
    public async Task Body_TextHtml_ScalarIsString()
    {
        var resp = await Get("https://x/p", r => r.Content = new StringContent("<p>hi</p>", Encoding.UTF8, "text/html"));
        await Assert.That(resp.Peek()).IsEqualTo((object)"<p>hi</p>");
    }

    [Test]
    public async Task Body_ImagePng_ScalarIsBytes_ValueMaterializesImage()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var resp = await Get("https://x/img", r => {
            r.Content = new ByteArrayContent(bytes);
            r.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        });
        await Assert.That(resp.Type.Name).IsEqualTo("image");
        await Assert.That(resp.Peek()).IsTypeOf<byte[]>();
    }

    [Test]
    public async Task Body_MissingContentType_StampsBytes()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var resp = await Get("https://x/raw", r => {
            r.Content = new ByteArrayContent(bytes);
            r.Content.Headers.ContentType = null;
        });
        // The stamp is {bytes} and the raw stays byte[] (scalar access may still
        // utf-8-decode if the bytes happen to be valid utf-8 — that's the Stage 5
        // scalar rule; the type/raw is what "stamps bytes" pins).
        await Assert.That(resp.Type.Name).IsEqualTo("bytes");
        await Assert.That(resp.Raw).IsTypeOf<byte[]>();
    }

    [Test]
    public async Task Body_TextCsv_StampsTable()
    {
        var resp = await Get("https://x/data", r => r.Content = new StringContent("a,b\n1,2", Encoding.UTF8, "text/csv"));
        await Assert.That(resp.Type.Name).IsEqualTo("table");
        await Assert.That(resp.Peek()).IsEqualTo((object)"a,b\n1,2"); // untouched = raw csv
    }

    // Malformed json no longer falls back at read time — it stays the raw string
    // (scalar) and would error only on a structured touch (lazy contract).
    [Test]
    public async Task Body_BrokenJson_ScalarIsRawString_NoEagerFail()
    {
        const string malformed = "{not json";
        var resp = await Get("https://x/broken", r =>
            r.Content = new StringContent(malformed, Encoding.UTF8, "application/json"));
        await Assert.That(resp.Peek()).IsEqualTo((object)malformed);
    }

    // status/headers are Properties — read with `!`, never touching the body.
    [Test]
    public async Task Metadata_StatusIsProperty_NotBody()
    {
        var resp = await Get("https://x/j", r => r.Content = new StringContent("{\"k\":\"v\"}", Encoding.UTF8, "application/json"));
        await Assert.That((await (await resp.GetChild("!StatusCode")).Value())?.ToString()).IsEqualTo("200");
        await Assert.That(resp.MaterializeCount()).IsEqualTo(0); // status read did not touch the body
    }

    [Test]
    public async Task HttpDownload_BodyDispatch_NotApplied()
    {
        var ret = typeof(global::app.module.http.download)
            .GetMethod("Run", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, System.Type.EmptyTypes)!
            .ReturnType;
        await Assert.That(ret).IsEqualTo(typeof(Task<Data>));
    }
}

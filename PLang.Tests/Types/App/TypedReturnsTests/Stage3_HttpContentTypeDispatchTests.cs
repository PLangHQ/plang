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
        _app = TestApp.Create(_tempDir);

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
        var action = new request(_app.User.Context) { Url = (global::app.type.text.@this)url, Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();
        await result.IsSuccess();
        return result;
    }

    // json body stamps {binary, json}; untouched it's the raw bytes, navigated
    // it materializes and a key resolves.
    [Test]
    public async Task Body_ApplicationJson_StampsBinaryJson_LazyThenNavigates()
    {
        var resp = await Get("https://x/y", r => r.Content = new StringContent("{\"a\":1}", Encoding.UTF8, "application/json"));
        await Assert.That(resp.Type.Name).IsEqualTo("binary"); // the flip: binary + json kind
        await Assert.That(resp.Raw is byte[]).IsTrue(); // untouched = raw bytes (Peek is the source carrier)
        await Assert.That((await (await resp.GetChild("a")).Value())?.ToString()).IsEqualTo("1"); // navigate materializes
    }

    // text/html stamps {binary, html}; untouched it's the raw bytes. (There is no
    // code/html reader, so Value() does not narrow it to a string — the html text
    // is reached only via an explicit `as text`.)
    [Test]
    public async Task Body_TextHtml_StampsBinaryHtml()
    {
        var resp = await Get("https://x/p", r => r.Content = new StringContent("<p>hi</p>", Encoding.UTF8, "text/html"));
        await Assert.That(resp.Type.Name).IsEqualTo("binary");
        // text/html → kind "htm" (canonicalised to the shortest extension form).
        await Assert.That(resp.Type.Kind).IsEqualTo("htm");
        await Assert.That(resp.Raw is byte[]).IsTrue(); // untouched = raw bytes
        // On access, the kind narrows to a `code` value (html is the code family).
        await Assert.That(await resp.Value()).IsTypeOf<global::app.type.code.@this>();
    }

    [Test]
    public async Task Body_ImagePng_ScalarIsBytes_ValueMaterializesImage()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 };
        var resp = await Get("https://x/img", r => {
            r.Content = new ByteArrayContent(bytes);
            r.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        });
        await Assert.That(resp.Type.Name).IsEqualTo("binary"); // the flip: binary + png kind (narrows to image on Value())
        await Assert.That(resp.Raw is byte[]).IsTrue(); // untouched = raw bytes
    }

    [Test]
    public async Task Body_MissingContentType_StampsBinary()
    {
        var bytes = new byte[] { 1, 2, 3 };
        var resp = await Get("https://x/raw", r => {
            r.Content = new ByteArrayContent(bytes);
            r.Content.Headers.ContentType = null;
        });
        // No Content-Type → opaque bytes → {binary, null}; the raw stays byte[].
        await Assert.That(resp.Type.Name).IsEqualTo("binary");
        await Assert.That(resp.Raw).IsTypeOf<byte[]>();
    }

    [Test]
    public async Task Body_TextCsv_StampsBinary()
    {
        var resp = await Get("https://x/data", r => r.Content = new StringContent("a,b\n1,2", Encoding.UTF8, "text/csv"));
        // The flip: csv body stamps {binary, csv}; untouched it's the raw bytes.
        await Assert.That(resp.Type.Name).IsEqualTo("binary");
        await Assert.That(resp.Type.Kind).IsEqualTo("csv");
        await Assert.That(resp.Raw is byte[]).IsTrue(); // untouched = raw bytes
    }

    // Malformed json no longer falls back at read time — the untouched value is
    // the raw bytes and would error only on a structured touch (lazy contract).
    [Test]
    public async Task Body_BrokenJson_UntouchedIsRawBytes_NoEagerFail()
    {
        const string malformed = "{not json";
        var resp = await Get("https://x/broken", r =>
            r.Content = new StringContent(malformed, Encoding.UTF8, "application/json"));
        await Assert.That(resp.Raw is byte[]).IsTrue();          // untouched = raw bytes, no eager parse
        await Assert.That(resp.MaterializeCount()).IsEqualTo(0); // malformed json never parsed at read
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

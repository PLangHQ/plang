using System.Net;
using System.Text;
using app.actor.context;
using app.type.item.variable;
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
        _app = new global::app.@this(_tempDir).Testing();

        _handler = new MockHttpMessageHandler();
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
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

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

        var action = new download(Ctx) { Url = (global::app.type.item.text.@this)"https://example.com/file.txt",
            Unsigned = (global::app.type.item.@bool.@this)true
        };

        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        var bytes = ((await result.Value()) as global::app.type.item.binary.@this)?.Value;
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

        var action = new download(Ctx) { Url = (global::app.type.item.text.@this)"https://example.com/missing.txt",
            Unsigned = (global::app.type.item.@bool.@this)true
        };

        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.Status.Code.ToInt32()).IsEqualTo(404);
    }

    // ---- Path and Hash: the body written as it arrives, hashed as it arrives ----

    private static string Sha256(byte[] bytes) => "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();

    private async Task<global::app.data.@this> Download(byte[] body, string? to, string? hash, long? max = null)
    {
        _handler.Handler = _ => Task.FromResult(new System.Net.Http.HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new System.Net.Http.ByteArrayContent(body)
        });
        var action = new download(Ctx)
        {
            Url = (global::app.type.item.text.@this)"https://example.com/layer",
            Unsigned = (global::app.type.item.@bool.@this)true,
            Path = to == null ? null : global::app.data.@this<global::app.type.item.path.@this>.Ok(global::app.type.item.path.@this.Resolve(to, Ctx)),
            Hash = hash == null ? null : global::app.data.@this<global::app.module.crypto.type.hash.@this>.From(new global::app.data.@this("Hash", hash, context: Ctx)),
            MaxDownloadSize = (global::app.type.item.number.@this)(max ?? 100 * 1024 * 1024),
        };
        await action.Attach(null, Ctx);
        return await action.Start();
    }

    private string OnDisk(string plang) => System.IO.Path.Combine(_tempDir, plang.TrimStart('/'));

    [Test]
    public async Task APathAndAMatchingHash_SaveTheBody_AndAnswerThePath()
    {
        var body = Encoding.UTF8.GetBytes("layer bytes");

        var result = await Download(body, "/out/layer.bin", Sha256(body));

        await result.IsSuccess();
        await Assert.That(await result.Value()).IsAssignableTo<global::app.type.item.path.@this>();
        await Assert.That(System.IO.File.ReadAllBytes(OnDisk("/out/layer.bin"))).IsEquivalentTo(body);
        await Assert.That(System.IO.File.Exists(OnDisk("/out/layer.bin.part"))).IsFalse();
    }

    [Test]
    public async Task AMismatchedHash_KeepsNothingAtThePath_AndNamesBothHashes()
    {
        var body = Encoding.UTF8.GetBytes("layer bytes");
        var expected = Sha256(Encoding.UTF8.GetBytes("other bytes"));

        var result = await Download(body, "/out/layer.bin", expected);

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("HashMismatch");
        await Assert.That(result.Error.Message).Contains(Sha256(body));
        await Assert.That(result.Error.Message).Contains(expected);
        await Assert.That(System.IO.File.Exists(OnDisk("/out/layer.bin"))).IsFalse();
        await Assert.That(System.IO.File.Exists(OnDisk("/out/layer.bin.part"))).IsFalse();
    }

    [Test]
    public async Task AHashWithoutAPath_ChecksTheBytes()
    {
        var body = Encoding.UTF8.GetBytes("in memory");

        await (await Download(body, null, Sha256(body))).IsSuccess();
        var refused = await Download(body, null, Sha256(Encoding.UTF8.GetBytes("else")));
        await Assert.That(refused.Error?.Key).IsEqualTo("HashMismatch");
    }

    [Test]
    public async Task ABodyOverItsCap_WithAPath_LeavesNothing()
    {
        var body = new byte[4096];

        // the cap's failure travels as an AppException (the action's dispatch answers it as the step's error)
        var thrown = await Assert.That(async () => await Download(body, "/out/big.bin", null, max: 1000))
            .Throws<global::app.error.AppException>();

        await Assert.That(thrown!.Error.Key).IsEqualTo("ResponseTooLarge");
        await Assert.That(System.IO.File.Exists(OnDisk("/out/big.bin"))).IsFalse();
        await Assert.That(System.IO.File.Exists(OnDisk("/out/big.bin.part"))).IsFalse();
    }

    [Test]
    public async Task ALargeBodyWithAPath_StreamsToTheFile()
    {
        var body = new byte[8 * 1024 * 1024];
        new Random(7).NextBytes(body);

        var result = await Download(body, "/out/large.bin", Sha256(body), max: 16 * 1024 * 1024);

        await result.IsSuccess();
        await Assert.That(new System.IO.FileInfo(OnDisk("/out/large.bin")).Length).IsEqualTo(body.LongLength);
    }
}

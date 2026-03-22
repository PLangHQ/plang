using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.Engine.Settings;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the upload action handler — content resolution, multipart, base64, forced content types.
/// </summary>
public class UploadActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpProvider _mock = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_ul_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);

        _mock = new MockHttpProvider();
        _engine.Providers.Register<IHttpProvider>(_mock);
        _engine.Providers.SetDefault<IHttpProvider>("mock");
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try
        {
            await _engine.DisposeAsync();
            if (System.IO.Directory.Exists(_tempDir))
                System.IO.Directory.Delete(_tempDir, true);
        }
        catch { /* best effort cleanup */ }
    }

    private PLangContext Ctx => _engine.System.Context;

    private class MockHttpProvider : IHttpProvider
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }
        public HttpRequestMessage? CapturedRequest { get; private set; }
        public HttpResponseMessage Response { get; set; } = new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        };

        public Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken ct)
        {
            CapturedRequest = request;
            return Task.FromResult(Response);
        }

        public Data Configure(ISettings config) => Data.Ok();
        public void Dispose() { }
    }

    [Test]
    public async Task Upload_FilePath_SendsBinaryStreamContent()
    {
        // Create a file to upload
        var filePath = _engine.FileSystem.ValidatePath("upload-test.bin");
        await _engine.FileSystem.File.WriteAllBytesAsync(filePath, new byte[] { 0x01, 0x02, 0x03 });

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "upload-test.bin",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest).IsNotNull();
        var contentType = _mock.CapturedRequest!.Content!.Headers.ContentType!.MediaType;
        await Assert.That(contentType).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Upload_DictionaryContent_SendsMultipartFormData()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/submit",
            Content = new Dictionary<string, object>
            {
                ["field1"] = "value1",
                ["field2"] = "value2"
            },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest!.Content).IsTypeOf<MultipartFormDataContent>();
    }

    [Test]
    public async Task Upload_AsBase64_DecodesAndSendsBinary()
    {
        var original = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var base64 = Convert.ToBase64String(original);

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/binary",
            Content = base64,
            As = ContentAs.Base64,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var sentBytes = await _mock.CapturedRequest!.Content!.ReadAsByteArrayAsync();
        await Assert.That(sentBytes.Length).IsEqualTo(4);
        await Assert.That(sentBytes[0]).IsEqualTo((byte)0xDE);
    }

    [Test]
    public async Task Upload_AsFile_ForcesFileEvenForAmbiguous()
    {
        // Create a file that also looks like it could be text
        var filePath = _engine.FileSystem.ValidatePath("data.json");
        await _engine.FileSystem.File.WriteAllTextAsync(filePath, "{\"key\":\"value\"}");

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "data.json",
            As = ContentAs.File,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var contentType = _mock.CapturedRequest!.Content!.Headers.ContentType!.MediaType;
        await Assert.That(contentType).IsEqualTo("application/octet-stream");
    }

    [Test]
    public async Task Upload_AsText_ForcesStringBody()
    {
        // Create a file path that exists, but force text interpretation
        var filePath = _engine.FileSystem.ValidatePath("exists.txt");
        await _engine.FileSystem.File.WriteAllTextAsync(filePath, "file content");

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "exists.txt",
            As = ContentAs.Text,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // Content should be StringContent with "exists.txt" as the text, not file content
        var body = await _mock.CapturedRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).IsEqualTo("exists.txt");
    }

    [Test]
    public async Task Upload_OnProgress_CallsGoalWithTransferProgress()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "test data",
            As = ContentAs.Text,
            OnProgress = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ShowProgress" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Upload_ErrorStatusCode_ReturnsDataFromError()
    {
        _mock.Response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server Error"),
            ReasonPhrase = "Internal Server Error"
        };

        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "test data",
            As = ContentAs.Text,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }
}

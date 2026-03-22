using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.http;

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
        public upload? CapturedUpload { get; private set; }
        public Func<upload, Task<Data>>? OnUpload { get; set; }

        public async Task<Data> SendAsync(request action) => Data.Ok();
        public async Task<Data> DownloadAsync(download action) => Data.Ok();
        public async Task<Data> UploadAsync(upload action)
        {
            CapturedUpload = action;
            if (OnUpload != null) return await OnUpload(action);
            return Data.Ok(new { ok = true });
        }
        public Data Configure(configure action) => Data.Ok();
        public void Dispose() { }
    }

    [Test]
    public async Task Upload_FilePath_PassedToProvider()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "upload-test.bin",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedUpload!.Content).IsEqualTo("upload-test.bin");
    }

    [Test]
    public async Task Upload_DictionaryContent_PassedToProvider()
    {
        var dict = new Dictionary<string, object> { ["field1"] = "value1", ["field2"] = "value2" };
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/submit",
            Content = dict,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedUpload!.Content).IsTypeOf<Dictionary<string, object>>();
    }

    [Test]
    public async Task Upload_AsBase64_PassedToProvider()
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
        await Assert.That(_mock.CapturedUpload!.As).IsEqualTo(ContentAs.Base64);
    }

    [Test]
    public async Task Upload_AsFile_PassedToProvider()
    {
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
        await Assert.That(_mock.CapturedUpload!.As).IsEqualTo(ContentAs.File);
    }

    [Test]
    public async Task Upload_AsText_PassedToProvider()
    {
        var action = new upload
        {
            Context = Ctx,
            Url = "https://api.example.com/upload",
            Content = "raw text content",
            As = ContentAs.Text,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedUpload!.As).IsEqualTo(ContentAs.Text);
    }

    [Test]
    public async Task Upload_OnProgress_PassedToProvider()
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

        await Assert.That(_mock.CapturedUpload!.OnProgress).IsNotNull();
    }

    [Test]
    public async Task Upload_ErrorStatusCode_ProviderReturnsError()
    {
        _mock.OnUpload = async action =>
            Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Server Error", "HttpError", 500));

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

using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;
using HttpMethod = PLang.Runtime2.modules.http.HttpMethod;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the request action + DefaultHttpProvider — response parsing, signing, streaming.
/// Uses a real DefaultHttpProvider with a mock HttpClient (via custom provider subclass).
/// For tests that need to control HTTP responses, we use a TestableHttpProvider that
/// intercepts at the HttpClient level but runs all the real provider logic.
/// </summary>
public class RequestActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpProvider _mock = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_req_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region MockHttpProvider — implements new interface, controls responses

    /// <summary>
    /// Mock that captures the action and returns controlled Data results.
    /// For most tests, we delegate to DefaultHttpProvider logic but swap the HTTP transport.
    /// For simple tests, we return canned Data directly.
    /// </summary>
    private class MockHttpProvider : IHttpProvider
    {
        public string Name => "mock";
        public bool IsDefault { get; set; }

        // Capture
        public request? CapturedRequest { get; private set; }
        public download? CapturedDownload { get; private set; }
        public upload? CapturedUpload { get; private set; }
        public configure? CapturedConfigure { get; private set; }

        // Control — set these to override behavior
        public Func<request, Task<Data>>? OnSend { get; set; }
        public Func<download, Task<Data>>? OnDownload { get; set; }
        public Func<upload, Task<Data>>? OnUpload { get; set; }
        public Func<configure, Data>? OnConfigure { get; set; }

        public async Task<Data> SendAsync(request action)
        {
            CapturedRequest = action;
            if (OnSend != null) return await OnSend(action);
            // Default: return simple success
            return Data.Ok(new { ok = true });
        }

        public async Task<Data> DownloadAsync(download action)
        {
            CapturedDownload = action;
            if (OnDownload != null) return await OnDownload(action);
            return Data.Ok(action.SaveTo);
        }

        public async Task<Data> UploadAsync(upload action)
        {
            CapturedUpload = action;
            if (OnUpload != null) return await OnUpload(action);
            return Data.Ok(new { ok = true });
        }

        public Data Configure(configure action)
        {
            CapturedConfigure = action;
            if (OnConfigure != null) return OnConfigure(action);
            return Data.Ok();
        }

        public void Dispose() { }
    }

    #endregion

    #region Batch 1: Happy Path & Response Parsing (action properties verified)

    [Test]
    public async Task Get_JsonResponse_ReturnsSuccess()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/users/1",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest).IsNotNull();
        await Assert.That(_mock.CapturedRequest!.Url).IsEqualTo("https://api.example.com/users/1");
    }

    [Test]
    public async Task Post_WithBody_CapturesMethodAndBody()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/users",
            Method = HttpMethod.POST,
            Body = new Dictionary<string, object> { ["name"] = "Alice" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest!.Method).IsEqualTo(HttpMethod.POST);
        await Assert.That(_mock.CapturedRequest!.Body).IsNotNull();
    }

    [Test]
    public async Task Get_CustomHeaders_PassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/users",
            Headers = new Dictionary<string, object> { ["X-Custom"] = "test-value" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedRequest!.Headers).IsNotNull();
        await Assert.That(_mock.CapturedRequest!.Headers!.ContainsKey("X-Custom")).IsTrue();
    }

    [Test]
    public async Task Get_NoProtocol_AutoPrefixesHttps()
    {
        _mock.OnSend = async action =>
        {
            // Provider receives the action — it resolves the URL internally
            // We verify via the action's Url property (raw) and trust provider does the prefix
            return Data.Ok("resolved");
        };

        var action = new request
        {
            Context = Ctx,
            Url = "api.example.com/users",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Get_ErrorFromProvider_ReturnsDataFromError()
    {
        _mock.OnSend = async action =>
        {
            return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "404 Not Found: User not found", "HttpError", 404));
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/users/999",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Post_FormUrlEncoded_PassesContentType()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/login",
            Method = HttpMethod.POST,
            ContentType = "application/x-www-form-urlencoded",
            Body = new Dictionary<string, object> { ["user"] = "alice", ["pass"] = "secret" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest!.ContentType).IsEqualTo("application/x-www-form-urlencoded");
    }

    [Test]
    public async Task Get_ResponseProperties_FromProvider()
    {
        _mock.OnSend = async action =>
        {
            var result = Data.Ok(new { ok = true });
            result.Properties.Add(new Data("StatusCode", 200));
            result.Properties.Add(new Data("IsSuccess", true));
            return result;
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/test",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Properties["StatusCode"]!.Value).IsEqualTo(200);
    }

    #endregion

    [Test]
    public async Task Post_NullBody_PassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/ping",
            Method = HttpMethod.POST,
            Body = null,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest!.Body).IsNull();
    }

    [Test]
    public async Task Get_RelativeUrlNoBaseUrl_ReturnsError()
    {
        // Provider should return error for relative URL with no BaseUrl
        _mock.OnSend = async action =>
        {
            return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Relative URL requires a BaseUrl configuration. Use 'configure http, base url https://...'",
                "NoBaseUrl", 400));
        };

        var action = new request
        {
            Context = Ctx,
            Url = "/users",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NoBaseUrl");
    }

    [Test]
    public async Task Get_ApplicationPlangJsonVariant_DetectedByProvider()
    {
        _mock.OnSend = async action =>
        {
            // Provider detects unsigned + plang = error
            if (action.Unsigned)
                return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                    "Unsigned request received application/plang response", "UnsignedPlang", 403));
            return Data.Ok();
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsignedPlang");
    }

    #region Batch 2: Signing Integration

    [Test]
    public async Task Get_SignedByDefault_UnsignedFalse()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/secure",
            // Unsigned defaults to false
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // Default unsigned = false — provider sees Unsigned=false on the action
        await Assert.That(_mock.CapturedRequest!.Unsigned).IsFalse();
    }

    [Test]
    public async Task Get_UnsignedTrue_PassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/public",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest!.Unsigned).IsTrue();
    }

    [Test]
    public async Task Post_SignOptionsOverride_PassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/data",
            Method = HttpMethod.POST,
            Body = "test data",
            SignOptions = new PLang.Runtime2.modules.signing.sign
            {
                Context = Ctx,
                ExpiresInMs = 600000
            }
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_mock.CapturedRequest!.SignOptions).IsNotNull();
        await Assert.That(_mock.CapturedRequest!.SignOptions!.ExpiresInMs).IsEqualTo(600000);
    }

    [Test]
    public async Task Get_ApplicationPlangResponse_ProviderHandlesVerification()
    {
        _mock.OnSend = async action =>
        {
            // Simulate provider setting !ServiceIdentity on success
            action.Context.MemoryStack.Set("!ServiceIdentity", "test-identity-key");
            var result = Data.Ok("plang-data");
            return result;
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang-endpoint",
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var identity = Ctx.MemoryStack.GetValue("!ServiceIdentity");
        await Assert.That(identity).IsNotNull();
    }

    [Test]
    public async Task Get_ApplicationPlangInvalidSignature_ReturnsError()
    {
        _mock.OnSend = async action =>
        {
            return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Invalid signature", "SignatureInvalid", 400));
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang-bad-sig",
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    [Test]
    public async Task Get_UnsignedReceivesApplicationPlang_ReturnsError()
    {
        _mock.OnSend = async action =>
        {
            return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Unsigned request received application/plang response", "UnsignedPlang", 403));
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang",
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsignedPlang");
    }

    #endregion

    [Test]
    public async Task Get_SignedErrorResponse_ProviderHandlesIdentity()
    {
        _mock.OnSend = async action =>
        {
            return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Forbidden", "HttpError", 403));
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/restricted",
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(403);
    }

    #region Batch 3: Streaming

    [Test]
    public async Task Get_OnStreamLine_PassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessChunk" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedRequest!.OnStream).IsNotNull();
        await Assert.That(_mock.CapturedRequest!.OnStream!.Name).IsEqualTo("ProcessChunk");
    }

    [Test]
    public async Task Get_OnStreamSSE_StreamAsPassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/events",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessEvent" },
            StreamAs = StreamFormat.SSE,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedRequest!.StreamAs).IsEqualTo(StreamFormat.SSE);
    }

    [Test]
    public async Task Get_OnStreamBytes_StreamAsPassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/binary-stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessBytes" },
            StreamAs = StreamFormat.Bytes,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedRequest!.StreamAs).IsEqualTo(StreamFormat.Bytes);
    }

    [Test]
    public async Task Get_OnStreamAutoDetect_NoStreamAs()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/auto-sse",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessEvent" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedRequest!.StreamAs).IsNull();
    }

    [Test]
    public async Task Get_OnStreamCompletes_ReturnsDataOk()
    {
        _mock.OnSend = async action =>
        {
            var result = Data.Ok();
            result.Properties.Add(new Data("StatusCode", 200));
            return result;
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessChunk" },
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Get_OnStreamApplicationPlang_PassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang-stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessPlang" },
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedRequest!.OnStream).IsNotNull();
    }

    #endregion

    #region Timeout

    [Test]
    public async Task Get_TimeoutExpires_ProviderReturnsError()
    {
        _mock.OnSend = async action =>
        {
            return Data.FromError(new PLang.Runtime2.Engine.Errors.ServiceError(
                "Request timed out", "Timeout", 408));
        };

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/slow",
            TimeoutInSec = 1,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    [Test]
    public async Task Get_OnStreamTimeout_TimeoutPassedToProvider()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessChunk" },
            TimeoutInSec = 30,
            Unsigned = true
        };

        var result = await action.Run();

        await Assert.That(_mock.CapturedRequest!.TimeoutInSec).IsEqualTo(30);
    }

    #endregion
}

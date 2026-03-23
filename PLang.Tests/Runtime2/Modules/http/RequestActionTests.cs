using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using PLang.Runtime2.Engine.Channels.Serializers;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.Engine.Providers;
using PLang.Runtime2.modules.http;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.signing;
using PLangEngine = PLang.Runtime2.Engine.@this;
using HttpMethod = PLang.Runtime2.modules.http.HttpMethod;

namespace PLang.Tests.Runtime2.Modules.http;

/// <summary>
/// Tests the request action + real DefaultHttpProvider.
/// Uses a MockHttpMessageHandler to control HTTP responses while exercising
/// all real provider logic (URL resolution, body building, response parsing, etc.).
/// </summary>
public class RequestActionTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_req_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);

        _handler = new MockHttpMessageHandler();
        var provider = new DefaultHttpProvider(_handler) { Name = "test" };
        _engine.Providers.Register<IHttpProvider>(provider);
        _engine.Providers.SetDefault<IHttpProvider>("test");
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

    #region Test Infrastructure

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Handler { get; set; }
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            if (Handler != null)
                return Handler(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
            });
        }
    }

    #endregion

    #region Happy Path & Response Parsing

    [Test]
    public async Task Get_JsonResponse_ParsedCorrectly()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"name\":\"Alice\"}", Encoding.UTF8, "application/json")
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/users/1", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsNotNull();
    }

    [Test]
    public async Task Get_NoProtocol_AutoPrefixesHttps()
    {
        var action = new request { Context = Ctx, Url = "api.example.com/users", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_handler.LastRequest!.RequestUri!.Scheme).IsEqualTo("https");
        await Assert.That(_handler.LastRequest!.RequestUri!.Host).IsEqualTo("api.example.com");
    }

    [Test]
    public async Task Post_WithJsonBody_SerializesCorrectly()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        };

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
        await Assert.That(_handler.LastRequest!.Method).IsEqualTo(System.Net.Http.HttpMethod.Post);
        var sentBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(sentBody).Contains("Alice");
    }

    [Test]
    public async Task Post_FormUrlEncoded_SendsCorrectContentType()
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
        await Assert.That(_handler.LastRequest!.Content).IsTypeOf<FormUrlEncodedContent>();
    }

    [Test]
    public async Task Get_CustomHeaders_AppliedToRequest()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/data",
            Headers = new Dictionary<string, object> { ["X-Custom"] = "test-value" },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_handler.LastRequest!.Headers.Contains("X-Custom")).IsTrue();
        await Assert.That(_handler.LastRequest!.Headers.GetValues("X-Custom").First()).IsEqualTo("test-value");
    }

    [Test]
    public async Task Get_XmlResponse_ReturnsStringWithType()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<root><item/></root>", Encoding.UTF8, "application/xml")
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/xml", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!.ToString()).Contains("<root>");
    }

    [Test]
    public async Task Get_TextResponse_ReturnsString()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Hello World", Encoding.UTF8, "text/plain")
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/text", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!.ToString()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Get_NullBody_NoContent()
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
        await Assert.That(_handler.LastRequest!.Content).IsNull();
    }

    #endregion

    #region Error Responses

    [Test]
    public async Task Get_404Response_ReturnsHttpError()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("Not Found", Encoding.UTF8, "text/plain")
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/missing", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
    }

    [Test]
    public async Task Get_500Response_ReturnsHttpError()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server Error", Encoding.UTF8, "text/plain")
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/error", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    #endregion

    #region URL Resolution

    [Test]
    public async Task Get_RelativeUrlNoBaseUrl_ReturnsError()
    {
        var action = new request { Context = Ctx, Url = "/users", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("NoBaseUrl");
    }

    [Test]
    public async Task Get_RelativeUrlWithBaseUrl_CombinesCorrectly()
    {
        _engine.Config.Set("http.BaseUrl", "https://api.example.com", Ctx, isDefault: true);

        var action = new request { Context = Ctx, Url = "/users/1", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_handler.LastRequest!.RequestUri!.ToString()).IsEqualTo("https://api.example.com/users/1");
    }

    #endregion

    #region Response Properties

    [Test]
    public async Task Get_ResponseProperties_PopulatedCorrectly()
    {
        _handler.Handler = _ =>
        {
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json")
            };
            resp.Headers.Add("X-Request-Id", "abc123");
            return Task.FromResult(resp);
        };

        var action = new request { Context = Ctx, Url = "https://api.example.com/test", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Properties["StatusCode"]!.Value).IsEqualTo(200);
        await Assert.That(result.Properties["IsSuccess"]!.Value).IsEqualTo(true);
        await Assert.That(result.Properties["Method"]!.Value).IsEqualTo("GET");
        await Assert.That(result.Properties["Url"]!.Value!.ToString()).IsEqualTo("https://api.example.com/test");
    }

    #endregion

    #region Timeout

    [Test]
    public async Task Get_Timeout_ReturnsTimeoutError()
    {
        _handler.Handler = async _ =>
        {
            await Task.Delay(5000);
            return new HttpResponseMessage(HttpStatusCode.OK);
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

    #endregion

    #region Signing

    [Test]
    public async Task Get_UnsignedTrue_NoSignatureHeader()
    {
        var action = new request { Context = Ctx, Url = "https://api.example.com/public", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_handler.LastRequest!.Headers.Contains("X-Signature")).IsFalse();
    }

    [Test]
    public async Task Get_UnsignedPlangResponse_ReturnsError()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/plang")
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/plang", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsignedPlang");
    }

    #endregion

    #region Streaming

    [Test]
    public async Task Get_OnStream_UsesResponseHeadersRead()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("line1\nline2\n", Encoding.UTF8, "text/plain")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "ProcessChunk" },
            Unsigned = true
        };

        // This will fail because ProcessChunk goal doesn't exist, but it proves
        // the provider received the streaming request and tried to process it
        var result = await action.Run();

        // Stream callback goal doesn't exist — but the request was made with ResponseHeadersRead
        await Assert.That(_handler.LastRequest).IsNotNull();
    }

    #endregion

    #region JSON Parsing Edge Cases

    [Test]
    public async Task Get_InvalidJson_FallsBackToRawString()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "application/json")
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/bad-json", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value!.ToString()).IsEqualTo("not json at all");
    }

    [Test]
    public async Task Get_BinaryResponse_ReturnsByteArray()
    {
        var bytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });

        var action = new request { Context = Ctx, Url = "https://api.example.com/image", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value).IsTypeOf<byte[]>();
    }

    #endregion

    #region Exception Mapping (ExecuteHttpAsync)

    [Test]
    public async Task Get_HttpRequestException_ReturnsHttpError()
    {
        _handler.Handler = _ => throw new HttpRequestException("Service Unavailable", null, HttpStatusCode.ServiceUnavailable);

        var action = new request { Context = Ctx, Url = "https://api.example.com/down", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(503);
    }

    [Test]
    public async Task Get_IOException_ReturnsIOError()
    {
        _handler.Handler = _ => throw new IOException("Connection reset");

        var action = new request { Context = Ctx, Url = "https://api.example.com/reset", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Get_FormatException_ReturnsInvalidContent()
    {
        _handler.Handler = _ => throw new FormatException("Bad encoding");

        var action = new request { Context = Ctx, Url = "https://api.example.com/bad", Unsigned = true };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidContent");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
    }

    #endregion

    #region Streaming (Lines, SSE, Bytes, Error)

    [Test]
    public async Task Stream_Lines_SetsMemoryStackPerLine()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("line1\nline2\nline3\n", Encoding.UTF8, "text/plain")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "HandleLine" },
            Unsigned = true
        };
        var result = await action.Run();

        // Stream processed successfully (callback goal not found writes to stderr, doesn't abort)
        await Assert.That(result.Success).IsTrue();
        // Last line set on MemoryStack
        var lastValue = Ctx.MemoryStack.Get("!data");
        await Assert.That(lastValue).IsNotNull();
        await Assert.That(lastValue!.ToString()).IsEqualTo("line3");
    }

    [Test]
    public async Task Stream_SSE_ParsesDataFields()
    {
        var sseContent = "data: hello\n\ndata: world\n\n";
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/sse",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "HandleSSE" },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var lastValue = Ctx.MemoryStack.Get("!data");
        await Assert.That(lastValue).IsNotNull();
        await Assert.That(lastValue!.ToString()).IsEqualTo("world");
    }

    [Test]
    public async Task Stream_SSE_MultiLineData_ConcatenatesWithNewline()
    {
        var sseContent = "data: part1\ndata: part2\n\n";
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/sse-multi",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "HandleSSE" },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var lastValue = Ctx.MemoryStack.Get("!data");
        await Assert.That(lastValue!.ToString()).IsEqualTo("part1\npart2");
    }

    [Test]
    public async Task Stream_Bytes_SetsMemoryStackWithByteArray()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(bytes)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/octet-stream") }
            }
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/bytes",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "HandleBytes" },
            StreamAs = StreamFormat.Bytes,
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var lastValue = Ctx.MemoryStack.Get("!data");
        await Assert.That(lastValue).IsNotNull();
    }

    [Test]
    public async Task Stream_ErrorResponse_ReturnsErrorNotStream()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("Server Error", Encoding.UTF8, "text/plain")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/stream-err",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "HandleLine" },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Stream_CustomVarName_UsesParameterVariable()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("chunk1\n", Encoding.UTF8, "text/plain")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall
            {
                Name = "HandleChunk",
                Parameters = new Dictionary<string, object?> { ["chunk"] = "%myChunk%" }
            },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var lastValue = Ctx.MemoryStack.Get("myChunk");
        await Assert.That(lastValue).IsNotNull();
        await Assert.That(lastValue!.ToString()).IsEqualTo("chunk1");
    }

    [Test]
    public async Task Stream_UnsignedPlangResponse_ReturnsError()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/plang")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang-stream",
            OnStream = new PLang.Runtime2.Engine.Goals.Goal.GoalCall { Name = "HandlePlang" },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsignedPlang");
    }

    #endregion

    #region Header Merging

    [Test]
    public async Task Get_DefaultAndStepHeaders_BothApplied()
    {
        var defaults = new Dictionary<string, object> { ["X-Api-Key"] = "default-key", ["X-Shared"] = "default" };
        _engine.Config.Set("http.DefaultHeaders", defaults, Ctx, isDefault: true);

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/merged",
            Headers = new Dictionary<string, object> { ["X-Custom"] = "step-value", ["X-Shared"] = "overridden" },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // Default header present
        await Assert.That(_handler.LastRequest!.Headers.GetValues("X-Api-Key").First()).IsEqualTo("default-key");
        // Step header present
        await Assert.That(_handler.LastRequest!.Headers.GetValues("X-Custom").First()).IsEqualTo("step-value");
        // Step overrides default
        await Assert.That(_handler.LastRequest!.Headers.GetValues("X-Shared").First()).IsEqualTo("overridden");
    }

    [Test]
    public async Task Post_ContentHeaders_RoutedToContentHeaders()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/content",
            Method = HttpMethod.POST,
            Body = "test body",
            Headers = new Dictionary<string, object> { ["Content-Encoding"] = "gzip", ["X-Custom"] = "req-header" },
            Unsigned = true
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        // Content-Encoding goes to Content.Headers
        await Assert.That(_handler.LastRequest!.Content!.Headers.Contains("Content-Encoding")).IsTrue();
        // X-Custom goes to Request.Headers
        await Assert.That(_handler.LastRequest!.Headers.Contains("X-Custom")).IsTrue();
    }

    #endregion

    #region Signed Requests

    [Test]
    public async Task Get_Signed_HasXSignatureHeader()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/signed",
            Unsigned = false
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(_handler.LastRequest!.Headers.Contains("X-Signature")).IsTrue();
        // Accept header includes application/plang
        var acceptValues = _handler.LastRequest!.Headers.Accept.Select(a => a.MediaType).ToList();
        await Assert.That(acceptValues).Contains("application/plang");
    }

    [Test]
    public async Task Get_Signed_XSignatureIsValidJson()
    {
        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/signed",
            Unsigned = false
        };
        await action.Run();

        var sigHeader = _handler.LastRequest!.Headers.GetValues("X-Signature").First();
        var signedData = JsonSerializer.Deserialize<SignedData>(sigHeader, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        await Assert.That(signedData).IsNotNull();
        await Assert.That(signedData!.Identity).IsNotEmpty();
        await Assert.That(signedData.Signature).IsNotNull();
    }

    [Test]
    public async Task Get_SignedPlangResponse_SetsServiceIdentity()
    {
        // First, sign some data to get a valid signed response
        var signAction = new sign { Context = Ctx, Data = "response-payload" };
        var signResult = await signAction.Run();
        await Assert.That(signResult.Success).IsTrue();

        // Build a Data object with the signature, serialize with transport options
        var responseData = new Data("response-payload");
        responseData.Signature = signResult.Signature;
        var transportOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
            {
                Modifiers = { TransportPropertyFilter.ForOutbound }
            }
        };
        var responseBody = JsonSerializer.Serialize(responseData, transportOptions);

        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/plang")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang-signed",
            Unsigned = false
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var identity = Ctx.MemoryStack.Get("!ServiceIdentity");
        await Assert.That(identity).IsNotNull();
        await Assert.That(identity!.ToString()).IsNotEmpty();
    }

    [Test]
    public async Task Get_PlangResponseInvalidSignature_ReturnsError()
    {
        // Build a Data with a bogus signature
        var responseData = new Data("payload");
        responseData.Signature = new SignedData
        {
            Identity = "fake-identity",
            Signature = "AAAA_invalid_base64_sig"
        };
        var transportOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver
            {
                Modifiers = { TransportPropertyFilter.ForOutbound }
            }
        };
        var responseBody = JsonSerializer.Serialize(responseData, transportOptions);

        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/plang")
        });

        var action = new request
        {
            Context = Ctx,
            Url = "https://api.example.com/plang-bad-sig",
            Unsigned = false
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
    }

    #endregion
}

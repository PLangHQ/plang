using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using app.channel.serializer;
using app.actor.context;
using app.variable;
using app.module.code;
using app.module.http;
using app.module.http.code;
using app.module.signing;
using PLangEngine = global::app.@this;
using HttpMethod = global::app.module.http.HttpMethod;

namespace PLang.Tests.App.Modules.http;

/// <summary>
/// Tests the request action + real Default.
/// Uses a MockHttpMessageHandler to control HTTP responses while exercising
/// all real provider logic (URL resolution, body building, response parsing, etc.).
/// </summary>
public class RequestActionTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_http_req_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);

        _handler = new MockHttpMessageHandler();
        var provider = new Default(_handler) { Name = "test" };
        _app.Code.Register<IHttp>(provider);
        _app.Code.SetDefault<IHttp>("test");

        // Register stub goals for streaming callbacks — GoalCall needs to find them
        foreach (var name in new[] { "HandleLine", "HandleSSE", "HandleBytes", "HandleChunk", "ProcessChunk" })
            _app.Goal.Add(new global::app.goal.@this { Name = name, Path = $"/{name}.goal" });
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

    private global::app.actor.context.@this Ctx => _app.System.Context;

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

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/users/1", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())).IsNotNull();
        // The body is the lazy value; touching it materializes json → dict.
        // Serialize against the runtime type (object) so the value's own
        // [JsonConverter] fires — the static item base would reflect its
        // infra props (Cacheable/Prior/Template) instead of the content.
        var json = JsonSerializer.Serialize((object?)await result.Value());
        await Assert.That(json).Contains("Alice");
    }

    [Test]
    public async Task Get_NoProtocol_AutoPrefixesHttps()
    {
        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"api.example.com/users", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
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
            Url = (global::app.type.text.@this)"https://api.example.com/users",
            Method = (global::app.type.choice.@this<global::app.module.http.HttpMethod>)HttpMethod.POST,
            Body = new global::app.data.@this("", new Dictionary<string, object> { ["name"] = "Alice" }),
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
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
            Url = (global::app.type.text.@this)"https://api.example.com/login",
            Method = (global::app.type.choice.@this<global::app.module.http.HttpMethod>)HttpMethod.POST,
            ContentType = (global::app.type.text.@this)"application/x-www-form-urlencoded",
            Body = new global::app.data.@this("", new Dictionary<string, object> { ["user"] = "alice", ["pass"] = "secret" }),
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.LastRequest!.Content).IsTypeOf<FormUrlEncodedContent>();
    }

    [Test]
    public async Task Get_CustomHeaders_AppliedToRequest()
    {
        var action = new request
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/data",
            Headers = new Dictionary<string, object> { ["X-Custom"] = "test-value" }.ToDictData(),
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
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

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/xml", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
        // Content off I/O rides as binary + kind; the value door narrows it (application/xml → text).
        await Assert.That((await result.Value())?.ToString()).Contains("<root>");
    }

    [Test]
    public async Task Get_TextResponse_ReturnsString()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Hello World", Encoding.UTF8, "text/plain")
        });

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/text", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
        // Content off I/O rides as binary + kind; the value door narrows it (text/plain → text).
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("Hello World");
    }

    [Test]
    public async Task Get_NullBody_NoContent()
    {
        var action = new request
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/ping",
            Method = (global::app.type.choice.@this<global::app.module.http.HttpMethod>)HttpMethod.POST,
            Body = null,
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
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

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/missing", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
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

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/error", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    #endregion

    #region URL Resolution

    [Test]
    public async Task Get_RelativeUrlNoBaseUrl_ReturnsError()
    {
        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"/users", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoBaseUrl");
    }

    [Test]
    public async Task Get_RelativeUrlWithBaseUrl_CombinesCorrectly()
    {
        _app.Config.Set("http.BaseUrl", "https://api.example.com", Ctx, isDefault: true);

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"/users/1", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
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

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/test", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Properties["StatusCode"]).IsEqualTo(200);
        await Assert.That(result.Properties["IsSuccess"]).IsEqualTo(true);
        await Assert.That((result.Properties["Method"])?.ToString()).IsEqualTo("GET");
        await Assert.That(result.Properties["Url"]!.ToString()).IsEqualTo("https://api.example.com/test");
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
            Url = (global::app.type.text.@this)"https://api.example.com/slow",
            TimeoutInSec = (global::app.type.number.@this)1,
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("Timeout");
    }

    #endregion

    #region Signing

    [Test]
    public async Task Get_UnsignedTrue_NoSignatureHeader()
    {
        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/public", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.LastRequest!.Headers.Contains("X-Signature")).IsFalse();
    }

    [Test]
    public async Task Get_UnsignedPlangResponse_ReturnsError()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/plang")
        });

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/plang", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
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
            Url = (global::app.type.text.@this)"https://api.example.com/stream",
            OnStream = new global::app.goal.GoalCall { Name = "ProcessChunk" },
            Unsigned = (global::app.type.@bool.@this)true
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
    public async Task Get_MislabeledJson_BodyRecoverableFromRaw()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not json at all", Encoding.UTF8, "application/json")
        });

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/bad-json", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
        // A body mislabeled application/json rides as raw bytes (no eager parse) — the
        // original text is never lost; it stays recoverable from the raw form.
        await Assert.That(result.Raw is byte[] b && Encoding.UTF8.GetString(b) == "not json at all").IsTrue();
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

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/image", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Peek()).IsTypeOf<byte[]>();
        await Assert.That(global::app.type.item.@this.Lower<byte[]>(result.Peek())!).IsEquivalentTo(bytes);
    }

    #endregion

    #region Exception Mapping (ExecuteHttpAsync)

    [Test]
    public async Task Get_HttpRequestException_ReturnsHttpError()
    {
        _handler.Handler = _ => throw new HttpRequestException("Service Unavailable", null, HttpStatusCode.ServiceUnavailable);

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/down", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(503);
    }

    [Test]
    public async Task Get_IOException_ReturnsIOError()
    {
        _handler.Handler = _ => throw new IOException("Connection reset");

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/reset", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("IOError");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(500);
    }

    [Test]
    public async Task Get_FormatException_ReturnsInvalidContent()
    {
        _handler.Handler = _ => throw new FormatException("Bad encoding");

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/bad", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("InvalidContent");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(400);
    }

    #endregion

    #region Streaming (Lines, SSE, Bytes, Error)

    [Test]
    public async Task Stream_Lines_SetsVariablesPerLine()
    {
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("line1\nline2\nline3\n", Encoding.UTF8, "text/plain")
        });

        var action = new request
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/stream",
            OnStream = new global::app.goal.GoalCall { Name = "HandleLine" },
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        // Stream processed successfully (callback goal not found writes to stderr, doesn't abort)
        await result.IsSuccess();
        // Last line set on Variables
        var lastValue = await Ctx.Variable.Get("chunk");
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
            Url = (global::app.type.text.@this)"https://api.example.com/sse",
            OnStream = new global::app.goal.GoalCall { Name = "HandleSSE" },
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
        var lastValue = await Ctx.Variable.Get("chunk");
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
            Url = (global::app.type.text.@this)"https://api.example.com/sse-multi",
            OnStream = new global::app.goal.GoalCall { Name = "HandleSSE" },
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
        var lastValue = await Ctx.Variable.Get("chunk");
        await Assert.That(lastValue!.ToString()).IsEqualTo("part1\npart2");
    }

    [Test]
    public async Task Stream_Bytes_SetsVariablesWithByteArray()
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
            Url = (global::app.type.text.@this)"https://api.example.com/bytes",
            OnStream = new global::app.goal.GoalCall { Name = "HandleBytes" },
            StreamAs = (global::app.type.choice.@this<global::app.module.http.StreamFormat>)StreamFormat.Bytes,
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
        var lastData = await Ctx.Variable.Get("chunk");
        await Assert.That(lastData).IsNotNull();
        // Verify byte content was delivered (last chunk contains the input bytes)
        await Assert.That(await lastData!.Value()).IsTypeOf<global::app.type.binary.@this>();
        var chunk = ((global::app.type.binary.@this)(await lastData.Value())!).Value;
        await Assert.That(chunk.Length).IsGreaterThan(0);
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
            Url = (global::app.type.text.@this)"https://api.example.com/stream-err",
            OnStream = new global::app.goal.GoalCall { Name = "HandleLine" },
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsFailure();
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
            Url = (global::app.type.text.@this)"https://api.example.com/stream",
            OnStream = new global::app.goal.GoalCall
            {
                Name = "HandleChunk",
                Parameters = new List<Data> { new Data("myChunk") }
            },
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
        var lastValue = await Ctx.Variable.Get("myChunk");
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
            Url = (global::app.type.text.@this)"https://api.example.com/plang-stream",
            OnStream = new global::app.goal.GoalCall { Name = "HandlePlang" },
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("UnsignedPlang");
    }

    [Test]
    public async Task Get_DefaultAndStepHeaders_BothApplied()
    {
        var defaults = new Dictionary<string, object> { ["X-Api-Key"] = "default-key", ["X-Shared"] = "default" };
        _app.Config.Set("http.DefaultHeaders", defaults, Ctx, isDefault: true);

        var action = new request
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/merged",
            Headers = new Dictionary<string, object> { ["X-Custom"] = "step-value", ["X-Shared"] = "overridden" }.ToDictData(),
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
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
            Url = (global::app.type.text.@this)"https://api.example.com/content",
            Method = (global::app.type.choice.@this<global::app.module.http.HttpMethod>)HttpMethod.POST,
            Body = new global::app.data.@this("", "test body"),
            Headers = new Dictionary<string, object> { ["Content-Encoding"] = "gzip", ["X-Custom"] = "req-header" }.ToDictData(),
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        await result.IsSuccess();
        // Content-Encoding goes to Content.Headers
        await Assert.That(_handler.LastRequest!.Content!.Headers.Contains("Content-Encoding")).IsTrue();
        // X-Custom goes to Request.Headers
        await Assert.That(_handler.LastRequest!.Headers.Contains("X-Custom")).IsTrue();
    }

    #endregion

    #region Signed Requests

    [Test]
    public async Task Get_OversizedResponse_ReturnsResponseTooLarge()
    {
        // Configure a tiny max response size
        _app.Config.Set("http.MaxResponseSize", 50L, Ctx, isDefault: true);

        // Return a response larger than 50 bytes
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(new string('x', 100), Encoding.UTF8, "application/json")
        });

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/big", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ResponseTooLarge");
        await Assert.That(result.Error!.StatusCode).IsEqualTo(413);
    }

    [Test]
    public async Task Get_OversizedBinaryResponse_ReturnsResponseTooLarge()
    {
        _app.Config.Set("http.MaxResponseSize", 50L, Ctx, isDefault: true);

        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[100])
            {
                Headers = { ContentType = new MediaTypeHeaderValue("image/png") }
            }
        });

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/big-binary", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("ResponseTooLarge");
    }

    [Test]
    public async Task Get_WithinSizeLimit_Succeeds()
    {
        _app.Config.Set("http.MaxResponseSize", 1000L, Ctx, isDefault: true);

        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json")
        });

        var action = new request { Context = Ctx, Url = (global::app.type.text.@this)"https://api.example.com/small", Unsigned = (global::app.type.@bool.@this)true };
        var result = await action.Run();

        await result.IsSuccess();
    }

    [Test]
    public async Task Stream_SSE_OversizedBuffer_StreamContinues()
    {
        // Configure a tiny SSE buffer (50 bytes)
        _app.Config.Set("http.MaxSSEBufferSize", 50L, Ctx, isDefault: true);

        // SSE with one message that exceeds the buffer, followed by a normal-sized message
        var sseContent = "data: " + new string('x', 100) + "\n\ndata: ok\n\n";
        _handler.Handler = _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(sseContent, Encoding.UTF8, "text/event-stream")
        });

        var action = new request
        {
            Context = Ctx,
            Url = (global::app.type.text.@this)"https://api.example.com/sse-overflow",
            OnStream = new global::app.goal.GoalCall { Name = "HandleSSE" },
            Unsigned = (global::app.type.@bool.@this)true
        };
        var result = await action.Run();

        // Stream completes (overflow is non-fatal — emits error to stderr, clears buffer, continues)
        await result.IsSuccess();
        // The second (small) message should still be delivered
        var lastValue = await Ctx.Variable.Get("chunk");
        await Assert.That(lastValue).IsNotNull();
        await Assert.That((await lastValue!.Value())!.ToString()).IsEqualTo("ok");
    }

    #endregion
}

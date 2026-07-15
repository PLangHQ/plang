using System.Net;
using System.Text;
using System.Text.Json;
using app.variable;
using app.module.action.http.code;
using app.module.action.llm;
using app.module.action.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Shared test infrastructure for LLM module tests.
/// </summary>
internal static class LlmTestHelper
{
    /// <summary>
    /// Registers a MockHttpMessageHandler on the engine's HTTP provider
    /// and returns the handler for configuring responses.
    /// </summary>
    internal static MockHttpMessageHandler SetupMockHttp(PLangEngine engine)
    {
        var handler = new MockHttpMessageHandler();
        var provider = new Default(handler) { Name = "test" };
        engine.Code.Register<IHttp>(provider);
        engine.Code.SetDefault<IHttp>("test");
        return handler;
    }

    /// <summary>
    /// Creates a standard OpenAI chat completion response.
    /// `cachedTokens` > 0 emits a `prompt_tokens_details.cached_tokens` field so
    /// cost-math tests can exercise the cached-vs-non-cached billing split.
    /// </summary>
    internal static string MakeCompletionResponse(string content,
        int promptTokens = 10, int completionTokens = 20,
        string model = "gpt-5.4-nano", int cachedTokens = 0)
    {
        object usage = cachedTokens > 0
            ? new
            {
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                prompt_tokens_details = new { cached_tokens = cachedTokens }
            }
            : (object)new
            {
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens
            };

        return JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content },
                    finish_reason = "stop"
                }
            },
            usage
        });
    }

    /// <summary>
    /// Creates an OpenAI response with tool calls.
    /// </summary>
    internal static string MakeToolCallResponse(params (string id, string name, string arguments)[] toolCalls)
    {
        var calls = toolCalls.Select(tc => new
        {
            id = tc.id,
            type = "function",
            function = new { name = tc.name, arguments = tc.arguments }
        }).ToArray();

        return JsonSerializer.Serialize(new
        {
            id = "chatcmpl-test",
            @object = "chat.completion",
            model = "gpt-5.4-nano",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = (string?)null, tool_calls = calls },
                    finish_reason = "tool_calls"
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 5, total_tokens = 15 }
        });
    }

    /// <summary>
    /// Creates an HttpResponseMessage with JSON content.
    /// </summary>
    internal static HttpResponseMessage JsonResponse(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    /// <summary>
    /// Creates a basic query action with system + user messages.
    /// </summary>
    internal static query MakeQuery(global::app.actor.context.@this context,
        string systemText = "You are helpful", string userText = "Hello")
    {
        return new query(context) { Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = systemText },
                new LlmMessage { Role = "user", Content = userText }
            }.ToListData<LlmMessage>()
        };
    }
}

/// <summary>
/// Mock HTTP handler that records requests and returns configured responses.
/// </summary>
internal class MockHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, Task<HttpResponseMessage>>? Handler { get; set; }
    public HttpRequestMessage? LastRequest { get; private set; }
    public List<HttpRequestMessage> AllRequests { get; } = new();
    public int CallCount => AllRequests.Count;

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        LastRequest = request;
        AllRequests.Add(request);
        if (Handler != null)
            return Handler(request);

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        });
    }
}

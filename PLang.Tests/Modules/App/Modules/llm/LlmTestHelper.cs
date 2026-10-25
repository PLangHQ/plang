using System.Net;
using System.Text;
using System.Text.Json;
using app.type.item.variable;
using app.module.http.code;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Shared test infrastructure for LLM module tests.
/// </summary>
internal static class LlmTestHelper
{
    /// <summary>The key a mocked app's queries are sent with: a mocked test never needs a real key, and a request that
    /// misses the mock can never spend one.</summary>
    internal const string Key = "sk-test-mock";

    /// <summary>
    /// Registers a MockHttpMessageHandler on the engine's HTTP provider (<see cref="Mock"/>) and returns the handler
    /// for configuring responses.
    /// </summary>
    internal static MockHttpMessageHandler SetupMockHttp(PLangEngine engine) => Mock(engine, new MockHttpMessageHandler());

    /// <summary>The engine's HTTP answered by <paramref name="handler"/>, and the engine given the test key (on the
    /// system actor, so a test's own key on the user still wins).</summary>
    internal static T Mock<T>(PLangEngine engine, T handler) where T : HttpMessageHandler
    {
        engine.Code.Register<IHttp>(new Default(handler) { Name = "test" });
        engine.Code.SetDefault<IHttp>("test");
        var setting = engine.actor.list.System.Setting;
        setting.Set(setting.Address(typeof(global::app.module.llm.setting.@this), "key"), Key);
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
        return new query(context) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = systemText },
                new LlmMessage { Role = "user", Content = userText }
            }.ToListData<LlmMessage>(context)
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

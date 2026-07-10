using System.Net;
using System.Text;
using System.Text.Json;
using app.actor.context;
using app.goal;
using app.variable;
using app.module.http.code;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Integration tests that run the full LLM module pipeline against OpenAI.
/// First run: real API call → saves snapshot.
/// Subsequent runs: replays snapshot (only HTTP transport mocked, full LLM pipeline runs).
/// Snapshot invalidates when input or class structure changes.
/// </summary>
public class LlmIntegrationTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_integ_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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

    // --- Test 1: Simple arithmetic ---

    [Test]
    public async Task Query_SimpleCalculation()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = "You are a calculator. Respond with ONLY the number, nothing else." },
            new LlmMessage { Role = "user", Content = "What is 7 * 6?" }
        };

        var result = await RunWithSnapshot("SimpleCalculation", messages, new query(Ctx) { Messages = messages.ToListData<LlmMessage>(),
            Temperature = (global::app.type.number.@this)0.0,
            MaxTokens = (global::app.type.number.@this)50,
            Cache = (global::app.type.item.@bool.@this)false
        });
        if (result == null) return; // skipped, no API key

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString() ?? "").Contains("42");
        await Assert.That((await result.Properties.Value("TotalTokens"))).IsNotNull();
    }

    // --- Test 2: JSON schema response ---

    [Test]
    public async Task Query_JsonSchema_ParsedCorrectly()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = "Analyze the sentiment of the text." },
            new LlmMessage { Role = "user", Content = "I absolutely love sunny days at the beach!" }
        };

        var result = await RunWithSnapshot("JsonSchema", messages, new query(Ctx) { Messages = messages.ToListData<LlmMessage>(),
            Schema = Ctx.Ok("{\"sentiment\": \"string\", \"score\": \"number\"}"),
            Temperature = (global::app.type.number.@this)0.0,
            MaxTokens = (global::app.type.number.@this)100,
            Cache = (global::app.type.item.@bool.@this)false
        });
        if (result == null) return;

        await result.IsSuccess();
        // Value should be parsed JSON (JsonElement)
        await Assert.That((await result.Value())).IsNotNull();
        var __low = global::app.type.item.@this.Lower<object>(await result.Value());
        var json = __low is JsonElement je ? je : JsonSerializer.SerializeToElement<object?>(__low is global::app.type.dict.@this _nd ? _nd.Clr<object>() : (await result.Value()));
        await Assert.That(json.TryGetProperty("sentiment", out _)).IsTrue();
        await Assert.That((await result.Properties.Value("Format"))?.ToString()).IsEqualTo("json");
    }

    // --- Test 3: Code format extraction ---

    [Test]
    public async Task Query_PythonFormat_ExtractedFromCodeBlock()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = "Write the requested code." },
            new LlmMessage { Role = "user", Content = "Write a Python function that returns the sum of two numbers." }
        };

        var result = await RunWithSnapshot("PythonFormat", messages, new query(Ctx) { Messages = messages.ToListData<LlmMessage>(),
            Format = (global::app.type.item.text.@this)"python",
            Temperature = (global::app.type.number.@this)0.0,
            MaxTokens = (global::app.type.number.@this)200,
            Cache = (global::app.type.item.@bool.@this)false
        });
        if (result == null) return;

        await result.IsSuccess();
        var code = (await result.Value())?.ToString() ?? "";
        await Assert.That(code).Contains("def ");
        await Assert.That(code).Contains("return");
        // Should NOT contain the ```python wrapper — extracted
        await Assert.That(code).DoesNotContain("```");
    }

    // --- Test 4: Conversation continuity ---

    [Test]
    public async Task Query_ConversationContinuity_RemembersPreviousContext()
    {
        var messages1 = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = "You are a helpful assistant. Be very brief." },
            new LlmMessage { Role = "user", Content = "My name is Alice." }
        };

        var result1 = await RunWithSnapshot("ConvPart1", messages1, new query(Ctx) { Messages = messages1.ToListData<LlmMessage>(),
            Temperature = (global::app.type.number.@this)0.0,
            MaxTokens = (global::app.type.number.@this)50,
            Cache = (global::app.type.item.@bool.@this)false
        });
        if (result1 == null) return;
        await result1.IsSuccess();

        // Second query continues the conversation
        var messages2 = new List<LlmMessage>
        {
            new LlmMessage { Role = "user", Content = "What is my name?" }
        };

        var result2 = await RunWithSnapshot("ConvPart2", messages2, new query(Ctx) { Messages = messages2.ToListData<LlmMessage>(),
            ContinuePreviousConversation = (global::app.type.item.@bool.@this)true,
            Temperature = (global::app.type.number.@this)0.0,
            MaxTokens = (global::app.type.number.@this)50,
            Cache = (global::app.type.item.@bool.@this)false
        });
        if (result2 == null) return;

        await result2.IsSuccess();
        await Assert.That((await result2.Value())?.ToString() ?? "").Contains("Alice");
    }

    // --- Test 5: Tool calling ---

    [Test]
    public async Task Query_ToolCall_LlmRequestsToolAndHandlesError()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = "You have tools available. Use them when appropriate. Be brief." },
            new LlmMessage { Role = "user", Content = "What is the weather in London right now?" }
        };

        var tools = new List<GoalCall>
        {
            new GoalCall
            {
                Name = "GetWeather",
                Parameters = new List<Data>
                {
                    new Data("city", null, global::app.type.@this.String, context: Ctx)
                }
            }
        };

        // For tool calls, we use multi-snapshot (multiple HTTP round trips)
        var result = await RunToolCallWithSnapshot("ToolCallWeather", messages, tools);
        if (result == null) return;

        await result.IsSuccess();
        // The LLM should have attempted the tool, got an error (goal doesn't exist),
        // and then responded with something about not being able to get the weather
        await Assert.That((await result.Value())?.ToString() ?? "").IsNotEmpty();
        var toolCallCount = (await result.Properties.Value("ToolCallCount"));
        await Assert.That(toolCallCount).IsNotNull();
    }

    // --- Snapshot infrastructure ---

    /// <summary>
    /// Runs a query with single-turn snapshot support.
    /// Live: uses default providers (real HTTP). Replay: mocks HTTP transport.
    /// Returns null if no API key and no snapshot (test skipped).
    /// </summary>
    private async Task<Data?> RunWithSnapshot(string testName, List<LlmMessage> messages, query action)
    {
        var snapshot = LlmSnapshotHelper.TryLoadSnapshot(testName, messages);

        if (snapshot != null)
        {
            var handler = new SnapshotReplayHandler(new List<string> { snapshot });
            var httpProvider = new Default(handler) { Name = "snapshot" };
            _app.Code.Register<IHttp>(httpProvider);
            _app.Code.SetDefault<IHttp>("snapshot");
        }
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return null; // skip
        }

        await action.Attach(null, Ctx);
        var result = await action.Run();

        if (snapshot == null && result.Success)
        {
            var json = await BuildSnapshotFromResult(result);
            if (json != null)
                LlmSnapshotHelper.SaveSnapshot(testName, messages, json);
        }

        return result;
    }

    /// <summary>
    /// Runs a tool-call query with multi-turn snapshot support.
    /// The HTTP handler captures all responses during live calls.
    /// </summary>
    private async Task<Data?> RunToolCallWithSnapshot(string testName, List<LlmMessage> messages, List<GoalCall> tools)
    {
        var multiSnapshot = LlmSnapshotHelper.TryLoadMultiSnapshot(testName, messages);

        CaptureHandler? captureHandler = null;

        if (multiSnapshot != null)
        {
            var handler = new SnapshotReplayHandler(multiSnapshot);
            var httpProvider = new Default(handler) { Name = "snapshot" };
            _app.Code.Register<IHttp>(httpProvider);
            _app.Code.SetDefault<IHttp>("snapshot");
        }
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return null;
        }
        else
        {
            // Capture all responses during the live call
            captureHandler = new CaptureHandler();
            var httpProvider = new Default(captureHandler) { Name = "capture" };
            _app.Code.Register<IHttp>(httpProvider);
            _app.Code.SetDefault<IHttp>("capture");
        }

        var action = new query(Ctx) { Messages = messages.ToListData<LlmMessage>(),
            Tools = tools.ToListData<GoalCall>(),
            Temperature = (global::app.type.number.@this)0.0,
            MaxTokens = (global::app.type.number.@this)200,
            Cache = (global::app.type.item.@bool.@this)false
        };

        await action.Attach(null, Ctx);
        var result = await action.Run();

        if (captureHandler != null && result.Success && captureHandler.Responses.Count > 0)
        {
            LlmSnapshotHelper.SaveMultiSnapshot(testName, messages, captureHandler.Responses);
        }

        return result;
    }

    private static async System.Threading.Tasks.Task<string?> BuildSnapshotFromResult(Data result)
    {
        var rawResponse = (await result.Properties.Value("RawResponse"))?.ToString();
        if (rawResponse == null) return null;

        var model = (await result.Properties.Value("Model"))?.ToString() ?? "gpt-5.4-nano";
        var promptTokens = (await result.Properties.Value("PromptTokens")) is int pt ? pt : 0;
        var completionTokens = (await result.Properties.Value("CompletionTokens")) is int ct ? ct : 0;

        return JsonSerializer.Serialize(new
        {
            id = "snapshot",
            @object = "chat.completion",
            model,
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new { role = "assistant", content = rawResponse },
                    finish_reason = "stop"
                }
            },
            usage = new
            {
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens
            }
        });
    }

    /// <summary>
    /// Replays one or more saved responses in order.
    /// </summary>
    private class SnapshotReplayHandler : HttpMessageHandler
    {
        private readonly List<string> _responses;
        private int _index;

        public SnapshotReplayHandler(List<string> responses) => _responses = responses;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var json = _index < _responses.Count ? _responses[_index] : _responses[^1];
            _index++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
        }
    }

    /// <summary>
    /// Captures all HTTP responses during a live multi-turn call.
    /// Delegates to the real HTTP stack.
    /// </summary>
    private class CaptureHandler : DelegatingHandler
    {
        public List<string> Responses { get; } = new();

        public CaptureHandler() : base(new HttpClientHandler()) { }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                Responses.Add(body);
                response.Content = new StringContent(body, Encoding.UTF8,
                    new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
            }

            return response;
        }
    }
}

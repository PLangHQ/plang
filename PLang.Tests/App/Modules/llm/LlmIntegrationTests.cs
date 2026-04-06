using System.Net;
using System.Text;
using System.Text.Json;
using App.Context;
using App.Goals.Goal;
using App.Variables;
using App.modules.http.providers;
using App.modules.llm;
using App.modules.llm.providers;
using PLangEngine = App.@this;

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
    private PLangEngine _engine = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_integ_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
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

    // --- Test 1: Simple arithmetic ---

    [Test]
    public async Task Query_SimpleCalculation()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Text = "You are a calculator. Respond with ONLY the number, nothing else." },
            new LlmMessage { Role = "user", Text = "What is 7 * 6?" }
        };

        var result = await RunWithSnapshot("SimpleCalculation", messages, new query
        {
            Context = Ctx,
            Messages = messages,
            Temperature = 0.0,
            MaxTokens = 50,
            Cache = false
        });
        if (result == null) return; // skipped, no API key

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString() ?? "").Contains("42");
        await Assert.That(result.Properties["TotalTokens"]?.Value).IsNotNull();
    }

    // --- Test 2: JSON schema response ---

    [Test]
    public async Task Query_JsonSchema_ParsedCorrectly()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Text = "Analyze the sentiment of the text." },
            new LlmMessage { Role = "user", Text = "I absolutely love sunny days at the beach!" }
        };

        var result = await RunWithSnapshot("JsonSchema", messages, new query
        {
            Context = Ctx,
            Messages = messages,
            Schema = "{\"sentiment\": \"string\", \"score\": \"number\"}",
            Temperature = 0.0,
            MaxTokens = 100,
            Cache = false
        });
        if (result == null) return;

        await Assert.That(result.Success).IsTrue();
        // Value should be parsed JSON (JsonElement)
        await Assert.That(result.Value).IsNotNull();
        var json = result.Value is JsonElement je ? je : JsonSerializer.SerializeToElement(result.Value);
        await Assert.That(json.TryGetProperty("sentiment", out _)).IsTrue();
        await Assert.That(result.Properties["Format"]?.Value?.ToString()).IsEqualTo("json");
    }

    // --- Test 3: Code format extraction ---

    [Test]
    public async Task Query_PythonFormat_ExtractedFromCodeBlock()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Text = "Write the requested code." },
            new LlmMessage { Role = "user", Text = "Write a Python function that returns the sum of two numbers." }
        };

        var result = await RunWithSnapshot("PythonFormat", messages, new query
        {
            Context = Ctx,
            Messages = messages,
            Format = "python",
            Temperature = 0.0,
            MaxTokens = 200,
            Cache = false
        });
        if (result == null) return;

        await Assert.That(result.Success).IsTrue();
        var code = result.Value?.ToString() ?? "";
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
            new LlmMessage { Role = "system", Text = "You are a helpful assistant. Be very brief." },
            new LlmMessage { Role = "user", Text = "My name is Alice." }
        };

        var result1 = await RunWithSnapshot("ConvPart1", messages1, new query
        {
            Context = Ctx,
            Messages = messages1,
            Temperature = 0.0,
            MaxTokens = 50,
            Cache = false
        });
        if (result1 == null) return;
        await Assert.That(result1.Success).IsTrue();

        // Second query continues the conversation
        var messages2 = new List<LlmMessage>
        {
            new LlmMessage { Role = "user", Text = "What is my name?" }
        };

        var result2 = await RunWithSnapshot("ConvPart2", messages2, new query
        {
            Context = Ctx,
            Messages = messages2,
            ContinuePreviousConversation = true,
            Temperature = 0.0,
            MaxTokens = 50,
            Cache = false
        });
        if (result2 == null) return;

        await Assert.That(result2.Success).IsTrue();
        await Assert.That(result2.Value?.ToString() ?? "").Contains("Alice");
    }

    // --- Test 5: Tool calling ---

    [Test]
    public async Task Query_ToolCall_LlmRequestsToolAndHandlesError()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Text = "You have tools available. Use them when appropriate. Be brief." },
            new LlmMessage { Role = "user", Text = "What is the weather in London right now?" }
        };

        var tools = new List<GoalCall>
        {
            new GoalCall
            {
                Name = "GetWeather",
                Description = "Gets the current weather for a city",
                Parameters = new List<Data>
                {
                    new Data("city", null, App.Variables.Type.String)
                }
            }
        };

        // For tool calls, we use multi-snapshot (multiple HTTP round trips)
        var result = await RunToolCallWithSnapshot("ToolCallWeather", messages, tools);
        if (result == null) return;

        await Assert.That(result.Success).IsTrue();
        // The LLM should have attempted the tool, got an error (goal doesn't exist),
        // and then responded with something about not being able to get the weather
        await Assert.That(result.Value?.ToString() ?? "").IsNotEmpty();
        var toolCallCount = result.Properties["ToolCallCount"]?.Value;
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
            var httpProvider = new DefaultHttpProvider(handler) { Name = "snapshot" };
            _engine.Providers.Register<IHttpProvider>(httpProvider);
            _engine.Providers.SetDefault<IHttpProvider>("snapshot");
        }
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return null; // skip
        }

        var result = await action.Run();

        if (snapshot == null && result.Success)
        {
            var json = BuildSnapshotFromResult(result);
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
            var httpProvider = new DefaultHttpProvider(handler) { Name = "snapshot" };
            _engine.Providers.Register<IHttpProvider>(httpProvider);
            _engine.Providers.SetDefault<IHttpProvider>("snapshot");
        }
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY")))
        {
            return null;
        }
        else
        {
            // Capture all responses during the live call
            captureHandler = new CaptureHandler();
            var httpProvider = new DefaultHttpProvider(captureHandler) { Name = "capture" };
            _engine.Providers.Register<IHttpProvider>(httpProvider);
            _engine.Providers.SetDefault<IHttpProvider>("capture");
        }

        var action = new query
        {
            Context = Ctx,
            Messages = messages,
            Tools = tools,
            Temperature = 0.0,
            MaxTokens = 200,
            Cache = false
        };

        var result = await action.Run();

        if (captureHandler != null && result.Success && captureHandler.Responses.Count > 0)
        {
            LlmSnapshotHelper.SaveMultiSnapshot(testName, messages, captureHandler.Responses);
        }

        return result;
    }

    private static string? BuildSnapshotFromResult(Data result)
    {
        var rawResponse = result.Properties["RawResponse"]?.Value?.ToString();
        if (rawResponse == null) return null;

        var model = result.Properties["Model"]?.Value?.ToString() ?? "gpt-5.4-nano";
        var promptTokens = result.Properties["PromptTokens"]?.Value is int pt ? pt : 0;
        var completionTokens = result.Properties["CompletionTokens"]?.Value is int ct ? ct : 0;

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

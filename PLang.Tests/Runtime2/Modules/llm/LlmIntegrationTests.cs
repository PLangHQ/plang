using System.Net;
using System.Text;
using System.Text.Json;
using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.http.providers;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Integration test that runs the full LLM module pipeline.
/// First run: real OpenAI API → saves raw response as snapshot.
/// Subsequent runs: replays snapshot via mock HTTP handler — LLM module
/// still runs its full code path (message building, format handling, etc.).
/// Snapshot invalidates when input messages or class structure changes.
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

    [Test]
    public async Task Query_RealOpenAi_SimpleQuestion_ReturnsValidResponse()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Text = "You are a calculator. Respond with ONLY the number, nothing else." },
            new LlmMessage { Role = "user", Text = "What is 7 * 6?" }
        };

        const string testName = "SimpleCalculation";
        var snapshot = LlmSnapshotHelper.TryLoadSnapshot(testName, messages);

        if (snapshot != null)
        {
            // Replay: inject snapshot as the HTTP response.
            // The LLM module still runs its full pipeline — message building,
            // format handling, response parsing, property population.
            // Only the HTTP transport is mocked.
            var handler = new SnapshotReplayHandler(snapshot);
            var httpProvider = new DefaultHttpProvider(handler) { Name = "snapshot" };
            _engine.Providers.Register<IHttpProvider>(httpProvider);
            _engine.Providers.SetDefault<IHttpProvider>("snapshot");
        }
        else
        {
            // No snapshot — need real API key
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                await Assert.That(true).IsTrue(); // skip gracefully
                return;
            }
            // Use default providers — real HTTP call through the http module
        }

        // Both paths: run through the full LLM module
        var action = new query
        {
            Context = Ctx,
            Messages = messages,
            Temperature = 0.0,
            MaxTokens = 50,
            Cache = false // don't let the LLM cache interfere with snapshot testing
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        var value = result.Value?.ToString() ?? "";
        await Assert.That(value).Contains("42");

        // Verify properties are populated
        await Assert.That(result.Properties["Model"]?.Value).IsNotNull();
        await Assert.That(result.Properties["TotalTokens"]?.Value).IsNotNull();
        await Assert.That(result.Properties["RawResponse"]?.Value).IsNotNull();

        // Save snapshot if this was a live call (no snapshot existed)
        if (snapshot == null)
        {
            // Reconstruct the API response JSON from the result properties
            var snapshotJson = BuildSnapshotFromResult(result);
            if (snapshotJson != null)
                LlmSnapshotHelper.SaveSnapshot(testName, messages, snapshotJson);
        }
    }

    /// <summary>
    /// Reconstructs an OpenAI-shaped API response from the LLM module result.
    /// This is what gets saved as the snapshot for replay.
    /// </summary>
    private static string? BuildSnapshotFromResult(Data result)
    {
        var rawResponse = result.Properties["RawResponse"]?.Value?.ToString();
        if (rawResponse == null) return null;

        var model = result.Properties["Model"]?.Value?.ToString() ?? "gpt-4.1-mini";
        var promptTokens = result.Properties["PromptTokens"]?.Value is int pt ? pt : 0;
        var completionTokens = result.Properties["CompletionTokens"]?.Value is int ct ? ct : 0;

        var response = new
        {
            id = "snapshot-replay",
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
        };

        return JsonSerializer.Serialize(response);
    }

    /// <summary>
    /// Replays a saved snapshot as the HTTP response.
    /// Only the HTTP transport is mocked — the LLM module runs its full pipeline.
    /// </summary>
    private class SnapshotReplayHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public SnapshotReplayHandler(string responseJson) => _responseJson = responseJson;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json")
            });
        }
    }
}

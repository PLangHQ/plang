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
/// Integration test that hits the real OpenAI API (or replays a saved snapshot).
/// Snapshot invalidates when input messages or class structure changes.
/// Requires OPENAI_API_KEY environment variable for first run.
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
            // Replay: inject snapshot as mock HTTP response
            var handler = new SnapshotReplayHandler(snapshot);
            var httpProvider = new DefaultHttpProvider(handler) { Name = "snapshot" };
            _engine.Providers.Register<IHttpProvider>(httpProvider);
            _engine.Providers.SetDefault<IHttpProvider>("snapshot");
        }
        else
        {
            // Real call: wrap the real HTTP handler to capture the response
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                // No API key and no snapshot — skip gracefully
                await Assert.That(true).IsTrue(); // pass without testing
                return;
            }

            var captureHandler = new SnapshotCaptureHandler();
            var httpProvider = new DefaultHttpProvider(captureHandler) { Name = "capture" };
            _engine.Providers.Register<IHttpProvider>(httpProvider);
            _engine.Providers.SetDefault<IHttpProvider>("capture");

            var action = new query
            {
                Context = Ctx,
                Messages = messages,
                Temperature = 0.0,
                MaxTokens = 50
            };
            var result = await action.Run();

            await Assert.That(result.Success).IsTrue();
            var value = result.Value?.ToString() ?? "";
            await Assert.That(value).Contains("42");

            // Save snapshot for next run
            if (captureHandler.CapturedResponse != null)
                LlmSnapshotHelper.SaveSnapshot(testName, messages, captureHandler.CapturedResponse);

            // Verify properties
            await Assert.That(result.Properties["Model"]?.Value).IsNotNull();
            await Assert.That(result.Properties["TotalTokens"]?.Value).IsNotNull();
            await Assert.That(result.Properties["Cached"]?.Value).IsEqualTo(false);
            return;
        }

        // Replay path
        var replayAction = new query
        {
            Context = Ctx,
            Messages = messages,
            Temperature = 0.0,
            MaxTokens = 50
        };
        var replayResult = await replayAction.Run();

        await Assert.That(replayResult.Success).IsTrue();
        var replayValue = replayResult.Value?.ToString() ?? "";
        await Assert.That(replayValue).Contains("42");
        await Assert.That(replayResult.Properties["Model"]?.Value).IsNotNull();
        await Assert.That(replayResult.Properties["TotalTokens"]?.Value).IsNotNull();
    }

    /// <summary>
    /// Replays a saved snapshot as the HTTP response.
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

    /// <summary>
    /// Makes real HTTP calls but captures the response body for snapshotting.
    /// Uses DelegatingHandler wrapping HttpClientHandler to get access to SendAsync.
    /// </summary>
    private class SnapshotCaptureHandler : DelegatingHandler
    {
        public string? CapturedResponse { get; private set; }

        public SnapshotCaptureHandler() : base(new HttpClientHandler()) { }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                CapturedResponse = body;

                // Rebuild the response content since we consumed it
                response.Content = new StringContent(body, Encoding.UTF8, new System.Net.Http.Headers.MediaTypeHeaderValue("application/json"));
            }

            return response;
        }
    }
}

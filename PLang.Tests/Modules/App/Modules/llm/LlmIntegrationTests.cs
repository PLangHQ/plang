using System.Net;
using System.Text;
using System.Text.Json;
using app.actor.context;
using app.goal;
using app.type.item.variable;
using app.module.http.code;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Integration tests that run the full LLM module pipeline against OpenAI.
/// Each request is answered from the answer recorded for its body (only the HTTP transport is replaced; the full LLM
/// pipeline runs). A changed request is a new question: without OPENAI_API_KEY it fails, with it it is asked once.
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
        _app = new global::app.@this(_tempDir, global::PLang.Tests.Parent.Actor).Testing();
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

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    // --- Test 1: Simple arithmetic ---

    [Test]
    public async Task Query_SimpleCalculation()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = "You are a calculator. Respond with ONLY the number, nothing else." },
            new LlmMessage { Role = "user", Content = "What is 7 * 6?" }
        };

        var result = await RunWithSnapshot(new query(Ctx) { Message = messages.ToListData<LlmMessage>(Ctx),
            Temperature = (global::app.type.item.number.@this)0.0,
            Limit = new global::app.module.llm.type.limit.@this(50, 10, 0),
            Cache = Ctx.Choice(global::app.module.cache.type.cache.skip)
        });

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString() ?? "").Contains("42");
        await Assert.That((await result.Properties.Value("TotalTokens"))).IsNotNull();
    }

    // --- Test 2: JSON schema response ---

    // (Query_JsonSchema_ParsedCorrectly retired — it read the result through a JsonElement fallback, the deleted
    //  clr(json) model. The result's value is now a plang dict read through plang navigation, proved by
    //  QueryJsonSchemaPlangDictTests.)

    // --- Test 3: Code format extraction ---

    [Test]
    public async Task Query_PythonFormat_ExtractedFromCodeBlock()
    {
        var messages = new List<LlmMessage>
        {
            new LlmMessage { Role = "system", Content = "Write the requested code." },
            new LlmMessage { Role = "user", Content = "Write a Python function that returns the sum of two numbers." }
        };

        var result = await RunWithSnapshot(new query(Ctx) { Message = messages.ToListData<LlmMessage>(Ctx),
            Format = (global::app.type.item.text.@this)"python",
            Temperature = (global::app.type.item.number.@this)0.0,
            Limit = new global::app.module.llm.type.limit.@this(200, 10, 0),
            Cache = Ctx.Choice(global::app.module.cache.type.cache.skip)
        });

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

        var result1 = await RunWithSnapshot(new query(Ctx) { Message = messages1.ToListData<LlmMessage>(Ctx),
            Temperature = (global::app.type.item.number.@this)0.0,
            Limit = new global::app.module.llm.type.limit.@this(50, 10, 0),
            Cache = Ctx.Choice(global::app.module.cache.type.cache.skip)
        });
        await result1.IsSuccess();

        // Second query continues the conversation
        var messages2 = new List<LlmMessage>
        {
            new LlmMessage { Role = "user", Content = "What is my name?" }
        };

        var result2 = await RunWithSnapshot(new query(Ctx) { Message = messages2.ToListData<LlmMessage>(Ctx),
            Conversation = new global::app.module.llm.type.conversation.@this(result1),
            Temperature = (global::app.type.item.number.@this)0.0,
            Limit = new global::app.module.llm.type.limit.@this(50, 10, 0),
            Cache = Ctx.Choice(global::app.module.cache.type.cache.skip)
        });

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

        var tools = new List<global::app.goal.step.action.@this>
        {
            Make.Tool(Ctx, "GetWeather", parameter: new List<Data>
            {
                new Data("city", null, Ctx.App.type.list["text"], context: Ctx)
            })
        };

        // a tool call is several round trips, each its own request
        var result = await RunToolCallWithSnapshot(messages, tools);

        await result.IsSuccess();
        // The LLM should have attempted the tool, got an error (goal doesn't exist),
        // and then responded with something about not being able to get the weather
        await Assert.That((await result.Value())?.ToString() ?? "").IsNotEmpty();
        var toolCallCount = (await result.Properties.Value("ToolCallCount"));
        await Assert.That(toolCallCount).IsNotNull();
    }

    // --- Snapshot infrastructure ---

    /// <summary>
    /// Runs a query, each request it sends answered from its recording. Without OPENAI_API_KEY a request with no
    /// recording fails the test; with it, that request is asked live once and recorded on this machine.
    /// </summary>
    private async Task<Data> RunWithSnapshot(query action)
    {
        Recording();
        await action.Attach(null, Ctx);
        return await action.Start();
    }

    /// <summary>
    /// Runs a tool-call query: one request per round trip, each answered from its own recording.
    /// </summary>
    private async Task<Data> RunToolCallWithSnapshot(List<LlmMessage> messages, List<global::app.goal.step.action.@this> tools)
    {
        Recording();
        var action = new query(Ctx) { Message = messages.ToListData<LlmMessage>(Ctx),
            Tool = tools.ToListData(Ctx),
            Temperature = (global::app.type.item.number.@this)0.0,
            Limit = new global::app.module.llm.type.limit.@this(200, 10, 0),
            Cache = Ctx.Choice(global::app.module.cache.type.cache.skip)
        };

        await action.Attach(null, Ctx);
        return await action.Start();
    }

    // the app's HTTP answered by a recording; live only with OPENAI_API_KEY set, so then without the test key
    private void Recording()
    {
        var live = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
        var recording = new RecordingHandler(live);
        if (!live)
        {
            LlmTestHelper.Mock(_app, recording);
            return;
        }
        _app.Code.Register<IHttp>(new Default(recording) { Name = "recording" });
        _app.Code.SetDefault<IHttp>("recording");
    }

    /// <summary>
    /// Answers each request from the answer recorded for its body. A request with no recording is asked live when
    /// <c>live</c>, and its answer recorded; otherwise it fails, naming the request's key.
    /// </summary>
    private class RecordingHandler(bool live) : DelegatingHandler(new HttpClientHandler())
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            if (LlmSnapshotHelper.Answer(body) is { } answer)
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(answer, Encoding.UTF8, "application/json")
                };
            if (!live)
                throw new InvalidOperationException(
                    $"No recorded llm answer for request {LlmSnapshotHelper.Key(body)}; run once with OPENAI_API_KEY set to record it.");

            var response = await base.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return response;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            LlmSnapshotHelper.Save(body, json);
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return response;
        }
    }
}

using System.Text.Json;
using app.actor.context;
using app.variable;
using app.module.code;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests basic llm.query behavior: simple messages, model override, error handling,
/// and response property population. Uses MockHttpMessageHandler to control API responses.
/// </summary>
public class QueryBasicTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_basic_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir);
        _handler = LlmTestHelper.SetupMockHttp(_app);
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

    #region Happy Path

    [Test]
    public async Task Query_SimpleMessage_ReturnsContentAsDataValue()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("Hello world")));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Value?.ToString()).IsEqualTo("Hello world");
    }

    [Test]
    public async Task Query_ModelParameter_OverridesDefault()
    {
        _handler.Handler = async req =>
        {
            var body = await req.Content!.ReadAsStringAsync();
            var json = JsonDocument.Parse(body);
            var model = json.RootElement.GetProperty("model").GetString();
            return LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("ok", model: model!));
        };

        var action = LlmTestHelper.MakeQuery(Ctx);
        action = new query
        {
            Context = Ctx,
            Messages = action.Messages,
            Model = "gpt-4o"
        };
        var result = await action.Run();

        await result.IsSuccess();
        // Verify model was sent to API
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("gpt-4o");
    }

    [Test]
    public async Task Query_TemperatureAndMaxTokens_SentToApi()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            },
            Temperature = 0.7,
            MaxTokens = 2000
        };
        var result = await action.Run();

        await result.IsSuccess();
        var reqBody = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(reqBody).Contains("0.7");
        await Assert.That(reqBody).Contains("2000");
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task Query_ApiError4xx_ReturnsDataFromError()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse("{\"error\":{\"message\":\"Bad request\"}}", System.Net.HttpStatusCode.BadRequest));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error?.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error?.Message).Contains("400");
    }

    [Test]
    public async Task Query_ApiError5xx_ReturnsDataFromError()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse("{\"error\":{\"message\":\"Server error\"}}",
                System.Net.HttpStatusCode.InternalServerError));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await result.IsFailure();
        await Assert.That(result.Error?.Key).IsEqualTo("HttpError");
        await Assert.That(result.Error?.Message).Contains("500");
    }

    #endregion

    #region Response Properties

    [Test]
    public async Task Query_ResponseProperties_Populated()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("result text",
                    promptTokens: 15, completionTokens: 25, cachedTokens: 5)));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Properties["RawResponse"]?.ToString()).IsEqualTo("result text");
        await Assert.That(result.Properties["Model"]?.ToString()).IsEqualTo("gpt-5.4-nano");
        await Assert.That(result.Properties["PromptTokens"]).IsEqualTo(15);
        await Assert.That(result.Properties["CompletionTokens"]).IsEqualTo(25);
        await Assert.That(result.Properties["TotalTokens"]).IsEqualTo(40);
        await Assert.That(result.Properties["Cached"]).IsEqualTo(false);
        // CachedTokens reaches Properties — wire-up check on the success-exit path.
        await Assert.That(result.Properties["CachedTokens"]).IsEqualTo(5);
    }

    // Pricing table covers gpt-5.4-{nano,mini,(base)}. A model not on that list
    // produces null Cost — the once-per-Run debug warning fires (covered separately
    // if/when a Debug-channel capture fixture lands).
    [Test]
    public async Task Query_CostNull_WhenNoPricingData()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("ok", model: "claude-99-future")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "You are helpful" },
                new LlmMessage { Role = "user", Content = "Hello" }
            },
            Model = new global::app.data.@this<string>("Model", "claude-99-future")
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(result.Properties["Cost"]).IsNull();
    }

    // Cost math: prompt_tokens=100, cached_tokens=40, completion_tokens=50 against
    // gpt-5.4-nano (input 0.20, cached 0.02, output 1.25 — USD per 1M tokens).
    // Non-cached input bucket = prompt − cached = 60.
    // Cost = (60·0.20 + 40·0.02 + 50·1.25) / 1_000_000.
    // Equality-check (not approx) — the math is pure decimal arithmetic.
    [Test]
    public async Task Query_Cost_PositiveArithmetic_PricedModel()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("ok",
                    promptTokens: 100, completionTokens: 50, cachedTokens: 40,
                    model: "gpt-5.4-nano")));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        decimal expected = (60m * 0.20m + 40m * 0.02m + 50m * 1.25m) / 1_000_000m;
        await Assert.That((decimal?)result.Properties["Cost"]).IsEqualTo(expected);
        // CachedTokens surfaces on Properties too (F5).
        await Assert.That(result.Properties["CachedTokens"]).IsEqualTo(40);
    }

    // Longest-prefix-wins. Both "gpt-5.4" and "gpt-5.4-mini" are pricing prefixes;
    // for model "gpt-5.4-mini-2026-03-17" the longer match must take precedence.
    // mini row: input 0.75, cached 0.075, output 4.50.
    // Cost = 1_000_000·0.75 + 1_000_000·4.50, divided by 1_000_000 = 5.25.
    [Test]
    public async Task Query_Cost_LongestPrefixWins_MiniBeatsBase()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("ok",
                    promptTokens: 1_000_000, completionTokens: 1_000_000,
                    model: "gpt-5.4-mini-2026-03-17")));

        // Pricing lookup uses the action's Model; set it to the dated variant.
        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "You are helpful" },
                new LlmMessage { Role = "user", Content = "Hello" }
            },
            Model = new global::app.data.@this<string>("Model", "gpt-5.4-mini-2026-03-17")
        };
        var result = await action.Run();

        await result.IsSuccess();
        // 1e6·0.75/1e6 + 1e6·4.50/1e6 = 5.25 exact.
        await Assert.That((decimal?)result.Properties["Cost"]).IsEqualTo(5.25m);
    }

    // Cost accumulates across the tool-call retry loop. First response asks for a
    // tool; the second is the final answer. Each call carries its own usage —
    // totalCost is the sum. (Same exit path also surfaces CachedTokens; F5.)
    [Test]
    public async Task Query_Cost_AccumulatesAcrossRetryLoop()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "Echo", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("done",
                    promptTokens: 200, completionTokens: 30, cachedTokens: 50,
                    model: "gpt-5.4-nano")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "go" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "Echo" }
            }
        };
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That(_handler.CallCount).IsEqualTo(2);

        // Call 1 (tool-call response, default usage: 10 prompt, 5 completion, 0 cached):
        //   10·0.20 + 0·0.02 + 5·1.25 = 8.25
        // Call 2 (final, 200 prompt / 30 comp / 50 cached):
        //   150·0.20 + 50·0.02 + 30·1.25 = 68.5
        // Total = 76.75 / 1_000_000
        decimal call1 = (10m * 0.20m + 0m * 0.02m + 5m * 1.25m) / 1_000_000m;
        decimal call2 = (150m * 0.20m + 50m * 0.02m + 30m * 1.25m) / 1_000_000m;
        await Assert.That((decimal?)result.Properties["Cost"]).IsEqualTo(call1 + call2);
        // CachedTokens on the tool-call exit path (F5).
        await Assert.That(result.Properties["CachedTokens"]).IsEqualTo(50);
    }

    #endregion
}

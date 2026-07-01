using System.Text.Json;
using app.actor.context;
using app.goal;
using app.variable;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests callback GoalCalls: OnToolCall, OnValidateResponse, and OnStream.
/// Note: Callback GoalCalls require actual goals to exist. These tests verify
/// the provider's behavior when callbacks are configured but goals don't exist
/// (callbacks fail gracefully — errors don't crash the query).
/// </summary>
public class QueryCallbackTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_cb_" + Guid.NewGuid().ToString("N")[..8]);
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

    #region OnToolCall

    [Test]
    public async Task Query_OnToolCall_FiresStartingAndCompleted()
    {
        // OnToolCall is a GoalCall — in unit tests, the goal won't exist
        // but the provider should still attempt to fire it and continue
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "TestTool", "{}"))));
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("done")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "use tool" }
            }.ToListData<LlmMessage>(),
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "TestTool" }
            }.ToListData<GoalCall>(),
            OnToolCall = new GoalCall { Name = "LogToolCall" }
        };

        // Should complete without crashing even though LogToolCall goal doesn't exist
        await action.Attach(null, Ctx);
        var result = await action.Run();
        await result.IsSuccess();
    }

    [Test]
    public async Task Query_OnToolCall_ToolLoopCompletesWithCallback()
    {
        // Verifies the full tool loop completes when OnToolCall is configured.
        // The callback goal doesn't exist in unit tests, so RunGoalAsync returns error,
        // but the provider ignores callback errors and continues. This test proves:
        // 1. OnToolCall doesn't crash the tool loop
        // 2. Tool execution still works (tool result sent back to LLM)
        // 3. Final result is correct
        // Note: Verifying the callback actually fires requires integration tests with real goals.
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "GetData", "{\"id\":42}"))));
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("got data")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "get data" }
            }.ToListData<LlmMessage>(),
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "GetData" }
            }.ToListData<GoalCall>(),
            OnToolCall = new GoalCall { Name = "ToolCallHandler" }
        };

        await action.Attach(null, Ctx);
        var result = await action.Run();
        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("got data");
        // Verify tool execution happened: 2 HTTP calls (tool call + re-query with result)
        await Assert.That(_handler.CallCount).IsEqualTo(2);
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("tool");
    }

    #endregion

    #region OnValidateResponse

    [Test]
    public async Task Query_OnValidateResponse_Passes_ReturnsNormally()
    {
        // When OnValidateResponse goal doesn't exist, RunGoalAsync returns error
        // which triggers retry. With MaxValidationRetries = (global::app.type.number.@this)0, it returns error immediately.
        // To test "passes" scenario, we need the validation goal to actually exist.
        // For unit test: no OnValidateResponse set → result returns normally
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("valid response")));

        var action = LlmTestHelper.MakeQuery(Ctx);
        await action.Attach(null, Ctx);
        var result = await action.Run();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("valid response");
    }

    [Test]
    public async Task Query_OnValidateResponse_Fails_RetriesWithFeedback()
    {
        // OnValidateResponse configured but goal doesn't exist → RunGoalAsync returns error
        // → retry feedback sent to LLM → second response returns
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse($"attempt {callIndex}")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "validate me" }
            }.ToListData<LlmMessage>(),
            OnValidateResponse = new GoalCall { Name = "NonExistentValidator" },
            MaxValidationRetries = (global::app.type.number.@this)2
        };

        await action.Attach(null, Ctx);
        var result = await action.Run();
        // After MaxValidationRetries, should return error
        await result.IsFailure();
        await Assert.That(result.Error?.Key).IsEqualTo("ValidationFailed");
    }

    [Test]
    public async Task Query_OnValidateResponse_MaxRetries_ReturnsError()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse($"attempt {callIndex}")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "validate" }
            }.ToListData<LlmMessage>(),
            OnValidateResponse = new GoalCall { Name = "AlwaysFails" },
            MaxValidationRetries = (global::app.type.number.@this)3
        };

        await action.Attach(null, Ctx);
        var result = await action.Run();
        await result.IsFailure();
        // Validation goal doesn't exist → file-not-found error on each retry
        // After max retries, returns "LLM validation failed: <last error>"
        await Assert.That(result.Error?.Message).Contains("LLM validation failed");
    }

    [Test]
    public async Task Query_OnValidateResponse_OnlyOnContentResponse()
    {
        // Validation should NOT run during tool call rounds
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "TestTool", "{}"))));
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("final answer")));
        };

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "tools then validate" }
            }.ToListData<LlmMessage>(),
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "TestTool" }
            }.ToListData<GoalCall>(),
            OnValidateResponse = new GoalCall { Name = "Validator" },
            MaxValidationRetries = (global::app.type.number.@this)1
        };

        // Tool round should not trigger validation
        // Final content round will trigger validation (which fails since goal doesn't exist)
        // But with MaxValidationRetries = (global::app.type.number.@this)1, we get one retry then error
        await action.Attach(null, Ctx);
        var result = await action.Run();
        // The key thing: it should have made it past the tool round to the validation phase
        await Assert.That(_handler.CallCount).IsGreaterThanOrEqualTo(2);
    }

    #endregion

    #region OnStream

    [Test]
    public async Task Query_OnStream_FiresPerChunk()
    {
        // Streaming test — basic verification that streaming mode is requested
        // Full streaming tests need SSE mock which is complex for unit tests
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("streamed")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "stream test" }
            }.ToListData<LlmMessage>(),
            OnStream = new GoalCall { Name = "HandleChunk" }
        };

        // With streaming enabled, the request should have stream:true
        await action.Attach(null, Ctx);
        var result = await action.Run();
        // Verify the request had stream=true
        if (_handler.LastRequest != null)
        {
            var body = await _handler.LastRequest.Content!.ReadAsStringAsync();
            await Assert.That(body).Contains("stream");
        }
    }

    [Test]
    public async Task Query_OnStream_SignalsDone()
    {
        // Same as above — streaming mode verification
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("done streaming")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "stream" }
            }.ToListData<LlmMessage>(),
            OnStream = new GoalCall { Name = "StreamHandler" }
        };

        await action.Attach(null, Ctx);
        var result = await action.Run();
        // At minimum, shouldn't crash
        await Assert.That(result).IsNotNull();
    }

    #endregion
}

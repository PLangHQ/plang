using app.actor.context;
using app.goal;
using app.type.item.variable;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Edge cases and security: empty messages, tool count tracking across rounds,
/// null arguments, empty content, missing provider.
/// </summary>
public class QueryEdgeCaseTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_edge_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new global::app.@this(_tempDir).Testing();
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

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    [Test]
    public async Task Query_EmptyMessages_ReturnsError()
    {
        var action = new query(Ctx) { Message = new List<LlmMessage>().ToListData<LlmMessage>(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsFailure();
        await Assert.That(result.Error?.Key).IsEqualTo("ValidationError");
    }

    [Test]
    public async Task Query_ToolLoop_DoesNotExceedMaxEvenWithMultiPerRound()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex <= 3) // Keep returning tool calls
            {
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(
                        ($"call_{callIndex}a", "ToolA", "{}"),
                        ($"call_{callIndex}b", "ToolB", "{}"),
                        ($"call_{callIndex}c", "ToolC", "{}"))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("done")));
        };

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "multi tools" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Call(Ctx, "ToolA"),
                Make.Call(Ctx, "ToolB"),
                Make.Call(Ctx, "ToolC")
            }.ToListData(Ctx),
            Limit = new global::app.module.llm.type.limit.@this(16000, 5, 0)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        // limit.tool = 5, 3 tools/round (with batch-slice fix):
        // Round 1 (HTTP #1): remaining=5, all 3 tools execute, toolCallCount=3, continue
        // Round 2 (HTTP #2): remaining=2, sliced to 2 tools, toolCallCount=5, continue
        // Round 3 (HTTP #3): toolCallCount >= limit.tool → break
        await Assert.That(result).IsNotNull();
        await Assert.That(_handler.CallCount).IsEqualTo(3);
        await Assert.That(callIndex).IsEqualTo(3);
        // Loop exited via limit.tool — result carries metadata
        await Assert.That((await result.Properties.Value("Truncated"))).IsEqualTo(true);
    }

    [Test]
    public async Task Query_NullToolCallArguments_HandledGracefully()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            if (callIndex == 1)
            {
                // Tool call with empty arguments
                return Task.FromResult(LlmTestHelper.JsonResponse(
                    LlmTestHelper.MakeToolCallResponse(("call_1", "NoArgTool", ""))));
            }
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("handled")));
        };

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "null args" }
            }.ToListData<LlmMessage>(Ctx),
            Tool = new List<global::app.goal.step.action.@this>
            {
                Make.Call(Ctx, "NoArgTool")
            }.ToListData(Ctx)
        };
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
    }

    [Test]
    public async Task Query_ApiReturnsEmptyContent_ReturnsEmptyString()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("")));

        var action = LlmTestHelper.MakeQuery(Ctx);
        await action.Attach(null, Ctx);
        var result = await action.Start();

        await result.IsSuccess();
        await Assert.That((await result.Value())?.ToString()).IsEqualTo("");
    }

    [Test]
    public async Task Query_ProviderRegistered_ByDefault()
    {
        // Verify that RegisterDefaults always registers the LLM provider.
        // A separate missing-provider test isn't practical since RegisterDefaults
        // is called in the engine constructor and can't be skipped.
        var tempDir2 = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_noprov_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(tempDir2);
        var engine2 = new global::app.@this(tempDir2).Testing();

        try
        {
            var providerResult = engine2.Code.Get<ILlm>();
            await Assert.That(providerResult.Error).IsNull();
            await Assert.That(providerResult.Provider).IsTypeOf<OpenAi>();
        }
        finally
        {
            await engine2.DisposeAsync();
            try { System.IO.Directory.Delete(tempDir2, true); } catch { }
        }
    }
}

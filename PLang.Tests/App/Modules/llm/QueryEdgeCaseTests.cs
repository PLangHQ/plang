using App.Engine.Context;
using App.Engine.Goals.Goal;
using App.Engine.Variables;
using App.modules.llm;
using App.modules.llm.providers;
using PLangEngine = App.Engine.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Edge cases and security: empty messages, tool count tracking across rounds,
/// null arguments, empty content, missing provider.
/// </summary>
public class QueryEdgeCaseTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_edge_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _engine = new PLangEngine(_tempDir);
        _handler = LlmTestHelper.SetupMockHttp(_engine);
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
    public async Task Query_EmptyMessages_ReturnsError()
    {
        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>()
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsFalse();
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

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "multi tools" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "ToolA", Description = "A" },
                new GoalCall { Name = "ToolB", Description = "B" },
                new GoalCall { Name = "ToolC", Description = "C" }
            },
            MaxToolCalls = 5
        };
        var result = await action.Run();

        // MaxToolCalls=5, 3 tools/round (with batch-slice fix):
        // Round 1 (HTTP #1): remaining=5, all 3 tools execute, toolCallCount=3, continue
        // Round 2 (HTTP #2): remaining=2, sliced to 2 tools, toolCallCount=5, continue
        // Round 3 (HTTP #3): toolCallCount >= MaxToolCalls → break
        await Assert.That(result).IsNotNull();
        await Assert.That(_handler.CallCount).IsEqualTo(3);
        await Assert.That(callIndex).IsEqualTo(3);
        // Loop exited via MaxToolCalls — result carries metadata
        await Assert.That(result.Properties["Truncated"]?.Value).IsEqualTo(true);
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

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "null args" }
            },
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "NoArgTool", Description = "no args needed" }
            }
        };
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task Query_ApiReturnsEmptyContent_ReturnsEmptyString()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("")));

        var action = LlmTestHelper.MakeQuery(Ctx);
        var result = await action.Run();

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.Value?.ToString()).IsEqualTo("");
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
        var engine2 = new PLangEngine(tempDir2);

        try
        {
            var providerResult = engine2.Providers.Get<ILlmProvider>();
            await Assert.That(providerResult.Success).IsTrue();
            await Assert.That(providerResult.Value).IsTypeOf<OpenAiProvider>();
        }
        finally
        {
            await engine2.DisposeAsync();
            try { System.IO.Directory.Delete(tempDir2, true); } catch { }
        }
    }
}

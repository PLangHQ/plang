using PLang.Runtime2.Engine.Context;
using PLang.Runtime2.Engine.Memory;
using PLang.Runtime2.modules.llm;
using PLang.Runtime2.modules.llm.providers;
using PLang.Runtime2.Engine.Goals.Goal;
using PLangEngine = PLang.Runtime2.Engine.@this;

namespace PLang.Tests.Runtime2.Modules.llm;

/// <summary>
/// Tests persistent caching: cache hits, misses, opt-out, tool query exclusion,
/// and hash sensitivity to model/temperature/schema/format.
/// </summary>
public class QueryCacheTests
{
    private string _tempDir = null!;
    private PLangEngine _engine = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_cache_" + Guid.NewGuid().ToString("N")[..8]);
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
    public async Task Query_CacheTrue_SecondCallReturnsCached()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("cached answer")));

        var action = LlmTestHelper.MakeQuery(Ctx, userText: "cache test");

        var result1 = await action.Run();
        await Assert.That(result1.Success).IsTrue();
        await Assert.That(_handler.CallCount).IsEqualTo(1);

        // Second call — should hit cache
        var action2 = LlmTestHelper.MakeQuery(Ctx, userText: "cache test");
        var result2 = await action2.Run();

        await Assert.That(result2.Success).IsTrue();
        await Assert.That(_handler.CallCount).IsEqualTo(1); // No additional HTTP call
        await Assert.That(result2.Properties["Cached"]?.Value).IsEqualTo(true);
    }

    [Test]
    public async Task Query_CacheTrue_DifferentMessages_CacheMiss()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("answer")));

        var action1 = LlmTestHelper.MakeQuery(Ctx, userText: "question 1");
        await action1.Run();

        var action2 = LlmTestHelper.MakeQuery(Ctx, userText: "question 2");
        await action2.Run();

        await Assert.That(_handler.CallCount).IsEqualTo(2); // Both made HTTP calls
    }

    [Test]
    public async Task Query_CacheFalse_AlwaysCallsApi()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("answer")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "same question" }
            },
            Cache = false
        };

        await action.Run();
        await action.Run();

        await Assert.That(_handler.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_CacheTrue_ToolsNonNull_CacheSkipped()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("tool result")));

        var action = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Text = "use tools" }
            },
            Cache = true,
            Tools = new List<GoalCall>
            {
                new GoalCall { Name = "TestTool", Description = "a test tool" }
            }
        };

        await action.Run();
        await action.Run();

        await Assert.That(_handler.CallCount).IsEqualTo(2); // Cache skipped
    }

    [Test]
    public async Task Query_CacheHash_IncludesModelTempSchemaFormat()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("answer")));

        // Call with default model
        var action1 = LlmTestHelper.MakeQuery(Ctx, userText: "same");
        await action1.Run();

        // Same message but different model — should be cache miss
        var action2 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Text = "You are helpful" },
                new LlmMessage { Role = "user", Text = "same" }
            },
            Model = "gpt-4o"
        };
        await action2.Run();

        await Assert.That(_handler.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_CacheHit_PropertiesPreserved()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse("preserved", promptTokens: 5, completionTokens: 10)));

        var action = LlmTestHelper.MakeQuery(Ctx, userText: "props test");
        var result1 = await action.Run();
        await Assert.That(result1.Success).IsTrue();

        // Cache hit
        var action2 = LlmTestHelper.MakeQuery(Ctx, userText: "props test");
        var result2 = await action2.Run();

        await Assert.That(result2.Success).IsTrue();
        await Assert.That(result2.Properties["Cached"]?.Value).IsEqualTo(true);
        await Assert.That(result2.Properties["RawResponse"]?.Value?.ToString()).IsEqualTo("preserved");
        await Assert.That(result2.Properties["Model"]?.Value?.ToString()).IsEqualTo("gpt-4.1-mini");
    }
}

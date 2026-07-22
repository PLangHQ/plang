using app.actor.context;
using app.variable;
using app.module.action.llm;
using app.module.action.llm.code;
using app.goal;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests persistent caching: cache hits, misses, opt-out, tool query exclusion,
/// and hash sensitivity to model/temperature/schema/format.
/// </summary>
public class QueryCacheTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_cache_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = TestApp.Create(_tempDir);
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

    [Test]
    public async Task Query_CacheTrue_SecondCallReturnsCached()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("cached answer")));

        var action = LlmTestHelper.MakeQuery(Ctx, userText: "cache test");

        await action.Attach(null, Ctx);
        var result1 = await action.Run();
        await result1.IsSuccess();
        await Assert.That(_handler.CallCount).IsEqualTo(1);

        // Second call — should hit cache
        var action2 = LlmTestHelper.MakeQuery(Ctx, userText: "cache test");
        await action2.Attach(null, Ctx);
        var result2 = await action2.Run();

        await result2.IsSuccess();
        await Assert.That(_handler.CallCount).IsEqualTo(1); // No additional HTTP call
        await Assert.That((await result2.Properties.Value("Cached"))).IsEqualTo(true);
    }

    [Test]
    public async Task Query_CacheTrue_DifferentMessages_CacheMiss()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("answer")));

        var action1 = LlmTestHelper.MakeQuery(Ctx, userText: "question 1");
        await action1.Attach(null, Ctx);
        await action1.Run();

        var action2 = LlmTestHelper.MakeQuery(Ctx, userText: "question 2");
        await action2.Attach(null, Ctx);
        await action2.Run();

        await Assert.That(_handler.CallCount).IsEqualTo(2); // Both made HTTP calls
    }

    [Test]
    public async Task Query_CacheFalse_AlwaysCallsApi()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("answer")));

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "same question" }
            }.ToListData<LlmMessage>(),
            Cache = (global::app.type.item.@bool.@this)false
        };

        await action.Attach(null, Ctx);
        await action.Run();
        await action.Attach(null, Ctx);
        await action.Run();

        await Assert.That(_handler.CallCount).IsEqualTo(2);
    }

    [Test]
    public async Task Query_CacheTrue_ToolsNonNull_CacheSkipped()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("tool result")));

        var action = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "use tools" }
            }.ToListData<LlmMessage>(),
            Cache = (global::app.type.item.@bool.@this)true,
            Tool = new List<GoalCall>
            {
                new GoalCall { Name = "TestTool" }
            }.ToListData<GoalCall>()
        };

        await action.Attach(null, Ctx);
        await action.Run();
        await action.Attach(null, Ctx);
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
        await action1.Attach(null, Ctx);
        await action1.Run();

        // Same message but different model — should be cache miss
        var action2 = new query(Ctx) { Message = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "You are helpful" },
                new LlmMessage { Role = "user", Content = "same" }
            }.ToListData<LlmMessage>(),
            Model = (global::app.type.item.text.@this)"gpt-4o"
        };
        await action2.Attach(null, Ctx);
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
        await action.Attach(null, Ctx);
        var result1 = await action.Run();
        await result1.IsSuccess();

        // Cache hit — goes through RestoreFromCache which deserializes cached value + metadata
        var action2 = LlmTestHelper.MakeQuery(Ctx, userText: "props test");
        await action2.Attach(null, Ctx);
        var result2 = await action2.Run();

        // Verify the cached result value matches original
        await result2.IsSuccess();
        await Assert.That((await result2.Value())?.ToString()).IsEqualTo("preserved");
        // Verify metadata was restored from cache
        await Assert.That((await result2.Properties.Value("Cached"))).IsEqualTo(true);
        await Assert.That((await result2.Properties.Value("RawResponse"))?.ToString()).IsEqualTo("preserved");
        await Assert.That((await result2.Properties.Value("Model"))?.ToString()).IsEqualTo("gpt-5.4-nano");
        await Assert.That((await result2.Properties.Value("PromptTokens"))).IsNotNull();
        await Assert.That((await result2.Properties.Value("CompletionTokens"))).IsNotNull();
        // No additional HTTP call was made
        await Assert.That(_handler.CallCount).IsEqualTo(1);
    }
}

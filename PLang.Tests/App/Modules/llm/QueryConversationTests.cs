using global::app.actor.context;
using global::app.variables;
using global::app.modules.llm;
using global::app.modules.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests ContinuePreviousConversation: message history management,
/// format instruction non-compounding, and schema reuse.
/// </summary>
public class QueryConversationTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang_test_llm_conv_" + Guid.NewGuid().ToString("N")[..8]);
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

    [Test]
    public async Task Query_ContinueConversation_PrependsPreviousMessages()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse($"answer {callIndex}")));
        };

        // First query
        var action1 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "You are helpful" },
                new LlmMessage { Role = "user", Content = "What is 2+2?" }
            },
            Cache = false
        };
        await action1.Run();

        // Second query with continuation
        var action2 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "And 3+3?" }
            },
            ContinuePreviousConversation = true,
            Cache = false
        };
        await action2.Run();

        // Second request should contain previous messages
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        // JSON escaping may turn + into \u002B
        await Assert.That(secondReq).Contains("What is 2");
        await Assert.That(secondReq).Contains("answer 1"); // assistant response from first query
        // JSON escaping may turn + into \u002B
        await Assert.That(secondReq).Contains("And 3");
    }

    [Test]
    public async Task Query_ContinueConversation_False_ClearsHistory()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("answer")));

        // First query — stores conversation
        var action1 = LlmTestHelper.MakeQuery(Ctx, userText: "first question");
        action1 = new query { Context = Ctx, Messages = action1.Messages, Cache = false };
        await action1.Run();

        // Second query with ContinuePreviousConversation=false — should clear
        var action2 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "fresh start" }
            },
            ContinuePreviousConversation = false,
            Cache = false
        };
        await action2.Run();

        // Second request should NOT contain first question
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).DoesNotContain("first question");
    }

    [Test]
    public async Task Query_FormatInstruction_DoesNotCompound()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("{\"ok\":true}")));

        // First query with schema
        var action1 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "analyze" },
                new LlmMessage { Role = "user", Content = "test" }
            },
            Schema = "{ok: bool}",
            Cache = false
        };
        await action1.Run();

        // Second query continuing conversation with same schema
        var action2 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "again" }
            },
            Schema = "{ok: bool}",
            ContinuePreviousConversation = true,
            Cache = false
        };
        await action2.Run();

        // The system message should NOT have doubled format instructions
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        // Count occurrences of the format instruction
        var count = secondReq.Split("You MUST respond in JSON").Length - 1;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_ContinueConversation_ReusesSchemaWhenNotSpecified()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("{\"result\":\"ok\"}")));

        // First query with schema
        var action1 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "system", Content = "analyze" },
                new LlmMessage { Role = "user", Content = "test" }
            },
            Schema = "{result: string}",
            Cache = false
        };
        await action1.Run();

        // Second query: no schema, continue conversation → should reuse
        var action2 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "again" }
            },
            ContinuePreviousConversation = true,
            Cache = false
        };
        await action2.Run();

        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("result: string");
    }

    [Test]
    public async Task Query_ContinueConversation_NewSchemaOverridesPrevious()
    {
        _handler.Handler = _ => Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("{\"data\":1}")));

        // First query with schema A
        var action1 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test" }
            },
            Schema = "{oldSchema: string}",
            Cache = false
        };
        await action1.Run();

        // Second query with schema B
        var action2 = new query
        {
            Context = Ctx,
            Messages = new List<LlmMessage>
            {
                new LlmMessage { Role = "user", Content = "test2" }
            },
            Schema = "{newSchema: int}",
            ContinuePreviousConversation = true,
            Cache = false
        };
        await action2.Run();

        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("newSchema");
        await Assert.That(secondReq).DoesNotContain("oldSchema");
    }
}

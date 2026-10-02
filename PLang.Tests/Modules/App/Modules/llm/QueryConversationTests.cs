using app.actor.context;
using app.type.item.variable;
using app.module.llm;
using app.module.llm.code;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Tests conversation.continue: a query continues the conversation the response it is given answered —
/// its messages carried on that response, format instructions not compounding, its schema reused.
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

    private async Task<global::app.data.@this> Ask(string user, global::app.data.@this? continues = null,
        global::app.data.@this? schema = null, bool cache = false)
    {
        var message = new List<LlmMessage> { new LlmMessage { Role = "user", Content = user } }.ToListData<LlmMessage>(Ctx);
        var action = continues == null
            ? new query(Ctx) { Message = message, Schema = schema, Cache = (global::app.type.item.@bool.@this)cache }
            : new query(Ctx) { Message = message, Schema = schema, Cache = (global::app.type.item.@bool.@this)cache,
                Conversation = new global::app.module.llm.type.conversation.@this(continues) };
        await action.Attach(null, Ctx);
        return await action.Start();
    }

    private void Answers(string content)
        => _handler.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(content)));

    [Test]
    public async Task Query_ContinueConversation_PrependsTheResponsesMessages()
    {
        int callIndex = 0;
        _handler.Handler = _ =>
        {
            callIndex++;
            return Task.FromResult(LlmTestHelper.JsonResponse(
                LlmTestHelper.MakeCompletionResponse($"answer {callIndex}")));
        };

        var first = await Ask("What is 2+2?");
        await Ask("And 3+3?", continues: first);

        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        // JSON escaping may turn + into +
        await Assert.That(secondReq).Contains("What is 2");
        await Assert.That(secondReq).Contains("answer 1"); // assistant response from first query
        await Assert.That(secondReq).Contains("And 3");
    }

    [Test]
    public async Task Query_ConversationLeftOut_StartsAfresh()
    {
        Answers("answer");
        await Ask("first question");
        await Ask("fresh start");

        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).DoesNotContain("first question");
    }

    // Two queries that each continue the first one carry its conversation, not each other's.
    [Test]
    public async Task Query_ContinueConversation_IsTheResponseGiven_NotTheLastQuery()
    {
        Answers("answer");
        var first = await Ask("first question");
        await Ask("an unrelated question");
        await Ask("back to the first", continues: first);

        var thirdReq = await _handler.AllRequests[2].Content!.ReadAsStringAsync();
        await Assert.That(thirdReq).Contains("first question");
        await Assert.That(thirdReq).DoesNotContain("an unrelated question");
    }

    [Test]
    public async Task Query_FormatInstruction_DoesNotCompound()
    {
        Answers("{\"ok\":true}");
        var first = await Ask("test", schema: Ctx.Ok("{ok: bool}"));
        await Ask("again", continues: first, schema: Ctx.Ok("{ok: bool}"));

        // The system message should NOT have doubled format instructions
        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        var count = secondReq.Split("You MUST respond in JSON").Length - 1;
        await Assert.That(count).IsEqualTo(1);
    }

    [Test]
    public async Task Query_ContinueConversation_ReusesSchemaWhenNotSpecified()
    {
        Answers("{\"result\":\"ok\"}");
        var first = await Ask("test", schema: Ctx.Ok("{result: string}"));
        await Ask("again", continues: first);

        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("result: string");
    }

    [Test]
    public async Task Query_ContinueConversation_NewSchemaOverridesPrevious()
    {
        Answers("{\"data\":1}");
        var first = await Ask("test", schema: Ctx.Ok("{oldSchema: string}"));
        await Ask("test2", continues: first, schema: Ctx.Ok("{newSchema: int}"));

        var secondReq = await _handler.AllRequests[1].Content!.ReadAsStringAsync();
        await Assert.That(secondReq).Contains("newSchema");
        await Assert.That(secondReq).DoesNotContain("oldSchema");
    }

    // A response read from the cache carries its conversation as a live one does.
    [Test]
    public async Task Query_ContinueConversation_FromACacheHit()
    {
        Answers("answer");
        await Ask("cached question", cache: true);
        var hit = await Ask("cached question", cache: true);
        await Assert.That(await hit.Properties.Get<bool>("Cached")).IsTrue();

        await Ask("go on", continues: hit);
        var continued = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(continued).Contains("cached question");
        await Assert.That(continued).Contains("answer");
    }

    // As a step writes it: the response is set to a variable (variable.set relays it with its properties) and
    // named in the conversation.
    [Test]
    public async Task Step_ContinueNamesTheResponseVariable()
    {
        Answers("answer");
        await Ctx.Action("llm.query(Message=[{\"Role\":\"user\", \"Content\":\"remember 7\"}], Cache=false)").Start(Ctx);
        await (await Ctx.Action("variable.set(Name=%answer%, Value=%!data%)").Start(Ctx)).IsSuccess();
        var answer = await Ctx.Variable.Get("answer");
        await Assert.That(answer.Properties.Contains("Messages")).IsTrue();

        var next = await Ctx.Action("llm.query(Message=[{\"Role\":\"user\", \"Content\":\"what was it\"}], Cache=false, Conversation={continue: %answer%})").Start(Ctx);
        await next.IsSuccess();
        var continued = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(continued).Contains("remember 7");
    }

    // The plain pick, as the decider writes it: Conversation=%answer% continues the answer — the conversation is born
    // from the response.
    [Test]
    public async Task Step_ConversationIsTheResponseItself()
    {
        Answers("answer");
        await Ctx.Action("llm.query(Message=[{\"Role\":\"user\", \"Content\":\"remember 7\"}], Cache=false)").Start(Ctx);
        await (await Ctx.Action("variable.set(Name=%answer%, Value=%!data%)").Start(Ctx)).IsSuccess();

        var next = await Ctx.Action("llm.query(Message=[{\"Role\":\"user\", \"Content\":\"what was it\"}], Cache=false, Conversation=%answer%)").Start(Ctx);

        await next.IsSuccess();
        await Assert.That(await _handler.LastRequest!.Content!.ReadAsStringAsync()).Contains("remember 7");
    }

    // A value that is no llm answer continues nothing: the query says so, naming it.
    [Test]
    public async Task Step_ConversationOfAText_IsRefused_NamingIt()
    {
        Answers("answer");
        await Ctx.Variable.Set("greeting", "hello");

        var read = await Ctx.Action("llm.query(Message=[{\"Role\":\"user\", \"Content\":\"x\"}], Cache=false, Conversation=%greeting%)").Start(Ctx);

        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Message).Contains("a conversation continues an llm answer; %greeting% isn't one");
    }

    // continue takes a response, never a yes: a bare true names no conversation.
    [Test]
    public async Task Step_ContinueTrue_IsRefused()
    {
        Answers("answer");
        var read = await Ctx.Action("llm.query(Message=[{\"Role\":\"user\", \"Content\":\"x\"}], Cache=false, Conversation={continue: true})").Start(Ctx);
        await Assert.That(read.Success).IsFalse();
        await Assert.That(read.Error!.Message).Contains("continue which conversation");
    }
}

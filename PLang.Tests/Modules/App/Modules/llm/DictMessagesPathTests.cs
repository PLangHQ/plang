using System.Collections.Generic;
using app.module.llm;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// Does the BUILDER's message path survive (C)? The builder does `set %messages% = [{Role, Content}]`
/// then `llm.query Message=%messages%` — i.e. the Message is a list of dicts (as variable.set stored
/// them), read into the typed list&lt;message&gt; slot. This exercises that end to end through OpenAi and
/// asserts it does NOT throw and the content reaches the body. (If dict→message now routes through the
/// reflection binder, it throws "cannot lower a String into text".)
/// </summary>
public class DictMessagesPathTests
{
    private string _tempDir = null!;
    private PLangEngine _app = null!;
    private MockHttpMessageHandler _handler = null!;

    [Before(Test)]
    public void Setup()
    {
        _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_test_llm_dictmsg_" + System.Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(_tempDir);
        _app = new PLangEngine(_tempDir).Testing();
        _handler = LlmTestHelper.SetupMockHttp(_app);
        _handler.Handler = _ => System.Threading.Tasks.Task.FromResult(
            LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse("ok")));
    }

    [After(Test)]
    public async Task Cleanup()
    {
        try { await _app.DisposeAsync(); if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true); }
        catch { }
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    [Test]
    public async Task DictListMessages_ThroughQuery_DoNotThrow_AndReachTheBody()
    {
        // the builder's shape: %messages% = [{Role, Content}] with a %var% in the content
        await Ctx.Variable.Set("name", "World");
        // the REAL path: %messages% is a plang list of plang DICTS (as `set` stores it) — build each
        // element as a plang dict (not a raw CLR Dictionary), so the list materializes through its
        // Born/Lower → message.Create, not the strict lower door.
        var tmpl = new global::app.type.item.text.@this("say hi to %name%", new global::app.type.item.template.kind.plang.@this());
        var raw = new List<object?>
        {
            global::app.type.item.@this.Create(new Dictionary<string, object?> { ["Role"] = "system", ["Content"] = "be brief" }, Ctx),
            global::app.type.item.@this.Create(new Dictionary<string, object?> { ["Role"] = "user", ["Content"] = tmpl }, Ctx),
        };
        var plang = global::app.type.item.@this.Create(raw, Ctx);
        var message = new global::app.data.@this("Message", plang,
            new global::app.type.@this("list", "message"), context: Ctx)
            .As<global::app.type.item.list.@this<LlmMessage>>();

        var action = new query(Ctx) { Message = message };
        await action.Attach(null, Ctx);
        await action.Start();

        var body = await _handler.LastRequest!.Content!.ReadAsStringAsync();
        await Assert.That(body).Contains("be brief");
        await Assert.That(body).Contains("say hi to World");
    }
}

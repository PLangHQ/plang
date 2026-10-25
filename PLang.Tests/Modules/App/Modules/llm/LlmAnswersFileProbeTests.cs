using app.module.llm;
using app.module.llm.code;

namespace PLang.Tests.App.Modules.llm;

/// <summary>Security probe (bugs full pass): a saved answer is served only to the request it was asked for. Asserts the
/// SAFE behaviour: red is a request answered by another's saved answer.</summary>
public class LlmAnswersFileProbeTests
{
    private const string Goal = "/q/Ask.test.goal";
    private string _root = null!;

    [Before(Test)]
    public void Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_llm_probe_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, "q"));
    }

    [After(Test)]
    public void Cleanup() { try { System.IO.Directory.Delete(_root, true); } catch (System.IO.IOException) { } }

    private async Task<(string? Answer, MockHttpMessageHandler OpenAi)> Run(string question, string answer)
    {
        await using var app = new global::app.@this(_root, global::PLang.Tests.Parent.Actor).Testing();
        var openAi = LlmTestHelper.SetupMockHttp(app);
        openAi.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(answer)));
        var context = app.actor.list.User.Context;
        var saved = new Saved(global::app.type.item.path.@this.Resolve(Goal, context), app.Code.Get<IAnswer>().Provider!);
        app.Code.Register<IAnswer>(saved);
        app.Code.SetDefault<IAnswer>(saved.Name);
        var query = LlmTestHelper.MakeQuery(context, userText: question);
        await query.Attach(null, context);
        var result = await query.Start();
        await result.IsSuccess();
        await (await saved.Keep(context)).IsSuccess();
        return ((await result.Value())?.ToString(), openAi);
    }

    [Test]
    public async Task ARequestDifferingOnlyInCase_IsNotServedTheOtherAnswer()
    {
        await Run("Delete the files", "no");
        var other = await Run("DELETE THE FILES", "yes");
        await Assert.That(other.OpenAi.CallCount).IsEqualTo(1);
        await Assert.That(other.Answer).IsEqualTo("yes");
    }

    [Test]
    public async Task ARequestWithATrailingSpace_IsNotServedTheOtherAnswer()
    {
        await Run("Is it safe?", "no");
        var other = await Run("Is it safe? ", "yes");
        await Assert.That(other.OpenAi.CallCount).IsEqualTo(1);
    }
}

using app.module.llm;
using app.module.llm.code;

namespace PLang.Tests.App.Modules.llm;

/// <summary>
/// A test's llm answers come from the file beside it: a question not in the file is asked once and added, and a later
/// run reads it without asking; a changed prompt is a new question; the key is never written. Each run here is an app
/// of its own (its LlmCache empty), its requests answered by a mock OpenAI, the test's saved answers in front of it as
/// a test registers them.
/// </summary>
public class LlmAnswersFileTests
{
    private const string Goal = "/q/Ask.test.goal";
    private string _root = null!;

    [Before(Test)]
    public void Setup()
    {
        _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang_llm_answers_" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, "q"));
    }

    [After(Test)]
    public void Cleanup()
    {
        try { System.IO.Directory.Delete(_root, true); } catch (System.IO.IOException) { }
    }

    private string File => System.IO.Path.Combine(_root, "q", "Ask.test.llm.json");

    // one run: an app of its own, the test's answers in front of a mock OpenAI that answers `answer`; what the query
    // got, how many requests reached OpenAI, and what was sent
    private async Task<(string? Answer, MockHttpMessageHandler OpenAi)> Run(string question, string answer, string? key = null)
    {
        await using var app = new global::app.@this(_root, global::PLang.Tests.Parent.Actor).Testing();
        var openAi = LlmTestHelper.SetupMockHttp(app);
        openAi.Handler = _ => Task.FromResult(LlmTestHelper.JsonResponse(LlmTestHelper.MakeCompletionResponse(answer)));
        var context = app.actor.list.User.Context;
        if (key != null)
            await (await new global::app.type.item.variable.parser.@this("%!llm.setting.key%", app.type.list).Variable.Single()
                .Set(context.Ok(context.Text(key)), context)).IsSuccess();

        var saved = new Saved(global::app.type.item.path.@this.Resolve(Goal, context), app.Code.Get<IAnswer>().Provider!, context);
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
    public async Task ANewQuestion_IsAskedOnce_ThenRead()
    {
        var first = await Run("What is 2+2?", "four");
        await Assert.That(first.OpenAi.CallCount).IsEqualTo(1).Because("a question not in the file goes to OpenAI");
        await Assert.That(System.IO.File.Exists(File)).IsTrue().Because("its answer is added to the file");

        var second = await Run("What is 2+2?", "not asked");
        await Assert.That(second.OpenAi.CallCount).IsEqualTo(0).Because("the file holds the question");
        await Assert.That(second.Answer).IsEqualTo("four");
    }

    [Test]
    public async Task AChangedPrompt_IsNotServedTheOldAnswer()
    {
        await Run("What is 2+2?", "four");

        var changed = await Run("What is 3+3?", "six");
        await Assert.That(changed.OpenAi.CallCount).IsEqualTo(1).Because("a changed prompt is a new question");
        await Assert.That(changed.Answer).IsEqualTo("six");

        var file = await System.IO.File.ReadAllTextAsync(File);
        await Assert.That(file).Contains("3+3").Because("the new question is added");
        await Assert.That(file).DoesNotContain("2+2").Because("the file keeps what the run asked, no more");
    }

    [Test]
    public async Task TheKey_IsNeverInTheFile()
    {
        const string key = "sk-test-never-written-7Q2";
        var run = await Run("What is 2+2?", "four", key);

        await Assert.That(run.OpenAi.LastRequest!.Headers.Authorization?.Parameter).IsEqualTo(key).Because("the key went with the request");
        await Assert.That(await System.IO.File.ReadAllTextAsync(File)).DoesNotContain(key);
    }

    // Two of plang's own llm tests, run at once by the test runner, each in its app: each file holds both tests'
    // questions, so a request answered from the other test's file still never goes live, and only the file's own
    // question is left in it after a pass. A test answered from the other's file would leave its own untouched.
    [Test]
    public async Task TwoTestsInParallel_EachReadsItsOwnFile()
    {
        string[] names = ["LlmQuery", "LlmFormat"];
        var llm = System.IO.Path.Combine(_root, "module", "llm");
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(llm, ".build"));
        var source = System.IO.Path.Combine(Fixture.Root(), "test", "module", "llm");
        var entries = names.ToDictionary(name => name, name =>
            System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(source, name + ".test.llm.json")))!.AsArray().Single()!);
        foreach (var name in names)
        {
            System.IO.File.Copy(System.IO.Path.Combine(source, name + ".test.goal"), System.IO.Path.Combine(llm, name + ".test.goal"));
            var pr = name.ToLowerInvariant() + ".test.pr";
            System.IO.File.Copy(System.IO.Path.Combine(source, ".build", pr), System.IO.Path.Combine(llm, ".build", pr));
            var both = new System.Text.Json.Nodes.JsonArray(names.Select(each => entries[each].DeepClone()).ToArray());
            System.IO.File.WriteAllText(System.IO.Path.Combine(llm, name + ".test.llm.json"), both.ToJsonString());
        }

        await using var app = new global::app.@this(_root, global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        await (await context.Setting.Flag("app.test.setting", new Dictionary<string, object?> { ["parallel"] = 2 }, context)).IsSuccess();
        var discovered = await new global::app.module.test.discover(context)
        {
            Path = context.Ok<global::app.type.item.path.@this>(global::app.type.item.path.@this.Resolve(".", context)),
            Pattern = new global::app.data.@this<global::app.type.item.text.@this>("Pattern", "*.test.goal", context: context),
            Subfolder = new global::app.data.@this<global::app.type.item.choice.@this<global::app.type.item.path.subfolder>>("Subfolder",
                context.Choice(global::app.type.item.path.subfolder.include), context: context),
        }.Start();
        var tests = discovered.GetValue<List<global::app.test.@this>>()!;
        var ran = await new global::app.module.test.start(context) { Tests = tests.ToListData<global::app.test.@this>(context) }.Start();
        var runs = ((global::app.type.item.list.@this)(await ran.Value())!).Items(context).Select(row => (global::app.test.@this)row.Peek()).ToList();

        await Assert.That(runs.Count).IsEqualTo(2);
        await runs.AssertAllPass();
        foreach (var name in names)
        {
            var kept = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(llm, name + ".test.llm.json")))!.AsArray();
            await Assert.That(kept.Count).IsEqualTo(1).Because($"{name} asked its own question and no other");
            await Assert.That(kept[0]!["request"]!.ToJsonString()).IsEqualTo(entries[name]["request"]!.ToJsonString());
        }
    }
}

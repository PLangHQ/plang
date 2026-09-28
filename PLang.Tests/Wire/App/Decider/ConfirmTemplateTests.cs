namespace PLang.Tests.App.Decider;

// BuildGoal/Start.goal ConfirmNumbers: the numbers build.match found unwritten (UnwrittenNumber's Details.numbers)
// render into one decider noul each (confirm.template) and a state naming their steps and actions
// (confirm.state.template).
public class ConfirmTemplateTests
{
    private static string RepoRoot()
    {
        var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "os", "system", "builder")))
            dir = dir.Parent;
        return dir!.FullName;
    }

    private static async Task<string> Rendered(string template, global::app.actor.context.@this context)
    {
        var render = new global::app.module.ui.Render(context)
        {
            Template = (global::app.type.item.text.@this)System.IO.File.ReadAllText(
                System.IO.Path.Combine(RepoRoot(), "os", "system", "builder", "llm", "templates", template)),
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.ui.code.Fluid().Render(render);
        return result.Success ? (await result.Value())!.ToString() : $"render failed: {result.Error!.Message}";
    }

    [Test]
    public async Task EachNumber_IsOneNoulUnderItsId_AndTheStateNamesItsStepAndAction()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.actor.list.User.Context;
        var numbers = new List<global::app.goal.step.unwritten.@this>
        {
            new(1, "on.error", "RetryCount", "1", "call Flaky, on error retry once, ignore"),
        };
        context.Variable.Set(new global::app.data.@this("numbers", numbers, context: context));
        context.Variable.Set(new global::app.data.@this("modules", context.App.module.list, context: context));

        var questions = System.Text.Json.Nodes.JsonNode.Parse(await Rendered("confirm.template", context))!.AsObject();
        var state = await Rendered("confirm.state.template", context);

        var asked = questions["s1_on.error.RetryCount=1"]!;
        await Assert.That(asked["type"]!.GetValue<string>()).IsEqualTo("noul");
        await Assert.That(asked["instructions"]!.GetValue<string>())
            .IsEqualTo("Step 1 is `call Flaky, on error retry once, ignore`. Its code writes on.error(RetryCount=1). Does step 1 give RetryCount the value 1?");
        await Assert.That(state).Contains("- step 1: call Flaky, on error retry once, ignore");
        await Assert.That(state).Contains("- on.error: Answer when the action before it fails");
    }

    // The eval's twin (tools/decider/prompt_c confirm_state / confirm_questions, via confirm_fixture.py): the same
    // numbers render the same state, byte for byte, and the same questions.
    [Test]
    public async Task TheStateAndQuestions_AreTheOnesPythonSends()
    {
        var golden = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(
            System.IO.Path.Combine(RepoRoot(), "PLang.Tests", "Wire", "App", "Decider", "confirm_golden.json"))).RootElement;
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.actor.list.User.Context;
        var numbers = golden.GetProperty("numbers").EnumerateArray().Select(n => new global::app.goal.step.unwritten.@this(
            n.GetProperty("step").GetInt32(), n.GetProperty("action").GetString()!, n.GetProperty("property").GetString()!,
            n.GetProperty("value").GetString()!, n.GetProperty("text").GetString()!)).ToList();
        context.Variable.Set(new global::app.data.@this("numbers", numbers, context: context));
        context.Variable.Set(new global::app.data.@this("modules", context.App.module.list, context: context));

        var state = await Rendered("confirm.state.template", context);
        var questions = System.Text.Json.Nodes.JsonNode.Parse(await Rendered("confirm.template", context))!;

        await Assert.That(state).IsEqualTo(golden.GetProperty("state").GetString());
        await Assert.That(System.Text.Json.Nodes.JsonNode.DeepEquals(questions,
            System.Text.Json.Nodes.JsonNode.Parse(golden.GetProperty("questions").GetRawText()))).IsTrue();
        await Assert.That(numbers.Select(n => n.Id)).IsEquivalentTo(
            golden.GetProperty("numbers").EnumerateArray().Select(n => n.GetProperty("id").GetString()!));
    }
}

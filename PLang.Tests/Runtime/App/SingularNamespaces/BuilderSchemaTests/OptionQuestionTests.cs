namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// An option whose notes line asks (<c>· ask:</c>) is asked of the decider beside the step's actions — a choice over
/// its own values and "none" — and a chosen value enters the starting line (<c>file.read(Path, Template=plang)</c>)
/// when its action is certain.
/// </summary>
public class OptionQuestionTests : System.IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "option-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this _app;

    public OptionQuestionTests()
    {
        // the app's own teaching for file.read: its Template line asks
        var notes = System.IO.Path.Combine(_root, "system", "modules", "file");
        System.IO.Directory.CreateDirectory(notes);
        System.IO.File.WriteAllText(System.IO.Path.Combine(notes, "read.notes.md"),
            "Path — the file to read\n" +
            "Template — the kind of template the text is · ask: does the step fill the file's variables from memory (load vars)?\n");
        _app = new global::app.@this(_root).Testing().Building();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync()
    {
        await _app.DisposeAsync();
        System.IO.Directory.Delete(_root, recursive: true);
    }

    private global::app.actor.context.@this Ctx => _app.actor.list.System.Context;

    // the step, its stage-1 answer (file.read at `certain`), and then the option answered `chosen` (none: not answered)
    private async Task<global::app.goal.step.pick.list.@this> Pick(double certain, string? chosen)
    {
        var goal = Make.Goal(_app.actor.list.User.Context, "G", Make.Step("read 'note.txt', load vars"));
        var first = new Dictionary<string, object?> { ["s0_file.read"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = certain } };
        var step = goal.Step[0];
        await step.Pick.Take(Make.Dict(first, Ctx), [], Ctx);
        if (chosen != null)
        {
            var second = new Dictionary<string, object?>
            {
                ["s0_@option.file.read.Template"] = new Dictionary<string, object?> { ["choice"] = chosen, ["confidence"] = 0.9 },
            };
            await step.Pick.Take(Make.Dict(second, Ctx), [], Ctx);
        }
        return step.Pick;
    }

    [Test]
    public async Task AnOptionThatAsks_IsAChoiceOfItsValuesAndNone()
    {
        var pick = await Pick(0.99, null);

        var question = pick.Question.Single(q => q.Kind == global::app.goal.step.pick.question.Kind.Option);
        await Assert.That(question.Id).IsEqualTo("s0_@option.file.read.Template");
        await Assert.That(question.Values).IsEquivalentTo(new[] { "plang", "none" });
        await Assert.That(question.Ask?.ToString()).Contains("load vars");
    }

    [Test]
    public async Task AChosenValue_EntersTheStartingLine()
        => await Assert.That((await Pick(0.99, "plang")).Formal).Contains("file.read(Path, Template=plang)");

    [Test]
    public async Task None_LeavesTheOptionOut()
        => await Assert.That((await Pick(0.99, "none")).Formal).DoesNotContain("Template");

    // a module stage 2 asks the action of: its actions' options are asked beside "which action" (crypto.hash's
    // Algorithm is never a common action — it is reached this way)
    [Test]
    public async Task AModuleAskedItsAction_IsAskedItsActionsOptions()
    {
        var goal = Make.Goal(_app.actor.list.User.Context, "G", Make.Step("read 'note.txt', load vars"));
        var first = new Dictionary<string, object?>
        {
            ["s0_@module"] = new Dictionary<string, object?> { ["choice"] = "file", ["confidence"] = 0.7 },
        };
        await goal.Step[0].Pick.Take(Make.Dict(first, Ctx), [], Ctx);

        await Assert.That(goal.Step[0].Pick.Question.Select(q => q.Id)).Contains("s0_@option.file.read.Template");
    }

    // an action the decider isn't certain of starts no line: its option's answer is unused
    [Test]
    public async Task AnAnswer_ForAnActionNotCertain_IsUnused()
        => await Assert.That((await Pick(0.3, "plang")).Formal ?? "").DoesNotContain("Template");
}

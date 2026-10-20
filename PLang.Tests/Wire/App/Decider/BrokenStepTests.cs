namespace PLang.Tests.App.Decider;

// A step the build cannot fix is written into the .pr as broken, and its goal does not start (decision 93).
// The step holds why it is refused (Step.Error: its list the gate messages), the .pr writes it as `error` beside
// `warning` and reads it back, and one door where a goal starts answers it — before step 1, nothing run.
// An unfixable step needs no LLM here: a formal step naming an action that does not exist stays refused.
public class BrokenStepTests : System.IAsyncDisposable
{
    private readonly string _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "broken-" + System.Guid.NewGuid().ToString("N")[..8]);
    private readonly global::app.@this _loadApp;

    public BrokenStepTests()
    {
        System.IO.Directory.CreateDirectory(System.IO.Path.Combine(_root, ".build"));
        _loadApp = global::PLang.Tests.Session.App(_root).Testing();
    }

    public async System.Threading.Tasks.ValueTask DisposeAsync() => await _loadApp.DisposeAsync();

    private static global::app.goal.@this Parse(string text, global::app.actor.context.@this context)
        => global::app.goal.@this.Parse(text, global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;

    // A goal whose step 0 builds and whose step 1 stays refused after any retry: built in the os app.
    private static async Task<(global::app.goal.@this Goal, global::app.actor.context.@this Context)> Built(
        global::app.@this os, string source, string answer = "")
    {
        var context = os.actor.list.User.Context;
        var goal = Parse(source, context);
        await goal.Step.Read(answer, context);
        return (goal, context);
    }

    // The goal's .pr text, saved under the load app's root and loaded back from there.
    private async Task<global::app.goal.@this> Reloaded(global::app.goal.@this built, global::app.actor.context.@this context)
    {
        System.IO.File.WriteAllText(System.IO.Path.Combine(_root, ".build", "start.pr"), await context.Pr(built));
        var loaded = await _loadApp.goal.Load("/Start.goal");
        await loaded.IsSuccess();
        return (global::app.goal.@this)(await loaded.Value())!;
    }

    private const string OneBroken = "Start\n- output.write(Data=\"STEPONE\")\n- output.shout(Data=\"a\")\n";

    [Test]
    public async Task AStepThatStaysRefused_HoldsItsError_AndTheOthersKeepTheirCode()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var (goal, _) = await Built(os, OneBroken);

        // the step that built keeps its code and holds no error
        await Assert.That(goal.Step[0].Error).IsNull();
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(1);
        // the refused one holds why — its text, and each reason the gate gave as a cause of its own
        var error = goal.Step[1].Error;
        await Assert.That(error).IsNotNull();
        await Assert.That(error!.Message).Contains("step 1");
        await Assert.That(error.Message).Contains("output.shout");
        await Assert.That(error.list.Count).IsGreaterThan(0);
        await Assert.That(error.list.Any(c => c.Message.Contains("does not read"))).IsTrue();
        await Assert.That(goal.Step[1].Code.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TheRefusalOfAnAnswer_NamesTheGoalItIsAbout()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var context = os.actor.list.User.Context;
        var goal = Parse(OneBroken, context);

        var refused = await goal.Step.Read("", context);

        // a build that ends on this refusal names the goal by its path
        await Assert.That(refused).IsNotNull();
        await Assert.That(refused!.Key).IsEqualTo("StepsRefused");
        await Assert.That(refused.Goal).IsNotNull();
        await Assert.That(refused.Goal!.Path.ToString()).Contains("/Start.goal");
    }

    [Test]
    public async Task AStepThatBuildsOnTheNextRead_LosesItsError()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var context = os.actor.list.User.Context;
        var goal = Parse("Start\n- write out \"b\"\n", context);

        // no entry in the answer: refused, and it says why
        await goal.Step.Read("", context);
        await Assert.That(goal.Step[0].Error).IsNotNull();
        await Assert.That(goal.Step[0].Error!.list.Any(c => c.Message.Contains("has no entry"))).IsTrue();

        // answered on the retry (the decider has listed output.write for it): it takes its code, and the error is gone
        var picks = Make.Dict(new Dictionary<string, object?>
        {
            ["s0_output.write"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 },
        }, context);
        await goal.Step[0].Pick.Take(picks, [], context);
        var refused = await goal.Step.Read("[0] output.write(Data=\"b\")", context);

        await Assert.That(refused?.Message).IsNull();
        await Assert.That(goal.Step[0].Code.Count).IsEqualTo(1);
        await Assert.That(goal.Step[0].Error).IsNull();
    }

    [Test]
    public async Task ABrokenStep_IsWrittenAsError_AndReadBack()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var (built, context) = await Built(os, OneBroken);

        var pr = System.Text.Json.Nodes.JsonNode.Parse(await context.Pr(built))!;
        // beside `warning`, only on the broken step: its key, its message, and its causes
        await Assert.That(pr["step"]![0]!["error"]).IsNull();
        var written = pr["step"]![1]!["error"];
        await Assert.That(written).IsNotNull();
        await Assert.That(written!["message"]!.GetValue<string>()).Contains("output.shout");
        await Assert.That(written["list"]!.AsArray().Count).IsGreaterThan(0);
        // the error writes itself, as it does everywhere: its id and status ride too, not a hand-built subset
        await Assert.That(written["key"]!.GetValue<string>()).IsEqualTo("StepBroken");
        await Assert.That(written["id"]).IsNotNull();
        await Assert.That(written["status"]).IsNotNull();

        var loaded = await Reloaded(built, context);
        await Assert.That(loaded.Step[0].Error).IsNull();
        var read = loaded.Step[1].Error;
        await Assert.That(read).IsNotNull();
        await Assert.That(read!.Key).IsEqualTo(built.Step[1].Error!.Key);
        await Assert.That(read.Message).IsEqualTo(built.Step[1].Error!.Message);
        await Assert.That(read.list.Select(c => c.Message).ToArray())
            .IsEquivalentTo(built.Step[1].Error!.list.Select(c => c.Message).ToArray());
    }

    [Test]
    public async Task AGoalWithABrokenStep_FailsToStart_BeforeStepOne_NamingThePathTheStepAndWhy()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var (built, buildContext) = await Built(os, OneBroken);
        var goal = await Reloaded(built, buildContext);
        var capture = new CapturedOutput(_loadApp);
        var context = _loadApp.actor.list.User.Context;

        var ran = await goal.Start(context);

        await Assert.That(ran.Success).IsFalse();
        // one error for the goal: its path and the broken step's index; the step's own error rides whole as its cause
        await Assert.That(ran.Error!.Key).IsEqualTo("GoalBroken");
        await Assert.That(ran.Error.Message).Contains("/Start.goal");
        await Assert.That(ran.Error.Message).Contains("1");
        // how to fix it is the error's FixSuggestion (the error page prints it as "Fix:"), not part of the message
        await Assert.That(ran.Error.FixSuggestion).IsEqualTo("fix the words and build again");
        await Assert.That(ran.Error.Message).DoesNotContain("fix the words");
        await Assert.That(ran.Error.Goal).IsNotNull();
        await Assert.That(ran.Error.list.Count).IsEqualTo(1);
        await Assert.That(ran.Error.list[0].Message).Contains("output.shout");
        await Assert.That(ran.Error.list[0].list.Count).IsGreaterThan(0);
        // step 0 built, and still it did not run: the goal failed before step 1
        await Assert.That(capture.Text).DoesNotContain("STEPONE");
    }

    [Test]
    public async Task AGoalWithoutABrokenStep_StillStarts()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var (built, buildContext) = await Built(os, "Start\n- output.write(Data=\"STEPONE\")\n");
        var goal = await Reloaded(built, buildContext);
        var capture = new CapturedOutput(_loadApp);
        var context = _loadApp.actor.list.User.Context;

        var ran = await goal.Start(context);

        await Assert.That(ran.Success).IsTrue();
        await Assert.That(capture.Text).Contains("STEPONE");
    }

    [Test]
    public async Task AGoalWithABrokenStep_IsNotRefusedAtSave_ButAStepWithNoCodeAndNoErrorStillIs()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var (goal, context) = await Built(os, OneBroken);

        // a broken step has no actions, and says why: that is its verdict, so the goal is saved with it
        await Assert.That(await goal.Validate(context)).IsNull();

        // a step that is simply unbuilt (no code, no error) is still a half-built artefact: refused
        var unbuilt = Parse("Start\n- write out \"b\"\n", context);
        var invalid = await unbuilt.Validate(context);
        await Assert.That(invalid).IsNotNull();
        await Assert.That(invalid!.Message).Contains("no actions");
    }

    // A step the build could not fix has no code, so no gate to fold its block into. That is not a second mistake:
    // the step is already broken. The fold leaves it be and keeps the steps under it, and the goal is saved broken.
    [Test]
    public async Task ABrokenStepWithStepsIndentedUnderIt_IsNotAFoldError_AndKeepsItsBlock()
    {
        await using var app = global::PLang.Tests.Session.App("/test").Testing();
        var context = app.actor.list.System.Context;
        var goal = Make.Goal(context, "G",
            Make.Step("if %x% = 1"),
            Make.Step("write out inside", 1, Make.Action(context, "output", "write", ("Content", "inside"))),
            Make.Step("write out after", Make.Action(context, "output", "write", ("Content", "after"))));
        goal.Step[0].Error = new global::app.error.@this("step 0 (\"if %x% = 1\") is broken", "StepBroken", 400);

        var action = new global::app.module.build.fold(context) { Goal = context.Ok<global::app.goal.@this>(goal) };
        var result = await new global::app.module.build.code.Default().Fold(action);

        await Assert.That(result.Success).IsTrue();
        // all three steps are still in the goal, the block under the broken step among them
        await Assert.That(goal.Step.Count).IsEqualTo(3);
        await Assert.That(goal.Step[1].Text).IsEqualTo("write out inside");
        await Assert.That(goal.Step[1].Code.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ABrokenStep_IsNotCached_SoTheNextBuildRetriesIt()
    {
        await using var os = global::PLang.Tests.Session.App(System.IO.Path.Combine(Fixture.Root(), "os")).Testing().Building();
        var (goal, _) = await Built(os, OneBroken);

        // it has no code, so even with its text unchanged since the last build the cache must not skip it
        await Assert.That(goal.Step[1].IsCached).IsFalse();
        await Assert.That(goal.Step.IsCached).IsFalse();
    }
}

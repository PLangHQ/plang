namespace PLang.Tests.App.Decider;

// An unchanged step is not rebuilt: its text is canonical at birth, goal.Merge gives it back its saved
// code, and a cached step is asked nothing — no decider question, `=> cached` in prompt C, its answer
// line set aside — so its code in the new .pr is byte-equal to the old. A cached step whose saved code
// no longer holds is opened again, with a warning. A goal whose source and steps are all cached isn't
// built again.
public class KeptStepTests
{
    private static string RepoRoot()
    {
        var dir = System.AppContext.BaseDirectory;
        while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir, "PLang", "app")))
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        return dir!;
    }

    private const string First = "Start\n- write out \"a\"\n- write out \"b\"\n- write out \"c\"\n";
    // step 1's text changed; step 2 gained trailing spaces and a \r\n — canonical, it is the same step
    private const string Second = "Start\r\n- write out \"a\"\r\n- write out \"B\"\r\n- write out \"c\"   \r\n";

    private static global::app.goal.@this Parse(string text, global::app.actor.context.@this context)
        => global::app.goal.@this.Parse(text, global::app.type.item.path.@this.Resolve("/Start.goal", context), context)!;

    // Each step's code as build.match gives it — its line read through formal.
    private static void Built(global::app.goal.@this goal, global::app.actor.context.@this context, params string[] lines)
    {
        for (int i = 0; i < lines.Length; i++)
        {
            var read = new global::app.goal.step.action.serializer.Formal(goal.Step[i]).Read(lines[i], context);
            goal.Step[i].Code = (global::app.goal.step.action.list.@this)read.Peek()!;
        }
    }

    private static async Task<string> Pr(global::app.goal.@this goal, global::app.@this app) =>
        await ((global::app.channel.serializer.plang.@this)app.User.Channel.Serializers.GetOrDefault("application/plang")).Text(goal);

    private static System.Text.Json.Nodes.JsonNode Code(string pr, int step) =>
        System.Text.Json.Nodes.JsonNode.Parse(pr)!["step"]![step]!["code"]!;

    private static async Task Picked(global::app.goal.@this goal, global::app.actor.context.@this context, int step, string action)
    {
        var answer = Make.Dict(new Dictionary<string, object?>
        {
            [$"s{step}_{action}"] = new Dictionary<string, object?> { ["type"] = "noul", ["noul"] = 0.99 },
        }, context);
        foreach (var s in goal.Step.Items()) await s.Pick.Take(answer, [], context);
    }

    [Test]
    public async Task ARebuild_AsksOnlyTheChangedStep_AndTheKeptCodeIsByteEqual()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;

        var first = Parse(First, context);
        Built(first, context, "output.write(Data=\"a\")", "output.write(Data=\"b\")", "output.write(Data=\"c\")");
        var before = await Pr(first, os);

        var second = Parse(Second, context);
        second.Merge(await RealGoalLoad.Read(os, before));
        await second.Reopen(context);

        await Assert.That(second.Step[2].Text).IsEqualTo("write out \"c\"");
        await Assert.That(second.Step.Items().Select(s => s.IsCached).ToList()).IsEquivalentTo(new[] { true, false, true });

        // only the changed step is asked of the decider, and prompt C marks the others kept
        await Picked(second, context, 1, "output.write");
        await Assert.That(second.Step[0].Pick.Question.Count + second.Step[2].Pick.Question.Count).IsEqualTo(0);
        await Assert.That(second.Step[0].Pick.Listed.Count).IsEqualTo(0);

        // the answer's lines for kept steps are set aside; the changed step takes its code
        var kept = second.Step[0].Code;
        var match = new global::app.module.action.build.match(context)
        {
            Goal = context.Ok<global::app.goal.@this>(second),
            Answer = context.Ok<global::app.type.item.text.@this>(
                "[0] output.write(Data=\"x\")\n[1] output.write(Data=\"B\")\n[2] output.write(Data=\"y\")"),
        };
        var matched = await new global::app.module.action.build.code.Default().Match(match);
        await matched.IsSuccess();
        await Assert.That(second.Step[0].Code).IsSameReferenceAs(kept);

        var after = await Pr(second, os);
        await Assert.That(Code(after, 0).ToJsonString()).IsEqualTo(Code(before, 0).ToJsonString());
        await Assert.That(Code(after, 2).ToJsonString()).IsEqualTo(Code(before, 2).ToJsonString());
        await Assert.That(Code(after, 1).ToJsonString()).Contains("\"B\"");
    }

    [Test]
    public async Task AKeptStepWhoseCodeNoLongerHolds_IsOpenedAgain_WithAWarning()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;

        var first = Parse(First, context);
        Built(first, context, "output.write()", "output.write(Data=\"b\")", "output.write(Data=\"c\")");   // step 0 misses its Data
        var second = Parse(First, context);
        second.Merge(await RealGoalLoad.Read(os, await Pr(first, os)));

        await second.Reopen(context);

        await Assert.That(second.Step[0].Code.Count).IsEqualTo(0);
        await Assert.That(second.Step[0].IsCached).IsFalse();
        await Assert.That(second.Step[0].Warning.Single().Key).IsEqualTo("Reopened");
        await Assert.That(second.Step[1].IsCached).IsTrue();
        await Assert.That(second.IsCached).IsFalse();
    }

    // The goal its .pr was built from, unchanged, is cached — and the build reads that through the goal.
    [Test]
    public async Task TheSourceItsPrWasBuiltFrom_IsCached()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;

        var first = Parse(First, context);
        Built(first, context, "output.write(Data=\"a\")", "output.write(Data=\"b\")", "output.write(Data=\"c\")");
        var second = Parse(First, context);
        second.Merge(await RealGoalLoad.Read(os, await Pr(first, os)));
        await second.Reopen(context);

        await Assert.That(second.IsCached).IsTrue();
        await Assert.That(second.Cache!.Hash).IsEqualTo(second.Hash);
        await context.Variable.Set("goal", second);
        await Assert.That((await (await context.Variable.Get("goal.IsCached")).Value())?.ToString()).IsEqualTo("true");
        await Assert.That((await (await context.Variable.Get("goal.Step.IsCached")).Value())?.ToString()).IsEqualTo("true");

        // the build's guard: `if %goal.IsCached%` — a bare if, Left's own truth
        var guard = new global::app.module.action.condition.If(context) { Left = await context.Variable.Get("goal.IsCached") };
        var answer = await new global::app.module.action.condition.code.Default().Evaluate(guard);
        await Assert.That((await answer.Value())?.ToString()).IsEqualTo("true");
    }

    // A step deleted: the steps left are cached, but the goal isn't — its .pr is written again.
    [Test]
    public async Task AStepDeleted_ItsStepsAreCached_TheGoalIsNot()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;

        var first = Parse(First, context);
        Built(first, context, "output.write(Data=\"a\")", "output.write(Data=\"b\")", "output.write(Data=\"c\")");
        var second = Parse("Start\n- write out \"a\"\n- write out \"c\"\n", context);
        second.Merge(await RealGoalLoad.Read(os, await Pr(first, os)));
        await second.Reopen(context);

        await Assert.That(second.Step.IsCached).IsTrue();
        await Assert.That(second.IsCached).IsFalse();
    }

    // A changed step: neither its steps nor the goal are cached.
    [Test]
    public async Task AStepChanged_NothingIsCached()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(RepoRoot(), "os"));
        var context = os.User.Context;

        var first = Parse(First, context);
        Built(first, context, "output.write(Data=\"a\")", "output.write(Data=\"b\")", "output.write(Data=\"c\")");
        var second = Parse(Second, context);
        second.Merge(await RealGoalLoad.Read(os, await Pr(first, os)));
        await second.Reopen(context);

        await Assert.That(second.Step.IsCached).IsFalse();
        await Assert.That(second.IsCached).IsFalse();
    }
}

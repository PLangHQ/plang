namespace PLang.Tests.App.Decider;

// The builder's own critical steps, as the installed start.pr holds them. The builder rebuilds itself, and
// a step mapped wrong there breaks every build after it — so the steps the build's flow stands on are
// pinned: a self-rebuild that maps them differently fails here, loudly.
public class BuilderPinTests
{
    private static async Task<global::app.goal.@this> Installed(global::app.@this os) =>
        await RealGoalLoad.Read(os, await System.IO.File.ReadAllTextAsync(System.IO.Path.Combine(
            BootstrapTests.RepoRoot(), "os", "system", "builder", "BuildGoal", ".build", "start.pr")));

    // a property's value as written (a text literal reads back quoted)
    private static string? Value(global::app.goal.step.action.@this action, string name) => action[name]?.Value?.ToString()?.Trim('"');

    // an on.error clause's Recovery — the actions its property holds
    private static IEnumerable<global::app.goal.step.action.@this> Recovery(global::app.goal.step.action.@this onError)
        => ((global::app.goal.step.action.list.@this)onError["Recovery"]!.Value!).Items();

    // `if %goal.IsCached%, return %goal.Cache%` — a bare if (Left's own truth) whose body returns the cache
    [Test]
    public async Task Start_ACachedGoalReturnsItsCache()
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os")).Testing();
        var start = await Installed(os);

        var guard = start.Step[0].Code[0];
        await Assert.That($"{guard.Module.Name}.{guard.Name}").IsEqualTo("condition.if");
        await Assert.That(Value(guard, "Left")).IsEqualTo("%goal.IsCached%");
        await Assert.That(guard["Operator"]).IsNull();
        await Assert.That(guard["Right"]).IsNull();
        var body = guard.Child.Items().Single().Code[0];
        await Assert.That($"{body.Module.Name}.{body.Name}").IsEqualTo("goal.return");
        await Assert.That(Value(body, "Data")).IsEqualTo("%goal.Cache%");
    }

    // `build.match(…); on.error(Key="UnwrittenNumber", …ConfirmNumbers); on.error(Key="ElseWithoutIf", …SourceError);
    // on.error(…FixSteps)` — its clauses follow it as siblings, asked in the order written: numbers the words don't
    // write as digits are confirmed, an else apart from its if goes to the programmer, anything else is fixed
    [Test]
    public async Task Compile_ARefusedAnswerIsConfirmedOrFixed_InTheOrderWritten()
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os")).Testing();
        var compile = (await Installed(os)).Child.Items().Single(g => g.Name == "Compile");

        var step = compile.Step.Items().Single(s => s.Code[0] is { Module.Name: "build", Name: "match" });
        var clauses = step.Code.Items().Skip(1).Where(a => a.Module.Name == "on" && a.Name == "error").ToList();
        await Assert.That(clauses.Select(c => Value(c, "Key") ?? "*")).IsEquivalentTo(new[] { "UnwrittenNumber", "ElseWithoutIf", "*" },
            TUnit.Assertions.Enums.CollectionOrdering.Matching);
        await Assert.That(clauses.Select(c => Value(Recovery(c).Single(), "Name")))
            .IsEquivalentTo(new[] { "ConfirmNumbers", "SourceError", "FixSteps" }, TUnit.Assertions.Enums.CollectionOrdering.Matching);
    }

    // FixSteps answers again and matches itself, so numbers left after the fix are confirmed too
    [Test]
    public async Task FixSteps_MatchesAgain_AndConfirmsNumbers()
    {
        await using var os = new global::app.@this(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os")).Testing();
        var fix = (await Installed(os)).Child.Items().Single(g => g.Name == "FixSteps");

        var step = fix.Step.Items().Single(s => s.Code[0] is { Module.Name: "build", Name: "match" });
        var clause = step.Code.Items().Skip(1).Single();
        await Assert.That(Value(clause, "Key")).IsEqualTo("UnwrittenNumber");
        await Assert.That(Value(Recovery(clause).Single(), "Name")).IsEqualTo("ConfirmNumbers");
    }
}

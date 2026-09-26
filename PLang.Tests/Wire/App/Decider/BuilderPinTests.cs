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

    // `if %goal.IsCached%, return %goal.Cache%` — a bare if (Left's own truth) whose body returns the cache
    [Test]
    public async Task Start_ACachedGoalReturnsItsCache()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os"));
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

    // `build.match …, on error key "ElseWithoutIf" call SourceError, on error call FixSteps first, then
    // retry 1 times` — the refused answer is fixed FIRST, then matched again
    [Test]
    public async Task Compile_ARefusedAnswerIsFixedThenMatchedAgain()
    {
        await using var os = TestApp.Create(System.IO.Path.Combine(BootstrapTests.RepoRoot(), "os"));
        var compile = (await Installed(os)).Child.Single(g => g.Name == "Compile");

        var match = compile.Step[3].Code[0];
        await Assert.That($"{match.Module.Name}.{match.Name}").IsEqualTo("build.match");
        var source = match.Modifier.Single(m => Value(m, "Key") == "ElseWithoutIf");
        await Assert.That(Value(source.Recovery.Items().Single(), "Name")).IsEqualTo("SourceError");
        var fix = match.Modifier.Single(m => m != source);
        await Assert.That(Value(fix.Recovery.Items().Single(), "Name")).IsEqualTo("FixSteps");
        await Assert.That(Value(fix, "RetryCount")).IsEqualTo("1");
        await Assert.That(Value(fix, "Order")).IsEqualTo("GoalFirst");
    }
}

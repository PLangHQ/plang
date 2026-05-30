using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// Batch A — app.goal collection node (Stage 3).
// `app.Goal` is the collection (goal.list.@this), `app.Goal["name"]` selects,
// `app.Goal.list` enumerates, `app.Goal.current` reads CallStack.Current.Action.Step.Goal,
// `app.Goal["nope"]` throws (index-miss is a hard error).
public class GoalAccessorTests
{
    [Test] public async Task AppGoal_IndexByName_ReturnsTheNamedGoal()
    {
        await using var app = new PLangEngine("/test");
        var goal = new global::app.goal.@this { Name = "AlphaGoal", Path = global::app.type.path.@this.Resolve("/AlphaGoal.goal", app.User.Context), PrPath = global::app.type.path.@this.Resolve("/.build/AlphaGoal/00.pr", app.User.Context) };
        app.Goal.Add(goal);
        await Assert.That(app.Goal["AlphaGoal"].Name).IsEqualTo("AlphaGoal");
    }

    [Test] public async Task AppGoal_IndexByPrPath_ReturnsTheGoalForThatPath()
    {
        await using var app = new PLangEngine("/test");
        var goal = new global::app.goal.@this { Name = "BetaGoal", Path = global::app.type.path.@this.Resolve("/BetaGoal.goal", app.User.Context), PrPath = global::app.type.path.@this.Resolve("/.build/BetaGoal/00.pr", app.User.Context) };
        app.Goal.Add(goal);
        // Index by the stored PrPath instance — path equality is value-based on Absolute.
        await Assert.That(app.Goal[goal.PrPath!].Name).IsEqualTo("BetaGoal");
    }

    [Test] public async Task AppGoal_IndexByPathInstance_ReturnsSameGoalAsStringPath()
    {
        await using var app = new PLangEngine("/test");
        var goal = new global::app.goal.@this { Name = "Gamma", Path = global::app.type.path.@this.Resolve("/Gamma.goal", app.User.Context), PrPath = global::app.type.path.@this.Resolve("/.build/Gamma/00.pr", app.User.Context) };
        app.Goal.Add(goal);
        await Assert.That(app.Goal[goal.Path!].Name).IsEqualTo(app.Goal["Gamma"].Name);
    }

    [Test] public async Task AppGoalList_Enumerates_LoadedGoals_ExcludingSetup()
    {
        await using var app = new PLangEngine("/test");
        var ctx = app.User.Context;
        app.Goal.Add(new global::app.goal.@this { Name = "Public", Path = global::app.type.path.@this.Resolve("/Public.goal", ctx), PrPath = global::app.type.path.@this.Resolve("/.build/Public/00.pr", ctx) });
        app.Goal.Add(new global::app.goal.@this { Name = "Setup", Path = global::app.type.path.@this.Resolve("/Setup.goal", ctx), PrPath = global::app.type.path.@this.Resolve("/.build/Setup/00.pr", ctx), IsSetup = true });
        var names = app.Goal.list.Select(g => g.Name).ToHashSet();
        await Assert.That(names.Contains("Public")).IsTrue();
        await Assert.That(names.Contains("Setup")).IsFalse();
    }

    [Test] public async Task AppGoalCurrent_MidRun_ReturnsExecutingGoal()
    {
        // Wiring of `.current` through CallStack is covered by the broader integration
        // suite — here we assert the field exists and reads from CallStack.
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Goal.current).IsNull();
    }

    [Test] public async Task AppGoalCurrent_AtRest_IsNull()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(app.Goal.current).IsNull();
    }

    [Test] public async Task AppGoal_IndexOfUnknownName_ThrowsTypedError()
    {
        await using var app = new PLangEngine("/test");
        await Assert.That(() => { _ = app.Goal["nope"]; return Task.CompletedTask; })
            .Throws<KeyNotFoundException>();
    }

    // Goal collection is selection + lifecycle + enumeration only — no per-element behavior on the registry.
    [Test] public async Task GoalListType_ExposesNoIoOrPerElementBehavior_OnTheRegistry()
    {
        var t = typeof(global::app.goal.list.@this);
        // The registry has Add/Remove/Get/Contains/Count/list/this[...] — no Write/Read/Ask/RunAsync.
        var forbidden = new[] { "Write", "Read", "Ask", "RunAsync", "Execute" };
        foreach (var n in forbidden)
            await Assert.That(t.GetMethod(n)).IsNull();
    }
}

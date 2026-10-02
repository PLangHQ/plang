using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using PLangEngine = global::app.@this;

namespace PLang.Tests.App.SingularNamespaces.AccessorTests;

// app.goal is the type named goal: `list` holds the goals read so far, Get(address) picks one as a result
// (NotFound when none answers), `current` is the running goal, `all` every goal of the app.
public class GoalAccessorTests
{
    private static global::app.goal.@this Goal(PLangEngine app, string name)
        => new() { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", app.actor.list.User.Context) };

    [Test] public async Task AppGoal_GetByAddress_ReturnsTheGoal()
    {
        await using var app = new global::app.@this("/test").Testing();
        var goal = Goal(app, "AlphaGoal");
        app.goal.list.Add(goal);

        var found = await app.goal.Get("/AlphaGoal");

        await found.IsSuccess();
        await Assert.That((await found.Value())!.Name).IsEqualTo("AlphaGoal");
    }

    // The builder's Keys line (properties.template) teaches %!app.goal["/show"]% and %!app.module["file"]%:
    // both read, as written, in a real App — a goal's key is its address, matched without case.
    [Test] public async Task PromptLineExamples_Resolve_AsWritten()
    {
        await using var app = new global::app.@this("/test").Testing();
        app.goal.list.Add(Goal(app, "Show"));
        var context = app.actor.list.User.Context;

        var goal = await new global::app.type.item.variable.parser.@this("%!app.goal[\"/show\"]%").Variable.Single().Start(context);
        await goal.IsSuccess();
        await Assert.That((await goal.Value() as global::app.goal.@this)!.Name).IsEqualTo("Show");

        var module = await new global::app.type.item.variable.parser.@this("%!app.module[\"file\"]%").Variable.Single().Start(context);
        await module.IsSuccess();
        await Assert.That((await module.Value() as global::app.module.@this)!.Name).IsEqualTo("file");
    }

    [Test] public async Task AppGoal_GetOfUnknownAddress_IsNotFound()
    {
        await using var app = new global::app.@this("/test").Testing();

        var found = await app.goal.Get("/nope");

        await found.IsFailure();
        await Assert.That(found.Error!.Key).IsEqualTo("NotFound");
    }

    [Test] public async Task AppGoalList_HoldsTheGoalsRead_AllLeavesOutSetup()
    {
        await using var app = new global::app.@this("/test").Testing();
        app.goal.list.Add(Goal(app, "Public"));
        app.goal.list.Add(Goal(app, "Setup"));

        var appOnly = new global::app.type.item.dict.@this();
        appOnly.Set("os", false);
        var all = (global::app.type.item.list.@this<global::app.goal.@this>)await app.goal.list.all(app.actor.list.System.Context!, appOnly);

        await Assert.That(all.Items().Select(g => g.Name).ToList()).IsEquivalentTo(new[] { "Public" });
    }

    [Test] public async Task AppGoalCurrent_IsTheRunningGoal()
    {
        await using var app = new global::app.@this("/test").Testing();
        var context = app.actor.list.User.Context;
        var goal = Goal(app, "Running");
        await using var running = context.CallStack.Push(goal);

        var current = app.goal.current(context);

        await current.IsSuccess();
        await Assert.That(await current.Value()).IsEqualTo(goal);
    }

    // The goal list does the list's work (holding, reading, finding a call's goal) — it declares no
    // I/O or running verbs of its own.
    [Test] public async Task GoalListType_DeclaresNoIoOrRunningVerbs()
    {
        var t = typeof(global::app.goal.list.@this);
        var own = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
        foreach (var n in new[] { "Write", "Read", "Ask", "Start" })
            await Assert.That(t.GetMethod(n, own)).IsNull();
    }
}

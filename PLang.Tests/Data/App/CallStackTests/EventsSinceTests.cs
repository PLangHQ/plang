using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class EventsSinceTests
{
    private static (global::app.@this app, ActionEntity action) BuildLive(string name)
    {
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var goal = new Goal(app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", app.actor.list.User.Context) };
        var step = new Step(app.type.list) { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity(app.type.list) { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = step };
        step.Code.Add(action); goal.Step.Add(step);
        app.goal.list.Add(goal);
        return (app, action);
    }

    [Test]
    public async Task EventsSince_ReturnsDiffEvents_WithTimestampGreaterThan()
    {
        var (app, action) = BuildLive("EvtA");
        var stack = app.actor.list.User.Context.call;
        var vars = app.actor.list.User.Context.Variable;
        app.actor.list.User.Context.Setting.Set("app.call.setting.diff.enabled", true);

        await using var call = stack.Push(action, await stack.Setting(), vars);

        var t = DateTimeOffset.UtcNow;
        await Task.Delay(5);
        vars.Set("x", 1);
        vars.Set("y", "two");

        var events = stack.EventsSince(t).ToList();
        await Assert.That(events.Count).IsGreaterThanOrEqualTo(2);
        await Assert.That(events.Any(e => e.Name == "x")).IsTrue();
        await Assert.That(events.Any(e => e.Name == "y")).IsTrue();
    }

    [Test]
    public async Task EventsSince_EmptyWhenNoMutations()
    {
        var (app, action) = BuildLive("EvtB");
        var stack = app.actor.list.User.Context.call;
        app.actor.list.User.Context.Setting.Set("app.call.setting.diff.enabled", true);
        await using var call = stack.Push(action, await stack.Setting(), app.actor.list.User.Context.Variable);

        var future = DateTimeOffset.UtcNow.AddSeconds(10);
        var events = stack.EventsSince(future);
        await Assert.That(events).IsNotNull();
        await Assert.That(events.Any()).IsFalse();
    }

    // A change kept as it was is the value's own copy: a value no change reaches (a text) is held as it is, never a walk
    // of everything it reaches (a task's value reaches the whole app).
    [Test]
    public async Task AChangeKeptAsItWas_HoldsTheValuesOwnCopy()
    {
        var (app, _) = BuildLive("EvtD");
        var text = new global::app.type.item.text.@this(new string('x', 300), app.type.list[typeof(global::app.type.item.text.@this)]);

        var diff = new global::app.call.Diff("t", text, deep: true);

        await Assert.That(diff.Before).IsSameReferenceAs(text);
    }

    // A frame on the current chain sits in the tree under the root as well: its changes come once.
    [Test]
    public async Task EventsSince_YieldsEachFramesChangesOnce()
    {
        var (app, action) = BuildLive("EvtC");
        var stack = app.actor.list.User.Context.call;
        var vars = app.actor.list.User.Context.Variable;
        app.actor.list.User.Context.Setting.Set("app.call.setting.diff.enabled", true);
        await using var call = stack.Push(action, await stack.Setting(), vars);

        var t = DateTimeOffset.UtcNow;
        await Task.Delay(5);
        await vars.Set("x", 1);

        await Assert.That(stack.EventsSince(t).Count(e => e.Name == "x")).IsEqualTo(1);
    }
}

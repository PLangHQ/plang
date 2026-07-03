using ActionEntity = app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class EventsSinceTests
{
    private static (global::app.@this app, ActionEntity action) BuildLive(string name)
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        var goal = new Goal { Name = name, Path = global::app.type.path.@this.Resolve($"/{name}.goal", global::PLang.Tests.TestApp.SharedContext) };
        var step = new Step { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity { Module = "test", ActionName = "test" };
        action.Step = step; step.Actions.Add(action); goal.Steps.Add(step);
        goal.Steps.Context = app.User.Context;
        app.Goal.Add(goal);
        return (app, action);
    }

    [Test]
    public async Task EventsSince_ReturnsDiffEvents_WithTimestampGreaterThan()
    {
        var (app, action) = BuildLive("EvtA");
        var stack = app.CallStack;
        var vars = app.User.Context.Variable;
        stack.Variables = vars;
        stack.Flags = stack.Flags with { Diff = true };

        await using var call = stack.Push(action, vars);

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
        var stack = app.CallStack;
        stack.Flags = stack.Flags with { Diff = true };
        await using var call = stack.Push(action, app.User.Context.Variable);

        var future = DateTimeOffset.UtcNow.AddSeconds(10);
        var events = stack.EventsSince(future);
        await Assert.That(events).IsNotNull();
        await Assert.That(events.Any()).IsFalse();
    }
}

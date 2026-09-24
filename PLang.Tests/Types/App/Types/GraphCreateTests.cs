namespace PLang.Tests.App.Types;

/// <summary>
/// Program structure has one way in — its reader. A goal, step or action held in a typed slot passes
/// through unchanged; any other value (a dict shaped like one included) is declined with the node's
/// own message, never converted.
/// </summary>
public class GraphCreateTests
{
    private static global::app.data.@this DictSlot(global::app.actor.context.@this ctx)
    {
        var dict = new global::app.type.item.dict.@this();
        dict.Set(new global::app.data.@this("name", "Start", context: ctx));
        return new global::app.data.@this("node", dict, context: ctx);
    }

    [Test]
    public async Task Goal_FromDict_IsDeclined()
    {
        await using var app = TestApp.Create("/t");
        var slot = DictSlot(app.User.Context);
        await Assert.That(await slot.Value<global::app.goal.@this>()).IsNull();
        await Assert.That(slot.Error?.Message).Contains("a goal is read from its .pr, never converted from a value");
        await Assert.That(slot.Error?.Key).IsEqualTo("CreateItemDeclined");
    }

    [Test]
    public async Task Step_FromDict_IsDeclined()
    {
        await using var app = TestApp.Create("/t");
        var slot = DictSlot(app.User.Context);
        await Assert.That(await slot.Value<global::app.goal.step.@this>()).IsNull();
        await Assert.That(slot.Error?.Message).Contains("a step is built by its goal, never converted from a value");
    }

    [Test]
    public async Task Action_FromDict_IsDeclined()
    {
        await using var app = TestApp.Create("/t");
        var slot = DictSlot(app.User.Context);
        await Assert.That(await slot.Value<global::app.goal.step.action.@this>()).IsNull();
        await Assert.That(slot.Error?.Message).Contains("an action is built by its step, never converted from a value");
    }

    [Test]
    public async Task Goal_PassesThrough()
    {
        await using var app = TestApp.Create("/t");
        var goal = new global::app.goal.@this { Name = "Start" };
        var slot = new global::app.data.@this("node", goal, context: app.User.Context);
        await Assert.That(await slot.Value<global::app.goal.@this>()).IsSameReferenceAs(goal);
    }

    [Test]
    public async Task Step_PassesThrough()
    {
        await using var app = TestApp.Create("/t");
        var step = new global::app.goal.step.@this { Index = 0, Text = "write out 'hi'" };
        var slot = new global::app.data.@this("node", step, context: app.User.Context);
        await Assert.That(await slot.Value<global::app.goal.step.@this>()).IsSameReferenceAs(step);
    }

    [Test]
    public async Task Action_PassesThrough()
    {
        await using var app = TestApp.Create("/t");
        var action = new global::app.goal.step.action.@this { Module = app.Module["output"], Name = "write" };
        var slot = new global::app.data.@this("node", action, context: app.User.Context);
        await Assert.That(await slot.Value<global::app.goal.step.action.@this>()).IsSameReferenceAs(action);
    }
}

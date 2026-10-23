namespace PLang.Tests.App.Types;

// A null under `item` (no declaration: any value) is the null type; a null under a real declaration keeps it. A json null
// and a literal `set %x% = null` are the same null.
public class NullUnderItemTests
{
    [Test]
    public async Task ANullUnderItem_IsTheNullType()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;

        await Assert.That(app.type.list["item"].Make(null, ctx).Type.Name).IsEqualTo("null");
    }

    [Test]
    public async Task ANullUnderADeclaration_KeepsIt()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;

        var held = app.type.list["path"].Make(null, ctx);

        await Assert.That(held.IsNull).IsTrue();
        await Assert.That(held.Type.Name).IsEqualTo("path");
    }

    // Through the .pr: `variable.set(Name=%x%, Value=null)` built and read back, its Value row {type: item, value: null}.
    [Test]
    public async Task SetToNull_ThroughThePr_ItsTypeIsNull()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        const string line = "variable.set(Name=%x%, Value=null)";
        var step = new global::app.goal.step.@this(app.type.list) { Text = line };
        var read = await new global::app.goal.step.action.formal.Reader(step, app.module.list).Read(line, ctx);
        await read.IsSuccess();
        var goal = await RealGoalLoad.ViaChannel(app, Make.Goal(ctx, "Start",
            Make.Step(line, ((global::app.goal.step.action.list.@this)read.Peek()!).Items().ToArray())));

        await (await app.Start(goal, ctx)).IsSuccess();

        var name = await new global::app.type.item.variable.@this("x!type.name", app.type.list).Start(ctx);
        await Assert.That((await name.Value())?.ToString()).IsEqualTo("null");
    }

    [Test]
    public async Task SetToNull_ItsTypeIsNull()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;

        await (await Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "%x%", "variable"), ("Value", null)).Start(ctx)).IsSuccess();

        var name = await new global::app.type.item.variable.@this("x!type.name", app.type.list).Start(ctx);
        await Assert.That((await name.Value())?.ToString()).IsEqualTo("null");
    }
}

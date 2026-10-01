namespace PLang.Tests.App;

/// <summary>
/// A concept execution is inside (goal, actor, test, error) is a node with a current: a dot past its own members
/// reads the current's member, brackets pick one by name. A concept nothing is ever inside (type, module) picks by
/// name with a dot too.
/// </summary>
public class CurrentNodeTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    [Test] public async Task Brackets_PickAnActorByName()
    {
        await using var app = new global::app.@this("/test").Testing();
        var user = await Read("%!app.actor[\"user\"]%", app.actor.list.System.Context);
        await Assert.That(user.Peek()).IsSameReferenceAs(app.actor.list.User);
    }

    // A dot: the type's facts first (%!goal.Name% is the type's name), then the current's member; .current reads the
    // running one.
    [Test] public async Task Dot_TheTypesFacts_ThenTheCurrentsMember()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var goal = new global::app.goal.@this { Name = "Checkout",
            Path = global::app.type.item.path.@this.Resolve("/Checkout.goal", ctx) };
        await using var frame = ctx.CallStack.Push(goal);

        await Assert.That((await (await Read("%!app.goal.Name%", ctx)).Value())?.ToString()).IsEqualTo("goal");
        await Assert.That((await (await Read("%!app.goal.current.Name%", ctx)).Value())?.ToString()).IsEqualTo("Checkout");
        await Assert.That((await Read("%!app.goal.Path%", ctx)).Peek()).IsSameReferenceAs(goal.Path);
        // a name is not a member of the current actor: the dot never picks by name
        await Assert.That((await Read("%!app.actor.user%", ctx)).IsInitialized).IsFalse();
    }

    [Test] public async Task Dot_WithNothingInside_IsNotFound()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!app.goal.Path%", app.actor.list.User.Context);
        await Assert.That(read.IsInitialized).IsFalse();
    }

    // The error node: its list is every error on the asker's call stack, one picked by its key in brackets.
    [Test] public async Task ErrorList_IsEveryErrorOnTheCallStack()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        ctx.CallStack.Audit.Add(new global::app.error.Error("the disk is full", "DiskFull", 507));
        ctx.CallStack.Audit.Add(new global::app.error.Error("quota reached", "Quota", 507));

        var list = await Read("%!app.error.list%", ctx);
        await Assert.That(list.Peek()).IsSameReferenceAs(ctx.CallStack.Audit);
        var picked = await Read("%!app.error[\"Quota\"].Message%", ctx);
        await Assert.That((await picked.Value())?.ToString()).IsEqualTo("quota reached");
    }

    [Test] public async Task Error_WithNoneInPlay_HasNoCurrent()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!app.error.current%", app.actor.list.User.Context);
        await read.IsFailure();
        await Assert.That(read.Error!.Status.Code.ToInt32()).IsEqualTo(404);
    }

    // The app's module: its own .list is the collection; any other key is a module by name, an item's inner
    // members not the module's.
    [Test] public async Task Module_OwnListFirst_ThenAModuleByName()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await Assert.That((await Read("%!module.list%", ctx)).Peek()).IsSameReferenceAs(app.module.list);
        var variable = await Read("%!module.variable%", ctx);
        await Assert.That((variable.Peek() as global::app.module.@this)?.Name).IsEqualTo("variable");
        var listModule = await Read("%!module[\"list\"]%", ctx);
        await Assert.That((listModule.Peek() as global::app.module.@this)?.Name).IsEqualTo("list");
    }

    // A module an app member shadows is reached by its file path.
    [Test] public async Task Module_ByItsPath_ReachesItsActionsSettings()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        await ctx.Setting.Set("variable.set.setting.name", ctx.Ok("%x%"));
        var read = await Read("%!module.variable.set.setting.name%", ctx);
        await Assert.That((await read.Value())?.ToString()).IsEqualTo("%x%");
    }

    [Test] public async Task Type_PicksByName_ByDotOrBrackets()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var dot = await Read("%!app.type.goal%", ctx);
        var bracket = await Read("%!app.type[\"goal\"]%", ctx);
        await Assert.That(dot.Peek()).IsSameReferenceAs(app.goal);
        await Assert.That(bracket.Peek()).IsSameReferenceAs(app.goal);
    }
}

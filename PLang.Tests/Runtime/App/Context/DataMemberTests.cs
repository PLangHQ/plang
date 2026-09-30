namespace PLang.Tests.App.Context;

/// <summary>
/// <c>%!data%</c> is the asker's last action's result: unset before any action, then what the last one answered —
/// each actor its own.
/// </summary>
public class DataMemberTests
{
    private static async Task<global::app.data.@this> Read(global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this("%!data%").Variable.Single().Start(ctx);

    private static async Task<global::app.data.@this> SetX(global::app.actor.context.@this ctx, int value)
        => await global::PLang.Tests.Shared.Make.Action(ctx, "variable", "set",
            global::PLang.Tests.Shared.Make.Param(ctx, "Name", "%x%", "variable"), ("value", value)).Start(ctx);

    [Test] public async Task BeforeAnyAction_IsUnset()
    {
        await using var app = new global::app.@this("/test").Testing();
        await Assert.That((await Read(app.actor.list.User.Context)).IsInitialized).IsFalse();
    }

    [Test] public async Task AfterAnAction_IsItsResult()
    {
        await using var app = new global::app.@this("/test").Testing();
        var user = app.actor.list.User.Context;
        var result = await SetX(user, 5);
        await result.IsSuccess();

        await Assert.That((await Read(user)).Peek()).IsSameReferenceAs(result.Peek());
    }

    [Test] public async Task EachActor_ItsOwn()
    {
        await using var app = new global::app.@this("/test").Testing();
        var system = app.actor.list.System.Context;
        await (await SetX(system, 7)).IsSuccess();

        // the system's action is not the user's %!data%
        await Assert.That((await Read(app.actor.list.User.Context)).IsInitialized).IsFalse();
    }
}

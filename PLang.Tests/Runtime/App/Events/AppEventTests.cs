namespace PLang.Tests.App.Events;

/// <summary>
/// <c>%!app.event.current%</c> (what the shortcut <c>%!event%</c> reads) answers as its asker: the running event's call's
/// Data — its value the event, its properties what it ran for and the value so far. Unset outside one.
/// </summary>
public class AppEventTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text, ctx.App.type.list).Variable.Single().Start(ctx);

    [Test] public async Task InsideABoundCall_IsTheFramesData()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var ctx = app.actor.list.User.Context;
        var goal = new global::app.goal.@this(app.type.list) { Name = "Checkout",
            Path = global::app.type.item.path.@this.Resolve("/Checkout.goal", ctx) };
        var fired = ctx.Actor!.@event.Add(new global::app.@event.code.@this(app.goal.type, "start", global::app.@event.When.before,
            (_, result, _) => System.Threading.Tasks.Task.FromResult(result), ctx.Actor!,
            global::app.@event.code.@this.Always));
        // as the event's call scopes it: the event, what it ran for, the value so far — on the frame it came in
        var running = new global::app.data.@this("!event", fired, context: ctx);
        running.Properties.Set("item", goal);
        running.Properties.Set("data", ctx.Text("so far"));
        await using var frame = ctx.call.Push(goal, await ctx.call.Setting());
        frame.Event = running;

        var read = await Read("%!app.event.current%", ctx);
        await Assert.That(read).IsSameReferenceAs(running);
        await Assert.That(read.Peek()).IsSameReferenceAs(fired);
        await Assert.That((await Read("%!app.event.current!item%", ctx)).Peek()).IsSameReferenceAs(goal);
        await Assert.That((await (await Read("%!app.event.current!data%", ctx)).Value())?.ToString()).IsEqualTo("so far");
    }

    [Test] public async Task OutsideOne_IsUnset()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var read = await Read("%!app.event.current%", app.actor.list.User.Context);
        await Assert.That(read.IsInitialized).IsFalse();
    }
}

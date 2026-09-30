namespace PLang.Tests.App.Events;

/// <summary>
/// <c>%!app.event%</c> answers as its asker: the running bound call's Data — its value the event, its properties
/// what it fired for and the result so far. Unset outside one; the event's type is <c>%!app.type.event%</c>.
/// </summary>
public class AppEventTests
{
    private static async Task<global::app.data.@this> Read(string text, global::app.actor.context.@this ctx)
        => await new global::app.type.item.variable.parser.@this(text).Variable.Single().Start(ctx);

    [Test] public async Task InsideABoundCall_IsTheFramesData()
    {
        await using var app = new global::app.@this("/test").Testing();
        var ctx = app.actor.list.User.Context;
        var goal = new global::app.goal.@this { Name = "Checkout",
            Path = global::app.type.item.path.@this.Resolve("/Checkout.goal", ctx) };
        var fired = app.goal.on.start;
        // as the binding scopes it: the event, what it fired for, the result so far — on the frame it fired in
        var running = new global::app.data.@this("!event", fired, context: ctx);
        running.Properties.Set("item", goal);
        running.Properties.Set("result", new global::app.type.item.text.@this("so far"));
        await using var frame = ctx.CallStack.Push(goal);
        frame.Event = running;

        var read = await Read("%!app.event%", ctx);
        await Assert.That(read).IsSameReferenceAs(running);
        await Assert.That(read.Peek()).IsSameReferenceAs(fired);
        await Assert.That((await Read("%!app.event!item%", ctx)).Peek()).IsSameReferenceAs(goal);
        await Assert.That((await (await Read("%!app.event!result%", ctx)).Value())?.ToString()).IsEqualTo("so far");
    }

    [Test] public async Task OutsideOne_IsUnset()
    {
        await using var app = new global::app.@this("/test").Testing();
        var read = await Read("%!app.event%", app.actor.list.User.Context);
        await Assert.That(read.IsInitialized).IsFalse();
    }
}

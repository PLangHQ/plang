namespace PLang.Tests.App.Events;

// An item's list for an actor: loaded on its first ask with every event that runs for it, in the order bound; an event
// bound later joins it. Each side starts its events in the order bound, for the items their filter takes.
public class OnTests
{
    private static global::app.type.item.dict.@this Item(global::app.actor.context.@this ctx) => ctx.Dict();

    // A handler that records it ran and answers ok.
    private static System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, Task<global::app.data.@this>> Record(List<string> ran, string name)
        => (_, _, ctx) => { lock (ran) ran.Add(name); return Task.FromResult(ctx.Ok()); };

    // A handler that fails with key.
    private static System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, Task<global::app.data.@this>> Fail(string key)
        => (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.@this("no", key, 400)));

    // The user's event on item's moment, run by handler for every item.
    private static async Task<global::app.@event.@this> Bind(global::app.@this app, global::app.type.item.@this item, string name, global::app.@event.When when,
        System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, Task<global::app.data.@this>> handler)
        => await app.actor.list.User.@event.Add(new global::app.@event.code.@this(item, name, when, handler, app.actor.list.User, global::app.@event.code.@this.Always));

    [Test] public async Task AnItemNothingIsBoundOn_HasAnEmptyList_AndNoMomentOfAnUnknownName()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var item = Item(app.actor.list.User.Context);
        var on = (await app.actor.list.User.@event.On(item));
        await Assert.That(on.before.Count).IsEqualTo(0);
        await Assert.That(on["nosuchevent"]).IsNull();
    }

    [Test] public async Task AnItemsList_IsTheSameOnEveryAsk_AndAnEventBoundLaterJoinsIt()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();

        var on = (await app.actor.list.User.@event.On(item));
        await Bind(app, item, "start", global::app.@event.When.before, Record(ran, "one"));

        await Assert.That(ReferenceEquals((await app.actor.list.User.@event.On(item)), on)).IsTrue();
        await Assert.That(on.before.Count).IsEqualTo(1);
        await Assert.That((await app.actor.list.User.@event.On(Item(app.actor.list.User.Context))).before.Count).IsEqualTo(0);
    }

    [Test] public async Task Start_RunsInTheOrderBound_AndAnswersWhatItWasHanded()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var ran = new List<string>();
        await Bind(app, item, "start", global::app.@event.When.before, Record(ran, "first"));
        await Bind(app, item, "start", global::app.@event.When.before, Record(ran, "second"));

        var result = context.Ok("kept");
        var answered = await (await context.Actor!.@event.On(item)).before.Start(item, context, result);

        await Assert.That(ran).IsEquivalentTo(new[] { "first", "second" });
        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
    }

    [Test] public async Task Start_WithNothingBound_AnswersTheResultAsIs()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var result = context.Ok("kept");
        var answered = await (await context.Actor!.@event.On(item)).after.Start(item, result, context);
        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
        await Assert.That(await (await context.Actor!.@event.On(item)).before.Start(item, context)).IsNull();
    }

    [Test] public async Task AFailingBeforeEvent_IsTheResult_AndStopsTheRest()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var ran = new List<string>();
        await Bind(app, item, "start", global::app.@event.When.before, Fail("Refused"));
        await Bind(app, item, "start", global::app.@event.When.before, Record(ran, "after the failure"));

        var answered = (await (await context.Actor!.@event.On(item)).before.Start(item, context))!;

        await answered.IsFailure();
        await Assert.That(answered.Error!.Key).IsEqualTo("Refused");
        await Assert.That(ran).IsEmpty();
    }

    [Test] public async Task AHandledBeforeAnswer_Cancels_AndIsTheResult()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var ran = new List<string>();
        await Bind(app, item, "start", global::app.@event.When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        });
        await Bind(app, item, "start", global::app.@event.When.before, Record(ran, "after the cancel"));

        var answered = (await (await context.Actor!.@event.On(item)).before.Start(item, context))!;

        await Assert.That(answered.Handled).IsTrue();
        await Assert.That((await answered.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(ran).IsEmpty();
    }

    [Test] public async Task AFailingAfterEvent_IsTheResult_AndTheRestStillRunOnIt()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        global::app.data.@this? seen = null;
        await Bind(app, item, "start", global::app.@event.When.after, Fail("Broke"));
        await Bind(app, item, "start", global::app.@event.When.after, (_, result, ctx) => { seen = result; return Task.FromResult(ctx.Ok()); });

        var answered = await (await context.Actor!.@event.On(item)).after.Start(item, context.Ok("ran"), context);

        await answered.IsFailure();
        await Assert.That(answered.Error!.Key).IsEqualTo("Broke");
        await Assert.That(seen).IsNotNull();
        await Assert.That(seen!.Error?.Key).IsEqualTo("Broke");
    }

    [Test] public async Task AHandledAfterAnswer_MeansNothing()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var ran = new List<string>();
        await Bind(app, item, "start", global::app.@event.When.after, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        });
        await Bind(app, item, "start", global::app.@event.When.after, Record(ran, "still runs"));

        var result = context.Ok("ran");
        var answered = await (await context.Actor!.@event.On(item)).after.Start(item, result, context);

        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
        await Assert.That(ran).IsEquivalentTo(new[] { "still runs" });
    }

    [Test] public async Task AHandlerThatThrows_StillLetsItsEventRunTheNextTime()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var calls = 0;
        await Bind(app, item, "start", global::app.@event.When.before, (_, _, ctx) =>
        {
            if (++calls == 1) throw new InvalidOperationException("first call fails");
            return Task.FromResult(ctx.Ok());
        });

        await Assert.That(async () => await (await context.Actor!.@event.On(item)).before.Start(item, context)).Throws<InvalidOperationException>();
        await (await (await context.Actor!.@event.On(item)).before.Start(item, context))!.IsSuccess();
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test] public async Task AnEvent_DoesNotRunInsideItsOwnHandler()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var times = 0;
        await Bind(app, item, "start", global::app.@event.When.before, async (fired, result, ctx) =>
        {
            times++;
            return await (await ctx.Actor!.@event.On(fired)).before.Start(fired, ctx, result) ?? result;   // starts its own moment again
        });

        await (await context.Actor!.@event.On(item)).before.Start(item, context);

        await Assert.That(times).IsEqualTo(1);
    }

    // An error's before that is already running is passed by, never taken as answering: a failure its own run meets goes
    // on past it, so an on-error event can't swallow a failure by being re-entered.
    [Test] public async Task AFailure_GoesOnPastAnErrorEventAlreadyRunning()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var inner = 0;
        await Bind(app, item, "error", global::app.@event.When.before, async (fired, failed, ctx) =>
        {
            // its own moment again, inside its run: it is passed by, and the failure stands
            inner++;
            return await (await ctx.Actor!.@event.On(fired))["error"]!.before.Catch(fired, ctx.Error(new global::app.error.@this("inner", "Inner", 400)), ctx)
                ?? ctx.Error(new global::app.error.@this("inner", "Inner", 400));
        });

        var answer = await (await context.Actor!.@event.On(item))["error"]!.before.Catch(item, context.Error(new global::app.error.@this("outer", "Outer", 400)), context);

        await Assert.That(inner).IsEqualTo(1);
        await answer!.IsFailure();
        await Assert.That(answer!.Error!.Key).IsEqualTo("Inner");
    }

    [Test] public async Task AnActorsEvent_RunsOnlyForThatActor()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        await Bind(app, item, "set", global::app.@event.When.after, Record(ran, "user's"));

        var system = app.actor.list.System.Context;
        await (await system.Actor!.@event.On(item))["set"]!.after.Start(item, system.Ok(), system);
        await Assert.That(ran).IsEmpty();

        var user = app.actor.list.User.Context;
        await (await user.Actor!.@event.On(item))["set"]!.after.Start(item, user.Ok(), user);
        await Assert.That(ran).IsEquivalentTo(new[] { "user's" });
    }

    [Test] public async Task AFilter_TakesTheItemsItRunsFor()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var type = Item(context);
        var wanted = Item(context);
        var ran = new List<string>();
        await app.actor.list.User.@event.Add(new global::app.@event.code.@this(type, "create", global::app.@event.When.after, Record(ran, "fired"),
            app.actor.list.User, (fired, _) => fired == wanted));

        var create = (await context.Actor!.@event.On(type))["create"]!;
        await create.after.Start(Item(context), context.Ok(), context);
        await Assert.That(ran).IsEmpty();
        await create.after.Start(wanted, context.Ok(), context);
        await Assert.That(ran).IsEquivalentTo(new[] { "fired" });
    }

    [Test] public async Task ARemovedEvent_RunsNoMore_AndLeavesTheList()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        var ran = new List<string>();
        var @event = await Bind(app, item, "start", global::app.@event.When.after, Record(ran, "fired"));
        var on = (await context.Actor!.@event.On(item));

        @event.Remove();
        await on.after.Start(item, context.Ok(), context);

        await Assert.That(ran).IsEmpty();
        await Assert.That(on.after.Count).IsEqualTo(0);
        await Assert.That(context.Actor!.@event.Items()).IsEmpty();
    }

    [Test] public async Task AClone_IsANewValue_WithNothingBound()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(context);
        await item.Set("a", false, 1, context);
        await Bind(app, item, "start", global::app.@event.When.after, Record(new List<string>(), "x"));

        var copy = item.Clone();

        await Assert.That((await context.Actor!.@event.On(copy)).after.Count).IsEqualTo(0);
        await Assert.That((await context.Actor!.@event.On(item)).after.Count).IsEqualTo(1);
    }

    // `on` stays off the wire: a value with an event bound writes the same bytes as one without.
    [Test] public async Task ABoundItem_WritesTheSameBytes_AsAnUnboundOne()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var plain = context.Dict();
        await plain.Set("a", false, 1, context);
        var bound = context.Dict();
        await bound.Set("a", false, 1, context);
        await Bind(app, bound, "start", global::app.@event.When.after, Record(new List<string>(), "x"));

        foreach (var view in new[] { global::app.View.Out, global::app.View.Store })
            await Assert.That(await Written(bound, view, context)).IsEqualTo(await Written(plain, view, context));
    }

    // `on` isn't taught as a property: a type's catalog facts don't list it.
    [Test] public async Task On_IsNotInATypesProperties()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var goal = app.type.list["goal"];
        await Assert.That(goal.Property?.Any(p => string.Equals(p.Name, "on", StringComparison.OrdinalIgnoreCase)) ?? false).IsFalse();
    }

    private static async Task<string> Written(global::app.type.item.@this item, global::app.View view, global::app.actor.context.@this context)
    {
        return await context.Pr(item, view);
    }
}

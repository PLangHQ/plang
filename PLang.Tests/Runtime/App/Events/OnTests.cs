using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.Events;

// An object's events: its class's shared `on` (closed) until an actor's first binding on it holds its own; each event's
// before/after bindings start in the order added, for their actor (or the app), for the items their filter takes.
public class OnTests
{
    private static global::app.type.item.dict.@this Item(global::app.actor.context.@this ctx) => ctx.Dict();

    // A handler that records it ran and answers ok.
    private static System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, Task<global::app.data.@this>> Record(List<string> ran, string name)
        => (_, _, ctx) => { lock (ran) ran.Add(name); return Task.FromResult(ctx.Ok()); };

    // A handler that fails with key.
    private static System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, Task<global::app.data.@this>> Fail(string key)
        => (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.@this("no", key, 400)));

    [Test] public async Task AnUnboundItem_HasTheSharedEmptyOn_WhoseEventsAreClosed()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var item = Item(app.actor.list.User.Context);
        await Assert.That(ReferenceEquals(item.on, global::app.@event.on.empty.Of(item.GetType()))).IsTrue();
        await Assert.That(item.on["start"]!.before.Count).IsEqualTo(0);
        await Assert.That(item.on["nosuchevent"]).IsNull();
    }

    [Test] public async Task TheFirstBinding_GivesTheItemItsOwnOn_AndEmptyNeverChanges()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();

        var events = app.@event.Add(item);
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, Record(ran, "one"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        await Assert.That(ReferenceEquals(app.@event.Add(item), events)).IsTrue();
        await Assert.That(ReferenceEquals(app.@event.Add(item), events)).IsTrue();
        await Assert.That(app.@event.Add(item).Add("start")!.before.Count).IsEqualTo(1);
        await Assert.That(global::app.@event.on.empty.Of(item.GetType())["start"]!.before.Count).IsEqualTo(0);
        await Assert.That(Item(app.actor.list.User.Context).on["start"]!.before.Count).IsEqualTo(0);
    }

    [Test] public async Task Start_RunsInTheOrderAdded_AndAnswersTheResultAsItStands()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, Record(ran, "first"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, Record(ran, "second"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        var result = context.Ok("kept");
        var answered = await app.@event.Add(item).Add("start")!.before.Start(item, result, context);

        await Assert.That(ran).IsEquivalentTo(new[] { "first", "second" });
        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
    }

    [Test] public async Task Start_WithNothingBound_AnswersTheResultAsIs()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var result = app.actor.list.User.Context.Ok("kept");
        var answered = await Item(app.actor.list.User.Context).on["start"]!.after.Start(Item(app.actor.list.User.Context), result, app.actor.list.User.Context);
        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
    }

    [Test] public async Task AFailingBeforeBinding_IsTheResult_AndStopsTheRest()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, Fail("Refused"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, Record(ran, "after the failure"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        var answered = await app.@event.Add(item).Add("start")!.before.Start(item, context.Ok(), context);

        await answered.IsFailure();
        await Assert.That(answered.Error!.Key).IsEqualTo("Refused");
        await Assert.That(ran).IsEmpty();
    }

    [Test] public async Task AHandledBeforeAnswer_Cancels_AndIsTheResult()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, Record(ran, "after the cancel"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        var answered = await app.@event.Add(item).Add("start")!.before.Start(item, context.Ok(), context);

        await Assert.That(answered.Handled).IsTrue();
        await Assert.That((await answered.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(ran).IsEmpty();
    }

    [Test] public async Task AFailingAfterBinding_IsTheResult_AndTheRestStillRunOnIt()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        global::app.data.@this? seen = null;
        app.@event.Add(item).Add("start")![When.after].Add(side => new global::app.@event.binding.@this(side, Fail("Broke"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));
        app.@event.Add(item).Add("start")![When.after].Add(side => new global::app.@event.binding.@this(side, (_, result, ctx) => { seen = result; return Task.FromResult(ctx.Ok()); }, app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        var answered = await app.@event.Add(item).Add("start")!.after.Start(item, context.Ok("ran"), context);

        await answered.IsFailure();
        await Assert.That(answered.Error!.Key).IsEqualTo("Broke");
        await Assert.That(seen).IsNotNull();
        await Assert.That(seen!.Error?.Key).IsEqualTo("Broke");
    }

    [Test] public async Task AHandledAfterAnswer_MeansNothing()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        app.@event.Add(item).Add("start")![When.after].Add(side => new global::app.@event.binding.@this(side, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));
        app.@event.Add(item).Add("start")![When.after].Add(side => new global::app.@event.binding.@this(side, Record(ran, "still runs"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        var result = context.Ok("ran");
        var answered = await app.@event.Add(item).Add("start")!.after.Start(item, result, context);

        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
        await Assert.That(ran).IsEquivalentTo(new[] { "still runs" });
    }

    [Test] public async Task AHandlerThatThrows_StillLetsItsBindingFireTheNextTime()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        var calls = 0;
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, (_, _, ctx) =>
        {
            if (++calls == 1) throw new InvalidOperationException("first call fails");
            return Task.FromResult(ctx.Ok());
        }, app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        await Assert.That(async () => await app.@event.Add(item).Add("start")!.before.Start(item, context.Ok(), context)).Throws<InvalidOperationException>();
        await (await app.@event.Add(item).Add("start")!.before.Start(item, context.Ok(), context)).IsSuccess();
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test] public async Task ABinding_DoesNotFireInsideItsOwnHandler()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        var times = 0;
        app.@event.Add(item).Add("start")![When.before].Add(side => new global::app.@event.binding.@this(side, async (fired, result, ctx) =>
        {
            times++;
            return await app.@event.Add(fired).Add("start")!.before.Start(fired, result, ctx);   // starts its own event again
        }, app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        await app.@event.Add(item).Add("start")!.before.Start(item, context.Ok(), context);

        await Assert.That(times).IsEqualTo(1);
    }

    [Test] public async Task AnActorsBinding_FiresOnlyForThatActor_AnAppsForEvery()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        app.@event.Add(item).Add("set")![When.after].Add(side => new global::app.@event.binding.@this(side, Record(ran, "user's"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));
        app.@event.Add(item).Add("set")![When.after].Add(side => new global::app.@event.binding.@this(side, Record(ran, "app's"), app.actor.list.User, Scope.app, global::app.@event.binding.@this.Always));

        await app.@event.Add(item).Add("set")!.after.Start(item, app.actor.list.System.Context.Ok(), app.actor.list.System.Context);
        await Assert.That(ran).IsEquivalentTo(new[] { "app's" });

        ran.Clear();
        await app.@event.Add(item).Add("set")!.after.Start(item, app.actor.list.User.Context.Ok(), app.actor.list.User.Context);
        await Assert.That(ran).IsEquivalentTo(new[] { "user's", "app's" });
    }

    [Test] public async Task AFilter_TakesTheItemsItFiresFor()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var type = Item(app.actor.list.User.Context);
        var wanted = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        app.@event.Add(type).Add("create")![When.after].Add(side => new global::app.@event.binding.@this(side, Record(ran, "fired"), app.actor.list.User, Scope.actor, (fired, _) => ReferenceEquals(fired, wanted)));

        await app.@event.Add(type).Add("create")!.after.Start(Item(app.actor.list.User.Context), context.Ok(), context);
        await Assert.That(ran).IsEmpty();
        await app.@event.Add(type).Add("create")!.after.Start(wanted, context.Ok(), context);
        await Assert.That(ran).IsEquivalentTo(new[] { "fired" });
    }

    [Test] public async Task ARemovedBinding_FiresNoMore()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var item = Item(app.actor.list.User.Context);
        var ran = new List<string>();
        var binding = app.@event.Add(item).Add("start")![When.after].Add(side => new global::app.@event.binding.@this(side, Record(ran, "fired"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        binding.Remove();
        await app.@event.Add(item).Add("start")!.after.Start(item, context.Ok(), context);

        await Assert.That(ran).IsEmpty();
        await Assert.That(app.@event.Add(item).Add("start")!.after.Count).IsEqualTo(0);
    }

    [Test] public async Task AClone_IsANewValue_WithNothingBound()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var item = Item(app.actor.list.User.Context);
        item.Set("a", false, 1, app.actor.list.User.Context);
        app.@event.Add(item).Add("start")![When.after].Add(side => new global::app.@event.binding.@this(side, Record(new List<string>(), "x"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

        var copy = item.Clone();

        await Assert.That(ReferenceEquals(copy.on, global::app.@event.on.empty.Of(copy.GetType()))).IsTrue();
        await Assert.That(app.@event.Add(item).Add("start")!.after.Count).IsEqualTo(1);
    }

    // `on` stays off the wire: a value with a binding writes the same bytes as one without.
    [Test] public async Task ABoundItem_WritesTheSameBytes_AsAnUnboundOne()
    {
        await using var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var context = app.actor.list.User.Context;
        var plain = context.Dict();
        plain.Set("a", false, 1, context);
        var bound = context.Dict();
        bound.Set("a", false, 1, context);
        app.@event.Add(bound).Add("start")![When.after].Add(side => new global::app.@event.binding.@this(side, Record(new List<string>(), "x"), app.actor.list.User, Scope.actor, global::app.@event.binding.@this.Always));

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

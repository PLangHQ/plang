using On = global::app.@event.on.@this;
using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.Events;

// An object's events: the shared empty `on` until the first binding gives the item its own; each event's
// before/after bindings start in the order added, for their actor (or the app), for the items their filter takes.
public class OnTests
{
    private static global::app.type.item.dict.@this Item() => new();

    // A handler that records it ran and answers ok.
    private static System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, Task<global::app.data.@this>> Record(List<string> ran, string name)
        => (_, _, ctx) => { lock (ran) ran.Add(name); return Task.FromResult(ctx.Ok()); };

    // A handler that fails with key.
    private static System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, Task<global::app.data.@this>> Fail(string key)
        => (_, _, ctx) => Task.FromResult(ctx.Error(new global::app.error.Error("no", key, 400)));

    [Test] public async Task AnUnboundItem_HasTheSharedEmptyOn_WhoseEventsAreClosed()
    {
        var item = Item();
        await Assert.That(ReferenceEquals(item.on, On.Empty)).IsTrue();
        await Assert.That(item.on["start"]!.before.Count).IsEqualTo(0);
        await Assert.That(item.on["nosuchevent"]).IsNull();
    }

    [Test] public async Task TheFirstBinding_GivesTheItemItsOwnOn_AndEmptyNeverChanges()
    {
        await using var app = TestApp.Create("/test");
        var item = Item();
        var ran = new List<string>();

        var own = item.Own();
        own.Bind("start", When.before, Record(ran, "one"), app.actor.list.User, Scope.actor);

        await Assert.That(ReferenceEquals(item.on, own)).IsTrue();
        await Assert.That(ReferenceEquals(item.Own(), own)).IsTrue();
        await Assert.That(item.on["start"]!.before.Count).IsEqualTo(1);
        await Assert.That(On.Empty["start"]!.before.Count).IsEqualTo(0);
        await Assert.That(Item().on["start"]!.before.Count).IsEqualTo(0);
    }

    [Test] public async Task Start_RunsInTheOrderAdded_AndAnswersTheResultAsItStands()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        var ran = new List<string>();
        var own = item.Own();
        own.Bind("start", When.before, Record(ran, "first"), app.actor.list.User, Scope.actor);
        own.Bind("start", When.before, Record(ran, "second"), app.actor.list.User, Scope.actor);

        var result = context.Ok("kept");
        var answered = await item.on["start"]!.before.Start(item, result, context);

        await Assert.That(ran).IsEquivalentTo(new[] { "first", "second" });
        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
    }

    [Test] public async Task Start_WithNothingBound_AnswersTheResultAsIs()
    {
        await using var app = TestApp.Create("/test");
        var result = app.actor.list.User.Context.Ok("kept");
        var answered = await Item().on["start"]!.after.Start(Item(), result, app.actor.list.User.Context);
        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
    }

    [Test] public async Task AFailingBeforeBinding_IsTheResult_AndStopsTheRest()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        var ran = new List<string>();
        var own = item.Own();
        own.Bind("start", When.before, Fail("Refused"), app.actor.list.User, Scope.actor);
        own.Bind("start", When.before, Record(ran, "after the failure"), app.actor.list.User, Scope.actor);

        var answered = await item.on["start"]!.before.Start(item, context.Ok(), context);

        await answered.IsFailure();
        await Assert.That(answered.Error!.Key).IsEqualTo("Refused");
        await Assert.That(ran).IsEmpty();
    }

    [Test] public async Task AHandledBeforeAnswer_Cancels_AndIsTheResult()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        var ran = new List<string>();
        var own = item.Own();
        own.Bind("start", When.before, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, app.actor.list.User, Scope.actor);
        own.Bind("start", When.before, Record(ran, "after the cancel"), app.actor.list.User, Scope.actor);

        var answered = await item.on["start"]!.before.Start(item, context.Ok(), context);

        await Assert.That(answered.Handled).IsTrue();
        await Assert.That((await answered.Value())?.ToString()).IsEqualTo("instead");
        await Assert.That(ran).IsEmpty();
    }

    [Test] public async Task AFailingAfterBinding_IsTheResult_AndTheRestStillRunOnIt()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        global::app.data.@this? seen = null;
        var own = item.Own();
        own.Bind("start", When.after, Fail("Broke"), app.actor.list.User, Scope.actor);
        own.Bind("start", When.after, (_, result, ctx) => { seen = result; return Task.FromResult(ctx.Ok()); }, app.actor.list.User, Scope.actor);

        var answered = await item.on["start"]!.after.Start(item, context.Ok("ran"), context);

        await answered.IsFailure();
        await Assert.That(answered.Error!.Key).IsEqualTo("Broke");
        await Assert.That(seen).IsNotNull();
        await Assert.That(seen!.Error?.Key).IsEqualTo("Broke");
    }

    [Test] public async Task AHandledAfterAnswer_MeansNothing()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        var ran = new List<string>();
        var own = item.Own();
        own.Bind("start", When.after, (_, _, ctx) =>
        {
            var instead = ctx.Ok("instead");
            instead.Handled = true;
            return Task.FromResult(instead);
        }, app.actor.list.User, Scope.actor);
        own.Bind("start", When.after, Record(ran, "still runs"), app.actor.list.User, Scope.actor);

        var result = context.Ok("ran");
        var answered = await item.on["start"]!.after.Start(item, result, context);

        await Assert.That(ReferenceEquals(answered, result)).IsTrue();
        await Assert.That(ran).IsEquivalentTo(new[] { "still runs" });
    }

    [Test] public async Task AHandlerThatThrows_StillLetsItsBindingFireTheNextTime()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        var calls = 0;
        item.Own().Bind("start", When.before, (_, _, ctx) =>
        {
            if (++calls == 1) throw new InvalidOperationException("first call fails");
            return Task.FromResult(ctx.Ok());
        }, app.actor.list.User, Scope.actor);

        await Assert.That(async () => await item.on["start"]!.before.Start(item, context.Ok(), context)).Throws<InvalidOperationException>();
        await (await item.on["start"]!.before.Start(item, context.Ok(), context)).IsSuccess();
        await Assert.That(calls).IsEqualTo(2);
    }

    [Test] public async Task ABinding_DoesNotFireInsideItsOwnHandler()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        var times = 0;
        item.Own().Bind("start", When.before, async (fired, result, ctx) =>
        {
            times++;
            return await fired.on["start"]!.before.Start(fired, result, ctx);   // starts its own event again
        }, app.actor.list.User, Scope.actor);

        await item.on["start"]!.before.Start(item, context.Ok(), context);

        await Assert.That(times).IsEqualTo(1);
    }

    [Test] public async Task AnActorsBinding_FiresOnlyForThatActor_AnAppsForEvery()
    {
        await using var app = TestApp.Create("/test");
        var item = Item();
        var ran = new List<string>();
        var own = item.Own();
        own.Bind("set", When.after, Record(ran, "user's"), app.actor.list.User, Scope.actor);
        own.Bind("set", When.after, Record(ran, "app's"), app.actor.list.User, Scope.app);

        await item.on["set"]!.after.Start(item, app.actor.list.System.Context.Ok(), app.actor.list.System.Context);
        await Assert.That(ran).IsEquivalentTo(new[] { "app's" });

        ran.Clear();
        await item.on["set"]!.after.Start(item, app.actor.list.User.Context.Ok(), app.actor.list.User.Context);
        await Assert.That(ran).IsEquivalentTo(new[] { "user's", "app's" });
    }

    [Test] public async Task AFilter_TakesTheItemsItFiresFor()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var type = Item();
        var wanted = Item();
        var ran = new List<string>();
        type.Own().Bind("create", When.after, Record(ran, "fired"), app.actor.list.User, Scope.actor,
            (fired, _) => ReferenceEquals(fired, wanted));

        await type.on["create"]!.after.Start(Item(), context.Ok(), context);
        await Assert.That(ran).IsEmpty();
        await type.on["create"]!.after.Start(wanted, context.Ok(), context);
        await Assert.That(ran).IsEquivalentTo(new[] { "fired" });
    }

    [Test] public async Task ARemovedBinding_FiresNoMore()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var item = Item();
        var ran = new List<string>();
        var binding = item.Own().Bind("start", When.after, Record(ran, "fired"), app.actor.list.User, Scope.actor);

        binding.Remove();
        await item.on["start"]!.after.Start(item, context.Ok(), context);

        await Assert.That(ran).IsEmpty();
        await Assert.That(item.on["start"]!.after.Count).IsEqualTo(0);
    }

    [Test] public async Task AClone_IsANewValue_WithNothingBound()
    {
        await using var app = TestApp.Create("/test");
        var item = Item();
        item.Set("a", false, 1, app.actor.list.User.Context);
        item.Own().Bind("start", When.after, Record(new List<string>(), "x"), app.actor.list.User, Scope.actor);

        var copy = item.Clone();

        await Assert.That(ReferenceEquals(copy.on, On.Empty)).IsTrue();
        await Assert.That(item.on["start"]!.after.Count).IsEqualTo(1);
    }

    // `on` stays off the wire: a value with a binding writes the same bytes as one without.
    [Test] public async Task ABoundItem_WritesTheSameBytes_AsAnUnboundOne()
    {
        await using var app = TestApp.Create("/test");
        var context = app.actor.list.User.Context;
        var plain = new global::app.type.item.dict.@this();
        plain.Set("a", false, 1, context);
        var bound = new global::app.type.item.dict.@this();
        bound.Set("a", false, 1, context);
        bound.Own().Bind("start", When.after, Record(new List<string>(), "x"), app.actor.list.User, Scope.actor);

        foreach (var view in new[] { global::app.View.Out, global::app.View.Store })
            await Assert.That(await Written(bound, view, context)).IsEqualTo(await Written(plain, view, context));
    }

    // `on` isn't taught as a property: a type's catalog facts don't list it.
    [Test] public async Task On_IsNotInATypesProperties()
    {
        await using var app = TestApp.Create("/test");
        var goal = app.type.list["goal"];
        await Assert.That(goal.Property?.Any(p => string.Equals(p.Name, "on", StringComparison.OrdinalIgnoreCase)) ?? false).IsFalse();
    }

    private static async Task<string> Written(global::app.type.item.@this item, global::app.View view, global::app.actor.context.@this context)
    {
        return await context.Pr(item, view);
    }
}

using When = global::app.@event.When;
using Scope = global::app.@event.Scope;

namespace PLang.Tests.App.Events;

// A sub app is born holding what its maker had bound then, each binding taken in as its own (an inherited binding): it
// fires for the sub app's actor in the maker binding's role, the maker's first, and the sub app can't take it off. What the
// maker binds after never reaches it.
public class InheritedBindingsTests
{
    private static global::app.@this App() => new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

    // A binding by app's user before item's start that records name.
    private static void Bind(global::app.@this app, global::app.type.item.@this item, List<string> ran, string name,
        Scope scope = Scope.actor)
        => app.actor.list.User.@event.Add(item, "start", global::app.@event.When.before, side => new global::app.@event.code.@this(side,
            (_, _, ctx) => { lock (ran) ran.Add(name); return Task.FromResult(ctx.Ok()); },
            app.actor.list.User, global::app.@event.code.@this.Always));

    [Test]
    public async Task ASubApp_TakesInItsMakersBinding_AtBirth_AndFiresItForItsUser()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        var ran = new List<string>();
        Bind(maker, item, ran, "maker");

        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        await item.on.start.Before(item, sub.actor.list.User.Context);

        await Assert.That(ran).IsEquivalentTo(new List<string> { "maker" });
        await Assert.That(sub.@event[item]!.start.before[0]).IsTypeOf<global::app.@event.inherited.@this>();
        await Assert.That(sub.@event[item]!.start.before[0].Actor).IsSameReferenceAs(sub.actor.list.User);
    }

    [Test]
    public async Task AMakersLaterBinding_DoesNotReachASubAppMadeBefore()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        var ran = new List<string>();
        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        // the app's, so it would fire for any actor that reached it
        Bind(maker, item, ran, "maker", Scope.app);

        await item.on.start.Before(item, sub.actor.list.User.Context);

        await Assert.That(ran).IsEmpty();
        await Assert.That(sub.@event[item]).IsNull();
    }

    [Test]
    public async Task ASubApp_CannotTakeOffItsMakersBinding()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        var ran = new List<string>();
        Bind(maker, item, ran, "maker");
        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        var context = sub.actor.list.User.Context;
        var inherited = sub.@event[item]!.start.before[0];

        await context.Variable.Set("event", inherited);
        var unbound = await (await context.Action("on.unbind(Event=%event%)")).Start(context);

        await unbound.IsFailure();
        await Assert.That(unbound.Error!.Key).IsEqualTo("EventNotYours");
        await item.on.start.Before(item, context);
        await Assert.That(ran).IsEquivalentTo(new List<string> { "maker" });
    }

    [Test]
    public async Task InASubApp_TheMakersBeforeRunsFirst_ThenItsOwn()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        var ran = new List<string>();
        Bind(maker, item, ran, "maker");
        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        Bind(sub, item, ran, "sub");

        await item.on.start.Before(item, sub.actor.list.User.Context);

        await Assert.That(string.Join(",", ran)).IsEqualTo("maker,sub");
    }
}

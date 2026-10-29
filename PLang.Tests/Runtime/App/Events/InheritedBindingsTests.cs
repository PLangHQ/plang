namespace PLang.Tests.App.Events;

// A sub app's actor is born holding what its maker's actor of its role had bound then, each event taken in as its own
// (an inherited event): it runs for the sub app's actor, the maker's first, and the sub app can't take it off. What the
// maker binds after never reaches it.
public class InheritedBindingsTests
{
    private static global::app.@this App() => new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

    // An event by app's user before item's start that records name.
    private static void Bind(global::app.@this app, global::app.type.item.@this item, List<string> ran, string name)
        => app.actor.list.User.@event.Add(new global::app.@event.code.@this(item, "start", global::app.@event.When.before,
            (_, _, ctx) => { lock (ran) ran.Add(name); return Task.FromResult(ctx.Ok()); },
            app.actor.list.User, global::app.@event.code.@this.Always));

    // item's start, run before for context's actor.
    private static System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item, global::app.actor.context.@this context)
        => context.Actor!.@event[item].before.Start(item, context);

    [Test]
    public async Task ASubApp_TakesInItsMakersEvent_AtBirth_AndRunsItForItsUser()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        var ran = new List<string>();
        Bind(maker, item, ran, "maker");

        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        await Before(item, sub.actor.list.User.Context);

        await Assert.That(ran).IsEquivalentTo(new List<string> { "maker" });
        await Assert.That(sub.actor.list.User.@event[item].before[0]).IsTypeOf<global::app.@event.inherited.@this>();
        await Assert.That(sub.actor.list.User.@event[item].before[0].Actor).IsSameReferenceAs(sub.actor.list.User);
    }

    [Test]
    public async Task AMakersLaterEvent_DoesNotReachASubAppMadeBefore()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        var ran = new List<string>();
        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        Bind(maker, item, ran, "maker");

        await Before(item, sub.actor.list.User.Context);

        await Assert.That(ran).IsEmpty();
        await Assert.That(sub.actor.list.User.@event.Items()).IsEmpty();
    }

    [Test]
    public async Task ASubApp_CannotTakeOffItsMakersEvent()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        var ran = new List<string>();
        Bind(maker, item, ran, "maker");
        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        var context = sub.actor.list.User.Context;
        var inherited = sub.actor.list.User.@event[item].before[0];

        await context.Variable.Set("event", inherited);
        var unbound = await (await context.Action("on.unbind(Event=%event%)")).Start(context);

        await unbound.IsFailure();
        await Assert.That(unbound.Error!.Key).IsEqualTo("EventNotYours");
        await Before(item, context);
        await Assert.That(ran).IsEquivalentTo(new List<string> { "maker" });
    }

    // From a sub app, an event reaches nothing of its maker's upward: only its name and when are navigable, never the
    // actor that bound it.
    [Test]
    public async Task FromASubApp_AnEventsActor_IsNotReached()
    {
        await using var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        Bind(maker, item, new List<string>(), "maker");
        await using var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        var context = sub.actor.list.User.Context;
        var running = new global::app.data.@this("!event", sub.actor.list.User.@event[item].before[0], context: context);

        await Assert.That((await (await running.Get("name")).Value())?.ToString()).IsEqualTo("start");
        await Assert.That((await running.Get("actor")).IsInitialized).IsFalse();
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

        await Before(item, sub.actor.list.User.Context);

        await Assert.That(string.Join(",", ran)).IsEqualTo("maker,sub");
    }
}

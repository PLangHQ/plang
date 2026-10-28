using When = global::app.@event.When;
using Scope = global::app.@event.binding.Scope;

namespace PLang.Tests.App.Events;

// Security probes on a sub app born holding its maker's bindings (snapshot 251). A child must hold the maker's policy
// without being able to see it, reach the maker through it, or take it off.
public class InheritedBindingProbeTests
{
    private static global::app.@this App() => new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();

    private static System.Threading.Tasks.ValueTask<global::app.data.@this> Read(string variable, global::app.actor.context.@this context)
        => new global::app.type.item.variable.@this(variable.Trim('%'), context.App.type.list).Start(context);

    private static global::app.@event.binding.@this Bind(global::app.@this app, global::app.type.item.@this item)
        => app.@event.Add(item).Add("start")!.before.Add(side => new global::app.@event.binding.@this(side,
            (_, _, ctx) => Task.FromResult(ctx.Ok()), app.actor.list.System, Scope.app, global::app.@event.binding.@this.Always));

    // A child's own and the maker's, in one place: the maker's binding is the child's inherited one.
    private static async Task<(global::app.@this maker, global::app.@this sub, global::app.type.item.@this item)> Pair()
    {
        var maker = App();
        var item = maker.actor.list.User.Context.Dict();
        Bind(maker, item);
        var sub = new global::app.@this(maker.filesystem.root.Absolute, maker.actor.list.User);
        await Task.CompletedTask;
        return (maker, sub, item);
    }

    // 2: what the child's %!app.event% writes and its snapshot keep is its own bindings; an inherited one writes nothing.
    [Test]
    public async Task AChildsAppEvent_ListAndWrite_HoldNothingOfTheMakers()
    {
        var (maker, sub, item) = await Pair();
        await using var _m = maker; await using var _s = sub;

        await Assert.That(sub.@event[item]!.start.before[0]).IsTypeOf<global::app.@event.binding.inherited.@this>();
        await Assert.That(sub.@event.list.Items(sub.actor.list.System.Context).Count()).IsEqualTo(0);

        var read = await Read("%!app.event%", sub.actor.list.User.Context);
        await Assert.That(read.Success).IsTrue();
        var text = (await read.Value())?.ToString() ?? "";
        await Assert.That(text).DoesNotContain("start");
    }

    // 3: no plang path reaches a binding's actor, from the inherited one or the maker's.
    [Test]
    [Arguments("actor")]
    [Arguments("scope")]
    public async Task NoPathReaches_AnInheritedBindings_ActorOrScope(string member)
    {
        var (maker, sub, item) = await Pair();
        await using var _m = maker; await using var _s = sub;
        var context = sub.actor.list.User.Context;
        await context.Variable.Set("binding", sub.@event[item]!.start.before[0]);

        var read = await Read($"%binding.{member}%", context);

        // reached = a value came back that is the maker's (or any) actor: that is a path up
        await Assert.That(read.IsInitialized && read.Success && read.Peek() is not null).IsFalse();
    }

    // 3: the maker's own binding holds its actor too; the child must never be able to read it from where it can get it
    // (the event a maker's binding runs on, %!event%).
    [Test]
    [Arguments("before")]
    [Arguments("after")]
    public async Task FromAMakersEvent_NoPathReaches_ABindingOrItsActor(string side)
    {
        var (maker, sub, item) = await Pair();
        await using var _m = maker; await using var _s = sub;
        var context = sub.actor.list.User.Context;
        // %!event% in a child is the maker layer's event while its binding runs (event/binding/action/this.cs Handle)
        await context.Variable.Set("ev", maker.@event[item]!.start);

        var list = await Read($"%ev.{side}%", context);
        var first = await Read($"%ev.{side}[0]%", context);
        var actor = await Read($"%ev.{side}[0].actor%", context);
        var remove = await Read($"%ev.{side}[0].remove()%", context);

        await Assert.That(first.IsInitialized && first.Success && first.Peek() is not null).IsFalse();
        await Assert.That(actor.IsInitialized && actor.Success && actor.Peek() is not null).IsFalse();
        // a miss reads Success with nothing; what counts is that nothing was done to the maker's list
        await Assert.That(remove.IsInitialized && remove.Success && remove.Peek() is not null).IsFalse();
        await Assert.That(maker.@event[item]!.start.before.Count).IsEqualTo(1);
        await Assert.That(list.IsInitialized && list.Success && list.Peek() is not null).IsFalse();
    }

    // 3: Remove on an inherited binding from C#, by a method hop and by on.unbind: refused, and the maker still fires it.
    [Test]
    public async Task RemovingAnInheritedBinding_ByAMethodHop_IsRefused_AndTheMakersListIsUntouched()
    {
        var (maker, sub, item) = await Pair();
        await using var _m = maker; await using var _s = sub;
        var context = sub.actor.list.User.Context;
        await context.Variable.Set("binding", sub.@event[item]!.start.before[0]);

        var hop = await Read("%binding.remove()%", context);

        await Assert.That(hop.Success).IsFalse();
        await Assert.That(maker.@event[item]!.start.before.Count).IsEqualTo(1);
        await Assert.That(sub.@event[item]!.start.before.Count).IsEqualTo(1);
    }
}

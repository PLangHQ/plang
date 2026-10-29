namespace PLang.Tests.App.actions.EventTests;

/// <summary>A binding made while a program runs is held on the context of the actor that made it, never on the item it
/// binds: on a type entry every sub app shares, it fires only in its own app, and it goes with that app.</summary>
public class HoldTests
{
    // a sub app made by the parent's user, testing, with a goal of its own named `name`
    private static global::app.@this Child(global::app.@this parent, string folder)
        => new global::app.@this(folder, parent.actor.list.User).Testing();

    // what a program's on.event step does: binds `call` on `event`, for the actor
    private static async Task<global::app.@event.@this> Bind(global::app.actor.context.@this ctx, string @event,
        global::app.goal.step.action.@this call)
    {
        var bound = await Make.Action(ctx, "on", "event", ("Event", @event), ("When", "before"), ("Action", call)).Start(ctx);
        await bound.IsSuccess();
        return (global::app.@event.@this)bound.Peek();
    }

    // one step run as a goal of its own in `app`, as its user: the step type's start fires
    private static async Task<global::app.data.@this> Run(global::app.@this app)
    {
        var ctx = app.actor.list.User.Context;
        var goal = Make.Goal(ctx, "Work", Make.Step("set %done% = true",
            Make.Action(ctx, "variable", "set", Make.Param(ctx, "Name", "done", "variable"), ("Value", true))));
        return await goal.Start(ctx);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static async Task<System.WeakReference> Bound(global::app.@this parent)
    {
        var child = Child(parent, "/parent/bound");
        var ctx = child.actor.list.User.Context;
        await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "Log"));
        await child.DisposeAsync();
        return new System.WeakReference(child);
    }

    [Test]
    public async Task ABindingGoesWithItsApp()
    {
        await using var parent = new global::app.@this("/parent").Testing();

        var child = await Bound(parent);
        // the child's events load through file reads, whose continuations ran on pool threads: those let go of what
        // they ran in a moment, so a collection is tried for a while; a real hold keeps it alive throughout
        for (var i = 0; i < 40 && child.IsAlive; i++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
            await Task.Delay(50);
        }

        await Assert.That(child.IsAlive).IsFalse().Because("nothing the app bound keeps it after it is gone");
        await Assert.That(parent.actor.list.User.@event.Items()).IsEmpty().Because("the parent's actors hold nothing it bound");
    }

    [Test]
    public async Task AnAppWideBinding_NeverFiresInASibling()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var binding = Child(parent, "/parent/binding");
        await using var sibling = Child(parent, "/parent/sibling");
        var ctx = binding.actor.list.User.Context;
        // a call to a goal the sibling doesn't have: fired there, its step would fail
        await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "OnlyInTheBindingApp"));

        await (await Run(sibling)).IsSuccess();
    }

    [Test]
    public async Task AnUnboundBinding_LeavesNothingInTheHold()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var app = Child(parent, "/parent/app");
        var ctx = app.actor.list.User.Context;
        var binding = await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "Log"));
        var events = (await app.actor.list.User.@event.On(app.type.list["step"]));
        await Assert.That(events.start.before.Count).IsEqualTo(1);

        var unbound = await new global::app.module.on.OnUnbind(ctx)
        {
            Event = new global::app.data.@this<global::app.@event.@this>("Event", binding, context: ctx),
        }.Start();

        await unbound.IsSuccess();
        await Assert.That(events.start.before.Count).IsEqualTo(0);
        await (await Run(app)).IsSuccess();
    }

    [Test]
    public async Task AnActorScopedBinding_FiresOnlyForItsActor()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var app = Child(parent, "/parent/app");
        var user = app.actor.list.User.Context;
        var watch = Make.Goal(user, "Watch", Make.Step("set %seen% = true",
            Make.Action(user, "variable", "set", Make.Param(user, "Name", "seen", "variable"), ("Value", true))));
        app.goal.list.Add(watch);
        app.goal.list.Add(Make.Goal(user, "Target"));
        await Bind(user, "%!app.goal[\"/Target\"].on.start%", Make.Call(user, "Watch"));

        var system = app.actor.list.System.Context;
        await Make.Call(user, "Target").Start(system);
        await Assert.That((await system.Variable.Get("seen")).IsInitialized).IsFalse().Because("another actor's run");

        await Make.Call(user, "Target").Start(user);
        await Assert.That((await user.Variable.Get("seen")).IsInitialized).IsTrue().Because("its own actor's run");
    }
}

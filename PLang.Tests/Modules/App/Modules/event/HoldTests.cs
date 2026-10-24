namespace PLang.Tests.App.actions.EventTests;

/// <summary>A binding made while a program runs is held on the context of the actor that made it, never on the item it
/// binds: on a type entry every sub app shares, it fires only in its own app, and it goes with that app.</summary>
public class HoldTests
{
    // a sub app made by the parent's user, testing, with a goal of its own named `name`
    private static global::app.@this Child(global::app.@this parent, string folder)
        => new global::app.@this(folder, parent.actor.list.User).Testing();

    // what a program's on.event step does: binds `call` on `event`, for the actor or the app
    private static async Task<global::app.@event.binding.@this> Bind(global::app.actor.context.@this ctx, string @event,
        global::app.goal.step.action.@this call, string scope)
    {
        var bound = await Make.Action(ctx, "on", "event", ("Event", @event), ("When", "before"), ("Action", call),
            ("Scope", scope)).Start(ctx);
        await bound.IsSuccess();
        return (global::app.@event.binding.@this)bound.Peek();
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
        var child = Child(parent, "/bound");
        var ctx = child.actor.list.User.Context;
        await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "Log"), "app");
        await child.DisposeAsync();
        return new System.WeakReference(child);
    }

    [Test]
    public async Task ABindingGoesWithItsApp()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        var step = parent.type.list["step"];

        var child = await Bound(parent);
        for (var i = 0; i < 3 && child.IsAlive; i++)
        {
            System.GC.Collect();
            System.GC.WaitForPendingFinalizers();
        }

        await Assert.That(child.IsAlive).IsFalse().Because("nothing the app bound keeps it after it is gone");
        await Assert.That(step.on.start.before.Count).IsEqualTo(0).Because("the shared entry holds nothing it bound");
    }

    [Test]
    public async Task AnAppWideBinding_NeverFiresInASibling()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var binding = Child(parent, "/binding");
        await using var sibling = Child(parent, "/sibling");
        var ctx = binding.actor.list.User.Context;
        // a call to a goal the sibling doesn't have: fired there, its step would fail
        await Bind(ctx, "%!app.type.step.on.start%", Make.Call(ctx, "OnlyInTheBindingApp"), "app");

        await (await Run(sibling)).IsSuccess();
    }

    [Test]
    public async Task AnActorScopedBinding_FiresOnlyForItsActor()
    {
        await using var parent = new global::app.@this("/parent").Testing();
        await using var app = Child(parent, "/app");
        var user = app.actor.list.User.Context;
        var watch = Make.Goal(user, "Watch", Make.Step("set %seen% = true",
            Make.Action(user, "variable", "set", Make.Param(user, "Name", "seen", "variable"), ("Value", true))));
        app.goal.list.Add(watch);
        app.goal.list.Add(Make.Goal(user, "Target"));
        await Bind(user, "%!app.goal[\"/Target\"].on.start%", Make.Call(user, "Watch"), "actor");

        var system = app.actor.list.System.Context;
        await Make.Call(user, "Target").Start(system);
        await Assert.That((await system.Variable.Get("seen")).IsInitialized).IsFalse().Because("another actor's run");

        await Make.Call(user, "Target").Start(user);
        await Assert.That((await user.Variable.Get("seen")).IsInitialized).IsTrue().Because("its own actor's run");
    }
}

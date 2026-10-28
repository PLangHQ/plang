using When = global::app.@event.When;

namespace PLang.Tests.App.Modules.snapshot;

/// <summary>
/// A run's bindings are part of the run: what a program bound (<c>on.event</c>) is saved where it lives, in the actor's
/// hold under the item it is on, and comes back there in a fresh app — firing as it did. A binding C# made (a handler, a
/// mock) is its maker's to make again, so it is never written; a binding a snapshot can't make again is refused.
/// </summary>
public class SavedBindingsTests
{
    // An app on a folder of its own, as a run saves one; a second app on the same folder is the fresh app it goes on in.
    private static global::app.@this App(string root)
        => new global::app.@this(root, global::PLang.Tests.Parent.Actor).Testing().TestIdentity();

    private static string Root() => System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plang-bind-" + System.Guid.NewGuid().ToString("N")[..8]);

    // The program each app holds: Log sets %logged%, Inner sets %inner%, and G binds, its step 0 the on.event given.
    private static Goal Program(global::app.@this app, string name, params global::app.goal.step.action.@this[] actions)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", context) };
        for (var index = 0; index < actions.Length; index++)
        {
            var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = index, Text = $"{name} step {index}" };
            step.Code.Add(actions[index].In(step));
            goal.Step.Add(step);
        }
        app.goal.list.Add(goal);
        return goal;
    }

    private static Goal Programs(global::app.@this app, string @event, object? action)
    {
        var context = app.actor.list.User.Context;
        Program(app, "Log", Make.Action(context, "variable", "set", Make.Param(context, "Name", "logged", "variable"), ("Value", "yes")));
        Program(app, "Inner", Make.Action(context, "variable", "set", Make.Param(context, "Name", "inner", "variable"), ("Value", "ran")));
        return Program(app, "G", Make.Action(context, "on", "event", ("Event", @event), ("When", "before"),
            ("Action", action ?? Make.Call(context, "Log"))));
    }

    // G's step 0 run, as a program runs it: the binding is made.
    private static async Task Bind(global::app.@this app, Goal goal)
    {
        var context = app.actor.list.User.Context;
        var setting = await context.call.Setting();
        await using (context.call.Push(goal, setting))
        await using (context.call.Push(goal.Step[0], setting))
            await (await goal.Step[0].Code[0].Start(context)).IsSuccess();
    }

    [Test]
    public async Task ABindingBeforeEachGoal_IsSaved_AndFiresAgainInAFreshApp()
    {
        var root = Root();
        await using var app = App(root);
        var context = app.actor.list.User.Context;
        await Bind(app, Programs(app, "%!app.type.goal.on.start%", null));
        var snapshot = await app.Snapshot(context).Saved();

        await using var fresh = App(root);
        Programs(fresh, "%!app.type.goal.on.start%", null);
        var into = fresh.actor.list.User.Context;
        await fresh.Restore(snapshot, into);

        await Assert.That(fresh.actor.list.User.on.Of(fresh.type.list["goal"])?.start.before.Count).IsEqualTo(1);
        await (await fresh.goal.list.Items().OfType<Goal>().First(goal => goal.Name == "Inner").Start(into)).IsSuccess();
        await Assert.That(await into.Variable.GetValue("logged")).IsEqualTo("yes");
    }

    [Test]
    public async Task ABindingOnTheApp_IsSaved_AndComesBackOnTheAppItGoesOnIn()
    {
        var root = Root();
        await using var app = App(root);
        await Bind(app, Programs(app, "%!app.on.start%", null));
        var snapshot = await app.Snapshot(app.actor.list.User.Context).Saved();

        await using var fresh = App(root);
        Programs(fresh, "%!app.on.start%", null);
        await fresh.Restore(snapshot, fresh.actor.list.User.Context);

        await Assert.That(fresh.actor.list.User.on.Of(fresh)?.start.before.Count).IsEqualTo(1);
    }

    // a module's own action (its catalog element) is written as what finds it, its module and name, and read back as it
    [Test]
    public async Task ABindingOnAModulesAction_IsSaved_AndComesBackOnThatAction()
    {
        var root = Root();
        await using var app = App(root);
        await Bind(app, Programs(app, "%!app.module.file.read.on.start%", null));
        var snapshot = await app.Snapshot(app.actor.list.User.Context).Saved();

        await using var fresh = App(root);
        Programs(fresh, "%!app.module.file.read.on.start%", null);
        await fresh.Restore(snapshot, fresh.actor.list.User.Context);

        await Assert.That(fresh.actor.list.User.on.Of(fresh.Module("file")["read"]!)?.start.before.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ABindingCSharpMade_IsNotWritten_AndTheSaveSucceeds()
    {
        var root = Root();
        await using var app = App(root);
        var user = app.actor.list.User;
        user.on[app.type.list["goal"]].start.before.Add(side => new global::app.@event.binding.@this(side,
            (_, _, ctx) => Task.FromResult(ctx.Ok()), user, global::app.@event.binding.Scope.actor, global::app.@event.binding.@this.Always));
        var snapshot = await app.Snapshot(user.Context).Saved();

        await using var fresh = App(root);
        await fresh.Restore(snapshot, fresh.actor.list.User.Context);

        await Assert.That(fresh.actor.list.User.on.Of(fresh.type.list["goal"])).IsNull();
    }

    [Test]
    public async Task ABindingThatNamesItsCallByAVariable_IsRefused()
    {
        await using var app = App(Root());
        var context = app.actor.list.User.Context;
        await context.Variable.Set("x", Make.Call(context, "Log"));
        await Bind(app, Programs(app, "%!app.type.goal.on.start%", "%x%"));

        var taken = await app.Snapshot(context);

        await taken.IsFailure();
        await Assert.That(taken.Error!.Key).IsEqualTo("SnapshotBindingVariable");
    }

    [Test]
    public async Task ABindingOnAChannel_IsRefused()
    {
        await using var app = App(Root());
        var user = app.actor.list.User;
        user.Channel.Register(new StreamChannel(user.Channel, "audit", new System.IO.MemoryStream(), ChannelDirection.Output, ownsStream: true));
        await Bind(app, Programs(app, "%!channel.audit.on.write%", null));

        var taken = await app.Snapshot(user.Context);

        await taken.IsFailure();
        await Assert.That(taken.Error!.Key).IsEqualTo("NoWireContract");
    }
}

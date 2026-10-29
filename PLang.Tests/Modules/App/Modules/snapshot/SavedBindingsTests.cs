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

    // a sub app holds its maker's binding, taken in at its birth, but it is the maker's: the sub app's save and its
    // %!app.event% hold only what the sub app bound itself
    [Test]
    public async Task ASubAppsSave_HoldsItsBindingsOnly()
    {
        var root = Root();
        await using var maker = App(root);
        await Bind(maker, Programs(maker, "%!app.type.goal.on.start%", null));
        await using var sub = new global::app.@this(root, maker.actor.list.User);
        var context = sub.actor.list.User.Context;

        var text = await sub.Snapshot(context).Saved().Text(context);

        await Assert.That(sub.actor.list.User.@event[sub.type.list["goal"]]?.start.before.Count).IsEqualTo(1);
        await Assert.That(text).DoesNotContain("\"before\"");
        await Assert.That(sub.actor.list.User.@event.Items().Count(@event => @event.IsSaved)).IsEqualTo(0);
    }

    // a binding calls the goal it names as seen from the goal its step sits in (/a/G names /a/Log), wherever the event
    // fires (/b/H, beside its own /b/Log)
    [Test]
    public async Task ABinding_CallsItsGoalFromWhereItWasBound_NotWhereItFires()
    {
        var root = Root();
        await using var app = App(root);
        var context = app.actor.list.User.Context;
        Goal At(string path, global::app.goal.step.action.@this action)
        {
            var goal = new Goal(app.type.list) { Name = System.IO.Path.GetFileNameWithoutExtension(path), Path = global::app.type.item.path.@this.Resolve(path, context) };
            var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = 0, Text = $"{path} step 0" };
            step.Code.Add(action.In(step));
            goal.Step.Add(step);
            app.goal.list.Add(goal);
            return goal;
        }
        // the two Logs are on disk, built, as a program's goals are: found by where they are, not by a name already loaded
        async Task OnDisk(string path, string value)
        {
            var goal = new Goal(app.type.list) { Name = "Log", Path = global::app.type.item.path.@this.Resolve(path, context) };
            var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = 0, Text = $"{path} step 0" };
            step.Code.Add(Make.Action(context, "variable", "set", Make.Param(context, "Name", "logged", "variable"), ("Value", value)).In(step));
            goal.Step.Add(step);
            var source = System.IO.Path.Combine(root, path.TrimStart('/'));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(source)!);
            await System.IO.File.WriteAllTextAsync(source, "Log\n- set %logged%\n");
            var pr = System.IO.Path.Combine(root, goal.Pr!.Raw.TrimStart('/'));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
            await System.IO.File.WriteAllTextAsync(pr, await context.Pr(goal));
        }
        await OnDisk("/a/Log.goal", "a");
        await OnDisk("/b/Log.goal", "b");
        var bound = At("/a/G.goal", Make.Action(context, "on", "event", ("Event", "%!app.type.goal.on.start%"), ("When", "before"),
            ("Action", Make.Call(context, "Log"))));
        var fires = At("/b/H.goal", Make.Action(context, "variable", "set", Make.Param(context, "Name", "inner", "variable"), ("Value", "ran")));
        await Bind(app, bound);

        await (await fires.Start(context)).IsSuccess();

        await Assert.That(await context.Variable.GetValue("logged")).IsEqualTo("a");
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

        await Assert.That(fresh.actor.list.User.@event[fresh.type.list["goal"]]?.start.before.Count).IsEqualTo(1);
        await (await fresh.goal.list.Items().OfType<Goal>().First(goal => goal.Name == "Inner").Start(into)).IsSuccess();
        await Assert.That(await into.Variable.GetValue("logged")).IsEqualTo("yes");
    }

    // the fresh app hasn't loaded the goal whose step bound: the snapshot's load pass loads it from its .pr, as it is
    // named in the binding's action, before the binding is read
    [Test]
    public async Task ABinding_WhoseGoalTheFreshAppHasNotLoaded_LoadsIt_AndFires()
    {
        var root = Root();
        await using var app = App(root);
        var context = app.actor.list.User.Context;
        var bound = Programs(app, "%!app.type.goal.on.start%", null);
        await Bind(app, bound);
        var text = await app.Snapshot(context).Saved().Text(context);
        // only the goal that bound is on disk, built
        var pr = System.IO.Path.Combine(root, bound.Pr!.Raw.TrimStart('/'));
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
        await System.IO.File.WriteAllTextAsync(pr, await context.Pr(bound));

        await using var fresh = App(root);
        var into = fresh.actor.list.User.Context;
        Program(fresh, "Log", Make.Action(into, "variable", "set", Make.Param(into, "Name", "logged", "variable"), ("Value", "yes")));
        var inner = Program(fresh, "Inner", Make.Action(into, "variable", "set", Make.Param(into, "Name", "inner", "variable"), ("Value", "ran")));
        await fresh.Restore(await into.Snapshot(text), into);

        await (await inner.Start(into)).IsSuccess();
        await Assert.That(await into.Variable.GetValue("logged")).IsEqualTo("yes");
    }

    // a binding whose action doesn't read (the goal's step now holds another action) answers its read's own error,
    // whole — never a reflection wrapper around it
    [Test]
    public async Task ABinding_WhoseActionDoesNotRead_AnswersItsReadsError()
    {
        var root = Root();
        await using var app = App(root);
        var context = app.actor.list.User.Context;
        await Bind(app, Programs(app, "%!app.type.goal.on.start%", null));
        var snapshot = await app.Snapshot(context).Saved();

        await using var fresh = App(root);
        var into = fresh.actor.list.User.Context;
        Program(fresh, "G", Make.Action(into, "variable", "set", Make.Param(into, "Name", "other", "variable"), ("Value", "x")));
        var started = await snapshot.Start(fresh, into);

        await started.IsFailure();
        await Assert.That(started.Error!.Key).IsEqualTo("CallbackActionMismatch");
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

        await Assert.That(fresh.actor.list.User.@event[fresh]?.start.before.Count).IsEqualTo(1);
    }

    // a cold app, its settings on disk and not yet read: the snapshot's read loads the app's name before it reads what
    // the snapshot names its app by
    [Test]
    public async Task ABindingOnTheApp_ComesBack_InAnAppThatHasNotReadItsSettings()
    {
        var root = Root();
        await using var app = App(root);
        await Bind(app, Programs(app, "%!app.on.start%", null));
        var snapshot = await app.Snapshot(app.actor.list.User.Context).Saved();

        await using var cold = new global::app.@this(root, global::PLang.Tests.Parent.Actor).TestSigning().TestIdentity();
        Programs(cold, "%!app.on.start%", null);
        await cold.Restore(snapshot, cold.actor.list.User.Context);

        await Assert.That(cold.actor.list.User.@event[cold]?.start.before.Count).IsEqualTo(1);
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

        await Assert.That(fresh.actor.list.User.@event[fresh.Module("file")["read"]!]?.start.before.Count).IsEqualTo(1);
    }

    [Test]
    public async Task ABindingCSharpMade_IsNotWritten_AndTheSaveSucceeds()
    {
        var root = Root();
        await using var app = App(root);
        var user = app.actor.list.User;
        user.@event.Add(new global::app.@event.code.@this(app.type.list["goal"], "start", global::app.@event.When.before,
            (_, _, ctx) => Task.FromResult(ctx.Ok()), user, global::app.@event.code.@this.Always));
        var snapshot = await app.Snapshot(user.Context).Saved();

        await using var fresh = App(root);
        await fresh.Restore(snapshot, fresh.actor.list.User.Context);

        await Assert.That(fresh.actor.list.User.@event.Items()).IsEmpty();
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
        await Assert.That(taken.Error!.Key).IsEqualTo("SnapshotEventVariable");
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

    // A program binds at start and resumes its own snapshot: the resumed app takes the binding in from its maker and
    // reads the saved one back, and the two are the same on.event action's — it fires once, as in an app that holds none.
    // Log counts its firings in %k%; the resume goes on from Inner's call of Counted, whose start fires Log.
    [Test]
    public async Task TheSameBinding_TakenInAndReadBack_FiresOnce()
    {
        var root = Root();
        await using var app = App(root);
        var context = app.actor.list.User.Context;
        Program(app, "Log",
            Make.Action(context, "math", "add", ("A", "%k%"), ("B", 1)),
            Make.Action(context, "variable", "set", Make.Param(context, "Name", "k", "variable"), ("Value", "%!data%")));
        var counted = Program(app, "Counted", Make.Action(context, "variable", "set", Make.Param(context, "Name", "x", "variable"), ("Value", "x")));
        var log = app.goal.list.Items().OfType<Goal>().First(goal => goal.Name == "Log");
        var inner = new Goal(app.type.list) { Name = "Inner", Path = global::app.type.item.path.@this.Resolve("/Inner.goal", context) };
        var step = new Step(app.type.list) { Goal = inner, List = inner.Step, Index = 0, Text = "call Counted, return k" };
        step.Code.Add(Make.Call(context, "Counted").In(step));
        step.Code.Add((await context.Action("goal.return(Data=%k%)")).In(step));
        inner.Step.Add(step);
        app.goal.list.Add(inner);
        var bound = Program(app, "G", Make.Action(context, "on", "event", ("Event", "%!app.type.goal.on.start%"), ("When", "before"),
            ("Action", Make.Call(context, "Log"))));
        await Bind(app, bound);
        System.IO.Directory.CreateDirectory(root);
        foreach (var goal in new[] { counted, log, bound, inner })
        {
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(root, goal.Name + ".goal"), goal.Name + "\n");
            var pr = System.IO.Path.Combine(root, goal.Pr!.Raw.TrimStart('/'));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(pr)!);
            await System.IO.File.WriteAllTextAsync(pr, await context.Pr(goal));
        }
        await context.Variable.Set("k", 0);
        global::app.snapshot.@this snapshot;
        await using (context.call.Push(inner, await context.call.Setting()))
        await using (context.call.Push(step, await context.call.Setting()))
        await using (context.call.Push(step.Code[0], await context.call.Setting(), context.Variable))
            snapshot = await app.Snapshot(context).Saved();

        var resumed = await snapshot.Resumed(context);

        await resumed.IsSuccess();
        await Assert.That((await resumed.Value())?.ToString()).IsEqualTo("1");

        // the same snapshot resumed in an app that holds no binding: only the read-back one fires
        await using var other = App(root);
        var into = other.actor.list.User.Context;
        var control = await (await into.Snapshot(await snapshot.Text(context))).Resumed(into);

        await control.IsSuccess();
        await Assert.That((await control.Value())?.ToString()).IsEqualTo("1");
    }

    // a resume restores the run as it stood: G's step ran twice, so it made two events on the same moment, and both come
    // back — the second is no copy of the first
    [Test]
    public async Task OneStep_BoundTwiceOnOneMoment_ComesBackAsTwo()
    {
        var root = Root();
        await using var app = App(root);
        var context = app.actor.list.User.Context;
        var bound = Programs(app, "%!app.type.goal.on.start%", null);
        await Bind(app, bound);
        await Bind(app, bound);
        var snapshot = await app.Snapshot(context).Saved();

        await using var fresh = App(root);
        Programs(fresh, "%!app.type.goal.on.start%", null);
        await fresh.Restore(snapshot, fresh.actor.list.User.Context);

        await Assert.That(fresh.actor.list.User.@event[fresh.type.list["goal"]]?.start.before.Count).IsEqualTo(2);
    }

    // one step binds two moments ("before each goal starts and loads, call Log"): its two on.event actions made one event
    // on each, and both come back, each on its own moment
    [Test]
    public async Task OneStep_BindingTwoMoments_ComesBackWithBoth()
    {
        var root = Root();
        await using var app = App(root);
        var bound = TwoMoments(app);
        var context = app.actor.list.User.Context;
        var setting = await context.call.Setting();
        await using (context.call.Push(bound, setting))
        await using (context.call.Push(bound.Step[0], setting))
            foreach (var action in bound.Step[0].Code.Items())
                await (await action.Start(context)).IsSuccess();
        var snapshot = await app.Snapshot(context).Saved();

        await using var fresh = App(root);
        TwoMoments(fresh);
        await fresh.Restore(snapshot, fresh.actor.list.User.Context);

        var on = fresh.actor.list.User.@event[fresh.type.list["goal"]];
        await Assert.That(on?.start.before.Count).IsEqualTo(1);
        await Assert.That(on?.load.before.Count).IsEqualTo(1);
    }

    // Log, and G, whose one step binds Log before each goal's start and before each goal's load
    private static Goal TwoMoments(global::app.@this app)
    {
        var context = app.actor.list.User.Context;
        Program(app, "Log", Make.Action(context, "variable", "set", Make.Param(context, "Name", "logged", "variable"), ("Value", "yes")));
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", context) };
        var step = new Step(app.type.list) { Goal = goal, List = goal.Step, Index = 0, Text = "before each goal starts and loads, call Log" };
        foreach (var moment in new[] { "start", "load" })
            step.Code.Add(Make.Action(context, "on", "event", ("Event", $"%!app.type.goal.on.{moment}%"), ("When", "before"),
                ("Action", Make.Call(context, "Log"))).In(step));
        goal.Step.Add(step);
        app.goal.list.Add(goal);
        return goal;
    }

    // each event the resuming app takes in from its maker stands for one saved event of the same action, never more: the
    // maker holds one of G's events and the save two, so the resumed app holds two — the one taken in, and one read back
    [Test]
    public async Task AMakersOne_AndASavesTwo_ComeBackAsTwo()
    {
        var root = Root();
        await using var app = App(root);
        var context = app.actor.list.User.Context;
        var bound = Programs(app, "%!app.type.goal.on.start%", null);
        await Bind(app, bound);
        await Bind(app, bound);
        var snapshot = await app.Snapshot(context).Saved();
        app.actor.list.User.@event[app.type.list["goal"]]!.start.before[1].Remove();

        await using var sub = new global::app.@this(root, app.actor.list.User);
        await sub.Restore(snapshot, sub.actor.list.User.Context);

        var before = sub.actor.list.User.@event[sub.type.list["goal"]]!.start.before;
        await Assert.That(before.Count).IsEqualTo(2);
        await Assert.That(before.OfType<global::app.@event.inherited.@this>().Count()).IsEqualTo(1);
    }
}

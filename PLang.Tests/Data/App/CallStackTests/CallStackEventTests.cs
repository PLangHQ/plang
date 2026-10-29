using ActionEntity = app.goal.step.action.@this;
using Make = global::PLang.Tests.Shared.Make;

namespace PLang.Tests.App.CallStackTests;

// The event running in a frame (%!event%) comes back with the run: a value is kept as it is, and an event a binding fired
// on is kept as what finds it again — its item and its name — read back as the resumed app's own event of that name for
// that item, which holds the binding.
public class CallStackEventTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private (Goal goal, Step step, ActionEntity action) MakeFrame(string goalName)
    {
        var goal = new Goal(app.type.list) { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", app.actor.list.User.Context) };
        var step = new Step(app.type.list) { Index = 0, Text = "step", Goal = goal };
        var action = new ActionEntity(app.type.list) { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = step };
        step.Code.Add(action);
        goal.Step.Add(step);
        return (goal, step, action);
    }

    private global::app.@this BuildAppWithGoals(params Goal[] goals)
    {
        var built = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        foreach (var g in goals) built.goal.list.Add(g);
        return built;
    }

    [Test]
    public async Task CallStack_RoundTrip_TheEventRunningInAFrame_ComesBackWithIt()
    {
        var (g1, s1, a1) = MakeFrame("EventGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;

        await using (context.call.Push(s1.Goal!, await context.call.Setting()))
        await using (context.call.Push(s1, await context.call.Setting()))
        await using (var frame = context.call.Push(a1, await context.call.Setting()))
        {
            frame.Event = new global::app.data.@this("!event", "fired", context: context);
            var snap = (await src.Snapshot(context).Saved());

            var (dg1, _, _) = MakeFrame("EventGoal");
            var dst = BuildAppWithGoals(dg1);
            await dst.Restore(snap, dst.actor.list.User.Context);

            var action = dst.actor.list.User.Context.call.Root!.Children.Single().Children.Single();
            await Assert.That(action.Event).IsNotNull();
            await Assert.That((await action.Event!.Value())?.ToString()).IsEqualTo("fired");
        }
    }

    // The program: Bind binds Log before each goal; Log saves the run inside its call and keeps what %!event% fired for;
    // Run answers it, and Other is any goal after.
    private static void Program(global::app.@this into)
    {
        var context = into.actor.list.User.Context;
        Goal(into, "Bind", ("before each goal, call Log", [Make.Action(context, "on", "event", ("Event", "%!app.type.goal.on.start%"),
            ("When", "before"), ("Action", Make.Call(context, "Log")))]));
        Goal(into, "Other", ("set %other% = done",
            [Make.Action(context, "variable", "set", Make.Param(context, "Name", "other", "variable"), ("Value", "done"))]));
        Goal(into, "Log",
            ("save snapshot to %snap%", [Make.Action(context, "snapshot", "save"),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "snap", "variable"), ("Value", "%!data%"))]),
            ("set %seen% = %!event!item.name%",
                [Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), ("Value", "%!event!item.name%"))]));
        Goal(into, "Run", ("return %seen%", [Make.Action(context, "goal", "return", ("Data", "%seen%"))]));
    }

    // A goal of the steps given, each action in its step as a program's read puts it — so a frame's key finds it.
    private static void Goal(global::app.@this into, string name, params (string text, ActionEntity[] actions)[] steps)
    {
        var goal = new Goal(into.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", into.actor.list.User.Context) };
        for (var index = 0; index < steps.Length; index++)
        {
            var step = new Step(into.type.list) { Goal = goal, List = goal.Step, Index = index, Text = steps[index].text };
            foreach (var action in steps[index].actions) step.Code.Add(action.In(step));
            goal.Step.Add(step);
        }
        into.goal.list.Add(goal);
    }

    // The frame below `frame` whose bound call is running, if any.
    private static global::app.call.@this? Running(global::app.call.@this frame)
        => frame.Event != null ? frame : frame.Children.Select(Running).FirstOrDefault(found => found != null);

    private static Goal Named(global::app.@this app, string name) => app.goal.list.Items().OfType<Goal>().First(goal => goal.Name == name);

    // The real case: a binding a program's step made fires before Run starts, and its call saves the run. The run goes on
    // reading %!event!item.name% again; read back, the event is the resumed app's own, holding the binding, which fires
    // again on the next goal. The frame's row is read whole and its event made from its key when it is read, so it is
    // found whatever order the app's members are read in.
    [Test]
    public async Task ARunSavedInsideABoundCall_ReadsItsEventAgain_AsTheResumedAppsOwn()
    {
        await using var src = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing().TestIdentity();
        var context = src.actor.list.User.Context;
        Program(src);
        await (await Named(src, "Bind").Start(context)).IsSuccess();
        await (await Named(src, "Run").Start(context)).IsSuccess();
        var snap = await (await context.Variable.Get("snap")).Value() as global::app.snapshot.@this;
        await Assert.That(snap).IsNotNull();
        // the event's key names it by the on.event action that made it and its place among that action's events
        await Assert.That(System.Text.RegularExpressions.Regex.Replace(await snap!.Text(context), @"\s", ""))
            .Contains("\"event\":{\"type\":{\"name\":\"event\"},\"value\":{\"action\":");

        await using var dst = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing().TestIdentity();
        Program(dst);
        var into = dst.actor.list.User.Context;
        await dst.Restore(snap!, into);
        var running = Running(into.call.Root!);
        await Assert.That(running).IsNotNull();
        var start = dst.@event[dst.type.list["goal"]]?["start"];
        await Assert.That(start).IsNotNull();
        await Assert.That(start!.before.Count).IsEqualTo(1);
        await Assert.That(await running!.Event!.Value()).IsSameReferenceAs(start.before[0]);
        await (await Named(dst, "Other").Start(into)).IsSuccess();
        await Assert.That(await into.Variable.GetValue("seen")).IsEqualTo("Other");

        var resumed = await snap!.Resumed(context);
        await resumed.IsSuccess();
        await Assert.That((await resumed.Value())?.ToString()).IsEqualTo("Run");
    }
}

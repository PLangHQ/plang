using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

// The event running in a frame (%!event%) comes back exactly where it was bound, its bindings active on resume: the
// running event is kept as what finds it again in the restored hold. Stage 10's: the actor's hold is saved there.
public class CallStackEventTests : System.IAsyncDisposable
{
    private const string Stage10 = "stage 10: the running %!event% comes back where it was bound, with the actor's hold";

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

    [Test, Skip(Stage10)]
    public async Task CallStack_RoundTrip_TheEventRunningInAFrame_ComesBackWithIt()
    {
        var (g1, _, a1) = MakeFrame("EventGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;

        await using (var frame = context.call.Push(a1, await context.call.Setting()))
        {
            frame.Event = new global::app.data.@this("!event", "fired", context: context);
            var snap = (await src.Snapshot(context).Saved());

            var (dg1, _, _) = MakeFrame("EventGoal");
            var dst = BuildAppWithGoals(dg1);
            await dst.Restore(snap, dst.actor.list.User.Context);

            var root = dst.actor.list.User.Context.call.Root!;
            await Assert.That(root.Event).IsNotNull();
            await Assert.That((await root.Event!.Value())?.ToString()).IsEqualTo("fired");
        }
    }

    [Test, Skip(Stage10)]
    public async Task CallStack_ThroughText_TheEventRunningInAFrame_ComesBackWithIt()
    {
        // %!event% as a bound call sees it: the running event, with what it fired for and the result so far
        var (g1, _, a1) = MakeFrame("EventTextGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;

        await using (var frame = context.call.Push(a1, await context.call.Setting()))
        {
            var running = new global::app.data.@this("!event", src.goal.type.on["start"]!, context: context);
            running.Properties.Set("item", g1);
            frame.Event = running;
            var text = await src.Snapshot(context).Saved().Text(context);

            var (dg1, _, _) = MakeFrame("EventTextGoal");
            var dst = BuildAppWithGoals(dg1);
            var into = dst.actor.list.User.Context;
            await dst.Restore(await into.Snapshot(text), into);

            var root = into.call.Root!;
            await Assert.That(root.Event).IsNotNull();
            await Assert.That(await root.Event!.Value()).IsSameReferenceAs(dst.goal.type.on["start"]);
        }
    }
}

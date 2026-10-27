using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class CallStackSnapshotTests : System.IAsyncDisposable
{
    // rooted where the apps its goals are put in are, so a goal's .pr is the one its own app's list holds
    private readonly global::app.@this app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private (Goal goal, Step step, ActionEntity action) MakeFrame(
        string goalName, string stepText = "step", string module = "test", string actionName = "test")
    {
        var goal = new Goal(app.type.list) { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", app.actor.list.User.Context) };
        var step = new Step(app.type.list) { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity(app.type.list) { Module = app.actor.list.User.Context.App.Module(module), Name = actionName, Step = step };
        step.Code.Add(action);
        goal.Step.Add(step);
        return (goal, step, action);
    }

    private global::app.@this BuildAppWithGoals(params Goal[] goals)
    {
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        foreach (var g in goals) app.goal.list.Add(g);
        return app;
    }

    [Test]
    public async Task CallStack_Store_WhileATaskRuns_IsRefused()
    {
        // a running task's first frame sits among its caller's children, but goes on in the task's own stack
        var (g1, _, a1) = MakeFrame("TaskOuter");
        var app = BuildAppWithGoals(g1);
        var user = app.actor.list.User;
        var stack = user.Context.call;
        await using var outer = stack.Push(a1, await stack.Setting());
        using var task = user.Context.Child(user, CancellationToken.None);
        await using var taskFrame = task.call.Isolate(null, await task.call.Setting(), caller: outer);

        var saved = await app.Snapshot(user.Context);

        await Assert.That(saved.Success).IsFalse();
        await Assert.That(saved.Error!.Key).IsEqualTo("SnapshotWhileTaskRuns");
    }

    [Test]
    public async Task CallStack_RoundTrip_AFramesErrors_ComeBackWithIt()
    {
        var (g1, _, a1) = MakeFrame("ErrGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;
        var stack = context.call;

        await using (var frame = stack.Push(a1, await stack.Setting()))
        {
            frame.Record(new global::app.error.@this("it broke", "Broke", 500), context);
            var snap = (await src.Snapshot(context).Saved());

            var (dg1, _, _) = MakeFrame("ErrGoal");
            var dst = BuildAppWithGoals(dg1);
            await dst.Restore(snap, dst.actor.list.User.Context);

            var root = dst.actor.list.User.Context.call.Root!;
            await Assert.That(root.Error).IsNotNull();
            await Assert.That(root.Error!.Key).IsEqualTo("Broke");
        }
    }

    [Test]
    public async Task CallStack_ThroughText_AFramesErrors_ComeBackWithIt()
    {
        var (g1, _, a1) = MakeFrame("ErrTextGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;

        await using (var frame = context.call.Push(a1, await context.call.Setting()))
        {
            frame.Record(new global::app.error.@this("it broke", "Broke", 500), context);
            var text = await src.Snapshot(context).Saved().Text(context);

            var (dg1, _, _) = MakeFrame("ErrTextGoal");
            var dst = BuildAppWithGoals(dg1);
            var into = dst.actor.list.User.Context;
            await dst.Restore(await into.Snapshot(text), into);

            var root = into.call.Root!;
            await Assert.That(root.Error).IsNotNull();
            await Assert.That(root.Error!.Key).IsEqualTo("Broke");
            await Assert.That(root.Error.Message).IsEqualTo("it broke");
        }
    }

    [Test]
    public async Task CallStack_ThroughText_ABinding_ComesBackAsABinding_WithItsNames()
    {
        // a loop's %item%: a binding under the action's frame, through its own type's Create
        var (g1, _, a1) = MakeFrame("BindTextGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;

        await using (var frame = context.call.Push(a1, await context.call.Setting()))
        await using (context.call.Push([new global::app.data.@this("item", "apple", context: context)], await context.call.Setting()))
        {
            var text = await src.Snapshot(context).Saved().Text(context);

            var (dg1, _, _) = MakeFrame("BindTextGoal");
            var dst = BuildAppWithGoals(dg1);
            var into = dst.actor.list.User.Context;
            await dst.Restore(await into.Snapshot(text), into);

            var binding = into.call.Root!.Children.Single();
            await Assert.That(binding is global::app.call.binding.@this and not global::app.call.isolated.@this).IsTrue();
            await Assert.That(binding.Goal).IsSameReferenceAs(into.call.Root!.Goal);    // it stands where its caller stands
            await Assert.That(binding.TryGet("item", out var item)).IsTrue();
            await Assert.That((await item.Value())?.ToString()).IsEqualTo("apple");
        }
    }

    [Test]
    public async Task CallStack_ThroughText_AnIsolatedFrame_ComesBackIsolated_KeepingItsWrites()
    {
        // a tool's goal runs in an isolated frame: what was written under it stays in it, and still does once it is back
        var (g1, _, a1) = MakeFrame("IsolateTextGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;

        await using (var frame = context.call.Push(a1, await context.call.Setting()))
        await using (context.call.Isolate([new global::app.data.@this("city", "Reykjavik", context: context)], await context.call.Setting()))
        {
            var text = await src.Snapshot(context).Saved().Text(context);

            var (dg1, _, _) = MakeFrame("IsolateTextGoal");
            var dst = BuildAppWithGoals(dg1);
            var into = dst.actor.list.User.Context;
            await dst.Restore(await into.Snapshot(text), into);

            var isolated = into.call.Root!.Children.Single();
            await Assert.That(isolated is global::app.call.isolated.@this).IsTrue();
            await Assert.That(isolated.TryGet("city", out var city)).IsTrue();
            await Assert.That((await city.Value())?.ToString()).IsEqualTo("Reykjavik");
            await Assert.That(isolated.Keeper("anything")).IsSameReferenceAs(isolated);    // every write still lands in it
        }
    }

    [Test]
    public async Task CallStack_Start_FindsTheGoalByItsAddress_NotItsName()
    {
        // two files each hold a goal named Show: the frame's goal is the one at its address
        var (g1, _, a1) = MakeFrame("Show");
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var other = new Goal(app.type.list) { Name = "Show", Path = global::app.type.item.path.@this.Resolve("/other/Show.goal", app.actor.list.User.Context) };
        var otherStep = new Step(app.type.list) { Index = 0, Text = "step", Goal = other };
        otherStep.Code.Add(new ActionEntity(app.type.list) { Module = app.Module("test"), Name = "test", Step = otherStep });
        other.Step.Add(otherStep);
        app.goal.list.Add(g1);
        app.goal.list.Add(other);
        var context = app.actor.list.User.Context;

        global::app.snapshot.@this snap;
        await using (context.call.Push(a1, await context.call.Setting()))
            snap = (await app.Snapshot(context).Saved());
        await app.Restore(snap, context);

        await Assert.That(context.call.Root!.Goal).IsSameReferenceAs(g1);
    }

    [Test]
    public async Task CallStack_RoundTrip_GoalStepAndActionFrames_EachComeBackWhereTheyStood()
    {
        // A real run's chain: goal → step → action (a call) → goal → step → action — every frame stored, every one back.
        var (g1, s1, a1) = MakeFrame("FrOuter");
        var (g2, s2, a2) = MakeFrame("FrInner");
        var src = BuildAppWithGoals(g1, g2);
        var stack = src.actor.list.User.Context.call;

        await using (stack.Push(g1, await stack.Setting()))
        await using (stack.Push(s1, await stack.Setting()))
        await using (stack.Push(a1, await stack.Setting()))
        await using (stack.Push(g2, await stack.Setting()))
        await using (stack.Push(s2, await stack.Setting()))
        await using (stack.Push(a2, await stack.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var (dg1, _, _) = MakeFrame("FrOuter");
            var (dg2, _, _) = MakeFrame("FrInner");
            var dst = BuildAppWithGoals(dg1, dg2);
            await dst.Restore(snap, dst.actor.list.User.Context);

            var frames = new List<global::app.call.@this>();
            for (var frame = dst.actor.list.User.Context.call.Root; frame != null; frame = frame.Children.FirstOrDefault())
                frames.Add(frame);
            await Assert.That(frames.Count).IsEqualTo(6);
            await Assert.That(frames[0].Goal!.Name).IsEqualTo("FrOuter");
            await Assert.That(frames[0].Step).IsNull();                    // the goal's own frame
            await Assert.That(frames[1].Step).IsSameReferenceAs(dg1.Step[0]);
            await Assert.That(frames[1].Action).IsNull();                  // the step's frame
            await Assert.That(frames[5].Goal!.Name).IsEqualTo("FrInner");
            await Assert.That(frames[5].Action!.Index).IsEqualTo(0);
            await Assert.That((int)frames[5].Depth.ToInt32()).IsEqualTo(6);
        }
    }

    [Test]
    public async Task CallStack_RoundTrip_AFramesOwnVariables_ComeBackWithIt()
    {
        var (g1, _, _) = MakeFrame("VarGoal");
        var src = BuildAppWithGoals(g1);
        var context = src.actor.list.User.Context;
        var stack = context.call;

        await using (stack.Push(g1, await stack.Setting(), [new global::app.data.@this("who", "ingi", context: context)]))
        {
            var snap = (await src.Snapshot(context).Saved());

            var (dg1, _, _) = MakeFrame("VarGoal");
            var dst = BuildAppWithGoals(dg1);
            await dst.Restore(snap, dst.actor.list.User.Context);

            var root = dst.actor.list.User.Context.call.Root!;
            await Assert.That(root.TryGet("who", out var who)).IsTrue();
            await Assert.That((await who.Value())?.ToString()).IsEqualTo("ingi");
            await Assert.That(root.Arguments.Contains("who")).IsTrue();
        }
    }
}

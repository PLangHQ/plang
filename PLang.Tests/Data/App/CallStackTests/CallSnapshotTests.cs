using app.error;
using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class CallSnapshotTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app", global::PLang.Tests.Parent.Actor).Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private (global::app.@this app, ActionEntity action) BuildLiveAction(
        string goalName = "TestGoal", string stepText = "test step",
        string module = "test", string actionName = "test")
    {
        var app = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        var goal = new Goal(app.type.list) { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", app.actor.list.User.Context) };
        var step = new Step(app.type.list) { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity(app.type.list) { Module = app.actor.list.User.Context.App.Module(module), Name = actionName, Step = step };
        step.Code.Add(action);
        goal.Step.Add(step);
        app.goal.list.Add(goal);
        return (app, action);
    }

    // A goal in dst like the one saved: its one step's text and action as given.
    private static (Goal goal, Step step, ActionEntity action) Build(global::app.@this dst, string goalName, string stepText,
        string module = "test", string actionName = "test")
    {
        var goal = new Goal(dst.type.list) { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", dst.actor.list.User.Context) };
        var step = new Step(dst.type.list) { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity(dst.type.list) { Module = dst.actor.list.User.Context.App.Module(module), Name = actionName, Step = step };
        step.Code.Add(action);
        goal.Step.Add(step);
        dst.goal.list.Add(goal);
        return (goal, step, action);
    }

    // A frame as the snapshot writes it: its tagged members, its element as the key that finds it.
    private static async Task<System.Text.Json.Nodes.JsonObject> Written(global::app.call.@this frame, global::app.actor.context.@this context)
        => System.Text.Json.Nodes.JsonNode.Parse((string)(await global::app.type.item.wire.@this.Of(frame, global::app.View.Snapshot, context)).Raw)!.AsObject();

    // The frame a restored run stopped in: the last frame down from the root.
    private static global::app.call.@this Stopped(global::app.call.@this frame)
        => frame.Children.LastOrDefault() is { } child ? Stopped(child) : frame;

    // A step's frame writes its step as the key that finds it: its goal's name and .goal, and its index — never the goal
    // written out, and no hash: a goal changed since the save is the goal as it is now.
    [Test]
    public async Task Call_Store_WritesItsStep_AsItsKey_NotTheWholeGoal()
    {
        var (app, action) = BuildLiveAction("StubGoal");
        var stack = app.actor.list.User.Context.call;
        await using var step = stack.Push(action.Step!, await stack.Setting());

        var json = await Written(step, app.actor.list.User.Context);

        await Assert.That(json["step"]!["goal"]!["name"]!.GetValue<string>()).IsEqualTo("StubGoal");
        await Assert.That(json["step"]!["goal"]!.AsObject().ContainsKey("hash")).IsFalse();
        await Assert.That(json.ToJsonString()).DoesNotContain("test step");
    }

    // An action's frame is written as what it keeps: its place in its step and what stood there, under its caller (its
    // step's frame) — never the action's key (its step is its caller's), an id or its children.
    [Test]
    public async Task Call_Capture_WritesOnlyWhatItKeeps()
    {
        var (app, action) = BuildLiveAction("G", "a step");
        var user = app.actor.list.User.Context;
        await using var step = user.call.Push(action.Step!, await user.call.Setting());
        await using var call = user.call.Push(action, await user.call.Setting());

        var json = await Written(call, user);

        foreach (var member in new[] { "index", "module", "name", "caller" })
            await Assert.That(json.ContainsKey(member)).IsTrue();
        foreach (var member in new[] { "action", "id", "goal", "step", "handled", "children", "current" })
            await Assert.That(json.ContainsKey(member)).IsFalse();
    }

    // The two numbers that say where a run stands: the step's index (its frame) and the action's (its frame), each once.
    [Test]
    public async Task Call_Capture_IncludesItsStepAndActionIndex()
    {
        var (app, action) = BuildLiveAction("PosGoal");
        var stack = app.actor.list.User.Context.call;
        await using var step = stack.Push(action.Step!, await stack.Setting());
        await using var call = stack.Push(action, await stack.Setting());

        await Assert.That((await Written(call, app.actor.list.User.Context))["index"]!.GetValue<int>()).IsEqualTo(0);
        await Assert.That((await Written(step, app.actor.list.User.Context))["step"]!["index"]!.GetValue<int>()).IsEqualTo(0);
    }

    [Test]
    public async Task Call_Restore_ResolvesGoalStubAgainstLiveRegistry()
    {
        var (src, action) = BuildLiveAction("ResolveGoal");
        var stack = src.actor.list.User.Context.call;
        await using (stack.Push(action.Step!, await stack.Setting()))
        await using (stack.Push(action, await stack.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var (dstGoal, _, dstAction) = Build(dst, "ResolveGoal", action.Step!.Text);

            await dst.Restore(snap, dst.actor.list.User.Context);

            var frame = Stopped(dst.actor.list.User.Context.call.Root!);
            await Assert.That(frame.Goal).IsSameReferenceAs(dstGoal);
            await Assert.That(frame.Action).IsSameReferenceAs(dstAction);
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnGoalNotFound()
    {
        var (src, action) = BuildLiveAction("DisappearingGoal");
        var stack = src.actor.list.User.Context.call;
        await using (stack.Push(action.Step!, await stack.Setting()))
        await using (stack.Push(action, await stack.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());
            // Restore on a fresh App that never had this goal registered.
            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();

            var started = await snap.Start(dst, dst.actor.list.User.Context);
            await Assert.That(started.Success).IsFalse();
            // the goal list's own answer, whole: its key names a .goal this app never built
            await Assert.That(started.Error!.Key).IsEqualTo("GoalNotBuilt");
        }
    }

    // A goal changed since the save (its step's text, so its hash) is the goal as it is now: the run comes back standing
    // at its saved place in it.
    [Test]
    public async Task Call_Restore_AGoalChangedSinceTheSave_StandsAtItsPlaceInIt()
    {
        var (src, action) = BuildLiveAction("HashGoal", "original step text");
        var stack = src.actor.list.User.Context.call;
        await using (stack.Push(action.Step!, await stack.Setting()))
        await using (stack.Push(action, await stack.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var (_, _, dstAction) = Build(dst, "HashGoal", "DIFFERENT step text");

            await dst.Restore(snap, dst.actor.list.User.Context);

            await Assert.That(Stopped(dst.actor.list.User.Context.call.Root!).Action).IsSameReferenceAs(dstAction);
        }
    }

    // Another action standing at the saved place (the goal rebuilt, or the place edited): the run goes on there, and
    // says so on the error channel, naming the action saved and the one there now.
    [Test]
    public async Task Call_Resume_AnotherActionAtThePlace_GoesOnThere_AndSaysSo()
    {
        var (src, action) = BuildLiveAction("RecompiledGoal", "same step text");
        var stack = src.actor.list.User.Context.call;
        await using (stack.Push(action.Step!, await stack.Setting()))
        await using (stack.Push(action, await stack.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var (_, _, dstAction) = Build(dst, "RecompiledGoal", "same step text", "variable", "set");
            var error = new System.IO.MemoryStream();
            dst.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(dst.actor.list.User.Channel,
                global::app.channel.list.@this.Error, error, global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true });
            var into = dst.actor.list.User.Context;
            await dst.Restore(snap, into);
            await Assert.That(Stopped(into.call.Root!).Action).IsSameReferenceAs(dstAction);

            await into.call.Resume(into);

            var said = System.Text.Encoding.UTF8.GetString(error.ToArray());
            await Assert.That(said).Contains("variable.set");
            await Assert.That(said).Contains("test.test");
        }
    }

    // A goal with lines as its steps (each line one or more actions, split by " ; "), held by app.
    private static async Task<Goal> Program(global::app.@this app, string name, params string[] lines)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", context) };
        for (var index = 0; index < lines.Length; index++)
        {
            var step = new Step(app.type.list) { Goal = goal, Index = index, Text = lines[index] };
            foreach (var call in lines[index].Split(" ; ")) step.Code.Add((await context.Action(call)).In(step));
            goal.Step.Add(step);
        }
        app.goal.list.Add(goal);
        return goal;
    }

    // A step inserted where the run stopped (the goal rebuilt since the save): the run goes on at its saved place, now
    // the new step — said on the error channel — then the step it stopped in, now after it; the steps before are not run
    // again.
    [Test]
    public async Task Call_Resume_AStepInsertedWhereTheRunStopped_RunsIt_ThenTheStepItStoppedIn()
    {
        var (src, _) = BuildLiveAction("Unused");
        var goal = await Program(src, "Order", "variable.set(Name=%a%, Value=\"0\")", "variable.set(Name=%b%, Value=\"1\")",
            "goal.return(Data={\"a\": %a%, \"b\": %b%, \"c\": %c%})");
        var context = src.actor.list.User.Context;
        foreach (var name in new[] { "a", "b", "c" }) await context.Variable.Set(name, "before");
        global::app.snapshot.@this snap;
        await using (context.call.Push(goal.Step[1], await context.call.Setting()))
        await using (context.call.Push(goal.Step[1].Code[0], await context.call.Setting(), context.Variable))
            snap = await src.Snapshot(context).Saved();

        var dst = new global::app.@this("/test", global::PLang.Tests.Parent.Actor).Testing();
        await Program(dst, "Order", "variable.set(Name=%a%, Value=\"0\")",
            "math.add(A=1, B=2) ; variable.set(Name=%c%, Value=%!data%)",
            "variable.set(Name=%b%, Value=\"1\")",
            "goal.return(Data={\"a\": %a%, \"b\": %b%, \"c\": %c%})");
        var error = new System.IO.MemoryStream();
        dst.actor.list.User.Channel.Register(new global::app.channel.type.stream.@this(dst.actor.list.User.Channel,
            global::app.channel.list.@this.Error, error, global::app.channel.ChannelDirection.Output, ownsStream: false) { Mime = "text/plain", Framed = true });
        var into = dst.actor.list.User.Context;
        await dst.Restore(snap, into);

        var result = await into.call.Resume(into);

        await result.IsSuccess();
        async Task<string?> Saw(string name) => (await (await result.Get(name)).Value())?.ToString();
        await Assert.That(await Saw("a")).IsEqualTo("before");
        // the action at the place ran (its answer is %c%), not only the rest of its step
        await Assert.That(await Saw("c")).IsEqualTo("3");
        await Assert.That(await Saw("b")).IsEqualTo("1");
        var said = System.Text.Encoding.UTF8.GetString(error.ToArray());
        await Assert.That(said).Contains("math.add");
        await Assert.That(said).Contains("variable.set");
    }

    // A saved action's place past its step's end is refused, naming how many actions the step holds.
    [Test]
    public async Task Call_Restore_AnActionPastItsStepsEnd_IsRefused_NamingTheCount()
    {
        var (src, action) = BuildLiveAction("ShortGoal");
        var extra = new ActionEntity(src.type.list) { Module = src.actor.list.User.Context.App.Module("test"), Name = "test", Step = action.Step };
        action.Step!.Code.Add(extra);
        var stack = src.actor.list.User.Context.call;
        await using (stack.Push(action.Step!, await stack.Setting()))
        await using (stack.Push(extra, await stack.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            Build(dst, "ShortGoal", action.Step.Text);

            var started = await snap.Start(dst, dst.actor.list.User.Context);
            await Assert.That(started.Success).IsFalse();
            await Assert.That(started.Error!.Key).IsEqualTo("ActionNotFound");
            await Assert.That(started.Error.Message).Contains("(1 actions)");
        }
    }

    [Test]
    public async Task Call_Restore_DoesNotMutateLiveGoal()
    {
        var (src, action) = BuildLiveAction("PureGoal");
        var stack = src.actor.list.User.Context.call;
        await using (stack.Push(action.Step!, await stack.Setting()))
        await using (stack.Push(action, await stack.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var (goalBefore, stepBefore, actionBefore) = Build(dst, "PureGoal", action.Step!.Text);

            await dst.Restore(snap, dst.actor.list.User.Context);

            // Same instances — Restore is read-only on the registry.
            await Assert.That(await dst.goal.list.Find("PureGoal").Found()).IsSameReferenceAs(goalBefore);
            await Assert.That(goalBefore.Step[0]).IsSameReferenceAs(stepBefore);
            await Assert.That(stepBefore.Code[0]).IsSameReferenceAs(actionBefore);
        }
    }

    [Test]
    public async Task Call_Capture_OmitsTimingTier_AndInFlightNetworkState()
    {
        var (app, action) = BuildLiveAction("DropGoal");
        (await app.actor.list.User.Context.call.Setting()).Timing = true;
        await using var call = app.actor.list.User.Context.call.Push(action, await app.actor.list.User.Context.call.Setting());

        var json = await Written(call, app.actor.list.User.Context);

        // Drop bucket: timing tier and any in-flight network state never reach the snapshot.
        await Assert.That(json.ContainsKey("startedAt")).IsFalse();
        await Assert.That(json.ContainsKey("completedAt")).IsFalse();
        await Assert.That(json.ContainsKey("duration")).IsFalse();
        await Assert.That(json.ContainsKey("inFlight")).IsFalse();
    }
}

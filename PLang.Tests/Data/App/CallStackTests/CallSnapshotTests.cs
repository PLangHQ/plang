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

    // A frame as the snapshot writes it: its tagged members, its element as the key that finds it.
    private static async Task<System.Text.Json.Nodes.JsonObject> Written(global::app.call.@this frame, global::app.actor.context.@this context)
        => System.Text.Json.Nodes.JsonNode.Parse((string)(await global::app.type.item.wire.@this.Of(frame, global::app.View.Snapshot, context)).Raw)!.AsObject();

    [Test]
    public async Task Call_Store_WritesItsGoal_AsItsKey_NotTheWholeGoal()
    {
        var (app, action) = BuildLiveAction("StubGoal");
        var stack = app.actor.list.User.Context.call;
        await using var call = stack.Push(action, await stack.Setting());

        var json = await Written(call, app.actor.list.User.Context);

        // the goal is what finds it — its name and hash — never the goal written out
        await Assert.That(json["action"]!["step"]!["goal"]!["name"]!.GetValue<string>()).IsEqualTo("StubGoal");
        await Assert.That(json["action"]!["step"]!["goal"]!["hash"]!.GetValue<string>()).IsEqualTo(action.Step!.Goal!.Hash);
        await Assert.That(json.ToJsonString()).DoesNotContain("test step");
    }

    // A frame is written as what it keeps: its element, never an id, a place of its own, its caller or its children.
    [Test]
    public async Task Call_Capture_WritesOnlyWhatItKeeps()
    {
        var (app, action) = BuildLiveAction("G", "a step");
        var user = app.actor.list.User.Context;
        await using var call = user.call.Push(action, await user.call.Setting());

        var json = await Written(call, user);

        await Assert.That(json.ContainsKey("action")).IsTrue();
        foreach (var member in new[] { "id", "goal", "step", "index", "handled", "caller", "children", "current" })
            await Assert.That(json.ContainsKey(member)).IsFalse();
    }

    [Test]
    public async Task Call_Capture_IncludesItsStepAndActionIndex()
    {
        var (app, action) = BuildLiveAction("PosGoal");
        var stack = app.actor.list.User.Context.call;
        await using var call = stack.Push(action, await stack.Setting());

        var json = await Written(call, app.actor.list.User.Context);

        await Assert.That(json["action"]!["index"]!.GetValue<int>()).IsEqualTo(0);
        await Assert.That(json["action"]!["step"]!["index"]!.GetValue<int>()).IsEqualTo(0);
    }

    [Test]
    public async Task Call_Restore_ResolvesGoalStubAgainstLiveRegistry()
    {
        var (src, action) = BuildLiveAction("ResolveGoal");
        await using (var call = src.actor.list.User.Context.call.Push(action, await src.actor.list.User.Context.call.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            // Build a fresh app with the *same* goal registered.
            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list)
            {
                Name = "ResolveGoal",
                Path = global::app.type.item.path.@this.Resolve("/ResolveGoal.goal", dst.actor.list.User.Context)
            };
            var dstStep = new Step(dst.type.list) { Index = 0, Text = action.Step!.Text, Goal = dstGoal };
            var dstAction = new ActionEntity(dst.type.list) { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            await dst.Restore(snap, dst.actor.list.User.Context);

            var frame = dst.actor.list.User.Context.call.Root;
            await Assert.That(frame).IsNotNull();
            await Assert.That(frame!.Goal).IsSameReferenceAs(dstGoal);
            await Assert.That(frame.Action).IsSameReferenceAs(dstAction);
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnGoalNotFound()
    {
        var (src, action) = BuildLiveAction("DisappearingGoal");
        await using (var call = src.actor.list.User.Context.call.Push(action, await src.actor.list.User.Context.call.Setting()))
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

    [Test]
    public async Task Call_Restore_HardErrors_OnHashMismatch_RaisesCallbackGoalHashMismatch()
    {
        var (src, action) = BuildLiveAction("HashGoal", "original step text");
        await using (var call = src.actor.list.User.Context.call.Push(action, await src.actor.list.User.Context.call.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            // Fresh App with the same path but different hash (different step prose).
            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list) { Name = "HashGoal", Path = global::app.type.item.path.@this.Resolve("/HashGoal.goal", dst.actor.list.User.Context) };
            var dstStep = new Step(dst.type.list) { Index = 0, Text = "DIFFERENT step text", Goal = dstGoal };
            var dstAction = new ActionEntity(dst.type.list) { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            var started = await snap.Start(dst, dst.actor.list.User.Context);
            // the goal's key refuses a goal changed since the save
            await Assert.That(started.Success).IsFalse();
            await Assert.That(started.Error!.Key).IsEqualTo("GoalHashMismatch");
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_WhenTheActionAtThePositionChanged()
    {
        // Same step text (so the goal hash matches) compiled to a different action: the position now
        // points at another action, which only the captured module/name can tell.
        var (src, action) = BuildLiveAction("RecompiledGoal", "same step text");
        await using (var call = src.actor.list.User.Context.call.Push(action, await src.actor.list.User.Context.call.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list) { Name = "RecompiledGoal", Path = global::app.type.item.path.@this.Resolve("/RecompiledGoal.goal", dst.actor.list.User.Context) };
            var dstStep = new Step(dst.type.list) { Index = 0, Text = "same step text", Goal = dstGoal };
            var dstAction = new ActionEntity(dst.type.list) { Module = app.actor.list.User.Context.App.Module("variable"), Name = "set", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            var started = await snap.Start(dst, dst.actor.list.User.Context);
            await Assert.That(started.Success).IsFalse();
            await Assert.That(started.Error!.Message).Contains("test.test");
            await Assert.That(started.Error.Message).Contains("variable.set");
        }
    }

    [Test]
    public async Task Call_Restore_DoesNotMutateLiveGoal()
    {
        var (src, action) = BuildLiveAction("PureGoal");
        await using (var call = src.actor.list.User.Context.call.Push(action, await src.actor.list.User.Context.call.Setting()))
        {
            var snap = (await src.Snapshot(src.actor.list.User.Context).Saved());

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list) { Name = "PureGoal", Path = global::app.type.item.path.@this.Resolve("/PureGoal.goal", dst.actor.list.User.Context) };
            var dstStep = new Step(dst.type.list) { Index = 0, Text = action.Step!.Text, Goal = dstGoal };
            var dstAction = new ActionEntity(dst.type.list) { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            var goalBefore = dstGoal;
            var stepBefore = dstStep;
            var actionBefore = dstAction;

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

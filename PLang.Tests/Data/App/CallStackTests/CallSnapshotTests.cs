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

    [Test]
    public async Task Call_Store_WritesItsGoal_AsAddressPlusHash_NotTheWholeGoal()
    {
        var (app, action) = BuildLiveAction("StubGoal");
        var stack = app.actor.list.User.Context.call;
        await using var call = stack.Push(action, await stack.Setting());

        var snap = new Snapshot(app.actor.list.User.Context);
        call.Store(snap, app.actor.list.User.Context);

        // the goal is the name a call reaches it by, and its hash — never the goal written out
        await Assert.That(await snap.Node("goal").Text("address")).IsEqualTo(action.Step!.Goal!.Address);
        await Assert.That(await snap.Node("goal").Text("hash")).IsEqualTo(action.Step.Goal.Hash);
        await Assert.That(snap.Has("steps")).IsFalse();
    }

    // A frame's captured position is written exactly so — its facts ride as the plain tokens they always have,
    // whatever their plang faces are.
    [Test]
    public async Task Call_Capture_WritesItsExactShape()
    {
        var (app, action) = BuildLiveAction("G", "a step");
        app.Compact();
        var user = app.actor.list.User.Context;
        await using var call = app.actor.list.User.Context.call.Push(action, await app.actor.list.User.Context.call.Setting());
        var snap = new Snapshot(user);
        call.Store(snap, app.actor.list.User.Context);

        using var ms = new MemoryStream();
        await user.Format("application/json").Encode(ms, user.Ok(snap), user);
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray()).Replace(call.Id.ToString()!, "ID");

        await Assert.That(json).IsEqualTo(
            "{\"id\":\"ID\",\"type\":{\"name\":\"app.call.action\"},\"goal\":{\"address\":\"/G\",\"hash\":\"" + action.Step!.Goal!.Hash
            + "\"},\"step\":0,\"action\":{\"index\":0,\"module\":\"test\",\"name\":\"test\"},\"handled\":false}");
    }

    [Test]
    public async Task Call_Capture_IncludesItsStepAndActionIndex()
    {
        var (app, action) = BuildLiveAction("PosGoal");
        var stack = app.actor.list.User.Context.call;
        await using var call = stack.Push(action, await stack.Setting());

        var snap = new Snapshot(app.actor.list.User.Context);
        call.Store(snap, app.actor.list.User.Context);

        await Assert.That(await snap.Int("step")).IsEqualTo(0);
        await Assert.That(await snap.Node("action").Int("index")).IsEqualTo(0);
    }

    [Test]
    public async Task Call_Restore_ResolvesGoalStubAgainstLiveRegistry()
    {
        var (src, action) = BuildLiveAction("ResolveGoal");
        await using (var call = src.actor.list.User.Context.call.Push(action, await src.actor.list.User.Context.call.Setting()))
        {
            var snap = src.Snapshot(src.actor.list.User.Context).Saved();

            // Build a fresh app with the *same* goal registered.
            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list)
            {
                Name = "ResolveGoal",
                Path = global::app.type.item.path.@this.Resolve("/ResolveGoal.goal", app.actor.list.User.Context)
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
            var snap = src.Snapshot(src.actor.list.User.Context).Saved();
            // Restore on a fresh App that never had this goal registered.
            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();

            var started = await snap.Start(dst, dst.actor.list.User.Context);
            await Assert.That(started.Success).IsFalse();
            // the goal list's own answer, whole
            await Assert.That(started.Error!.Key).IsEqualTo("NotFound");
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnHashMismatch_RaisesCallbackGoalHashMismatch()
    {
        var (src, action) = BuildLiveAction("HashGoal", "original step text");
        await using (var call = src.actor.list.User.Context.call.Push(action, await src.actor.list.User.Context.call.Setting()))
        {
            var snap = src.Snapshot(src.actor.list.User.Context).Saved();

            // Fresh App with the same path but different hash (different step prose).
            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list) { Name = "HashGoal", Path = global::app.type.item.path.@this.Resolve("/HashGoal.goal", app.actor.list.User.Context) };
            var dstStep = new Step(dst.type.list) { Index = 0, Text = "DIFFERENT step text", Goal = dstGoal };
            var dstAction = new ActionEntity(dst.type.list) { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            var started = await snap.Start(dst, dst.actor.list.User.Context);
            // a keyed program error: the resume's result carries CallbackGoalHashMismatch, not ServiceError
            await Assert.That(started.Success).IsFalse();
            await Assert.That(started.Error!.Key).IsEqualTo("CallbackGoalHashMismatch");
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
            var snap = src.Snapshot(src.actor.list.User.Context).Saved();

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list) { Name = "RecompiledGoal", Path = global::app.type.item.path.@this.Resolve("/RecompiledGoal.goal", app.actor.list.User.Context) };
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
            var snap = src.Snapshot(src.actor.list.User.Context).Saved();

            var dst = new global::app.@this("/dst", global::PLang.Tests.Parent.Actor).Testing();
            var dstGoal = new Goal(dst.type.list) { Name = "PureGoal", Path = global::app.type.item.path.@this.Resolve("/PureGoal.goal", app.actor.list.User.Context) };
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

        var snap = new Snapshot(app.actor.list.User.Context);
        call.Store(snap, app.actor.list.User.Context);

        // Drop bucket: timing tier and any in-flight network state never reach the snapshot.
        await Assert.That(snap.Has("startedAt")).IsFalse();
        await Assert.That(snap.Has("completedAt")).IsFalse();
        await Assert.That(snap.Has("duration")).IsFalse();
        await Assert.That(snap.Has("inFlight")).IsFalse();
    }
}

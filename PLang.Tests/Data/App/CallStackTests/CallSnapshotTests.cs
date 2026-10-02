using app.error;
using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class CallSnapshotTests : System.IAsyncDisposable
{
    private readonly global::app.@this app = new global::app.@this("/app").Testing();
    public async System.Threading.Tasks.ValueTask DisposeAsync() => await app.DisposeAsync();

    private (global::app.@this app, ActionEntity action) BuildLiveAction(
        string goalName = "TestGoal", string stepText = "test step",
        string module = "test", string actionName = "test")
    {
        var app = new global::app.@this("/test").Testing();
        var goal = new Goal { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", app.actor.list.User.Context) };
        var step = new Step { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity { Module = app.actor.list.User.Context.App.Module(module), Name = actionName, Step = step };
        step.Code.Add(action);
        goal.Step.Add(step);
        app.goal.list.Add(goal);
        return (app, action);
    }

    [Test]
    public async Task Call_Capture_EmitsGoalStub_PrPathPlusHash_NotFullGoal()
    {
        var (app, action) = BuildLiveAction("StubGoal");
        var stack = app.actor.list.User.CallStack;
        await using var call = stack.Push(action);

        var snap = new Snapshot(app.actor.list.User.Context);
        call.Capture(snap);

        await Assert.That(await snap.Text("goalPrPath")).IsEqualTo(action.Step!.Goal!.PrPath?.ToString());
        await Assert.That(await snap.Text("goalHash")).IsEqualTo(action.Step.Goal.Hash);
        // Wire shape is the stub triple — no full goal serialised.
        await Assert.That(snap.Has("goal")).IsFalse();
        await Assert.That(snap.Has("steps")).IsFalse();
    }

    // A frame's captured position is written exactly so — its facts ride as the plain tokens they always have,
    // whatever their plang faces are.
    [Test]
    public async Task Call_Capture_WritesItsExactShape()
    {
        var (app, action) = BuildLiveAction("G", "a step");
        var user = app.actor.list.User.Context;
        await using var call = app.actor.list.User.CallStack.Push(action);
        var snap = new Snapshot(user);
        call.Capture(snap);

        using var ms = new MemoryStream();
        await user.Format("application/json").Encode(ms, user.Ok(snap), user);
        var json = System.Text.Encoding.UTF8.GetString(ms.ToArray()).Replace(call.Id.ToString()!, "ID");

        await Assert.That(json).IsEqualTo(
            "{\"goalName\":\"G\",\"goalPrPath\":\"/.build/g.pr\",\"goalHash\":\"" + action.Step!.Goal!.Hash
            + "\",\"stepIndex\":0,\"actionIndex\":0,\"actionModule\":\"test\",\"actionName\":\"test\",\"id\":\"ID\"}");
    }

    [Test]
    public async Task Call_Capture_IncludesStepIndexAndActionIndex()
    {
        var (app, action) = BuildLiveAction("PosGoal");
        var stack = app.actor.list.User.CallStack;
        await using var call = stack.Push(action);

        var snap = new Snapshot(app.actor.list.User.Context);
        call.Capture(snap);

        await Assert.That(await snap.Int("stepIndex")).IsEqualTo(0);
        await Assert.That(await snap.Int("actionIndex")).IsEqualTo(0);
    }

    [Test]
    public async Task Call_Restore_ResolvesGoalStubAgainstLiveRegistry()
    {
        var (src, action) = BuildLiveAction("ResolveGoal");
        await using (var call = src.actor.list.User.CallStack.Push(action))
        {
            var snap = src.Snapshot(src.actor.list.User.Context);

            // Build a fresh app with the *same* goal registered.
            var dst = new global::app.@this("/dst").Testing();
            var dstGoal = new Goal
            {
                Name = "ResolveGoal",
                Path = global::app.type.item.path.@this.Resolve("/ResolveGoal.goal", app.actor.list.User.Context)
            };
            var dstStep = new Step { Index = 0, Text = action.Step!.Text, Goal = dstGoal };
            var dstAction = new ActionEntity { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            await dst.Restore(snap, dst.actor.list.User.Context);

            var bottom = dst.actor.list.User.CallStack.BottomFrame;
            await Assert.That(bottom).IsNotNull();
            await Assert.That(bottom!.Goal.PrPath).IsEqualTo(dstGoal.PrPath);
            await Assert.That(bottom.Action).IsSameReferenceAs(dstAction);
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnGoalNotFound()
    {
        var (src, action) = BuildLiveAction("DisappearingGoal");
        await using (var call = src.actor.list.User.CallStack.Push(action))
        {
            var snap = src.Snapshot(src.actor.list.User.Context);
            // Restore on a fresh App that never had this goal registered.
            var dst = new global::app.@this("/dst").Testing();

            await Assert.ThrowsAsync<CallbackGoalNotFound>(async () =>
            {
                await dst.Restore(snap, dst.actor.list.User.Context);
                await Task.CompletedTask;
            });
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnHashMismatch_RaisesCallbackGoalHashMismatch()
    {
        var (src, action) = BuildLiveAction("HashGoal", "original step text");
        await using (var call = src.actor.list.User.CallStack.Push(action))
        {
            var snap = src.Snapshot(src.actor.list.User.Context);

            // Fresh App with the same path but different hash (different step prose).
            var dst = new global::app.@this("/dst").Testing();
            var dstGoal = new Goal { Name = "HashGoal", Path = global::app.type.item.path.@this.Resolve("/HashGoal.goal", app.actor.list.User.Context) };
            var dstStep = new Step { Index = 0, Text = "DIFFERENT step text", Goal = dstGoal };
            var dstAction = new ActionEntity { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            var mismatch = await Assert.ThrowsAsync<CallbackGoalHashMismatch>(async () =>
            {
                await dst.Restore(snap, dst.actor.list.User.Context);
                await Task.CompletedTask;
            });
            // a keyed program error: the resume's result carries CallbackGoalHashMismatch, not ServiceError
            await Assert.That(mismatch!.Error.Key).IsEqualTo("CallbackGoalHashMismatch");
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_WhenTheActionAtThePositionChanged()
    {
        // Same step text (so the goal hash matches) compiled to a different action: the position now
        // points at another action, which only the captured module/name can tell.
        var (src, action) = BuildLiveAction("RecompiledGoal", "same step text");
        await using (var call = src.actor.list.User.CallStack.Push(action))
        {
            var snap = src.Snapshot(src.actor.list.User.Context);

            var dst = new global::app.@this("/dst").Testing();
            var dstGoal = new Goal { Name = "RecompiledGoal", Path = global::app.type.item.path.@this.Resolve("/RecompiledGoal.goal", app.actor.list.User.Context) };
            var dstStep = new Step { Index = 0, Text = "same step text", Goal = dstGoal };
            var dstAction = new ActionEntity { Module = app.actor.list.User.Context.App.Module("variable"), Name = "set", Step = dstStep };
            dstStep.Code.Add(dstAction);
            dstGoal.Step.Add(dstStep);
            dst.goal.list.Add(dstGoal);

            var thrown = await Assert.ThrowsAsync<CallbackActionMismatch>(async () =>
            {
                await dst.Restore(snap, dst.actor.list.User.Context);
                await Task.CompletedTask;
            });
            await Assert.That(thrown!.Message).Contains("test.test");
            await Assert.That(thrown.Message).Contains("variable.set");
        }
    }

    [Test]
    public async Task Call_Restore_DoesNotMutateLiveGoal()
    {
        var (src, action) = BuildLiveAction("PureGoal");
        await using (var call = src.actor.list.User.CallStack.Push(action))
        {
            var snap = src.Snapshot(src.actor.list.User.Context);

            var dst = new global::app.@this("/dst").Testing();
            var dstGoal = new Goal { Name = "PureGoal", Path = global::app.type.item.path.@this.Resolve("/PureGoal.goal", app.actor.list.User.Context) };
            var dstStep = new Step { Index = 0, Text = action.Step!.Text, Goal = dstGoal };
            var dstAction = new ActionEntity { Module = app.actor.list.User.Context.App.Module("test"), Name = "test", Step = dstStep };
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
    public async Task Call_Restore_HashErrorIsTypedNotBoolean()
    {
        // The restore path raises a typed exception — there is no boolean Success / Failure
        // bubbling up. Restore returns a bare Task: it carries no result, so a failure has
        // nowhere to hide except a throw. The call stack restores ITSELF (an instance member).
        var restoreMethod = typeof(global::app.callstack.@this).GetMethod("Restore",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        await Assert.That(restoreMethod).IsNotNull();
        await Assert.That(restoreMethod!.ReturnType).IsEqualTo(typeof(Task));
    }

    [Test]
    public async Task Call_Capture_OmitsTimingTier_AndInFlightNetworkState()
    {
        var (app, action) = BuildLiveAction("DropGoal");
        app.actor.list.User.CallStack.Setting.Timing = true;
        await using var call = app.actor.list.User.CallStack.Push(action);

        var snap = new Snapshot(app.actor.list.User.Context);
        call.Capture(snap);

        // Drop bucket: timing tier and any in-flight network state never reach the snapshot.
        await Assert.That(snap.Has("startedAt")).IsFalse();
        await Assert.That(snap.Has("completedAt")).IsFalse();
        await Assert.That(snap.Has("duration")).IsFalse();
        await Assert.That(snap.Has("inFlight")).IsFalse();
    }
}

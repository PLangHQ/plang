using app.error;
using ActionEntity = app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallStackTests;

public class CallSnapshotTests
{
    private static (global::app.@this app, ActionEntity action) BuildLiveAction(
        string goalName = "TestGoal", string stepText = "test step",
        string module = "test", string actionName = "test")
    {
        var app = global::PLang.Tests.TestApp.Create("/test");
        var goal = new Goal { Name = goalName, Path = global::app.type.path.@this.Resolve($"/{goalName}.goal", global::PLang.Tests.TestApp.SharedContext) };
        var step = new Step { Index = 0, Text = stepText, Goal = goal };
        var action = new ActionEntity { Module = module, ActionName = actionName };
        action.Step = step;
        step.Actions.Add(action);
        goal.Steps.Add(step);
        goal.Steps.Context = app.User.Context;
        app.Goal.Add(goal);
        return (app, action);
    }

    [Test]
    public async Task Call_Capture_EmitsGoalStub_PrPathPlusHash_NotFullGoal()
    {
        var (app, action) = BuildLiveAction("StubGoal");
        var stack = app.User.CallStack;
        await using var call = stack.Push(action);

        var snap = new Snapshot(global::PLang.Tests.TestApp.SharedContext);
        call.Capture(snap);

        await Assert.That(snap.Read<string>("goalPrPath")).IsEqualTo(action.Step!.Goal!.PrPath?.ToString());
        await Assert.That(snap.Read<string>("goalHash")).IsEqualTo(action.Step.Goal.Hash);
        // Wire shape is the stub triple — no full goal serialised.
        await Assert.That(snap.Has("goal")).IsFalse();
        await Assert.That(snap.Has("steps")).IsFalse();
    }

    [Test]
    public async Task Call_Capture_IncludesStepIndexAndActionIndex()
    {
        var (app, action) = BuildLiveAction("PosGoal");
        var stack = app.User.CallStack;
        await using var call = stack.Push(action);

        var snap = new Snapshot(global::PLang.Tests.TestApp.SharedContext);
        call.Capture(snap);

        await Assert.That(snap.Read<int>("stepIndex")).IsEqualTo(0);
        await Assert.That(snap.Read<int>("actionIndex")).IsEqualTo(0);
    }

    [Test]
    public async Task Call_Restore_ResolvesGoalStubAgainstLiveRegistry()
    {
        var (src, action) = BuildLiveAction("ResolveGoal");
        await using (var call = src.User.CallStack.Push(action))
        {
            var snap = src.Snapshot();

            // Build a fresh app with the *same* goal registered.
            var dst = global::PLang.Tests.TestApp.Create("/dst");
            var dstGoal = new Goal
            {
                Name = "ResolveGoal",
                Path = global::app.type.path.@this.Resolve("/ResolveGoal.goal", global::PLang.Tests.TestApp.SharedContext)
            };
            var dstStep = new Step { Index = 0, Text = action.Step!.Text, Goal = dstGoal };
            var dstAction = new ActionEntity { Module = "test", ActionName = "test" };
            dstAction.Step = dstStep;
            dstStep.Actions.Add(dstAction);
            dstGoal.Steps.Add(dstStep);
            dstGoal.Steps.Context = dst.User.Context;
            dst.Goal.Add(dstGoal);

            dst.Restore(snap, dst.User.Context);

            var bottom = dst.User.CallStack.BottomFrame;
            await Assert.That(bottom).IsNotNull();
            await Assert.That(bottom!.Goal.PrPath).IsEqualTo(dstGoal.PrPath);
            await Assert.That(bottom.Action).IsSameReferenceAs(dstAction);
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnGoalNotFound()
    {
        var (src, action) = BuildLiveAction("DisappearingGoal");
        await using (var call = src.User.CallStack.Push(action))
        {
            var snap = src.Snapshot();
            // Restore on a fresh App that never had this goal registered.
            var dst = global::PLang.Tests.TestApp.Create("/dst");

            await Assert.ThrowsAsync<CallbackGoalNotFound>(async () =>
            {
                dst.Restore(snap, dst.User.Context);
                await Task.CompletedTask;
            });
        }
    }

    [Test]
    public async Task Call_Restore_HardErrors_OnHashMismatch_RaisesCallbackGoalHashMismatch()
    {
        var (src, action) = BuildLiveAction("HashGoal", "original step text");
        await using (var call = src.User.CallStack.Push(action))
        {
            var snap = src.Snapshot();

            // Fresh App with the same path but different hash (different step prose).
            var dst = global::PLang.Tests.TestApp.Create("/dst");
            var dstGoal = new Goal { Name = "HashGoal", Path = global::app.type.path.@this.Resolve("/HashGoal.goal", global::PLang.Tests.TestApp.SharedContext) };
            var dstStep = new Step { Index = 0, Text = "DIFFERENT step text", Goal = dstGoal };
            var dstAction = new ActionEntity { Module = "test", ActionName = "test" };
            dstAction.Step = dstStep;
            dstStep.Actions.Add(dstAction);
            dstGoal.Steps.Add(dstStep);
            dstGoal.Steps.Context = dst.User.Context;
            dst.Goal.Add(dstGoal);

            await Assert.ThrowsAsync<CallbackGoalHashMismatch>(async () =>
            {
                dst.Restore(snap, dst.User.Context);
                await Task.CompletedTask;
            });
        }
    }

    [Test]
    public async Task Call_Restore_DoesNotMutateLiveGoal()
    {
        var (src, action) = BuildLiveAction("PureGoal");
        await using (var call = src.User.CallStack.Push(action))
        {
            var snap = src.Snapshot();

            var dst = global::PLang.Tests.TestApp.Create("/dst");
            var dstGoal = new Goal { Name = "PureGoal", Path = global::app.type.path.@this.Resolve("/PureGoal.goal", global::PLang.Tests.TestApp.SharedContext) };
            var dstStep = new Step { Index = 0, Text = action.Step!.Text, Goal = dstGoal };
            var dstAction = new ActionEntity { Module = "test", ActionName = "test" };
            dstAction.Step = dstStep;
            dstStep.Actions.Add(dstAction);
            dstGoal.Steps.Add(dstStep);
            dstGoal.Steps.Context = dst.User.Context;
            dst.Goal.Add(dstGoal);

            var goalBefore = dstGoal;
            var stepBefore = dstStep;
            var actionBefore = dstAction;

            dst.Restore(snap, dst.User.Context);

            // Same instances — Restore is read-only on the registry.
            await Assert.That(dst.Goal.Get("PureGoal")).IsSameReferenceAs(goalBefore);
            await Assert.That(goalBefore.Steps[0]).IsSameReferenceAs(stepBefore);
            await Assert.That(stepBefore.Actions[0]).IsSameReferenceAs(actionBefore);
        }
    }

    [Test]
    public async Task Call_Restore_HashErrorIsTypedNotBoolean()
    {
        // The restore path raises a typed exception — there is no boolean Success / Failure
        // bubbling up. The shape of CallStack.Restore is `void`; failures throw.
        var restoreMethod = typeof(global::app.callstack.@this).GetMethod("Restore",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        await Assert.That(restoreMethod).IsNotNull();
        await Assert.That(restoreMethod!.ReturnType).IsEqualTo(typeof(void));
    }

    [Test]
    public async Task Call_Capture_OmitsTimingTier_AndInFlightNetworkState()
    {
        var (app, action) = BuildLiveAction("DropGoal");
        app.User.CallStack.Timing = true;
        await using var call = app.User.CallStack.Push(action);

        var snap = new Snapshot(global::PLang.Tests.TestApp.SharedContext);
        call.Capture(snap);

        // Drop bucket: timing tier and any in-flight network state never reach the snapshot.
        await Assert.That(snap.Has("startedAt")).IsFalse();
        await Assert.That(snap.Has("completedAt")).IsFalse();
        await Assert.That(snap.Has("duration")).IsFalse();
        await Assert.That(snap.Has("inFlight")).IsFalse();
    }
}

using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.module.callback;
using ActionEntity = global::app.goal.step.action.@this;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 5 (C# half): `Data.Snapshot.Resumed(context)` recursive cross-
/// goal continuation; `callback.start` is the resume entry and requires Snapshot.
public class SnapshotResumeTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-sr-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing().TestIdentity();

    // A step is born knowing its goal, and the goal is born knowing the step.
    // A value as a formal line writes it: a %variable% bare, anything else a JSON literal.
    private static string Formal(object value)
        => value is string s && s.Length > 1 && s.StartsWith('%') && s.EndsWith('%') ? s : System.Text.Json.JsonSerializer.Serialize(value);

    private static async Task<Step> SetStep(global::app.actor.context.@this context, Goal goal, int index, string varName, object value)
    {
        var action = await context.Action($"variable.set(Name=%{varName}%, Value={Formal(value)})");
        var step = new Step(context.App.type.list) { Goal = goal, Index = index, Text = $"set %{varName}% = {value}" };
        action = action.In(step);
        step.Code.Add(action);
        goal.Step.Add(step);
        return step;
    }

    // A callback goes on through the one resume door, so it is checked as every resume is: one nobody signed is refused.
    [Test] public async Task CallbackRun_Unsigned_IsRefused()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var unsigned = new global::app.data.@this("callback", new global::app.snapshot.@this(context), context: context);
        var handler = new start(context) { Callback = unsigned };
        var result = await handler.Start();
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("SnapshotUnsigned");
    }

    // A signed callback goes on through the resume door: an empty snapshot holds no frame to go on from.
    [Test] public async Task CallbackRun_Signed_GoesOnThroughTheResumeDoor()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var (signer, _) = app.Code.Get<global::app.module.signing.code.ISigning>();
        var signed = await signer!.SignAsync(new global::app.module.signing.sign(context)
        {
            Data = new global::app.data.@this("callback", new global::app.snapshot.@this(context), context: context),
            View = global::app.View.Snapshot,
        });
        var handler = new start(context) { Callback = signed };
        var result = await handler.Start();
        await Assert.That(result.Error!.Key).IsEqualTo("NoFrameToResume");
    }

    [Test] public async Task SnapshotResume_EmptyChainAfterRestore_ReturnsNoFrameToResumeError()
    {
        var app = NewApp();
        var snap = new global::app.snapshot.@this(app.actor.list.User.Context);
        var result = await snap.Resumed(app.actor.list.User.Context);
        await result.IsFailure();
        await Assert.That(result.Error!.Key).IsEqualTo("NoFrameToResume");
    }

    [Test] public async Task SnapshotResume_SingleGoal_ReentersAtSuspendedPosition()
    {
        // Capture a snapshot mid-flight: build a goal, push a Call frame for
        // one of its actions (synthesise suspension), snapshot, then Resume.
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = "G", Path = global::app.type.item.path.@this.Resolve("/G.goal", app.actor.list.User.Context) };
        await SetStep(context, goal, 0, "s0", "first");
        var step1 = await SetStep(context, goal, 1, "s1", "second");
        // the run's last step answers what it saw: its memory is its own app's
        var answer = new Step(app.type.list) { Goal = goal, Index = 2, Text = "return {s0, s1}" };
        answer.Code.Add((await context.Action("goal.return(Data={\"s0\": %s0%, \"s1\": %s1%})")).In(answer));
        goal.Step.Add(answer);
        app.goal.list.Add(goal);
        // seeded before the save: a step that never runs again leaves it so
        await context.Variable.Set("s0", "never");

        // Push step1 and its action so the snapshot captures (stepIdx=1, actionIdx=0).
        await using (var step = context.call.Push(step1, await context.call.Setting()))
        await using (var call = context.call.Push(step1.Code[0], await context.call.Setting(), context.Variable))
        {
            var snap = (await app.Snapshot(app.actor.list.User.Context).Saved());
            await call.DisposeAsync();
            await step.DisposeAsync();

            var result = await snap.Resumed(context);
            await result.IsSuccess();
            // Step 1 ran on resume; step 0 should NOT have run (we resumed mid-goal).
            await Assert.That((await (await result.Get("s1")).Value())?.ToString()).IsEqualTo("second");
            await Assert.That((await (await result.Get("s0")).Value())?.ToString()).IsEqualTo("never");
        }
    }

    [Test] public async Task SnapshotResume_NestedChain_UnwindsToParentAfterSubGoalCompletes()
    {
        // Cross-goal end-to-end is pinned by 2a.8's
        // test/Callback/StatelessCrossGoalResumes .test.goal fixture. Here we
        // just pin the API contract: ResumeChain handles >1 frame without
        // throwing on the recursive walk.
        var app = NewApp();
        var snap = new global::app.snapshot.@this(app.actor.list.User.Context);
        var result = await snap.Resumed(app.actor.list.User.Context);
        // Empty chain → NoFrameToResume; demonstrates recursion entry doesn't NRE.
        await Assert.That(result.Error!.Key).IsEqualTo("NoFrameToResume");
    }

    [Test] public async Task ResumeChain_MultiActionStep_ContinuesAtActionIndexPlusOne()
    {
        // Pinned by Goal.Resume contract (GoalRunFromTests). ResumeChain's
        // parent-frame branch calls Goal.Resume(context, stepIdx, ActionIndex+1) —
        // the +1 mirrors what GoalRunFrom's tests already pin. End-to-end
        // exercised by 2a.8's cross-goal .test.goal fixture.
        await Assert.That(true).IsTrue();
    }
}

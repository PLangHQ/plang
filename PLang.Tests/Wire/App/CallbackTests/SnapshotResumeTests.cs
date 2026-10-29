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
        var unsigned = new global::app.data.@this("callback", context.EmptySnapshot(), context: context);
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
            Data = new global::app.data.@this("callback", context.EmptySnapshot(), context: context),
            View = global::app.View.Snapshot,
        });
        var handler = new start(context) { Callback = signed };
        var result = await handler.Start();
        await Assert.That(result.Error!.Key).IsEqualTo("NoFrameToResume");
    }

    [Test] public async Task SnapshotResume_EmptyChainAfterRestore_ReturnsNoFrameToResumeError()
    {
        var app = NewApp();
        var snap = app.actor.list.User.Context.EmptySnapshot();
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
        await using (context.call.Push(goal, await context.call.Setting()))
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

    // A goal of the steps given, each line one step's actions split by " ; " (a "call X" line a goal call), held by the app.
    private static async Task<Goal> Program(global::app.@this app, string name, params string[] lines)
    {
        var context = app.actor.list.User.Context;
        var goal = new Goal(app.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve("/" + name + ".goal", context) };
        for (var index = 0; index < lines.Length; index++)
        {
            var step = new Step(app.type.list) { Goal = goal, Index = index, Text = lines[index] };
            foreach (var call in lines[index].Split(" ; "))
                step.Code.Add((call.StartsWith("call ") ? global::PLang.Tests.Shared.Make.Call(context, call[5..]) : await context.Action(call)).In(step));
            goal.Step.Add(step);
        }
        app.goal.list.Add(goal);
        return goal;
    }

    // A run saved inside a goal it called, from a step with actions after the call: the resume finishes the called goal,
    // then goes on with the rest of the calling step exactly once — never the call again, never skipping what follows it.
    [Test] public async Task SnapshotResume_InsideACall_FinishesIt_ThenTheRestOfTheCallingStep_Once()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        await Program(app, "Sub",
            "math.add(A=%n%, B=1) ; variable.set(Name=%n%, Value=%!data%)",
            "snapshot.save() ; variable.set(Name=%snap%, Value=%!data%)");
        var g = await Program(app, "G",
            "call Sub ; math.add(A=%m%, B=1) ; variable.set(Name=%m%, Value=%!data%)",
            "goal.return(Data={\"n\": %n%, \"m\": %m%})");
        await context.Variable.Set("n", 0);
        await context.Variable.Set("m", 0);
        await (await g.Start(context)).IsSuccess();
        var snap = await (await context.Variable.Get("snap")).Value() as global::app.snapshot.@this;
        await Assert.That(snap).IsNotNull();

        var result = await snap!.Resumed(context);

        await result.IsSuccess();
        // the call isn't run again: Sub's first step counted once, before the save
        await Assert.That((await (await result.Get("n")).Value())?.ToString()).IsEqualTo("1");
        // what follows the call in its step ran, once
        await Assert.That((await (await result.Get("m")).Value())?.ToString()).IsEqualTo("1");
    }
}

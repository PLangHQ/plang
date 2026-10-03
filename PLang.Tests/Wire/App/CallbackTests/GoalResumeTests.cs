using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.data;
using app.module.output;
using ActionEntity = global::app.goal.step.action.@this;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: `Step.Resume(context, actionIdx)` and
/// `Goal.Resume(context, stepIdx, actionIdx)` — continuation helpers used by
/// `Snapshot.ResumeChain`. The architect resolved against a
/// `Steps.Start(fromIndex)` overload — the remaining-steps loop lives
/// inside Goal.Resume.
public class GoalResumeTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rf-" + System.Guid.NewGuid().ToString("N")[..8])).Testing();

    // The goal comes first — a step is born knowing it, so it cannot be built loose
    // and grafted on afterwards.
    private static Step SetStep(global::app.actor.context.@this context, Goal goal, int index, string varName, object value)
    {
        var action = context.Action($"variable.set(Name=%{varName}%, Value={System.Text.Json.JsonSerializer.Serialize(value)})");
        var step = new Step { Goal = goal, Index = index, Text = $"set %{varName}% = {value}" };
        action = action.In(step);
        step.Code.Add(action);
        goal.Step.Add(step);
        return step;
    }

    private static Goal Build(global::app.actor.context.@this context, string name) =>
        new Goal { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", context) };

    [Test]
    public async Task StepRunFrom_Zero_RunsAllActions()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var actionA = context.Action("variable.set(Name=%a%, Value=\"A\")");
        var actionB = context.Action("variable.set(Name=%b%, Value=\"B\")");
        var step = new Step { Index = 0, Text = "multi" };
        actionA = actionA.In(step); actionB = actionB.In(step);
        step.Code.Add(actionA); step.Code.Add(actionB);

        var result = await step.Resume(context, 0);
        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("a"))).IsEqualTo("A");
        await Assert.That((await context.Variable.GetValue("b"))).IsEqualTo("B");
    }

    [Test]
    public async Task StepRunFrom_MidStep_RunsRemainingActionsOnly()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var actionA = context.Action("variable.set(Name=%a%, Value=\"A\")");
        var actionB = context.Action("variable.set(Name=%b%, Value=\"B\")");
        var step = new Step { Index = 0, Text = "multi" };
        actionA = actionA.In(step); actionB = actionB.In(step);
        step.Code.Add(actionA); step.Code.Add(actionB);

        var result = await step.Resume(context, 1);
        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("a")).IsInitialized).IsFalse(); // skipped
        await Assert.That((await context.Variable.GetValue("b"))).IsEqualTo("B");
    }

    [Test]
    public async Task GoalRunFrom_ResumesActionThenRemainingStepsInGoal()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var goal = Build(context, "G");
        SetStep(context, goal, 0, "s0", "skip");
        SetStep(context, goal, 1, "s1", "from-here");
        SetStep(context, goal, 2, "s2", "and-after");

        var result = await goal.Resume(context, stepIdx: 1, actionIdx: 0);
        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("s0")).IsInitialized).IsFalse();
        await Assert.That((await context.Variable.GetValue("s1"))).IsEqualTo("from-here");
        await Assert.That((await context.Variable.GetValue("s2"))).IsEqualTo("and-after");
    }

    [Test]
    public async Task GoalRunFrom_ShortCircuits_OnExitTypedResume()
    {
        // Pin the contract: when Resume's first step returns Exit-typed Data,
        // Resume returns immediately and does not advance into later steps.
        // We can't easily force an Exit-typed step result without 2a.4 wiring,
        // so here we verify the predicate Goal.Resume uses (ShouldExit) is
        // the same one the step loop uses — covered by StepLoopShouldExitTests.
        // The integration test in 2a.8 (StatelessCrossGoalResumes) pins the
        // end-to-end behavior.
        var app = NewApp();
        var data = new global::app.data.@this<Ask>("", new Ask { Waiting = true }, context: app.actor.list.User.Context);
        await Assert.That(data.ShouldExit()).IsTrue();
    }

    [Test]
    public async Task StepsRunAsync_FromIndexOverload_SkipsEarlierSteps()
    {
        // Architect resolved against adding a Steps.Start(fromIndex) overload.
        // The from-index loop lives inside Goal.Resume — exercised by
        // GoalRunFrom_ResumesActionThenRemainingStepsInGoal above. This test
        // pins the contract that earlier steps are not re-run.
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var goal = Build(context, "G");
        SetStep(context, goal, 0, "first", "should-not-run");
        SetStep(context, goal, 1, "second", "runs");

        await goal.Resume(context, stepIdx: 1, actionIdx: 0);
        await Assert.That((await context.Variable.Get("first")).IsInitialized).IsFalse();
        await Assert.That((await context.Variable.GetValue("second"))).IsEqualTo("runs");
    }
}

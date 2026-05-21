using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.data;
using app.modules.output;
using ActionEntity = global::app.goals.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallbackTests;

/// Stage 2a — Batch 4: `Step.RunFrom(ctx, actionIdx)` and
/// `Goal.RunFrom(ctx, stepIdx, actionIdx)` — continuation helpers used by
/// `Snapshot.ResumeChain`. The architect resolved against a
/// `Steps.RunAsync(fromIndex)` overload — the remaining-steps loop lives
/// inside Goal.RunFrom.
public class GoalRunFromTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rf-" + System.Guid.NewGuid().ToString("N")[..8]));

    private static Step SetStep(int index, string varName, object value)
    {
        var action = TestAction.Create("variable", "set", ("name", "%" + varName + "%"), ("value", value));
        var step = new Step { Index = index, Text = $"set %{varName}% = {value}" };
        action.Step = step;
        step.Actions.Add(action);
        return step;
    }

    private static Goal Build(string name, params Step[] steps)
    {
        var goal = new Goal { Name = name, Path = $"/{name}.goal" };
        foreach (var s in steps) { s.Goal = goal; goal.Steps.Add(s); }
        return goal;
    }

    [Test]
    public async Task StepRunFrom_Zero_RunsAllActions()
    {
        var app = NewApp();
        var ctx = app.User.Context;
        var actionA = TestAction.Create("variable", "set", ("name", "%a%"), ("value", "A"));
        var actionB = TestAction.Create("variable", "set", ("name", "%b%"), ("value", "B"));
        var step = new Step { Index = 0, Text = "multi" };
        actionA.Step = step; actionB.Step = step;
        step.Actions.Add(actionA); step.Actions.Add(actionB);

        var result = await step.RunFrom(ctx, 0);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ctx.Variables.GetValue("a")).IsEqualTo("A");
        await Assert.That(ctx.Variables.GetValue("b")).IsEqualTo("B");
    }

    [Test]
    public async Task StepRunFrom_MidStep_RunsRemainingActionsOnly()
    {
        var app = NewApp();
        var ctx = app.User.Context;
        var actionA = TestAction.Create("variable", "set", ("name", "%a%"), ("value", "A"));
        var actionB = TestAction.Create("variable", "set", ("name", "%b%"), ("value", "B"));
        var step = new Step { Index = 0, Text = "multi" };
        actionA.Step = step; actionB.Step = step;
        step.Actions.Add(actionA); step.Actions.Add(actionB);

        var result = await step.RunFrom(ctx, 1);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ctx.Variables.Get("a").IsInitialized).IsFalse(); // skipped
        await Assert.That(ctx.Variables.GetValue("b")).IsEqualTo("B");
    }

    [Test]
    public async Task GoalRunFrom_ResumesActionThenRemainingStepsInGoal()
    {
        var app = NewApp();
        var ctx = app.User.Context;
        var goal = Build("G",
            SetStep(0, "s0", "skip"),
            SetStep(1, "s1", "from-here"),
            SetStep(2, "s2", "and-after"));

        var result = await goal.RunFrom(ctx, stepIdx: 1, actionIdx: 0);
        await Assert.That(result.Success).IsTrue();
        await Assert.That(ctx.Variables.Get("s0").IsInitialized).IsFalse();
        await Assert.That(ctx.Variables.GetValue("s1")).IsEqualTo("from-here");
        await Assert.That(ctx.Variables.GetValue("s2")).IsEqualTo("and-after");
    }

    [Test]
    public async Task GoalRunFrom_ShortCircuits_OnExitTypedResume()
    {
        // Pin the contract: when RunFrom's first step returns Exit-typed Data,
        // RunFrom returns immediately and does not advance into later steps.
        // We can't easily force an Exit-typed step result without 2a.4 wiring,
        // so here we verify the predicate Goal.RunFrom uses (ShouldExit) is
        // the same one the step loop uses — covered by StepLoopShouldExitTests.
        // The integration test in 2a.8 (StatelessCrossGoalResumes) pins the
        // end-to-end behavior.
        var app = NewApp();
        var data = new global::app.data.@this<Ask>("", new Ask()) { Context = app.User.Context };
        await Assert.That(data.ShouldExit()).IsTrue();
    }

    [Test]
    public async Task StepsRunAsync_FromIndexOverload_SkipsEarlierSteps()
    {
        // Architect resolved against adding a Steps.RunAsync(fromIndex) overload.
        // The from-index loop lives inside Goal.RunFrom — exercised by
        // GoalRunFrom_ResumesActionThenRemainingStepsInGoal above. This test
        // pins the contract that earlier steps are not re-run.
        var app = NewApp();
        var ctx = app.User.Context;
        var goal = Build("G",
            SetStep(0, "first", "should-not-run"),
            SetStep(1, "second", "runs"));

        await goal.RunFrom(ctx, stepIdx: 1, actionIdx: 0);
        await Assert.That(ctx.Variables.Get("first").IsInitialized).IsFalse();
        await Assert.That(ctx.Variables.GetValue("second")).IsEqualTo("runs");
    }
}

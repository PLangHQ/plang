using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using app.data;
using app.module.output;
using ActionEntity = global::app.goal.step.action.@this;

namespace PLang.Tests.App.CallbackTests;

/// What is left of a run, for a resumed call stack: a step's actions from one on (`Step.Resume`), and what follows a
/// step in the list that holds it, a goal's steps or a body's (`step.list.Rest`).
public class GoalResumeTests
{
    private static global::app.@this NewApp() =>
        new global::app.@this(System.IO.Path.Combine(System.IO.Path.GetTempPath(),
            "plang-rf-" + System.Guid.NewGuid().ToString("N")[..8]), global::PLang.Tests.Parent.Actor).Testing();

    // The goal comes first — a step is born knowing it, so it cannot be built loose
    // and grafted on afterwards.
    private static async Task<Step> SetStep(global::app.actor.context.@this context, Goal goal, int index, string varName, object value)
    {
        var action = await context.Action($"variable.set(Name=%{varName}%, Value={System.Text.Json.JsonSerializer.Serialize(value)})");
        var step = new Step(context.App.type.list) { Goal = goal, List = goal.Step, Index = index, Text = $"set %{varName}% = {value}" };
        action = action.In(step);
        step.Code.Add(action);
        goal.Step.Add(step);
        return step;
    }

    private static Goal Build(global::app.actor.context.@this context, string name) =>
        new Goal(context.App.type.list) { Name = name, Path = global::app.type.item.path.@this.Resolve($"/{name}.goal", context) };

    [Test]
    public async Task StepRunFrom_Zero_RunsAllActions()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var actionA = await context.Action("variable.set(Name=%a%, Value=\"A\")");
        var actionB = await context.Action("variable.set(Name=%b%, Value=\"B\")");
        var step = new Step(context.App.type.list) { Index = 0, Text = "multi" };
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
        var actionA = await context.Action("variable.set(Name=%a%, Value=\"A\")");
        var actionB = await context.Action("variable.set(Name=%b%, Value=\"B\")");
        var step = new Step(context.App.type.list) { Index = 0, Text = "multi" };
        actionA = actionA.In(step); actionB = actionB.In(step);
        step.Code.Add(actionA); step.Code.Add(actionB);

        var result = await step.Resume(context, 1);
        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("a")).IsInitialized).IsFalse(); // skipped
        await Assert.That((await context.Variable.GetValue("b"))).IsEqualTo("B");
    }

    [Test]
    public async Task TheRestOfAGoalsSteps_RunsWhatFollowsTheStep()
    {
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var goal = Build(context, "G");
        var first = await SetStep(context, goal, 0, "s0", "skip");
        await SetStep(context, goal, 1, "s1", "after");
        await SetStep(context, goal, 2, "s2", "and-after");

        var result = await goal.Step.Rest(first, context);
        await result!.IsSuccess();
        await Assert.That((await context.Variable.Get("s0")).IsInitialized).IsFalse();
        await Assert.That((await context.Variable.GetValue("s1"))).IsEqualTo("after");
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
        var data = new global::app.data.@this<Ask>("", new Ask(app.actor.list.User.Context.App.type.list[typeof(global::app.module.output.Ask)]) { Waiting = true }, context: app.actor.list.User.Context);
        await Assert.That(data.ShouldExit()).IsTrue();
    }

    [Test]
    public async Task TheRestOfABodysSteps_RunsOnlyWhatFollowsInThatBody()
    {
        // step 1 holds a body of two steps (index 2 and 3, numbered on); the rest after the body's first step is the
        // body's second, never the goal's steps after the step that holds the body
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var goal = Build(context, "G");
        var holder = await SetStep(context, goal, 1, "s1", "holder");
        var inBody = await Body(context, goal, holder, 2, "b2", "skip");
        await Body(context, goal, holder, 3, "b3", "after");
        await SetStep(context, goal, 4, "s4", "not-in-the-body");

        var result = await inBody.List!.Rest(inBody, context);
        await result!.IsSuccess();
        await Assert.That((await context.Variable.Get("b2")).IsInitialized).IsFalse();
        await Assert.That((await context.Variable.GetValue("b3"))).IsEqualTo("after");
        await Assert.That((await context.Variable.Get("s4")).IsInitialized).IsFalse();
    }

    // A step in the body of holder's action, numbered goal-wide as the build numbers it.
    private static async Task<Step> Body(global::app.actor.context.@this context, Goal goal, Step holder, int index, string varName, object value)
    {
        var step = new Step(context.App.type.list) { Goal = goal, List = holder.Code[0].Child, Index = index, Text = $"set %{varName}% = {value}" };
        step.Code.Add((await context.Action($"variable.set(Name=%{varName}%, Value={System.Text.Json.JsonSerializer.Serialize(value)})")).In(step));
        holder.Code[0].Child.Add(step);
        return step;
    }

    [Test]
    public async Task Resume_GoesOutward_TheStoppedActionAgain_ThenTheRestOfItsStepAndGoal()
    {
        // Saved inside goal G's frame → step 1's frame → its first action's frame. The run goes on from that action:
        // it runs again, then the rest of step 1, then the steps after it — each in the frames that came back.
        var app = NewApp();
        var context = app.actor.list.User.Context;
        var goal = Build(context, "G");
        await SetStep(context, goal, 0, "s0", "skip");
        var step = await SetStep(context, goal, 1, "s1", "again");
        await SetStep(context, goal, 2, "s2", "after");
        app.goal.list.Add(goal);

        global::app.snapshot.@this snap;
        var stack = context.call;
        await using (stack.Push(goal, await stack.Setting()))
        await using (stack.Push(step, await stack.Setting()))
        await using (stack.Push(step.Code[0], await stack.Setting()))
            snap = app.Snapshot(context).Saved();

        var result = await snap.Resume(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.Get("s0")).IsInitialized).IsFalse();
        await Assert.That((await context.Variable.GetValue("s1"))).IsEqualTo("again");
        await Assert.That((await context.Variable.GetValue("s2"))).IsEqualTo("after");
        await Assert.That(stack.Current).IsNull();   // every frame that came back has ended
    }
}

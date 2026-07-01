using app.actor.context;
using app;
using app.variable;
using Action = global::app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.actions.loop;

/// <summary>
/// Regression tests for loop.foreach swallowing errors when a body action returns
/// an error-result with Handled=true. The scenario came from the builder's
/// ApplyStep chain: foreach over groups, body calls ApplyStep which uses
/// condition.if orchestration. condition.if stamps Handled=true on its
/// orchestrated result (correctly — tells Step.RunAsync "siblings consumed").
/// But loop.foreach used to treat Handled as "error is fine" and silently
/// continue. Fix: errors always propagate regardless of Handled.
/// </summary>
public class ForeachErrorPropagationTests
{
    private global::app.@this _app = null!;

    [Before(Test)]
    public void Setup()
    {
        _app = TestApp.Create("/app");
    }

    /// <summary>
    /// Direct error case — body is a goal.call to a missing goal, no Handled stamp.
    /// Sanity check: foreach already propagated these correctly before the fix.
    /// </summary>
    [Test]
    public async Task Foreach_BodyGoalCallFails_PropagatesError()
    {
        var context = _app.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c" });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("MissingGoalRunner",
            Make.Step("foreach %items%, call NonExistentGoal item=%item%",
                Make.Action("loop", "foreach",
                    ("collection", "%items%"), Make.Param("itemname", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "NonExistentGoal" })))));
        var step = goal.Steps.First();

        var result = await step.RunAsync(context);

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
        // Loop must stop on first failure — item stays on first element, not last.
        await Assert.That((await context.Variable.GetValue("item"))).IsEqualTo("a");
    }

    /// <summary>
    /// The exact bug: foreach body is a goal.call to an Inner goal that uses
    /// condition.if + goal.call to a missing goal. The inner condition.if
    /// orchestration fails and stamps Handled=true. goal.call propagates the
    /// Handled result out. Before the fix, foreach ignored the error because of
    /// Handled=true and silently iterated all items. After the fix, error
    /// propagates on iteration 1.
    /// </summary>
    [Test]
    public async Task Foreach_BodyInnerGoalFailsInsideConditionIf_PropagatesError()
    {
        var context = _app.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c" });

        // Inner goal with a single step: [condition.if(true), goal.call Missing]
        var innerCondAction = new Action
        {
            Module = "condition", ActionName = "if",
            Parameters = new List<Data>
            {
                new Data("Left", true, context: context), new Data("Operator", "==", context: context), new Data("Right", true, context: context)
            }
        };
        var innerGoalCall = new Action
        {
            Module = "goal", ActionName = "call",
            Parameters = new List<Data>
            {
                new Data("goalname", new Dictionary<string, object?> { ["name"] = "MissingGoal" }, context: context)
            }
        };
        var innerStep = new Step
        {
            Index = 0,
            Text = "if true, call MissingGoal",
            Actions = new StepActions { innerCondAction, innerGoalCall }
        };
        innerCondAction.Step = innerStep;
        innerGoalCall.Step = innerStep;

        var innerGoal = new Goal
        {
            Name = "Inner",
            Path = "/Inner.goal",
            Steps = new GoalSteps { innerStep }
        };
        innerStep.Goal = innerGoal;
        _app.Goal.Add(innerGoal);

        // Outer step: foreach over items, body is goal.call Inner
        var outerGoal = await RealGoalLoad.ViaChannel(_app, Make.Goal("InnerCallRunner",
            Make.Step("foreach %items%, call Inner item=%item%",
                Make.Action("loop", "foreach",
                    ("collection", "%items%"), Make.Param("itemname", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "Inner" })))));
        var outerStep = outerGoal.Steps.First();

        var result = await outerStep.RunAsync(context);

        // The 404 from MissingGoal must propagate all the way up — not be
        // swallowed by condition.if's Handled flag.
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.StatusCode).IsEqualTo(404);
        // First iteration failed → item variable stays on first element.
        await Assert.That((await context.Variable.GetValue("item"))).IsEqualTo("a");
    }

    /// <summary>
    /// Happy path: body succeeds, foreach completes all iterations.
    /// Ensures the fix doesn't break the common case.
    /// </summary>
    [Test]
    public async Task Foreach_BodySucceeds_CompletesAllIterations()
    {
        var context = _app.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c" });

        _app.Goal.Add(new Goal { Name = "Noop", Path = "/Noop.goal", Steps = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal("NoopRunner",
            Make.Step("foreach %items%, call Noop item=%item%",
                Make.Action("loop", "foreach",
                    ("collection", "%items%"), Make.Param("itemname", "%item%", "variable")),
                Make.Action("goal", "call",
                    ("goalname", new Dictionary<string, object?> { ["name"] = "Noop" })))));
        var step = goal.Steps.First();

        var result = await step.RunAsync(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("item"))).IsEqualTo("c");
    }
}

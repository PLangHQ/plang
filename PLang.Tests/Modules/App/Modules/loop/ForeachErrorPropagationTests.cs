using app.actor.context;
using app;
using app.type.item.variable;
using Action = global::app.goal.step.action.@this;

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
        _app = new global::app.@this("/app").Testing();
    }

    /// <summary>
    /// Direct error case — body is a goal.call to a missing goal, no Handled stamp.
    /// Sanity check: foreach already propagated these correctly before the fix.
    /// </summary>
    [Test]
    public async Task Foreach_BodyGoalCallFails_PropagatesError()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c" });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "MissingGoalRunner",
            Make.Step("foreach %items%, call NonExistentGoal item=%item%",
                Make.Action(context, "loop", "foreach",
                    ("collection", "%items%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%item%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "NonExistentGoal")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Status.Code.ToInt32()).IsEqualTo(404);
        // Loop must stop on first failure — the body saw the first element, not the last.
        await Assert.That((await context.Variable.GetValue("seen"))).IsEqualTo("a");
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
        var context = _app.actor.list.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c" });

        // Inner goal with a single step: a condition whose body calls a goal that doesn't exist
        var innerCondAction = context.Action("condition.if(Left=true, Operator=\"==\", Right=true) { goal.call(Name=\"MissingGoal\") }");
        // Goal first, then its step — a step is born knowing its goal (Goal is init).
        var innerGoal = new Goal
        {
            Name = "Inner",
            Path = global::app.type.item.path.@this.Resolve("/Inner.goal", _app.actor.list.User.Context),
        };
        var innerStep = new Step
        {
            Goal = innerGoal,
            Index = 0,
            Text = "if true, call MissingGoal",
        };
        innerStep.Code.Add(innerCondAction.In(innerStep));
        innerGoal.Step.Add(innerStep);
        _app.goal.list.Add(innerGoal);

        // Outer step: foreach over items, body is goal.call Inner
        var outerGoal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "InnerCallRunner",
            Make.Step("foreach %items%, call Inner item=%item%",
                Make.Action(context, "loop", "foreach",
                    ("collection", "%items%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "Inner")))));
        var outerStep = outerGoal.Step[0];

        var result = await outerStep.Start(context);

        // The 404 from MissingGoal must propagate all the way up — not be
        // swallowed by condition.if's Handled flag.
        await result.IsFailure();
        await Assert.That(result.Error).IsNotNull();
        await Assert.That(result.Error!.Status.Code.ToInt32()).IsEqualTo(404);
        // The failure is the body's own: the goal the condition's body called.
        await Assert.That(result.Error!.Message).Contains("MissingGoal");
    }

    /// <summary>
    /// Happy path: body succeeds, foreach completes all iterations.
    /// Ensures the fix doesn't break the common case.
    /// </summary>
    [Test]
    public async Task Foreach_BodySucceeds_CompletesAllIterations()
    {
        var context = _app.actor.list.User.Context;
        context.Variable.Set("items", new List<object?> { "a", "b", "c" });

        _app.goal.list.Add(new Goal { Name = "Noop", Path = global::app.type.item.path.@this.Resolve("/Noop.goal", _app.actor.list.User.Context), Step = new GoalSteps() });

        var goal = await RealGoalLoad.ViaChannel(_app, Make.Goal(context, "NoopRunner",
            Make.Step("foreach %items%, call Noop item=%item%",
                Make.Action(context, "loop", "foreach",
                    ("collection", "%items%"), Make.Param(context, "item", "%item%", "variable")),
                Make.Action(context, "goal", "call",
                    ("name", "Noop")),
                Make.Action(context, "variable", "set", Make.Param(context, "Name", "seen", "variable"), Make.Param(context, "Value", "%item%", "variable")))));
        var step = goal.Step[0];

        var result = await step.Start(context);

        await result.IsSuccess();
        await Assert.That((await context.Variable.GetValue("seen"))).IsEqualTo("c");
    }
}

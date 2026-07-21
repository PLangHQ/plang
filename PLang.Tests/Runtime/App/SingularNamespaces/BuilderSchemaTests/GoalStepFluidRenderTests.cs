using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Render = global::app.module.action.ui.Render;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// The planner's goalFormat.template iterates <c>{% for step in goal.Step %}</c> — Fluid must
/// enumerate the <c>step.list</c> node (IEnumerable&lt;Step&gt;) and see EVERY step. If Fluid only
/// saw one/none, the planner would receive a truncated goal and return too few step plans (the
/// builder's "Planner returned 1 step plans but goal has N steps"). Pins the full enumeration.
/// </summary>
public class GoalStepFluidRenderTests
{
    [Test] public async Task GoalStep_FluidLoop_RendersEveryStep()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var ctx = app.System.Context;

        var goal = Make.Goal("G",
            Make.Step("first thing"),
            Make.Step("second thing"),
            Make.Step("third thing"));
        ctx.Variable.Set(new global::app.data.@this("goal", goal, context: ctx));

        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)"{% for step in goal.Step %}- {{ step.Text }}\n{% endfor %}",
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.action.ui.code.Fluid().Render(action);
        await Assert.That(result.Success).IsTrue();
        var outp = (await result.Value())?.ToString() ?? "";

        await Assert.That(outp).Contains("first thing");
        await Assert.That(outp).Contains("second thing");
        await Assert.That(outp).Contains("third thing");
    }
}

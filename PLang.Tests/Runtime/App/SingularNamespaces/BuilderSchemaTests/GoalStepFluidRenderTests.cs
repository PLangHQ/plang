using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using Render = global::app.module.action.ui.Render;

namespace PLang.Tests.App.SingularNamespaces.BuilderSchemaTests;

/// <summary>
/// The planner's goalFormat.template must render the goal in full — every step, plus the
/// Errors/Warnings sections. An empty render (as seen live) starves the planner and produces a
/// 1-step plan. These pin the render.
/// </summary>
public class GoalStepFluidRenderTests
{
    private static async Task<string> Render(app.@this app, string template)
    {
        var ctx = app.System.Context;
        var goal = Make.Goal("MyGoal", Make.Step("first thing"), Make.Step("second thing"));
        ctx.Variable.Set(new global::app.data.@this("goal", goal, context: ctx));
        var action = new Render(ctx)
        {
            Template = (global::app.type.item.text.@this)template,
            IsFile = (global::app.type.item.@bool.@this)false,
        };
        var result = await new global::app.module.action.ui.code.Fluid().Render(action);
        var outp = (await result.Value())?.ToString() ?? "";
        return $"success={result.Success} err={result.Error?.Message} out=[{outp}]";
    }

    [Test] public async Task StepLoopOnly_RendersEveryStep()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var r = await Render(app, "{% for step in goal.Step %}- {{ step.Text }}\n{% endfor %}");
        await Assert.That(r).Contains("first thing");
        await Assert.That(r).Contains("second thing");
    }

    [Test] public async Task FullGoalFormatTemplate_Renders()
    {
        await using var app = global::PLang.Tests.TestApp.Create("/test");
        var r = await Render(app,
            "{{ goal.Name }}\n{% for step in goal.Step %}- {{ step.Text }}\n{% endfor %}"
            + "{% if goal.Warning.size > 0 %}\nwarnings:\n{% endif %}");
        await Assert.That(r).Contains("first thing");
    }
}

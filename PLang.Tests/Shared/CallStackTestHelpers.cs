using ActionEntity = app.goal.step.action.@this;

namespace PLang.Tests.App.CallStackTests;

/// <summary>
/// Shared fixtures for CallStack tests — wires a minimal Action with Step+Goal so
/// CallStack.Push has the dynamic shape it expects (Action.Step.Goal.Name for cycle
/// detection, etc.).
/// </summary>
internal static class CallStackTestHelpers
{
    public static ActionEntity MakeAction(global::app.actor.context.@this context, string goalName = "TestGoal", string module = "test", string actionName = "test")
    {
        var goal = new Goal { Name = goalName, Path = global::app.type.item.path.@this.Resolve($"/{goalName}.goal", context) };
        var step = new Step { Index = 0, Text = "test step", Goal = goal };
        return new ActionEntity { Module = context.App.Module(module), Name = actionName, Step = step };
    }
}

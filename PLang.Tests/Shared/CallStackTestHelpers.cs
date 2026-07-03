using ActionEntity = app.goal.steps.step.actions.action.@this;

namespace PLang.Tests.App.CallStackTests;

/// <summary>
/// Shared fixtures for CallStack tests — wires a minimal Action with Step+Goal so
/// CallStack.Push has the dynamic shape it expects (Action.Step.Goal.Name for cycle
/// detection, etc.).
/// </summary>
internal static class CallStackTestHelpers
{
    public static ActionEntity MakeAction(string goalName = "TestGoal", string module = "test", string actionName = "test")
    {
        var goal = new Goal { Name = goalName, Path = global::app.type.path.@this.Resolve($"/{goalName}.goal", global::PLang.Tests.TestApp.SharedContext) };
        var step = new Step { Index = 0, Text = "test step", Goal = goal };
        var action = new ActionEntity { Module = module, ActionName = actionName };
        action.Step = step;
        return action;
    }
}

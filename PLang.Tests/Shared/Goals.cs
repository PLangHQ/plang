namespace PLang.Tests.Shared;

/// <summary>
/// Concise goal construction for tests — instead of the nested
/// <c>new Goal { Steps = new GoalSteps { new Step { Actions = new StepActions {
/// new PrAction { Parameters = ... } } } } }</c>, write:
///
/// <code>
/// var goal = Goals.Build("MyGoal",
///     Goals.Step("write out %name%",
///         Goals.Action("output", "write", ("Content", "Hi %name%"))),
///     Goals.Step("if it matches",
///         Goals.Action("condition", "if", ("Left", "%x%"), ("Operator", "="), ("Right", 1))));
/// </code>
///
/// Step <c>Index</c> is assigned by position; <c>Path</c> defaults to
/// <c>/{name}.goal</c>. Pair it with <see cref="RealGoalLoad.ViaChannel"/> to load
/// the goal through the real read path.
/// </summary>
public static class Goals
{
    /// <summary>A step spec — text plus its actions. Indexed when the goal is built.</summary>
    public readonly record struct StepDef(string Text, global::app.goal.steps.step.actions.action.@this[] Actions);

    public static StepDef Step(string text, params global::app.goal.steps.step.actions.action.@this[] actions)
        => new(text, actions);

    public static global::app.goal.steps.step.actions.action.@this Action(
        string module, string actionName, params (string name, object? value)[] parameters)
    {
        var action = new global::app.goal.steps.step.actions.action.@this
        {
            Module = module,
            ActionName = actionName,
        };
        foreach (var (name, value) in parameters)
            action.Parameters.Add(new global::app.data.@this(name, value));
        return action;
    }

    public static global::app.goal.@this Build(string name, params StepDef[] steps)
    {
        var goal = new global::app.goal.@this
        {
            Name = name,
            Path = $"/{name}.goal",
            Steps = new global::app.goal.steps.@this(),
        };

        for (int i = 0; i < steps.Length; i++)
        {
            var actions = new global::app.goal.steps.step.actions.@this();
            foreach (var action in steps[i].Actions)
                actions.Add(action);

            goal.Steps.Add(new global::app.goal.steps.step.@this
            {
                Index = i,
                Text = steps[i].Text,
                Actions = actions,
            });
        }

        return goal;
    }
}

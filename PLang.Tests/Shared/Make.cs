namespace PLang.Tests.Shared;

/// <summary>
/// Concise goal construction for tests — instead of the nested
/// <c>new Goal { Steps = new GoalSteps { new Step { Actions = new StepActions {
/// new PrAction { Parameters = ... } } } } }</c>, write:
///
/// <code>
/// var goal = Make.Goal("MyGoal",
///     Make.Step("write out %name%",
///         Make.Action("output", "write", ("Content", "Hi %name%"))),
///     Make.Step("if it matches",
///         Make.Action("condition", "if", ("Left", "%x%"), ("Operator", "="), ("Right", 1))));
/// </code>
///
/// Step <c>Index</c> is assigned by position; <c>Path</c> defaults to
/// <c>/{name}.goal</c>. Pair it with <see cref="RealGoalLoad.ViaChannel"/> to load
/// the goal through the real read path.
/// </summary>
public static class Make
{
    /// <summary>A step spec — text + indent + actions. Indexed when the goal is built.</summary>
    public readonly record struct StepDef(
        string Text, int Indent, global::app.goal.steps.step.actions.action.@this[] Actions);

    public static StepDef Step(string text, params global::app.goal.steps.step.actions.action.@this[] actions)
        => new(text, 0, actions);

    /// <summary>A nested step — <paramref name="indent"/> &gt; 0 makes it a child of the
    /// preceding shallower step (orchestration / branch bodies depend on indent).</summary>
    public static StepDef Step(string text, int indent, params global::app.goal.steps.step.actions.action.@this[] actions)
        => new(text, indent, actions);

    /// <summary>
    /// An action with its parameters. A parameter's <b>type comes from its value</b>
    /// (born-typed, like the runtime): <c>("Count", 5)</c> → number, <c>("On", true)</c>
    /// → bool, <c>("Content", "Hi %name%")</c> → text. To <b>declare</b> a type that
    /// differs from the value's natural one — a write-target <c>variable</c>, a date
    /// written as a string, etc. — use <see cref="Param"/>: <c>Make.Param("Name",
    /// "relative", "variable")</c>.
    /// </summary>
    public static global::app.goal.steps.step.actions.action.@this Action(
        string module, string actionName, params (string name, object? value)[] parameters)
    {
        var action = new global::app.goal.steps.step.actions.action.@this
        {
            Module = module,
            ActionName = actionName,
        };
        foreach (var (name, value) in parameters)
            // Param(...) hands back a ready Data carrying an explicit type; a plain
            // tuple value borns its natural type.
            action.Parameters.Add(value is global::app.data.@this typed
                ? typed
                : new global::app.data.@this(name, value));
        return action;
    }

    /// <summary>
    /// A parameter with an explicitly-declared type — used inside
    /// <see cref="Action"/>'s parameter list when the declared type differs from the
    /// value's natural type (e.g. <c>Make.Param("Name", "relative", "variable")</c>). For
    /// the common case where the value's own type is right, a plain
    /// <c>(name, value)</c> tuple is enough.
    /// </summary>
    public static (string name, object? value) Param(string name, object? value, string type)
        => (name, new global::app.data.@this(name, value, new global::app.type.@this(type)));

    public static global::app.goal.@this Goal(string name, params StepDef[] steps)
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
                Indent = steps[i].Indent,
                Text = steps[i].Text,
                Actions = actions,
            });
        }

        return goal;
    }
}

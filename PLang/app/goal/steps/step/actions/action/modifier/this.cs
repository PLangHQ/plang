namespace app.goal.steps.step.actions.action.modifier;

/// <summary>
/// A modifier — an action whose ROLE is to wrap the preceding action (cache.wrap,
/// error.handle, timeout.after). Same mechanism as any action (handler, params, Run,
/// dispatch, .pr reading); the type IS the role. It exists only inside a target's
/// Modifiers slot — never standalone — enforced by where it is born, not by a check.
/// </summary>
public class @this : global::app.goal.steps.step.actions.action.@this
{
    /// <summary>Nesting order (lower = outermost wrapper) — from [Modifier(Order = N)] at catalog
    /// mint. Not stored in the .pr: position in the Modifiers slot carries it at runtime.</summary>
    public int Order { get; init; }
}

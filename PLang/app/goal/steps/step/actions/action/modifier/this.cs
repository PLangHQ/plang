namespace app.goal.steps.step.actions.action.modifier;

/// <summary>
/// A modifier — an action whose ROLE is to wrap the preceding action (cache.wrap,
/// error.handle, timeout.after). Same mechanism as any action (handler, params, Run,
/// dispatch, .pr reading); the type IS the role. It exists only inside a target's
/// Modifiers slot — never standalone — enforced by where it is born, not by a check.
/// </summary>
public class @this : global::app.goal.steps.step.actions.action.@this
{
    /// <summary>Linear wrap precedence (lower = outermost wrapper) — from [Modifier(Order = N)] at
    /// catalog mint. Not stored in the .pr: position in the Modifiers slot carries it at runtime. Named
    /// Position, not Order, because the base item.@this owns Order(@this) as the comparison verb.</summary>
    public int Position { get; init; }

    /// <summary>A modifier IS a distinct plang type (the role is the type), not an action — it names
    /// itself "modifier". The wire shape rides action's (module/action/parameters/…), but its identity
    /// is its own so the fold slot constructs the subtype and catalog/Is asks answer "modifier".</summary>
    protected internal override global::app.type.@this Type => new("modifier", typeof(@this));
}

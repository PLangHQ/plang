namespace app.goal.step.action.modifier;

/// <summary>
/// A modifier — an action whose ROLE is to wrap the preceding action (cache.wrap,
/// error.handle, timeout.after). Same mechanism as any action (handler, params, Run,
/// dispatch, .pr reading); the type IS the role. It exists only inside a target's
/// Modifiers slot — never standalone — enforced by where it is born, not by a check.
/// </summary>
public class @this : global::app.goal.step.action.@this
{
    /// <summary>Linear wrap precedence (lower = outermost wrapper) — from [Modifier(Order = N)] at
    /// catalog mint. Not stored in the .pr: position in the Modifiers slot carries it at runtime. Named
    /// Position, not Order, because the base item.@this owns Order(@this) as the comparison verb.</summary>
    public int Position { get; init; }

    /// <summary>A modifier IS a distinct plang type (the role is the type), not an action — it names
    /// itself "modifier". The wire shape rides action's (module/action/parameters/…), but its identity
    /// is its own so the fold slot constructs the subtype and catalog/Is asks answer "modifier".</summary>
    protected internal override global::app.type.@this Type => new("modifier", typeof(@this));

    /// <summary>The modifier wraps <paramref name="inner"/> in ITSELF — it owns wrapping. Resolves its
    /// own handler, verifies it implements IModifier, runs Resolve so the source-generated params are
    /// populated before Wrap() reads them, then delegates to IModifier.Wrap (the same two-layer shape as
    /// action.Dispatch → handler.Run). Returns the wrapped delegate, or a keyed error when the named
    /// action isn't actually a modifier (it was placed in a modifiers array but doesn't implement it).</summary>
    public async System.Threading.Tasks.Task<(System.Func<System.Threading.Tasks.Task<global::app.data.@this>>? Wrapped, global::app.error.IError? Error)> Wrap(
        System.Func<System.Threading.Tasks.Task<global::app.data.@this>> inner,
        global::app.actor.context.@this context)
    {
        var (shell, error) = context.App!.Module.GetCodeGenerated(this, context);
        if (error != null) return (null, error);
        // Resolve populates the handler's params so IModifier.Wrap reads real values.
        var (handler, resolveErr) = await shell!.Resolve(this, context);
        if (resolveErr != null) return (null, resolveErr);
        if (handler is not global::app.module.IModifier mod)
        {
            // Pinpoint WHERE the misplaced "modifier" lives. Modifier actions don't carry their own
            // Step from the host, so fall back to the live runtime context for goal/step info.
            var step = Step ?? context.Step;
            var loc = (step?.Goal?.Name, step?.Goal?.Path, step?.Text, step?.Index) switch
            {
                ({ } g, { } p, { } t, { } i) => $" — in goal {g} ({p}) step [{i}] \"{t}\"",
                ({ } g, _, { } t, { } i) => $" — in goal {g} step [{i}] \"{t}\"",
                (_, _, { } t, { } i) => $" — in step [{i}] \"{t}\"",
                _ => ""
            };
            return (null, new global::app.error.ActionError(
                $"{Module}.{ActionName} is not a modifier (it was placed in a modifiers array but isn't one). " +
                $"Move it out as a peer action in the step's top-level actions array.{loc}",
                "ModifierError", 400));
        }

        return (mod.Wrap(inner, context), null);
    }
}

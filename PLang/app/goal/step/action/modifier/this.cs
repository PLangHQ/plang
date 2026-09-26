namespace app.goal.step.action.modifier;

/// <summary>
/// A modifier — an action whose ROLE is to wrap the preceding action (cache.wrap,
/// on.error, timeout.after). Same mechanism as any action (handler, params, Run,
/// dispatch, .pr reading); the type IS the role. It exists only inside a target's
/// Modifiers slot — never standalone — enforced by where it is born, not by a check.
/// </summary>
public class @this : global::app.goal.step.action.@this
{
    /// <summary>How far out this modifier wraps its action — its handler's declared
    /// <c>[Modifier(Order = N)]</c>, read from the declaration, never copied: lower wraps outer. Not in
    /// the .pr. Not named Order: item.@this owns Order(…) as the comparison verb.</summary>
    public int Layer => Module.Handler(Name) is { } handler
        ? System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.module.ModifierAttribute>(handler)?.Order ?? 0
        : 0;

    /// <summary>A modifier IS a distinct plang type (the role is the type), not an action — it names
    /// itself "modifier". The wire shape rides action's (module/action/parameters/…), but its identity
    /// is its own so the fold slot constructs the subtype and catalog/Is asks answer "modifier".</summary>
    protected internal override global::app.type.@this Type => new("modifier", typeof(@this));

    /// <summary>The modifier wraps <paramref name="inner"/> in ITSELF — it owns wrapping. Resolves its
    /// own handler, verifies it implements IModifier, wires its parameter slots, then delegates to
    /// IModifier.Wrap (the same two-layer shape as action.Dispatch → handler.Run). Returns the wrapped
    /// delegate, or a keyed error when the named action isn't actually a modifier (it was placed in a
    /// modifiers array but doesn't implement it).
    /// <para>Wrap composes delegates and reads no values. A parameter loaded from a .pr is lazy — it
    /// lifts on the typed ask — so a modifier reads its own parameters INSIDE the delegate it returns,
    /// where awaiting is free. Reading at wrap time sees the wire form and has to invent a value.</para></summary>
    public async System.Threading.Tasks.Task<(System.Func<System.Threading.Tasks.Task<global::app.data.@this>>? Wrapped, global::app.error.Error? Error)> Wrap(
        System.Func<System.Threading.Tasks.Task<global::app.data.@this>> inner,
        global::app.actor.context.@this context)
    {
        var (mod, error) = await Handler(context);
        if (error != null) return (null, error);

        // The modifier's own verdict — a timeout, a cache failure — is produced inside the delegate
        // the handler returned, after the inner action's own result was already recorded. Nothing
        // else sees it, so the node records whatever failure leaves its layer. Recording on the
        // ACTION's frame, not one of its own: a child frame pops before the layers outside it read
        // the result, and the error walk never descends into closed children, so a verdict on a
        // modifier's own frame would be invisible to an enclosing `on error`.
        var wrapped = mod!.Wrap(inner, context);
        return (async () =>
        {
            var result = await wrapped();
            if (!result.Success) Recorded(result.Error!, context);
            return result;
        }, null);
    }

    /// <summary>This modifier is an on-error clause: its handler catches (<see cref="global::app.module.ICatch"/>).
    /// Read off the module's registered handler type — no instance is made to ask.</summary>
    public bool Catches => typeof(global::app.module.ICatch).IsAssignableFrom(Module.Handler(Name));

    /// <summary>The handler this modifier runs through, its parameter slots wired — or a recorded,
    /// keyed error when the named action isn't a modifier at all.</summary>
    internal async System.Threading.Tasks.Task<(global::app.module.IModifier? Handler, global::app.error.Error? Error)> Handler(
        global::app.actor.context.@this context)
    {
        var (instance, error) = Instance(context);
        if (error != null) return (null, Recorded(error, context));
        // Resolve wires the handler's parameter slots; the values lift later, on the typed ask.
        var (handler, resolveErr) = await instance!.Resolve(this, context);
        if (resolveErr != null) return (null, Recorded(resolveErr, context));
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
            return (null, Recorded(new global::app.error.ActionError(
                $"{Module}.{Name} is not a modifier (it was placed in a modifiers array but isn't one). " +
                $"Move it out as a peer action in the step's top-level actions array.{loc}",
                "ModifierError", 400), context));
        }
        return (mod, null);
    }

    /// <summary>The error, recorded on the frame this modifier is running inside — the action's own,
    /// because the fold runs after the action pushed it. The frame keeps each error once, so a layer
    /// passing one through adds nothing and a retry's fresh error is kept.</summary>
    internal global::app.error.Error Recorded(
        global::app.error.Error error, global::app.actor.context.@this context)
    {
        var frame = context.CallStack.Current
            ?? throw new System.InvalidOperationException(
                $"{Module}.{Name} has no live frame to record '{error.Key}' on — a modifier runs inside " +
                "the frame its action pushed, so reaching here means it was wrapped outside one.");
        frame.Record(error, context);
        return error;
    }
}

using app.error;

namespace app.variable.list;

public partial class @this
{
    /// <summary>
    /// Returns a fresh <see cref="@this"/> projecting this Variables store back to the
    /// state at <paramref name="error"/>'s throw time. Asks <c>App.CallStack.EventsSince(t)</c>
    /// for variable mutation events that happened after the throw, then reverse-applies each
    /// (sets the variable back to its <see cref="app.callstack.Diff.Before"/> value).
    ///
    /// Variables owns the projection method; CallStack owns the time-ordered data. Pure —
    /// same (error, current state) → same result. No caching at this stage.
    /// </summary>
    public @this SnapshotAt(IError error)
    {
        var clone = ShallowCloneStore();
        var stack = _context?.App?.CallStack;
        if (stack == null) return clone;

        // Latest first — undo each mutation by writing its Before value.
        var events = stack.EventsSince(error.CreatedUtc).Reverse();
        foreach (var diff in events)
            clone._variables[diff.Name] = new data.@this(diff.Name, diff.Before, context: _context);
        return clone;
    }

    private @this ShallowCloneStore()
    {
        var copy = new @this { _context = _context };
        foreach (var kvp in _variables)
        {
            // Same skip rule as Clone(): per-execution cells are shared live, never
            // cloned. DynamicData (Now/GUID) and !-prefixed context vars (!app,
            // !context, …) hold live references back into the App graph — deep-cloning
            // them walks the whole runtime and overflows the stack. Snapshotting is a
            // projection of USER variable state, not of infrastructure handles.
            if (kvp.Value is data.DynamicData || kvp.Key.StartsWith("!")) continue;
            // Carry the live Data instance by clone so mutations on `copy` don't bleed back.
            copy._variables[kvp.Key] = kvp.Value.Clone();
        }
        return copy;
    }
}

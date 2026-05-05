using App.Errors;

namespace App.Variables;

public partial class @this
{
    /// <summary>
    /// Returns a fresh <see cref="@this"/> projecting this Variables store back to the
    /// state at <paramref name="error"/>'s throw time. Asks <c>App.CallStack.EventsSince(t)</c>
    /// for variable mutation events that happened after the throw, then reverse-applies each
    /// (sets the variable back to its <see cref="App.CallStack.Diff.Before"/> value).
    ///
    /// Variables owns the projection method; CallStack owns the time-ordered data. Pure —
    /// same (error, current state) → same result. No caching at this stage.
    /// </summary>
    public @this SnapshotAt(IError error)
    {
        var clone = ShallowCloneStore();
        var stack = _context?.App?.Debug?.CallStack;
        if (stack == null) return clone;

        // Latest first — undo each mutation by writing its Before value.
        var events = stack.EventsSince(error.CreatedUtc).Reverse();
        foreach (var diff in events)
            clone._variables[diff.Name] = new Data.@this(diff.Name, diff.Before) { Context = _context };
        return clone;
    }

    private @this ShallowCloneStore()
    {
        var copy = new @this { _context = _context };
        foreach (var kvp in _variables)
        {
            // Carry the live Data instance by clone so mutations on `copy` don't bleed back.
            copy._variables[kvp.Key] = kvp.Value.Clone();
        }
        return copy;
    }
}

namespace app.error.trail;

public sealed partial class @this : ISnapshot
{
    /// <summary>
    /// Captures the Trail entries as a List&lt;IError&gt;. Iteration uses the snapshot
    /// path on the underlying list (lock + ToArray), so concurrent Add is safe.
    /// </summary>
    public void Capture(global::app.snapshot.@this s) => s.Write("entries", this.ToList());

    /// <summary>
    /// Replaces the live App's Errors.Trail with one populated from the snapshot
    /// and freezes it — historic record, not a live append target.
    /// </summary>
    public static void Restore(global::app.snapshot.@this s, global::app.actor.context.@this context)
    {
        var entries = s.Read<List<IError>>("entries") ?? new List<IError>();
        context.App.Error.RestoreTrail(entries);
    }

    /// <summary>
    /// Bulk-loads entries from a snapshot and freezes the Trail in one step.
    /// Used by <see cref="Restore"/>; not part of the public API.
    /// </summary>
    internal void LoadAndFreeze(IEnumerable<IError> entries)
    {
        lock (_lock)
        {
            _entries.Clear();
            _entries.AddRange(entries);
            _frozen = true;
        }
    }
}

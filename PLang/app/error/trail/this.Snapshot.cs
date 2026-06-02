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
    /// Serializes the trail entries. The polymorphic <see cref="global::app.error.ErrorWire"/>
    /// converter carried by the io options handles each IError's shape.
    /// </summary>
    public static void Write(global::app.snapshot.@this s, global::app.snapshot.Io io)
        => io.Put("entries", s.Read<List<IError>>("entries") ?? new List<IError>());

    public static void Read(global::app.snapshot.Io io, global::app.snapshot.@this s)
        => s.Write("entries", io.Get<List<IError>>("entries") ?? new List<IError>());

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

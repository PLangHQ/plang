using Error = global::app.error.Error;

namespace app.callstack.call.error;

/// <summary>
/// Errors that occurred during this Call's lifetime. Per-frame view; the run-wide
/// view is <see cref="app.callstack.audit.@this"/>. App.Run and Goal.RunAsync record
/// here whenever a handler returns Data.FromError or throws.
///
/// Owns its append lock; sibling branches under Task.WhenAll on the same Call (rare
/// but possible if a parallel construct dispatches under the active frame) can hit
/// <see cref="Add"/> concurrently. Implements <see cref="IReadOnlyList{T}"/> for
/// natural access; iteration snapshots to avoid throwing on concurrent Add.
/// </summary>
public sealed class @this : IReadOnlyList<Error>
{
    private readonly List<Error> _entries = new();
    private readonly object _lock = new();

    public void Add(Error error)
    {
        lock (_lock) _entries.Add(error);
    }

    /// <summary>The most recent error observed at this frame — null when none. An observation
    /// log answers with its newest entry; a frame that failed twice is in play on the second.</summary>
    public Error? Newest
    {
        get { lock (_lock) return _entries.Count == 0 ? null : _entries[^1]; }
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public Error this[int index]
    {
        get { lock (_lock) return _entries[index]; }
    }

    public IEnumerator<Error> GetEnumerator()
    {
        Error[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<Error>)snapshot).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

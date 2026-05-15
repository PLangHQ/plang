using app.Errors;

namespace app.CallStack.Audit;

/// <summary>
/// Run-wide accumulator of every error observed at every Call frame. Survives Pop —
/// downstream observers (test harness, telemetry) treat this as authoritative for
/// "did anything fail during this run, even if recovered?". PLang reads it as
/// <c>%!callStack.Audit%</c> / <c>%!callStack.Audit.Count%</c>.
///
/// Owns its append lock; sibling Task.WhenAll branches on goal.call can hit
/// <see cref="Add"/> concurrently. Implements <see cref="IReadOnlyList{T}"/> for natural
/// PLang access (Count, indexer, iteration). Iteration takes a snapshot to avoid
/// throwing on concurrent Add.
///
/// Lifecycle: unbounded for the App's lifetime — long-running processes accumulate
/// linearly. Bounded retention is a future opt-in.
/// </summary>
public sealed class @this : IReadOnlyList<IError>
{
    private readonly List<IError> _entries = new();
    private readonly object _lock = new();

    /// <summary>Thread-safe append. Safe under Task.WhenAll on goal.call.</summary>
    public void Add(IError error)
    {
        lock (_lock) _entries.Add(error);
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public IError this[int index]
    {
        get { lock (_lock) return _entries[index]; }
    }

    public IEnumerator<IError> GetEnumerator()
    {
        IError[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<IError>)snapshot).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

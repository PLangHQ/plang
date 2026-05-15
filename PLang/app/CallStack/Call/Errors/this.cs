using IError = app.Errors.IError;

namespace app.CallStack.Call.Errors;

/// <summary>
/// Errors that occurred during this Call's lifetime. Per-frame view; the run-wide
/// view is <see cref="app.CallStack.Audit.@this"/>. App.Run and Goal.RunAsync record
/// here whenever a handler returns Data.FromError or throws.
///
/// Owns its append lock; sibling branches under Task.WhenAll on the same Call (rare
/// but possible if a parallel construct dispatches under the active frame) can hit
/// <see cref="Add"/> concurrently. Implements <see cref="IReadOnlyList{T}"/> for
/// natural access; iteration snapshots to avoid throwing on concurrent Add.
/// </summary>
public sealed class @this : IReadOnlyList<IError>
{
    private readonly List<IError> _entries = new();
    private readonly object _lock = new();

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

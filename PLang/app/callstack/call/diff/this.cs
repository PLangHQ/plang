namespace app.callstack.call.diff;

/// <summary>
/// Variable mutations observed during this Call's lifetime. Populated synchronously by
/// the OnSet handler subscribed to <see cref="Variables.@this"/> when
/// <see cref="Flags.Diff"/> is on at Push time. Otherwise the property is null.
///
/// Owns its lock; sibling Task.WhenAll branches sharing the same Variables instance can
/// fire OnSet concurrently and Add lands safely. Implements <see cref="IReadOnlyList{T}"/>
/// for natural access; iteration takes a snapshot so a debug observer iterating
/// <c>call.Diffs</c> won't throw <c>InvalidOperationException</c> when a sibling branch
/// adds a new entry.
/// </summary>
public sealed class @this : IReadOnlyList<Diff>
{
    private readonly List<Diff> _entries = new();
    private readonly object _lock = new();

    public void Add(Diff diff)
    {
        lock (_lock) _entries.Add(diff);
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public Diff this[int index]
    {
        get { lock (_lock) return _entries[index]; }
    }

    public IEnumerator<Diff> GetEnumerator()
    {
        Diff[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<Diff>)snapshot).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

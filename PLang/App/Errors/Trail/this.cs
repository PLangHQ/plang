using App.Errors;

namespace App.Errors.Trail;

/// <summary>
/// Run-wide trail of every error pushed into <see cref="Errors.@this.Push"/>'s scope.
/// Survives Pop — answers "did anything fail during this run, even if recovered?".
/// Distinct from <see cref="App.CallStack.Audit.@this"/> which records errors observed
/// at Call frames; this one records errors flowing through error.handle.Wrap.
///
/// Owns its append lock; <see cref="Errors.@this.Push"/> and any external caller can
/// <c>Trail.Add</c> safely. Implements <see cref="IReadOnlyList{T}"/> for natural
/// access; iteration snapshots to avoid throwing on concurrent Add.
///
/// Lifecycle: unbounded for the App's lifetime — long-running processes accumulate
/// linearly. Bounded retention is a future opt-in.
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

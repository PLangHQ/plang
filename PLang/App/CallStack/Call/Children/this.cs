using CallEntity = App.CallStack.Call.@this;

namespace App.CallStack.Call.Children;

/// <summary>
/// Live siblings under a Call. Owns its lock + FIFO eviction policy — callers
/// (<see cref="App.CallStack.@this.Push"/>, <see cref="CallEntity.DisposeAsync"/>)
/// add/remove without touching synchronization. Implements
/// <see cref="IReadOnlyList{T}"/> for natural iteration; iteration takes a snapshot
/// to avoid throwing on concurrent Add/Remove.
///
/// FIFO eviction triggers only when <see cref="Flags.History"/> is on; when
/// off, popped Calls are removed at dispose so the list stays bounded by live depth
/// and the Add path never evicts. Eviction reads the live Flags via the back-reference
/// — Debug.Apply can flip History mid-run, and Add reflects the current state.
/// </summary>
public sealed class @this : IReadOnlyList<CallEntity>
{
    private readonly List<CallEntity> _entries = new();
    private readonly object _lock = new();
    private readonly App.CallStack.@this _stack;

    internal @this(App.CallStack.@this stack) { _stack = stack; }

    /// <summary>
    /// Append a child under the lock. Evicts the oldest entry when History is on and
    /// the post-add count exceeds MaxFrames.
    /// </summary>
    public void Add(CallEntity child)
    {
        lock (_lock)
        {
            _entries.Add(child);
            if (_stack.Flags.History && _entries.Count > _stack.Flags.MaxFrames)
                _entries.RemoveAt(0);
        }
    }

    /// <summary>Remove a child under the lock. Used by Call.DisposeAsync when History is off.</summary>
    public void Remove(CallEntity child)
    {
        lock (_lock) _entries.Remove(child);
    }

    public int Count
    {
        get { lock (_lock) return _entries.Count; }
    }

    public CallEntity this[int index]
    {
        get { lock (_lock) return _entries[index]; }
    }

    public IEnumerator<CallEntity> GetEnumerator()
    {
        CallEntity[] snapshot;
        lock (_lock) snapshot = _entries.ToArray();
        return ((IEnumerable<CallEntity>)snapshot).GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

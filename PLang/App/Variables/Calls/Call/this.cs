namespace App.Variables.Calls.Call;

/// <summary>
/// One forked flow's variable scope — a mutable overlay over the actor-shared
/// <see cref="Variables.@this"/> dictionary.
///
/// Push points are the operators that fork a new flow (channel fire, parallel
/// foreach iteration, concurrent task) — not the goal-call boundary. Sequential
/// <c>goal.call</c> stays in the caller's flow and writes/reads pass through
/// whatever scope (or none) is currently active.
///
/// Reads walk this overlay first, then the <see cref="Caller"/> chain. Writes
/// (routed by <see cref="Variables.@this.Set"/> when an overlay is active) land
/// in the innermost overlay only — they do not leak to siblings, and they
/// disappear when the scope disposes.
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly Dictionary<string, Data.@this> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly Calls.@this _owner;

    /// <summary>Outer Call (the one that was Current when this was pushed). Null at root.</summary>
    public @this? Caller { get; }

    internal @this(IEnumerable<Data.@this>? parameters, @this? caller, Calls.@this owner)
    {
        Caller = caller;
        _owner = owner;
        if (parameters == null) return;
        foreach (var p in parameters)
        {
            if (p == null || string.IsNullOrEmpty(p.Name)) continue;
            _entries[p.Name] = p;   // last wins on duplicate names
        }
    }

    /// <summary>
    /// Looks up <paramref name="name"/> in this overlay, walking up <see cref="Caller"/>
    /// so an inner scope shadows an outer one. Case-insensitive.
    /// </summary>
    public bool TryGet(string name, out Data.@this value)
    {
        var node = this;
        while (node != null)
        {
            if (node._entries.TryGetValue(name, out var hit))
            {
                value = hit;
                return true;
            }
            node = node.Caller;
        }
        value = null!;
        return false;
    }

    /// <summary>
    /// Writes <paramref name="value"/> into this overlay under <paramref name="name"/>.
    /// Does not propagate to <see cref="Caller"/> — siblings are isolated.
    /// </summary>
    public void Set(string name, Data.@this value)
    {
        _entries[name] = value;
    }

    /// <summary>
    /// True if this overlay (not the Caller chain) holds an entry for <paramref name="name"/>.
    /// Used by Variables.Set to decide whether the existing binding lives in this scope.
    /// </summary>
    public bool ContainsLocal(string name) => _entries.ContainsKey(name);

    public ValueTask DisposeAsync()
    {
        _owner.RestoreCurrent(this, Caller);
        return ValueTask.CompletedTask;
    }
}

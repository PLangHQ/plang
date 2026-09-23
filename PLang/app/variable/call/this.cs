namespace app.variable.call;

/// <summary>
/// One forked flow's variable scope — a mutable overlay over the actor-shared
/// <see cref="Variables.@this"/> dictionary.
///
/// Push points are the operators that fork a new flow (a tool invocation, a parallel
/// foreach iteration, a listener accept loop) — not the goal-call boundary, and not a
/// channel write or a callback, which are calls in the caller's flow. Sequential
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
    private readonly Dictionary<string, data.@this> _entries =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly call.list.@this _owner;
    // The names this frame was born with — what its runner supplied. A birth fact, kept apart from
    // _entries, which also grows with every Set inside the invocation.
    private readonly HashSet<string> _born = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Outer Call (the one that was Current when this was pushed). Null at root.</summary>
    public @this? Caller { get; }

    // The held action this frame was pushed to run (a tool invocation) — the one call its supplied
    // names are FOR. A call nested deeper in the same flow is not it.
    private readonly global::app.goal.step.action.@this? _for;

    internal @this(IEnumerable<data.@this>? parameters, @this? caller, call.list.@this owner,
        global::app.goal.step.action.@this? held = null)
    {
        Caller = caller;
        _owner = owner;
        _for = held;
        if (parameters == null) return;
        foreach (var p in parameters)
        {
            if (p == null || string.IsNullOrEmpty(p.Name)) continue;
            _entries[p.Name] = p;   // last wins on duplicate names
            _born.Add(p.Name);
        }
    }

    /// <summary>True when this frame was pushed FOR <paramref name="call"/> and born with
    /// <paramref name="name"/> — its runner supplied it (a tool's argument). A name set later in the
    /// flow is not supplied, and neither is anything to a call the frame was not pushed for.</summary>
    public bool Supplies(global::app.goal.step.action.@this call, string name)
        => ReferenceEquals(_for, call) && _born.Contains(name);

    /// <summary>
    /// Looks up <paramref name="name"/> in this overlay, walking up <see cref="Caller"/>
    /// so an inner scope shadows an outer one. Case-insensitive.
    /// </summary>
    public bool TryGet(string name, out data.@this value)
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
    public void Set(string name, data.@this value)
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

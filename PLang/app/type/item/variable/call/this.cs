namespace app.type.item.variable.call;

/// <summary>
/// A call's frame over the actor's <see cref="Variables.@this"/>: the names it was born with — a loop's
/// <c>%item%</c>, a call's parameters, a callback's state. Reads walk this frame first, then the
/// <see cref="Caller"/> chain, then the actor's memory. A write to a name this frame binds stays here and
/// ends with it; a write to any other name goes where it would without the frame — the nearest enclosing
/// frame that binds it, else the actor's memory (<see cref="Keeper"/>).
/// </summary>
public class @this : IAsyncDisposable
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

    protected internal @this(IEnumerable<data.@this>? parameters, @this? caller, call.list.@this owner,
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

    /// <summary>The frame a write to <paramref name="name"/> lands in: this one when it binds the name, else
    /// the nearest caller that does; null when none does — the write goes to the actor's memory.</summary>
    internal virtual @this? Keeper(string name) => _born.Contains(name) ? this : Caller?.Keeper(name);

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

    /// <summary>The names this overlay and its callers hold, the inner scope first.</summary>
    internal IEnumerable<string> Names
    {
        get
        {
            for (var node = this; node != null; node = node.Caller)
                foreach (var name in node._entries.Keys) yield return name;
        }
    }

    /// <summary>Writes <paramref name="value"/> into this frame under <paramref name="name"/> (the frame
    /// <see cref="Keeper"/> chose).</summary>
    public void Set(string name, data.@this value)
    {
        _entries[name] = value;
    }

    /// <summary>True if this frame (not the Caller chain) holds an entry for <paramref name="name"/>.</summary>
    public bool ContainsLocal(string name) => _entries.ContainsKey(name);

    public ValueTask DisposeAsync()
    {
        _owner.RestoreCurrent(this, Caller);
        return ValueTask.CompletedTask;
    }
}

using System.Collections.Immutable;

namespace App.Variables.Calls.Call;

/// <summary>
/// One call's parameter bindings — read-only overlay scoped to the call's lifetime.
/// Reads go through <see cref="TryGet"/>; writes are not supported here (the
/// <see cref="Variables.@this"/> Set path always targets the underlying dict, so
/// goal-body mutations like <c>set %x% = 1</c> persist on actor state regardless
/// of an active call frame).
/// </summary>
public sealed class @this : IAsyncDisposable
{
    private readonly ImmutableDictionary<string, Data.@this> _parameters;
    private readonly Calls.@this _owner;

    /// <summary>Outer Call (the one that was Current when this was pushed). Null at root.</summary>
    public @this? Caller { get; }

    internal @this(IEnumerable<Data.@this>? parameters, @this? caller, Calls.@this owner)
    {
        Caller = caller;
        _owner = owner;
        if (parameters == null)
        {
            _parameters = ImmutableDictionary<string, Data.@this>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase);
            return;
        }
        var b = ImmutableDictionary.CreateBuilder<string, Data.@this>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parameters)
        {
            if (p == null || string.IsNullOrEmpty(p.Name)) continue;
            b[p.Name] = p;   // last wins on duplicate names
        }
        _parameters = b.ToImmutable();
    }

    /// <summary>
    /// Looks up <paramref name="name"/> in this frame, walking up <see cref="Caller"/>
    /// so an inner frame can shadow an outer one. Case-insensitive.
    /// </summary>
    public bool TryGet(string name, out Data.@this value)
    {
        var node = this;
        while (node != null)
        {
            if (node._parameters.TryGetValue(name, out var hit))
            {
                value = hit;
                return true;
            }
            node = node.Caller;
        }
        value = null!;
        return false;
    }

    public ValueTask DisposeAsync()
    {
        _owner.RestoreCurrent(this, Caller);
        return ValueTask.CompletedTask;
    }
}

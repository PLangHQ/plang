namespace app.@event.on.list;

/// <summary>
/// The bindings a context holds — what its actor bound while a program ran, by the item each is on: that item's events
/// as its own would hold them, made on the first bind on it. The item is held by reference while the context lives, so
/// the app that bound keeps the binding and the item keeps nothing of it: on an entry its parent shares, a binding fires
/// only in its own app and goes with it. What a program binds when it is read stays on the item.
/// </summary>
internal sealed class @this
{
    // Each item's held events, made on the first bind on any item: a context that binds nothing holds no table.
    private System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.@this, own>? _held;

    /// <summary>The events <paramref name="item"/> has held here — made on the first bind on it, with the events its
    /// type declares.</summary>
    internal own this[global::app.type.item.@this item]
    {
        get
        {
            var held = _held ?? System.Threading.Interlocked.CompareExchange(ref _held,
                new(System.Collections.Generic.ReferenceEqualityComparer.Instance), null) ?? _held!;
            return held.GetOrAdd(item, static each => new own(each.on.Declared));
        }
    }

    /// <summary>The events <paramref name="item"/> has held here; null when nothing was bound on it here.</summary>
    internal own? Of(global::app.type.item.@this item)
        => _held is { } held && held.TryGetValue(item, out var events) ? events : null;

    /// <summary>Lets go of every binding held here — the context's end.</summary>
    internal void Clear() => _held?.Clear();
}

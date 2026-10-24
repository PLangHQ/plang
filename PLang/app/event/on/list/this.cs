namespace app.@event.on.list;

/// <summary>
/// The bindings an actor's context holds — what that actor bound while a program ran, by the item each is on: that
/// item's events as its own would hold them, made on the first bind on it. The item is held by reference while the
/// context lives, so the app that bound keeps the binding and the item keeps nothing of it: on an entry its parent
/// shares, a binding fires only in its own app and goes with it. What a program binds when it is read stays on the item.
/// </summary>
internal sealed class @this
{
    // The actor whose bindings these are: every binding held here is that actor's.
    private readonly global::app.actor.@this _actor;

    // Each item's held events, made on the first bind on any item: a context that binds nothing holds no table.
    private System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.@this, own>? _held;

    /// <summary>The bindings <paramref name="actor"/>'s context holds.</summary>
    internal @this(global::app.actor.@this actor) => _actor = actor;

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

    /// <summary>Binds <paramref name="handler"/> on <paramref name="item"/>'s event named <paramref name="event"/>,
    /// <paramref name="when"/> it runs — this actor's binding, in <paramref name="scope"/>, for the items
    /// <paramref name="filter"/> takes (every one when none is given).</summary>
    internal binding.@this Bind(global::app.type.item.@this item, string @event, When when,
        System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> handler,
        binding.Scope scope, System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool>? filter = null)
        => this[item].Bind(@event, when, side => new binding.@this(side, handler, _actor, scope, filter ?? binding.@this.Always));

    /// <summary>Binds the binding <paramref name="make"/> makes — handed its list and this actor — on
    /// <paramref name="item"/>'s event named <paramref name="event"/>, <paramref name="when"/> it runs.</summary>
    internal T Bind<T>(global::app.type.item.@this item, string @event, When when,
        System.Func<binding.list.@this, global::app.actor.@this, T> make) where T : binding.@this
        => this[item].Bind(@event, when, side => make(side, _actor));

    /// <summary>Lets go of every binding held here — the context's end.</summary>
    internal void Clear() => _held?.Clear();
}

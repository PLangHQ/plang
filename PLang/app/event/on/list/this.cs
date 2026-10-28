namespace app.@event.on.list;

/// <summary>
/// The bindings an actor's context holds — what that actor bound while a program ran, by the item each is on: that
/// item's events as its own would hold them, made on the first bind on it. The item is held by reference while the
/// context lives, so the app that bound keeps the binding and the item keeps nothing of it: on an entry its parent
/// shares, a binding fires only in its own app and goes with it. What a program binds when it is read stays on the item.
/// </summary>
internal sealed class @this
{
    // The actor whose bindings these are: its app types the held events' rows.
    private readonly global::app.actor.@this _actor;

    // Each item's held events, keyed by the item itself.
    private readonly System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.@this, on.@this> _held =
        new(System.Collections.Generic.ReferenceEqualityComparer.Instance);

    /// <summary>The bindings <paramref name="actor"/> holds.</summary>
    internal @this(global::app.actor.@this actor) => _actor = actor;

    /// <summary>The events <paramref name="item"/> has held here — made on the first bind on it, with the events its
    /// type declares. A binding is added to one of them (<c>on[item]["start"]![When.before].Add(…)</c>).</summary>
    internal on.@this this[global::app.type.item.@this item] => _held.GetOrAdd(item, static each => new on.@this(each));

    /// <summary>The events <paramref name="item"/> has held here; null when nothing was bound on it here.</summary>
    internal on.@this? Of(global::app.type.item.@this item) => _held.TryGetValue(item, out var events) ? events : null;

    /// <summary>What a snapshot keeps of this hold: the held events of each item a program bound on, which write the item
    /// they are on and those bindings. An item only C# bound on (a handler, a mock) is left out: its maker binds it again.
    /// Read back, each item's events are held again; where the item already holds events here (a binding its maker made
    /// again at start), the read-back bindings join them.</summary>
    [global::app.Snapshot]
    internal global::app.type.item.list.@this list
    {
        get => new(_held.Values
                .Where(events => events.Names.Any(name => events.Bound(name) is { } @event
                    && System.Enum.GetValues<When>().Any(when => @event[when].Any(binding => binding.IsSaved))))
                .Cast<global::app.type.item.@this>().ToList(),
            _actor.App.type.list[typeof(global::app.type.item.list.@this)]);
        private set
        {
            var context = _actor.Context;
            foreach (var row in value.Items(context))
            {
                // each row read as what it says it is: an item's held events, read back as they wrote themselves
                if (global::app.View.Snapshot.Make(row.Type ?? _actor.App.type.list[typeof(on.@this)], row.Peek(), context)
                        is not on.@this events || events.item is not { } item) continue;
                if (_held.TryAdd(item, events)) continue;
                var held = _held[item];
                foreach (var name in events.Names)
                    if (events.Bound(name) is { } @event)
                        foreach (var when in System.Enum.GetValues<When>())
                            foreach (var binding in @event[when])
                                if (binding is binding.action.@this saved)
                                    held[name]![when].Add(side => new binding.action.@this(side, held[name]!, saved.action, saved.Actor, saved.Scope));
            }
        }
    }

    /// <summary>Lets go of every binding held here — the context's end.</summary>
    internal void Clear() => _held.Clear();
}

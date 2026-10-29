namespace app.@event.on;

/// <summary>
/// The events on one side of a moment (its <see cref="before"/>, or its <see cref="after"/>), in the order they were
/// added. An item's moment's sides take events; the shared empty moments' are closed, made so, and never hold one.
/// Starting it reads the list as it stands, so a moment nothing is bound on costs one read. How the events' answers fold
/// into the result is the side's own. Not an item of its own (a plang value named <c>list</c> would be a second type of
/// that name); navigation reaches it as a sequence.
/// </summary>
public abstract class side : IReadOnlyList<global::app.@event.@this>
{
    private readonly object _gate = new();
    private global::app.@event.@this[] _events = [];

    // the moment this side is of; none on the shared empty moments' closed sides
    private readonly @this? _on;

    private protected side(@this? on) => _on = on;

    /// <summary>The moment this side is of.</summary>
    internal @this on => _on ?? throw new InvalidOperationException("a closed side is of no moment");

    /// <summary>Which side it is: before its moment, or after it.</summary>
    internal abstract When When { get; }

    public int Count => _events.Length;

    /// <summary>Takes in <paramref name="maker"/>'s: each of its events, in order, as this side's own inherited event —
    /// run for <paramref name="app"/>'s actor in its role, never saved or taken off by the sub app.</summary>
    internal void Inherit(side maker, global::app.@this app)
    {
        foreach (var @event in maker._events) Add(side => new inherited.@this(side, @event, app));
    }

    /// <summary>Whether a snapshot keeps anything on this side: an event a program made.</summary>
    internal bool IsSaved => _events.Any(@event => @event.IsSaved);

    public global::app.@event.@this this[int index] => _events[index];

    public IEnumerator<global::app.@event.@this> GetEnumerator() => ((IEnumerable<global::app.@event.@this>)_events).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Binds the event <paramref name="make"/> makes on this side — an event is born knowing the side it is on;
    /// answers it.</summary>
    internal T Add<T>(System.Func<side, T> make) where T : global::app.@event.@this
    {
        if (_on == null) throw new InvalidOperationException("the shared empty moment takes no event: bind through the item's own");
        var @event = make(this);
        lock (_gate) _events = [.. _events, @event];
        return @event;
    }

    // Takes an event off (its own Remove).
    internal void Remove(global::app.@event.@this @event)
    {
        lock (_gate) _events = System.Array.FindAll(_events, held => !ReferenceEquals(held, @event));
    }

    /// <summary>
    /// Starts each event that runs for <paramref name="item"/> while <paramref name="context"/>'s actor runs, in the
    /// order added, on <paramref name="result"/> as it stands. With nothing bound it answers <paramref name="result"/>
    /// as is.
    /// </summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> Start(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        // the list as it stands: an event added while it runs runs the next time
        var events = _events;
        return events.Length == 0 ? new(result) : Run(events, item, result, context);
    }

    /// <summary>Starts <paramref name="events"/> — the ones that run for <paramref name="item"/> — and folds their
    /// answers into the result.</summary>
    private protected abstract System.Threading.Tasks.ValueTask<global::app.data.@this> Run(global::app.@event.@this[] events,
        global::app.type.item.@this item, global::app.data.@this result, global::app.actor.context.@this context);
}

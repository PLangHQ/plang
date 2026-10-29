namespace app.@event.on;

/// <summary>
/// The events on one side of a moment (its <see cref="before"/>, or its <see cref="after"/>), in the order they were
/// bound. A moment's sides take events; the shared empty moments' are closed, made so, and never hold one. Running it
/// reads the list as it stands, so a moment nothing is bound on costs one read. How the events' answers fold into the
/// result is the side's own. Not an item of its own (a plang value named <c>list</c> would be a second type of that
/// name); navigation reaches it as a sequence.
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

    public global::app.@event.@this this[int index] => _events[index];

    public IEnumerator<global::app.@event.@this> GetEnumerator() => ((IEnumerable<global::app.@event.@this>)_events).GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>Binds the event <paramref name="make"/> makes on this side — an event is born knowing the side it is on;
    /// answers it.</summary>
    internal T Add<T>(System.Func<side, T> make) where T : global::app.@event.@this
    {
        var @event = make(this);
        Join(@event);
        return @event;
    }

    /// <summary>Takes in <paramref name="event"/>, born on another moment's side (an event bound on a type, joining an
    /// item of it): it runs here too, after the ones already here.</summary>
    internal void Join(global::app.@event.@this @event)
    {
        if (_on == null) throw new InvalidOperationException("the shared empty moment takes no event: bind through the actor's events");
        lock (_gate) _events = [.. _events, @event];
    }

    // Takes an event off: it runs here no more.
    internal void Remove(global::app.@event.@this @event)
    {
        lock (_gate) _events = System.Array.FindAll(_events, held => held != @event);
    }

    // The events as they stand, none when the run is in one of plang's own goals and events don't run there
    // (event.setting.goal): a side nothing is bound on asks nothing.
    private protected async System.Threading.Tasks.ValueTask<global::app.@event.@this[]> Running(global::app.actor.context.@this context)
    {
        var events = _events;
        if (events.Length == 0 || context.Actor is not { } actor) return events;
        return await actor.@event.Skips(context) ? [] : events;
    }
}

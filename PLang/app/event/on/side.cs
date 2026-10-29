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

    /// <summary>Takes in <paramref name="event"/>: it runs here, after the ones already here.</summary>
    internal void Add(global::app.@event.@this @event)
    {
        if (_on == null) throw new InvalidOperationException("the shared empty moment takes no event: bind through the actor's events");
        lock (_gate) _events = [.. _events, @event];
    }

    // Takes an event off: it runs here no more.
    internal void Remove(global::app.@event.@this @event)
    {
        lock (_gate) _events = [.. _events.Except([@event])];
    }

    // The events as they stand: a side nothing is bound on answers with no await and nothing made.
    private protected global::app.@event.@this[] Events => _events;

    // The events as they stand. When they are an actor's, inside a goal under event.setting.goal app, each event that
    // skips that goal (a general one, in one of plang's own goals for it) is left out; an action's own clauses always run.
    private protected async System.Threading.Tasks.ValueTask<global::app.@event.@this[]> Running(
        global::app.@event.@this[] events, global::app.actor.context.@this context)
    {
        if (on.list is not { } actor || context.call.Scope?.Goal is not { } goal
            || !System.Array.Exists(events, @event => @event.Skips(goal)) || !await actor.Skips(context)) return events;
        return System.Array.FindAll(events, @event => !@event.Skips(goal));
    }
}

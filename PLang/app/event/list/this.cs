using System.Collections.Concurrent;

namespace app.@event.list;

/// <summary>
/// An actor's events — <c>%!app.event%</c>: every event it bound, in the order bound, and each item's list of the ones
/// that run for it. An item's list (a goal's, a step's, a catalog action's, a type's, a channel's) is loaded on its first
/// ask with every event whose Item has it (<see cref="global::app.type.item.@this.Has"/>), in the order bound, and never
/// asks again; an event bound later is added to every loaded list it runs for, and an unloaded list gets it when it
/// loads. A variable's list is its name's, loaded on the name's first set and only when an event is bound on that name (a
/// variable object is made anew at every set). A goal run as another actor reads that actor's events, never these. A sub
/// app's actor is born holding its maker's actor's events, each taken in as its own (<see cref="inherited.@this"/>). A
/// loaded list is held as long as the actor is, one per item that stays around (a goal, a step, a catalog action, a type,
/// a channel, the app).
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.setting.ISetting<setting.@this>
{
    private readonly global::app.actor.@this _actor;
    private readonly object _gate = new();
    private global::app.@event.@this[] _events = [];

    // each item's list for this actor, loaded on its first ask: none until then, by the item's own equality (a type by its
    // name), held as long as the actor is — a list for an item a sub app shares with its maker dies with the sub app
    private readonly ConcurrentDictionary<global::app.type.item.@this, on.@this> _on = new();

    // each variable name an event is bound on, with those events; a name's list, loaded on its first set
    private readonly Dictionary<string, List<global::app.@event.@this>> _named = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, on.@this> _variable = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The events of <paramref name="actor"/>: none until it binds one.</summary>
    internal @this(global::app.actor.@this actor, global::app.type.@this type) : base(type) => _actor = actor;

    /// <summary>The event in play for the asker — <c>%!app.event.current%</c>, which <c>%!event%</c> reads: the running
    /// event's call's Data whole, its value the event, its properties what it ran for (<c>!item</c>) and the value so far
    /// (<c>!data</c>). Unset where no event's call is running.</summary>
    [global::app.LlmBuilder]
    public global::app.data.@this current(global::app.actor.context.@this context)
        => context.call.Event ?? context.NotFound("current");

    /// <summary>Every event this actor bound, in the order bound.</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.list.@this<global::app.@event.@this> list
        => new(_events, _actor.App.type.list[typeof(global::app.type.item.list.@this<global::app.@event.@this>)]);

    /// <summary>The list of <paramref name="item"/> for this actor: loaded on the first ask with every event whose Item
    /// has it, in the order bound; the same after that.</summary>
    internal on.@this this[global::app.type.item.@this item]
    {
        get
        {
            if (_on.TryGetValue(item, out var on)) return on;
            lock (_gate) return _on.GetOrAdd(item, Load);
        }
    }

    /// <summary>The list of the variable named <paramref name="name"/>: loaded on the first ask when an event is bound on
    /// that name; null when none is (a set then runs the variable type's).</summary>
    internal on.@this? this[string name]
    {
        get
        {
            if (_variable.TryGetValue(name, out var on)) return on;
            lock (_gate)
            {
                if (!_named.TryGetValue(name, out var bound) || bound.Count == 0) return null;
                return _variable.GetOrAdd(name, _ => Load(bound[0].Item));
            }
        }
    }

    // A list for item: every event whose Item has it, in the order bound.
    private on.@this Load(global::app.type.item.@this item)
    {
        var on = new on.@this(item, this);
        foreach (var @event in _events)
            if (@event.Item.Has(item)) on.Add(@event);
        return on;
    }

    // Every loaded list.
    private IEnumerable<on.@this> Loaded => _on.Values.Concat(_variable.Values);

    /// <summary>Binds <paramref name="event"/>: it joins this actor's events, and every loaded list it runs for. An
    /// unloaded list gets it when it loads. Answers it.</summary>
    internal T Add<T>(T @event) where T : global::app.@event.@this
    {
        lock (_gate)
        {
            _events = [.. _events, @event];
            foreach (var on in Loaded)
                if (@event.Item.Has(on.item!)) on.Add(@event);
        }
        return @event;
    }

    /// <summary>Binds <paramref name="event"/>, bound on the variable named <paramref name="name"/>: the name is recorded
    /// as having events, so its list loads on its next set. Answers it.</summary>
    internal T Add<T>(string name, T @event) where T : global::app.@event.@this
    {
        lock (_gate)
        {
            if (!_named.TryGetValue(name, out var bound)) _named[name] = bound = [];
            bound.Add(@event);
            return Add(@event);
        }
    }

    /// <summary>Takes in <paramref name="event"/>, an event of the actor this one's sub app was made by: bound again on
    /// what it was bound on, as this actor's own inherited event.</summary>
    internal void Inherit(global::app.@event.@this @event, global::app.@this app) => Add(new inherited.@this(@event, app));

    /// <summary>Takes <paramref name="event"/> off: out of the list and every loaded list it was in; a variable name no
    /// event is bound on any more has no list.</summary>
    internal void Remove(global::app.@event.@this @event)
    {
        lock (_gate)
        {
            _events = [.. _events.Except([@event])];
            foreach (var on in Loaded) on.Remove(@event);
            foreach (var (name, bound) in _named.ToList())
            {
                if (!bound.Remove(@event) || bound.Count > 0) continue;
                _named.Remove(name);
                _variable.TryRemove(name, out _);
            }
        }
    }

    /// <summary>Every event this actor bound, in the order bound.</summary>
    internal IEnumerable<global::app.@event.@this> Items() => _events;

    /// <summary>The events <paramref name="action"/> (an <c>on.event</c> step's) made, in the order bound — what an
    /// event's key numbers it among.</summary>
    internal IEnumerable<global::app.@event.@this> Of(global::app.goal.step.action.@this action)
        => _events.Where(@event => Equals(@event.action, action));

    /// <summary>Whether this actor's general events (bound on many) skip a run in <paramref name="context"/>: it is inside
    /// one of plang's own goals (a goal under system/, the os's or the app's own copy), and <c>event.setting.goal</c> is
    /// app.</summary>
    internal async System.Threading.Tasks.ValueTask<bool> Skips(global::app.actor.context.@this context)
        => context.call.Scope?.Goal is { IsSystem: true }
           && (await context.Setting.Get<setting.@this>()).Goal.Value == setting.Goal.app;

    // ---- what a snapshot keeps ----

    /// <summary>What a snapshot keeps of this actor's events: for each item a program bound an event on, its moments
    /// holding those events, which write the item and them. Read back, each row's events are bound again here.</summary>
    [global::app.Snapshot]
    internal global::app.type.item.list.@this on
    {
        get => new(_events.Where(@event => @event.IsSaved).GroupBy(@event => @event.Item)
                .Select(bound =>
                {
                    var on = new on.@this(bound.Key);
                    foreach (var @event in bound) on.Add(@event);
                    return (global::app.type.item.@this)on;
                }).ToList(),
            _actor.App.type.list[typeof(global::app.type.item.list.@this)]);
        private set
        {
            var context = _actor.Context;
            var type = _actor.App.type.list[typeof(on.@this)];
            foreach (var row in value.Items(context))
                global::app.View.Snapshot.Make(type, row.Peek(), context);
        }
    }

    /// <summary>
    /// The events a snapshot kept of one item (<c>{item, start: {before: [event]}}</c>), bound again here: the item read
    /// as what it says it is, then each saved event on its moment and side. Each event this actor took in from its maker at
    /// birth stands for one saved event of the same action on the same moment — the same program's, which runs once — and
    /// absorbs at most one; every other saved event is bound, so a step that bound twice comes back twice. Answers the
    /// item's moments as read. A record that doesn't read is refused, thrown to the read boundary.
    /// </summary>
    internal on.@this Read(global::app.type.item.dict.@this record, global::app.actor.context.@this context)
    {
        if (record.Get("item", context) is not { } slot
            || global::app.View.Snapshot.Make(slot.Type ?? context.App.type.list[typeof(global::app.type.item.@this)], slot.Peek(), context)
                is not { } item)
            throw Unread("a saved on's row names no item it is the moments of");
        var read = new on.@this(item);
        var choices = context.App.type.list[typeof(global::app.type.item.choice.@this<When>)];
        lock (_gate)
            foreach (var moment in record.Rows(context))
            {
                if (string.Equals(moment.Name, "item", StringComparison.OrdinalIgnoreCase)) continue;
                if (read[moment.Name] == null) throw new global::app.error.DeclinedException(read.NoEvent(moment.Name));
                if (moment.Peek() is not global::app.type.item.dict.@this sides)
                    throw Unread($"a saved on's '{moment.Name}' holds no sides");
                foreach (var side in sides.Rows(context))
                {
                    // the side reads itself as its When; a name that is no side is refused
                    When when = global::app.type.item.choice.@this<When>.Create(side.Name, choices)
                        ?? throw Unread($"a saved event '{moment.Name}' holds '{side.Name}', which is no side of its events");
                    if (side.Peek() is not global::app.type.item.list.@this rows)
                        throw Unread($"a saved event '{moment.Name}' holds '{side.Name}', which is no list of events");
                    var inherited = _events.OfType<inherited.@this>()
                        .Where(@event => Equals(@event.Item, item) && string.Equals(@event.Name, moment.Name, StringComparison.OrdinalIgnoreCase)
                                         && @event.When.Value == when).ToList();
                    foreach (var entry in rows.Items(context))
                    {
                        var saved = new call.@this(item, read.Add(moment.Name).Name, when, entry.Peek() as global::app.type.item.dict.@this
                            ?? throw Unread($"a saved event on '{moment.Name}' is no record"), context);
                        var index = inherited.FindIndex(@event => Equals(@event.action, saved.action));
                        if (index >= 0) { inherited.RemoveAt(index); continue; }
                        Add(saved);
                        read.Add(saved);
                    }
                }
            }
        return read;
    }

    private static global::app.error.DeclinedException Unread(string message)
        => new(new global::app.error.@this(message, "SnapshotRowUnread", 400));

    /// <summary>The actor's events written: each as it writes itself, in the order bound.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var events = _events;
        writer.BeginArray(events.Length);
        foreach (var @event in events) await @event.Output(writer);
        writer.EndArray();
    }
}

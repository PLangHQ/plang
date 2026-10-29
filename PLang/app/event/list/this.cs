using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace app.@event.list;

/// <summary>
/// An actor's events — <c>%!app.event%</c>: every event it bound, in the order bound, and each item's moments for it,
/// made on the item's first ask. A made moment takes in every event bound on a type the item is of (<see cref="@this.Each"/>),
/// in the order bound; an event bound on one item is placed into that item's moments when it is bound. A later event
/// joins every made moment it reaches. A goal run as another actor reads that actor's events, never these. A sub app's
/// actor is born holding its maker's actor's events, each taken in as its own (<see cref="inherited.@this"/>). The
/// moments are held as long as their item is, and made again after; a variable's are held by its name, while an event is
/// bound on it (a variable is made anew at every set).
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.setting.ISetting<setting.@this>
{
    private readonly global::app.actor.@this _actor;
    private readonly object _gate = new();
    private global::app.@event.@this[] _events = [];

    // each item's moments for this actor, made on its first ask: none until then, gone with the item
    private readonly ConditionalWeakTable<global::app.type.item.@this, on.@this> _on = new();

    // each named variable's moments, made when an event is bound on that name, dropped when its last one is taken off
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
    {
        get
        {
            var list = new global::app.type.item.list.@this<global::app.@event.@this>(
                _actor.App.type.list[typeof(global::app.type.item.list.@this<global::app.@event.@this>)]);
            foreach (var @event in _events) list.Add(@event);
            return list;
        }
    }

    /// <summary>The moments of <paramref name="item"/> for this actor: made on the first ask from every event bound on a
    /// type the item is of, in the order bound; the same after that, events bound later joined.</summary>
    internal on.@this this[global::app.type.item.@this item]
    {
        get
        {
            if (_on.TryGetValue(item, out var on)) return on;
            lock (_gate) return _on.GetValue(item, Make);
        }
    }

    /// <summary>The moments of the variable named <paramref name="name"/>; null when no event is bound on that name.</summary>
    internal on.@this? this[string name] => _variable.GetValueOrDefault(name);

    // The moments of item, taking in every event bound on a type it is of, in the order bound.
    private on.@this Make(global::app.type.item.@this item)
    {
        var on = new on.@this(item);
        foreach (var @event in _events)
            if (@event.Each is { } type && item.Is(type)) on.Add(@event.Name)[@event.When].Join(@event);
        return on;
    }

    /// <summary>Binds the event <paramref name="make"/> makes on <paramref name="item"/>'s moment
    /// <paramref name="name"/>, <paramref name="when"/>: it is placed there, and when it is bound on a type, it joins
    /// every made moment of an item of it. Answers it.</summary>
    internal T Add<T>(global::app.type.item.@this item, string name, When when, System.Func<on.side, T> make)
        where T : global::app.@event.@this
    {
        lock (_gate)
        {
            var side = this[item].Add(name)[when];
            var @event = make(side);
            Add(side, @event);
            return @event;
        }
    }

    /// <summary>Binds the event <paramref name="make"/> makes on the variable named <paramref name="variable"/>'s moment
    /// <paramref name="name"/>, <paramref name="when"/>: its moments are made then, the variable read once from its
    /// name. Answers it.</summary>
    internal T Add<T>(string variable, string name, When when, System.Func<on.side, T> make)
        where T : global::app.@event.@this
    {
        lock (_gate)
        {
            var side = _variable.GetOrAdd(variable, named => Make(Named(named))).Add(name)[when];
            var @event = make(side);
            Add(side, @event);
            return @event;
        }
    }

    // Places event on side, joins it into every made moment of an item of the type it is bound on, and lists it.
    private void Add(on.side side, global::app.@event.@this @event)
    {
        side.Join(@event);
        if (@event.Each is { } type)
            foreach (var on in _on.Select(made => made.Value).Concat(_variable.Values))
                if (on.item!.Is(type)) on.Add(@event.Name)[@event.When].Join(@event);
        _events = [.. _events, @event];
    }

    // The variable named name, as its moments are handed it.
    private global::app.type.item.variable.@this Named(string name)
        => new global::app.type.item.variable.parser.@this($"%{name}%", _actor.App.type.list).Variable.Single();

    /// <summary>Takes in <paramref name="maker"/>, an event of the actor this one's sub app was made by: bound again on
    /// the same moment, as this actor's own inherited event.</summary>
    internal void Inherit(global::app.@event.@this maker, global::app.@this app)
        => Add(maker.Item, maker.Name, maker.When, side => new inherited.@this(side, maker, app));

    /// <summary>Takes <paramref name="event"/> off: off every moment it was in, and out of the list; a variable no event
    /// is bound on any more has no moments.</summary>
    internal void Remove(global::app.@event.@this @event)
    {
        lock (_gate)
        {
            _events = System.Array.FindAll(_events, held => held != @event);
            foreach (var (_, on) in _on) on.Remove(@event);
            foreach (var (name, on) in _variable)
            {
                on.Remove(@event);
                if (on.IsEmpty) _variable.TryRemove(name, out _);
            }
        }
    }

    /// <summary>Every event this actor bound, in the order bound.</summary>
    internal IEnumerable<global::app.@event.@this> Items() => _events;

    /// <summary>The events <paramref name="action"/> (an <c>on.event</c> step's) made, in the order bound — what an
    /// event's key numbers it among.</summary>
    internal IEnumerable<global::app.@event.@this> Of(global::app.goal.step.action.@this action)
        => _events.Where(@event => Equals(@event.action, action));

    /// <summary>Whether this actor's events skip a run in <paramref name="context"/>: it is inside one of plang's own goals
    /// (a goal under system/, the os's or the app's own copy), and <c>event.setting.goal</c> is app.</summary>
    internal async System.Threading.Tasks.ValueTask<bool> Skips(global::app.actor.context.@this context)
        => context.call.Current?.Goal is { IsSystem: true }
           && (await context.Setting.Get<setting.@this>()).Goal.Value == setting.Goal.app;

    // ---- what a snapshot keeps ----

    /// <summary>What a snapshot keeps of this actor's events: the moments of each item a program bound an event on, which
    /// write the item and those events. Read back, each row's events are bound again on this actor's events.</summary>
    [global::app.Snapshot]
    internal global::app.type.item.list.@this on
    {
        get => new(_events.Where(@event => @event.IsSaved).Select(@event => @event.Item).Distinct()
                .Select(item => (global::app.type.item.@this)this[item]).ToList(),
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
    /// birth stands for one saved event of the same action — the same program's, which runs once — and absorbs at most
    /// one; every other saved event is bound, so a step that bound twice comes back twice. Answers the item's moments. A
    /// record that doesn't read is refused, thrown to the read boundary.
    /// </summary>
    internal on.@this Read(global::app.type.item.dict.@this record, global::app.actor.context.@this context)
    {
        if (record.Get("item", context) is not { } slot
            || global::app.View.Snapshot.Make(slot.Type ?? context.App.type.list[typeof(global::app.type.item.@this)], slot.Peek(), context)
                is not { } item)
            throw Unread("a saved on's row names no item it is the moments of");
        var moments = this[item];
        lock (_gate)
            foreach (var row in record.Rows(context))
            {
                if (string.Equals(row.Name, "item", StringComparison.OrdinalIgnoreCase)) continue;
                if (moments[row.Name] == null) throw new global::app.error.DeclinedException(moments.NoEvent(row.Name));
                if (row.Peek() is not global::app.type.item.dict.@this sides)
                    throw Unread($"a saved on's '{row.Name}' holds no sides");
                foreach (var held in sides.Rows(context))
                {
                    var when = string.Equals(held.Name, "before", StringComparison.OrdinalIgnoreCase) ? When.before
                        : string.Equals(held.Name, "after", StringComparison.OrdinalIgnoreCase) ? When.after
                        : throw Unread($"a saved event '{row.Name}' holds '{held.Name}', which is no side of its events");
                    if (held.Peek() is not global::app.type.item.list.@this rows)
                        throw Unread($"a saved event '{row.Name}' holds '{held.Name}', which is no list of events");
                    var side = moments.Add(row.Name)[when];
                    var inherited = side.OfType<inherited.@this>().ToList();
                    foreach (var saved in rows.Items(context))
                    {
                        var @event = new call.@this(side, saved.Peek() as global::app.type.item.dict.@this
                            ?? throw Unread($"a saved event on '{row.Name}' is no record"), context);
                        var taken = inherited.FindIndex(maker => Equals(maker.action, @event.action));
                        if (taken >= 0) { inherited.RemoveAt(taken); continue; }
                        Add(side, @event);
                    }
                }
            }
        return moments;
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

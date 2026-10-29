using System.Collections.Concurrent;

namespace app.@event.list;

/// <summary>
/// An actor's events — <c>%!app.event%</c>: every event it bound, in the order bound, and each item's list of the ones
/// that run for it. Lazy: the first ask or add loads them — the actor's system events (each file of system/event/ run as
/// this actor), then, in a sub app, what its maker's actor of its role had bound, each taken in as its own
/// (<see cref="inherited.@this"/>); a snapshot's read loads them before it binds what it kept. An item's list (a goal's, a step's, a catalog action's, a
/// type's, a channel's) is made on its first ask with every event whose Item has it
/// (<see cref="global::app.type.item.@this.Has"/>), in the order bound; an event bound later is added to every list made.
/// A variable's list is its name's, made on the name's first set and only when an event is bound on that name (a
/// variable object is made anew at every set). A goal run as another actor reads that actor's events, never these. A
/// list is held as long as the actor is, one per item that stays around (a goal, a step, a catalog action, a type, a
/// channel, the app).
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.setting.ISetting<setting.@this>
{
    private readonly global::app.actor.@this _actor;
    private readonly object _gate = new();
    private global::app.@event.@this[] _events = [];

    // each item's list for this actor, made on its first ask: none until then, by the item's own equality (a type by its
    // name), held as long as the actor is — a list for an item a sub app shares with its maker dies with the sub app
    private readonly ConcurrentDictionary<global::app.type.item.@this, on.@this> _on = new();

    // each variable name an event is bound on, with those events; a name's list, made on its first set
    // (read without the lock: a set of a name nothing is bound on takes none; each name's events replaced, never changed)
    private readonly ConcurrentDictionary<string, global::app.@event.@this[]> _named = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, on.@this> _variable = new(StringComparer.OrdinalIgnoreCase);

    // the load, once: started by the first ask or add, every other asker awaiting the same
    private System.Threading.Tasks.TaskCompletionSource? _load;

    // the flow that is loading: the system/event goals it runs ask the door themselves, and go on with what is loaded so far
    private readonly System.Threading.AsyncLocal<bool> _loading = new();

    /// <summary>The events of <paramref name="actor"/>: loaded on their first use.</summary>
    internal @this(global::app.actor.@this actor, global::app.type.@this type) : base(type) => _actor = actor;

    /// <summary>The event in play for the asker — <c>%!app.event.current%</c>, which <c>%!event%</c> reads: the running
    /// event's call's Data whole, its value the event, its properties what it ran for (<c>!item</c>) and the value so far
    /// (<c>!data</c>). Unset where no event's call is running.</summary>
    [global::app.LlmBuilder]
    public global::app.data.@this current(global::app.actor.context.@this context)
        => context.call.Event ?? context.NotFound("current");

    /// <summary>Every event this actor bound, in the order bound, as they stand (<c>%!app.event</c> loads them first).</summary>
    [global::app.LlmBuilder]
    public global::app.type.item.list.@this<global::app.@event.@this> list
        => new(_events, _actor.App.type.list[typeof(global::app.type.item.list.@this<global::app.@event.@this>)]);

    /// <summary>The list of <paramref name="item"/> for this actor, its events loaded first if they never were: made on
    /// the first ask with every event whose Item has it, in the order bound; the same after that. Once loaded (or inside
    /// the flow that is loading) it answers at once, nothing awaited.</summary>
    internal System.Threading.Tasks.ValueTask<on.@this> On(global::app.type.item.@this item)
    {
        var loaded = Loaded();
        if (!loaded.IsCompletedSuccessfully) return Awaited(loaded, item);
        if (_on.TryGetValue(item, out var on)) return new(on);
        lock (_gate) return new(_on.GetOrAdd(item, List));
    }

    // The first ask of an actor whose events never loaded: they load, then the list.
    private async System.Threading.Tasks.ValueTask<on.@this> Awaited(System.Threading.Tasks.ValueTask loaded, global::app.type.item.@this item)
    {
        await loaded;
        return await On(item);
    }

    /// <summary>The list a set or a remove of the variable named <paramref name="name"/> runs, the events loaded first:
    /// the name's own when an event is bound on that name (made on its first ask, the variable type's events in it too),
    /// else the variable type's.</summary>
    internal System.Threading.Tasks.ValueTask<on.@this> On(string name)
    {
        var loaded = Loaded();
        if (!loaded.IsCompletedSuccessfully) return Awaited(loaded, name);
        if (_variable.TryGetValue(name, out var on)) return new(on);
        if (!_named.TryGetValue(name, out var bound)) return On(_actor.App.variable.type);
        // the name's list made as the events stand, which a bind changes only under the same lock
        lock (_gate) return new(_variable.GetOrAdd(name, _ => List(bound[0].Item)));
    }

    // The first ask of an actor whose events never loaded: they load, then the name's list.
    private async System.Threading.Tasks.ValueTask<on.@this> Awaited(System.Threading.Tasks.ValueTask loaded, string name)
    {
        await loaded;
        return await On(name);
    }

    // A list for item: every event whose Item has it, in the order bound.
    private on.@this List(global::app.type.item.@this item)
    {
        var on = new on.@this(item, this);
        foreach (var @event in _events)
            if (@event.Item.Has(item)) on.Add(@event);
        return on;
    }

    // Every list made.
    private IEnumerable<on.@this> Made => _on.Values.Concat(_variable.Values);

    /// <summary>Binds <paramref name="event"/>, the events loaded first: it joins this actor's events, and every list made
    /// that it runs for. A list not made yet gets it when it is. Answers it.</summary>
    internal async System.Threading.Tasks.ValueTask<T> Add<T>(T @event) where T : global::app.@event.@this
    {
        await Loaded();
        Join(@event);
        return @event;
    }

    /// <summary>Binds <paramref name="event"/>, bound on the variable named <paramref name="name"/>, the events loaded
    /// first: the name is recorded as having events, so its list is made on its next set. Answers it.</summary>
    internal async System.Threading.Tasks.ValueTask<T> Add<T>(string name, T @event) where T : global::app.@event.@this
    {
        await Loaded();
        lock (_gate) _named[name] = _named.TryGetValue(name, out var bound) ? [.. bound, @event] : [@event];
        Join(@event);
        return @event;
    }

    // Binds event as the events stand: it joins this actor's events, and every list made that it runs for.
    private void Join(global::app.@event.@this @event)
    {
        lock (_gate)
        {
            _events = [.. _events, @event];
            foreach (var on in Made)
                if (@event.Item.Has(on.item!)) on.Add(@event);
        }
    }

    /// <summary>Takes <paramref name="event"/> off: out of the list and every list made it was in; a variable name no event
    /// is bound on any more has no list.</summary>
    internal void Remove(global::app.@event.@this @event)
    {
        lock (_gate)
        {
            _events = [.. _events.Except([@event])];
            foreach (var on in Made) on.Remove(@event);
            foreach (var (name, bound) in _named)
            {
                if (!bound.Contains(@event)) continue;
                global::app.@event.@this[] left = [.. bound.Except([@event])];
                if (left.Length > 0) { _named[name] = left; continue; }
                _named.TryRemove(name, out _);
                _variable.TryRemove(name, out _);
            }
        }
    }

    /// <summary>Every event this actor bound, in the order bound, as they stand.</summary>
    internal IEnumerable<global::app.@event.@this> Items() => _events;

    /// <summary>The events <paramref name="action"/> (an <c>on.event</c> step's) made, in the order bound — what an
    /// event's key numbers it among.</summary>
    internal IEnumerable<global::app.@event.@this> Of(global::app.goal.step.action.@this action)
        => _events.Where(@event => Equals(@event.action, action));

    // ---- the load ----

    /// <summary>This actor's events, loaded: at once when they are, or inside the flow that is loading them (which goes on
    /// with what is loaded so far); else the one load, started by whoever asks first — claimed, never under a lock, so
    /// its first part runs with nothing held.</summary>
    internal System.Threading.Tasks.ValueTask Loaded()
    {
        if (_load is { Task.IsCompletedSuccessfully: true } || _loading.Value) return default;
        var load = new System.Threading.Tasks.TaskCompletionSource();
        if (System.Threading.Interlocked.CompareExchange(ref _load, load, null) is { } started) return new(started.Task);
        _ = Load(load);
        return new(load.Task);
    }

    // The load, answered on done: the system events, then what this actor took in from its maker's (a sub app's). The
    // app's System loads first: every goal is read as it, so a goal another actor's load reads finds System's events
    // standing — and System's own load, reading the same file, never waits on this one.
    private async System.Threading.Tasks.Task Load(System.Threading.Tasks.TaskCompletionSource done)
    {
        _loading.Value = true;
        try
        {
            if (_actor.App.actor.list.System is { } system && !Equals(system, _actor)) await system.@event.Loaded();
            var replaced = await Run(_actor.Context);
            foreach (var made in await _actor.App.origin.@event(_actor.Role))
                if (made.File is not { } file || !replaced.Contains(file))
                    Join(new inherited.@this(made, _actor.App));
            done.SetResult();
        }
        catch (System.Exception ex) { done.SetException(ex); }
    }

    // Runs the system/event/ files as this actor: their names from the folders the app's origin names (the root: its own
    // then the os's; a sub app: its own only), each loaded at its plang path, /system/event/<name>, as the goal list
    // finds it (the app's own copy first, else the os's), so it is one of plang's own goals. Nothing runs under
    // event.setting.system skip. A file that doesn't load is said on the error channel, and the next one loads. Answers
    // the names it ran: a sub app's own file replaces what its maker bound from the file of that name.
    private async System.Threading.Tasks.Task<HashSet<string>> Run(global::app.actor.context.@this context)
    {
        var ran = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        // no folder of its own (a sub app over its maker's), nothing to read — the setting not either
        var folders = context.App.origin.Events(context.App).ToList();
        if (folders.Count == 0 || (await context.Setting.Get<setting.@this>()).System.Value == setting.System.skip) return ran;
        foreach (var folder in folders)
        {
            if (!await (await folder.Exists(context)).ToBooleanAsync()) continue;
            var listed = await folder.List("*.goal", recursive: false, context);
            if (!listed.Success || await listed.Value() is not { } files) continue;
            foreach (var file in files.Items())
            {
                var name = file.Name.ToString();
                if (!ran.Add(name)) continue;
                var goal = await context.App.goal.list.Load(global::app.type.item.path.@this.Resolve("/system/event/" + name, context));
                var started = goal.Success && await goal.Value() is global::app.goal.@this bound ? await bound.Start(context) : goal;
                if (!started.Success && context.Actor!.Channel.Get(global::app.channel.list.@this.Error) is { } error)
                    await error.WriteText($"the system event {name} didn't load: {started.Error?.Message}");
            }
        }
        return ran;
    }

    /// <summary>Whether this actor's general events (bound on many) skip a run in <paramref name="context"/>: it is inside
    /// one of plang's own goals (a goal under system/, the os's or the app's own copy), and <c>event.setting.goal</c> is
    /// app.</summary>
    internal async System.Threading.Tasks.ValueTask<bool> Skips(global::app.actor.context.@this context)
        => context.call.Scope?.Goal is { IsSystem: true }
           && (await context.Setting.Get<setting.@this>()).Goal.Value == setting.Goal.app;

    // ---- what a snapshot keeps ----

    /// <summary>What a snapshot keeps of this actor's events: for each item a program bound an event on, its moments
    /// holding those events, which write the item and them. Read back, each row binds its events again
    /// (<see cref="Read"/>).</summary>
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

    /// <summary>The events a snapshot kept of one item (<c>{item, start: {before: [event]}}</c>), bound here — the
    /// snapshot's read loads this actor's events first, so they bind after its system events and its maker's: the item
    /// read as what it says it is, then each saved event on its moment and side. Each event this actor took in from its
    /// maker stands for one saved event of the same action on the same moment — the same program's, which runs once — and
    /// absorbs at most one; every other saved event is bound, so a step that bound twice comes back twice. A record that
    /// doesn't read is refused, thrown to the read boundary. Answers the moments read.</summary>
    internal on.@this Read(global::app.type.item.dict.@this record, global::app.actor.context.@this context)
    {
        if (record.Get("item", context) is not { } slot
            || global::app.View.Snapshot.Make(slot.Type ?? context.App.type.list[typeof(global::app.type.item.@this)], slot.Peek(), context)
                is not { } item)
            throw global::app.@event.@this.Unread("a saved on's row names no item it is the moments of");
        var read = new on.@this(item);
        var choices = context.App.type.list[typeof(global::app.type.item.choice.@this<When>)];
        foreach (var moment in record.Rows(context))
        {
            if (string.Equals(moment.Name, "item", StringComparison.OrdinalIgnoreCase)) continue;
            if (read[moment.Name] == null) throw new global::app.error.DeclinedException(read.NoEvent(moment.Name));
            if (moment.Peek() is not global::app.type.item.dict.@this sides)
                throw global::app.@event.@this.Unread($"a saved on's '{moment.Name}' holds no sides");
            foreach (var side in sides.Rows(context))
            {
                // the side reads itself as its When; a name that is no side is refused
                When when = global::app.type.item.choice.@this<When>.Create(side.Name, choices)
                    ?? throw global::app.@event.@this.Unread($"a saved event '{moment.Name}' holds '{side.Name}', which is no side of its events");
                if (side.Peek() is not global::app.type.item.list.@this rows)
                    throw global::app.@event.@this.Unread($"a saved event '{moment.Name}' holds '{side.Name}', which is no list of events");
                var inherited = _events.OfType<inherited.@this>()
                    .Where(@event => Equals(@event.Item, item) && string.Equals(@event.Name, moment.Name, StringComparison.OrdinalIgnoreCase)
                                     && @event.When.Value == when).ToList();
                foreach (var entry in rows.Items(context))
                {
                    var saved = new call.@this(item, read.Add(moment.Name).Name, when, entry.Peek() as global::app.type.item.dict.@this
                        ?? throw global::app.@event.@this.Unread($"a saved event on '{moment.Name}' is no record"), context);
                    var index = inherited.FindIndex(@event => Equals(@event.action, saved.action));
                    if (index >= 0) { inherited.RemoveAt(index); continue; }
                    Join(saved);
                }
            }
        }
        return read;
    }

    /// <summary>The actor's events written: each as it writes itself, in the order bound.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var events = _events;
        writer.BeginArray(events.Length);
        foreach (var @event in events) await @event.Output(writer);
        writer.EndArray();
    }
}

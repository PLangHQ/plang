namespace app.@event.list;

/// <summary>
/// An app's events — <c>%!app.event%</c>: the moments of each item something bound on while a program ran
/// (<see cref="on.@this"/>, which knows its item), made on the first bind on it. A sub app is born holding what its maker
/// had bound then, each event taken in as its own (<see cref="inherited.@this"/>), the maker's first; what it binds after
/// is its own, and what its maker binds later never reaches it. The item is held by reference for the app's life, so an
/// event on an item an app shares runs only in the app that bound it. What a program binds when it is read (an action's
/// clauses) stays on the item. Reading it as it stands takes no lock and makes nothing, so a moment nothing is bound on
/// costs one read.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private readonly global::app.@this _app;
    private readonly object _gate = new();
    private on.@this[] _on = [];

    /// <summary>The events of <paramref name="app"/>, born holding its maker's (<paramref name="maker"/>), each taken in
    /// as its own.</summary>
    internal @this(global::app.@this app, @this? maker) : base(app.type.list[typeof(@this)])
    {
        _app = app;
        // each item's moments inherit the maker's, each side its own part
        foreach (var on in maker?._on ?? []) Add(on.item!).Inherit(on, app);
    }

    /// <summary>The event in play for the asker — <c>%!app.event.current%</c>, which <c>%!event%</c> reads: the running
    /// event's call's Data whole, its value the event, its properties what it ran for (<c>!item</c>) and the value so far
    /// (<c>!data</c>). Unset where no event's call is running.</summary>
    [global::app.LlmBuilder]
    public global::app.data.@this current(global::app.actor.context.@this context)
        => context.call.Event ?? context.NotFound("current");

    /// <summary>The moments of <paramref name="item"/> this app bound on; null when it bound nothing on it. Makes
    /// nothing.</summary>
    internal on.@this? this[global::app.type.item.@this item]
    {
        get
        {
            foreach (var on in _on)
                if (ReferenceEquals(on.item, item)) return on;
            return null;
        }
    }

    /// <summary>The moments of <paramref name="item"/> this app binds on — added the first time, with the moments its type
    /// declares. An event is added to one of them (<c>app.event.Add(item).Add("start").before.Add(…)</c>).</summary>
    internal on.@this Add(global::app.type.item.@this item)
    {
        if (this[item] is { } on) return on;
        lock (_gate)
        {
            if (this[item] is { } made) return made;
            var added = new on.@this(item);
            _on = [.. _on, added];
            return added;
        }
    }

    /// <summary>Every event this app holds, each item's in the order the items were first bound on.</summary>
    internal IEnumerable<global::app.@event.@this> Items() => _on.SelectMany(on => on.Items());

    /// <summary>The events <paramref name="action"/> (an <c>on.event</c> step's) made, in <see cref="Items"/>' order —
    /// what an event's key numbers it among.</summary>
    internal IEnumerable<global::app.@event.@this> Of(global::app.goal.step.action.@this action)
        => Items().Where(@event => Equals(@event.action, action));

    /// <summary>What a snapshot keeps of this app's events: the moments a snapshot keeps anything of, which write the item
    /// they are on and this app's own events. Read back, each row's events land in the moments this app holds for its
    /// item.</summary>
    [global::app.Snapshot]
    internal global::app.type.item.list.@this list
    {
        get => new(_on.Where(on => on.IsSaved).Cast<global::app.type.item.@this>().ToList(),
            _app.type.list[typeof(global::app.type.item.list.@this)]);
        private set
        {
            var context = _app.actor.list.System.Context;
            var type = _app.type.list[typeof(on.@this)];
            foreach (var row in value.Items(context))
                global::app.View.Snapshot.Make(type, row.Peek(), context);
        }
    }

    /// <summary>The app's events written: each item's moments, as they write themselves — the events this app made.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var shown = _on.Where(on => on.IsSaved).ToList();
        writer.BeginArray(shown.Count);
        foreach (var on in shown) await on.Output(writer);
        writer.EndArray();
    }
}

namespace app.@event.on.list;

/// <summary>
/// An app's events — <c>%!app.event%</c>: the events of each item something bound on while a program ran
/// (<see cref="on.@this"/>, which knows its item), made on the first bind on it. A sub app is born holding what its maker
/// had bound then, each binding taken in as its own (<see cref="binding.inherited.@this"/>), the maker's first; what it
/// binds after is its own, and what its maker binds later never reaches it. The item is held by reference for the app's
/// life, so a binding on an item an app shares fires only in the app that bound it. What a program binds when it is
/// read (an action's clauses) stays on the item. Reading it as it stands takes no lock and makes nothing, so firing an
/// event nothing is bound on costs one read.
/// </summary>
public sealed class @this : global::app.type.item.@this
{
    private readonly global::app.@this _app;
    private readonly object _gate = new();
    private on.@this[] _on = [];

    /// <summary>The events of <paramref name="app"/>, born holding its maker's bindings (<paramref name="maker"/>), each
    /// taken in as its own.</summary>
    internal @this(global::app.@this app, @this? maker) : base(app.type.list[typeof(@this)])
    {
        _app = app;
        // each item's events inherit the maker's, each level its own part
        foreach (var events in maker?._on ?? []) Add(events.item!).Inherit(events, app);
    }

    /// <summary>The event in play for the asker — <c>%!app.event.current%</c>, which <c>%!event%</c> reads: the running
    /// bound call's Data whole, its value the event, its properties what it fired for (<c>!item</c>) and the result so far
    /// (<c>!result</c>). Unset where no bound call is running.</summary>
    [global::app.LlmBuilder]
    public global::app.data.@this current(global::app.actor.context.@this context)
        => context.call.Event ?? context.NotFound("current");

    /// <summary>The events of <paramref name="item"/> this app bound on; null when it bound nothing on it. Makes
    /// nothing.</summary>
    internal on.@this? this[global::app.type.item.@this item]
    {
        get
        {
            foreach (var events in _on)
                if (ReferenceEquals(events.item, item)) return events;
            return null;
        }
    }

    /// <summary>The events of <paramref name="item"/> this app binds on — added the first time, with the events its type
    /// declares. A binding is added to one of them (<c>app.event.Add(item).Add("start").before.Add(…)</c>).</summary>
    internal on.@this Add(global::app.type.item.@this item)
    {
        if (this[item] is { } events) return events;
        lock (_gate)
        {
            if (this[item] is { } made) return made;
            var added = new on.@this(item);
            _on = [.. _on, added];
            return added;
        }
    }

    /// <summary>What a snapshot keeps of this app's events: the events a snapshot keeps anything of, which write the item
    /// they are on and this app's own bindings. Read back, each row's bindings land in the events this app holds for its
    /// item.</summary>
    [global::app.Snapshot]
    internal global::app.type.item.list.@this list
    {
        get => new(_on.Where(events => events.IsSaved).Cast<global::app.type.item.@this>().ToList(),
            _app.type.list[typeof(global::app.type.item.list.@this)]);
        private set
        {
            var context = _app.actor.list.System.Context;
            var type = _app.type.list[typeof(on.@this)];
            foreach (var row in value.Items(context))
                global::app.View.Snapshot.Make(type, row.Peek(), context);
        }
    }

    /// <summary>The app's events written: each item's, as it writes itself — the bindings this app made.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        var shown = _on.Where(events => events.IsSaved).ToList();
        writer.BeginArray(shown.Count);
        foreach (var events in shown) await events.Output(writer);
        writer.EndArray();
    }
}

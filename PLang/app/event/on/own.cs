using System.Collections.Concurrent;

namespace app.@event.on;

/// <summary>
/// An item's own events, given to it by its first binding: each event is made the first time it is asked for,
/// with lists that take bindings.
/// </summary>
public sealed class own : @this
{
    private readonly ConcurrentDictionary<string, global::app.@event.@this> _events = new(StringComparer.OrdinalIgnoreCase);

    private start? _start;
    private load? _load;
    private create? _create;
    private set? _set;
    private remove? _remove;
    private write? _write;
    private read? _read;
    private ask? _ask;

    internal own() { }

    /// <summary>This item's own start — the one its indexer answers under <c>start</c>, held.</summary>
    public override start start => _start ??= (start)this["start"]!;

    /// <summary>This item's own load — the one its indexer answers under <c>load</c>, held.</summary>
    public override load load => _load ??= (load)this["load"]!;

    /// <summary>This item's own create — the one its indexer answers under <c>create</c>, held.</summary>
    public override create create => _create ??= (create)this["create"]!;

    /// <summary>This item's own set — the one its indexer answers under <c>set</c>, held.</summary>
    public override set set => _set ??= (set)this["set"]!;

    /// <summary>This item's own remove — the one its indexer answers under <c>remove</c>, held.</summary>
    public override remove remove => _remove ??= (remove)this["remove"]!;

    /// <summary>This item's own write — the one its indexer answers under <c>write</c>, held.</summary>
    public override write write => _write ??= (write)this["write"]!;

    /// <summary>This item's own read — the one its indexer answers under <c>read</c>, held.</summary>
    public override read read => _read ??= (read)this["read"]!;

    /// <summary>This item's own ask — the one its indexer answers under <c>ask</c>, held.</summary>
    public override ask ask => _ask ??= (ask)this["ask"]!;

    /// <summary>The event named <paramref name="name"/>, this item's own; null when there is no event of that name.</summary>
    public override global::app.@event.@this? this[string name]
        => Made.TryGetValue(name, out var made) ? _events.GetOrAdd(name, _ => made(new binding.list.before(), new binding.list.after())) : null;

    /// <summary>Binds <paramref name="handler"/> on the event named <paramref name="event"/>, <paramref name="when"/>
    /// it runs, for <paramref name="actor"/> in <paramref name="scope"/>; it fires for every item.</summary>
    public binding.@this Bind(string @event, When when,
        System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> handler,
        global::app.actor.@this actor, binding.Scope scope)
        => Bind(@event, when, handler, actor, scope, binding.@this.Always);

    /// <summary>Binds <paramref name="handler"/> on the event named <paramref name="event"/>, <paramref name="when"/>
    /// it runs, for <paramref name="actor"/> in <paramref name="scope"/>, for the items <paramref name="filter"/> takes.</summary>
    public binding.@this Bind(string @event, When when,
        System.Func<global::app.type.item.@this, global::app.data.@this, global::app.actor.context.@this, System.Threading.Tasks.Task<global::app.data.@this>> handler,
        global::app.actor.@this actor, binding.Scope scope,
        System.Func<global::app.type.item.@this, global::app.actor.context.@this, bool> filter)
        => Bind(@event, when, side => new binding.@this(side, handler, actor, scope, filter));

    /// <summary>Binds the binding <paramref name="make"/> makes on the event named <paramref name="event"/>,
    /// <paramref name="when"/> it runs — a kind of binding (a mock) that is its own handler.</summary>
    public T Bind<T>(string @event, When when, System.Func<binding.list.@this, T> make) where T : binding.@this
    {
        var found = this[@event] ?? throw new KeyNotFoundException($"there is no event '{@event}'");
        binding.list.@this side = when == When.before ? found.before : found.after;
        return side.Add(make);
    }
}

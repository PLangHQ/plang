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

    internal own() { }

    /// <summary>This item's own start — the one its indexer answers under <c>start</c>, held.</summary>
    public override start start => _start ??= (start)this["start"]!;

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
    {
        var found = this[@event] ?? throw new KeyNotFoundException($"there is no event '{@event}'");
        binding.list.@this side = when == When.before ? found.before : found.after;
        return side.Add(handler, actor, scope, filter);
    }
}

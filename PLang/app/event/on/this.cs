using System.Collections.Frozen;

namespace app.@event.on;

/// <summary>
/// An object's events, by name — <c>%!app.goal["/start"].on.start%</c>, <c>%!app.type.text.on.create%</c>. An
/// item nothing is bound on has the shared empty one, whose events are shared and closed to bindings; the
/// first binding gives the item its own (<see cref="own"/>).
/// </summary>
public class @this : global::app.type.item.@this
{
    /// <summary>Each event by its name, made with its two lists — the one place an event's class is named.</summary>
    private protected static readonly FrozenDictionary<string, System.Func<binding.list.before, binding.list.after, global::app.@event.@this>> Made =
        new Dictionary<string, System.Func<binding.list.before, binding.list.after, global::app.@event.@this>>(StringComparer.OrdinalIgnoreCase)
        {
            ["start"] = (before, after) => new start(before, after),
            ["load"] = (before, after) => new load(before, after),
            ["create"] = (before, after) => new create(before, after),
            ["set"] = (before, after) => new set(before, after),
            ["remove"] = (before, after) => new remove(before, after),
            ["write"] = (before, after) => new write(before, after),
            ["read"] = (before, after) => new read(before, after),
            ["ask"] = (before, after) => new ask(before, after),
            ["error"] = (before, after) => new error(before, after),
            ["hit"] = (before, after) => new hit(before, after),
            ["miss"] = (before, after) => new miss(before, after),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // The shared empty events: one of each, closed to bindings.
    private static readonly FrozenDictionary<string, global::app.@event.@this> None =
        Made.ToFrozenDictionary(made => made.Key, made => made.Value(binding.list.before.None, binding.list.after.None), StringComparer.OrdinalIgnoreCase);

    /// <summary>The events of an item nothing is bound on — one for the whole app, never changed: every event
    /// it answers is shared and closed to bindings.</summary>
    public static readonly @this Empty = new();

    private protected @this() { }

    /// <summary>The event named <paramref name="name"/>; null when there is no event of that name.</summary>
    public virtual global::app.@event.@this? this[string name] => None.GetValueOrDefault(name);

    /// <summary>One step down: the event by its name (<c>.start</c>).</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => new(this[key] is { } found
            ? new global::app.data.@this(key, found, parent: parent)
            : parent.Context.NotFound(key));
}

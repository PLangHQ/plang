using System.Collections.Frozen;

namespace app.@event.on;

/// <summary>
/// An object's events, by name — <c>%!app.goal["/start"].on.start%</c>, <c>%!app.type.text.on.create%</c>: the core's,
/// and the ones the item's plang type declares (<c>[Events("closed")]</c>, a window's). An item nothing is bound on has
/// its class's shared empty one, whose events are shared and closed to bindings; the first binding gives the item its
/// own (<see cref="own"/>).
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
        Made.ToFrozenDictionary(entry => entry.Key, entry => entry.Value(binding.list.before.None, binding.list.after.None), StringComparer.OrdinalIgnoreCase);

    /// <summary>The events of an item nothing is bound on and whose type declares none — one for the whole app,
    /// never changed: every event it answers is shared and closed to bindings.</summary>
    public static readonly @this Empty = new();

    // Each class's shared empty events, its type's declared ones among them — a fact of the class, made once.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<System.Type, @this> Emptied = new();

    /// <summary>The events of an item of <paramref name="class"/> nothing is bound on: <see cref="Empty"/>, or, when its
    /// plang type declares events (<c>[Events(…)]</c>), its class's own shared empty ones, which answer those too.</summary>
    internal static @this Of(System.Type @class)
        => Emptied.GetOrAdd(@class, static type =>
            System.Reflection.CustomAttributeExtensions.GetCustomAttribute<global::app.Attributes.EventsAttribute>(type, inherit: false) is { } events
                ? new @this(events.Names)
                : Empty);

    // the events this item's type declares besides the core's
    private readonly FrozenSet<string> _declared;

    // a declared event of the shared empty events, each made once, closed to bindings
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::app.@event.@this> _closed = new(StringComparer.OrdinalIgnoreCase);

    private protected @this() : this([]) { }

    private protected @this(IEnumerable<string> declared) => _declared = declared.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The events the item's type declares besides the core's (a window's <c>closed</c>) — what its own events
    /// are made with when something is first bound on it.</summary>
    internal IReadOnlyCollection<string> Declared => _declared;

    /// <summary>Whether <paramref name="name"/> is one of the events the item's type declares.</summary>
    private protected bool IsDeclared(string name) => _declared.Contains(name);

    // The shared empty start and load, typed.
    private static readonly start NoStart = (start)None["start"];
    private static readonly load NoLoad = (load)None["load"];
    private static readonly create NoCreate = (create)None["create"];
    private static readonly set NoSet = (set)None["set"];
    private static readonly remove NoRemove = (remove)None["remove"];
    private static readonly write NoWrite = (write)None["write"];
    private static readonly read NoRead = (read)None["read"];
    private static readonly ask NoAsk = (ask)None["ask"];
    private static readonly error NoError = (error)None["error"];

    /// <summary>The event named <paramref name="name"/>: one of the core's, or one the item's type declares; null when
    /// there is no event of that name.</summary>
    public virtual global::app.@event.@this? this[string name]
        => None.GetValueOrDefault(name)
           ?? (IsDeclared(name) ? _closed.GetOrAdd(name, n => new declared(n, binding.list.before.None, binding.list.after.None)) : null);

    /// <summary>The names of the events an item has: the core's (<c>ask</c>, <c>create</c>, … <c>write</c>), every item
    /// the same ones, and those its type declares; before or after one is a binding's <c>When</c>, never part of its name.</summary>
    public IEnumerable<string> Names => Made.Keys.Concat(_declared).Order(StringComparer.Ordinal);

    /// <summary>The start — what runs before and after the item starts.</summary>
    public virtual start start => NoStart;

    /// <summary>The load — what runs before and after the item is loaded.</summary>
    public virtual load load => NoLoad;

    /// <summary>The create — what runs before and after a value of the item (a type) is born.</summary>
    public virtual create create => NoCreate;

    /// <summary>The error — what answers when the item fails.</summary>
    public virtual error error => NoError;

    /// <summary>The set — what runs before and after the item is set.</summary>
    public virtual set set => NoSet;

    /// <summary>The remove — what runs before and after the item is removed.</summary>
    public virtual remove remove => NoRemove;

    /// <summary>The write — what runs before and after the item writes.</summary>
    public virtual write write => NoWrite;

    /// <summary>The read — what runs before and after the item reads.</summary>
    public virtual read read => NoRead;

    /// <summary>The ask — what runs before and after the item asks.</summary>
    public virtual ask ask => NoAsk;

    /// <summary>The event named <paramref name="name"/> as a binding made it here; null when none did. Events no
    /// binding gave an item are the shared empty ones, closed to bindings: they hold none.</summary>
    internal virtual global::app.@event.@this? Bound(string name) => null;

    /// <summary>The refusal of <paramref name="name"/>, which is no event: it says the events this item has (the core's
    /// and its type's), and that before or after one is a binding's When — so the name never binds.</summary>
    public global::app.error.@this NoEvent(string name)
        => new global::app.error.ActionError(
            $"'{name}' names no event — this item's events are {string.Join(", ", Names)}; before or after one is When " +
            "(Event=%!app.type.step.on.start%, When=before)", "EventNotFound", 404);

    /// <summary>One step down: the event by its name (<c>.start</c>); a name that is no event is refused
    /// (<see cref="NoEvent"/>).</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => new(this[key] is { } @event
            ? new global::app.data.@this(key, @event, parent: parent)
            : parent.Context.Error(NoEvent(key)));
}

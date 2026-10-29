using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace app.@event.on;

/// <summary>
/// An item's moments, for one actor — <c>%!app.goal["/start"].on%</c>, <c>%!app.type.text.on.create%</c>: what runs before
/// it starts and after (<see cref="before"/>, <see cref="after"/>), and each named moment — the core's (<c>load</c>,
/// <c>create</c>, <c>error</c> …) and the ones the item's plang type declares (<c>[Events("closed")]</c>, a window's) —
/// itself an on with its own before and after. <c>start</c> is the on itself. Made by the actor's events on the item's
/// first ask (<see cref="list.@this"/>); a named moment is made the first time an event is added to it, and one no event
/// was added to is the class's shared one (<see cref="empty"/>), closed.
/// </summary>
[global::app.Attributes.PlangType("on")]
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>A moment is an item's, reached by its path, never made from a value: one passes as it is.</summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this on) return on;
        data.Fail(new global::app.error.@this(
            $"%{data.Name}% holds a {(raw as global::app.type.item.@this)?.Type.Name ?? raw?.GetType().Name ?? "null"} — a moment is " +
            "reached by its item's path (%!app.type.goal.on.start%), never made from a value", "NotAnEvent", 400));
        return null;
    }

    /// <summary>The core's moments, every item the same ones.</summary>
    private protected static readonly FrozenSet<string> Core = new[]
    {
        "start", "load", "create", "set", "remove", "write", "read", "ask", "error", "hit", "miss",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // the moments this item's type declares besides the core's
    private protected readonly FrozenSet<string> _names;

    // the start a named moment is of; none on the start itself
    private readonly @this? _start;

    // each named moment an event was added to, by its name; made when the first one is (the start's alone)
    private ConcurrentDictionary<string, @this>? _moments;

    // the shared, closed moments of the item's class: what a read answers for a moment nothing was added to
    private readonly empty? _shared;

    private protected @this(@this? start, string name, FrozenSet<string> names, bool open)
    {
        _start = start;
        Name = name;
        _names = names;
        item = start?.item;
        list = start?.list;
        before = open ? new before(this) : before.None;
        after = open ? new after(this) : after.None;
    }

    /// <summary>The moments of <paramref name="item"/>: the core's and the ones its type declares, each taking events —
    /// an actor's list for it when <paramref name="list"/>, the actor's events, loaded it; an action's own clauses
    /// when none did.</summary>
    internal @this(global::app.type.item.@this item, global::app.@event.list.@this? list = null) : this(empty.Of(item.GetType()))
    {
        this.item = item;
        this.list = list;
    }

    private @this(empty shared) : this(null, "start", shared._names, open: true) => _shared = shared;

    /// <summary>The item these are the moments of; none on a class's shared moments (<see cref="empty"/>).</summary>
    internal global::app.type.item.@this? item { get; }

    /// <summary>The actor's events that loaded these moments; none for an action's own clauses.</summary>
    internal global::app.@event.list.@this? list { get; }

    /// <summary>The moment's name: <c>start</c> for the on itself, else its own (<c>load</c>, <c>error</c> …).</summary>
    public string Name { get; }

    /// <summary>What runs before the moment.</summary>
    public before before { get; }

    /// <summary>What runs after the moment.</summary>
    public after after { get; }

    /// <summary>The side that runs <paramref name="when"/> — what an event is added to.</summary>
    internal side this[When when] => when == When.before ? before : after;

    // Whether name is the start's — the on itself.
    private protected static bool IsStart(string name) => string.Equals(name, "start", StringComparison.OrdinalIgnoreCase);

    /// <summary>The moment named <paramref name="name"/> as it stands: <c>start</c> the on itself, else its own if an event
    /// was added to it, else the class's shared one, closed, with empty sides. Makes nothing: a moment's run and a
    /// navigation (<c>%x.on.load%</c>) read here. Null when there is no moment of that name.</summary>
    public virtual @this? this[string name]
        => IsStart(name) ? start : start._moments?.GetValueOrDefault(name) ?? start._shared![name];

    /// <summary>The moment named <paramref name="name"/> an event is added to: made with sides that take events the first
    /// time. A name that is no moment is refused (<see cref="NoEvent"/>), thrown: no event is added for it.</summary>
    internal @this Add(string name) => this[name] is null ? throw new global::app.error.AppException(NoEvent(name)) : Made(name);

    // The moment named name, a moment's name: the start itself, else made the first time with sides that take events.
    private protected @this Made(string name)
    {
        if (IsStart(name)) return start;
        return (start._moments ?? System.Threading.LazyInitializer.EnsureInitialized(ref start._moments,
            static () => new(StringComparer.OrdinalIgnoreCase))).GetOrAdd(name, static (named, moments) => moments.Moment(named), start);
    }

    // The start and each named moment made.
    private IEnumerable<@this> Moments => _moments is null ? [this] : [this, .. _moments.Values];

    /// <summary>The moment named <paramref name="name"/> of this start, made with sides that take events.</summary>
    private protected virtual @this Moment(string name) => new(this, name, _names, open: true);

    /// <summary>The names of the moments an item has: the core's (<c>ask</c>, <c>create</c>, … <c>write</c>), every item
    /// the same ones, and those its type declares; before or after one is an event's <c>When</c>, never part of its name.</summary>
    public IEnumerable<string> Names => Core.Concat(_names).Order(StringComparer.Ordinal);

    /// <summary>The start — the on itself, which a named moment is of: what runs before and after the item starts.</summary>
    public @this start => _start ?? this;

    /// <summary>The load — what runs before and after the item is loaded.</summary>
    public @this load => this["load"]!;

    /// <summary>The create — what runs before and after a value of the item (a type) is born.</summary>
    public @this create => this["create"]!;

    /// <summary>The error — what answers when the item fails.</summary>
    public @this error => this["error"]!;

    /// <summary>The set — what runs before and after the item is set.</summary>
    public @this set => this["set"]!;

    /// <summary>The remove — what runs before and after the item is removed.</summary>
    public @this remove => this["remove"]!;

    /// <summary>The write — what runs before and after the item writes.</summary>
    public @this write => this["write"]!;

    /// <summary>The read — what runs before and after the item reads.</summary>
    public @this read => this["read"]!;

    /// <summary>The ask — what runs before and after the item asks.</summary>
    public @this ask => this["ask"]!;

    /// <summary>Whether anything is on this moment, before it or after it.</summary>
    internal bool IsBound => before.Count > 0 || after.Count > 0;

    /// <summary>The refusal of <paramref name="name"/>, which is no moment: it says the moments this item has (the core's
    /// and its type's), and that before or after one is an event's When — so the name never binds.</summary>
    public global::app.error.@this NoEvent(string name)
        => new global::app.error.ActionError(
            $"'{name}' names no event — this item's events are {string.Join(", ", Names)}; before or after one is When " +
            "(Event=%!app.type.step.on.start%, When=before)", "EventNotFound", 404);

    /// <summary>One step down: the moment by its name (<c>.load</c>), made if it isn't yet, so a moment reached knows its
    /// item; a name that is no moment is refused (<see cref="NoEvent"/>).</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => new(this[key] is null
            ? parent.Context.Error(NoEvent(key))
            : new global::app.data.@this(key, Made(key), parent: parent));

    /// <summary>Takes in <paramref name="event"/> on its own moment and side: it runs there, after the ones already
    /// there.</summary>
    internal void Add(global::app.@event.@this @event) => Add(@event.Name)[@event.When].Add(@event);

    /// <summary>Takes <paramref name="event"/> off each of these moments' sides it is on.</summary>
    internal void Remove(global::app.@event.@this @event)
    {
        foreach (var moment in Moments)
        {
            moment.before.Remove(@event);
            moment.after.Remove(@event);
        }
    }

    /// <summary>Whether no moment holds an event.</summary>
    internal bool IsEmpty => !Moments.Any(moment => moment.IsBound);

    // ---- what a snapshot keeps ----

    // The events on side a snapshot keeps here: the ones a program bound on this item (an event a type's moment joined
    // here is written where it was bound).
    private List<global::app.@event.@this> Saved(side side)
        => side.Where(@event => @event.IsSaved && Equals(@event.Item, item)).ToList();

    /// <summary>Whether a snapshot keeps anything of these moments: an event a program bound on this item.</summary>
    internal bool IsSaved => Moments.Any(moment => moment.Saved(moment.before).Count > 0 || moment.Saved(moment.after).Count > 0);

    /// <summary>These moments written for a snapshot: the item they are the moments of, as what it is, then each moment
    /// holding an event a program bound on it, by its name, with its sides — <c>{item, start: {before: [event]}}</c>. Here
    /// is where the events live, so each is written whole, never by its key.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("item");
        await new global::app.data.@this("", item, context: writer.Context).Output(writer);
        foreach (var moment in Moments)
        {
            var sides = new (string Name, List<global::app.@event.@this> Saved)[]
                { ("before", moment.Saved(moment.before)), ("after", moment.Saved(moment.after)) };
            if (sides.All(side => side.Saved.Count == 0)) continue;
            writer.Name(moment.Name);
            writer.BeginObject();
            foreach (var (name, saved) in sides)
            {
                if (saved.Count == 0) continue;
                writer.Name(name);
                writer.BeginArray(saved.Count);
                foreach (var @event in saved) await writer.Write(@event);
                writer.EndArray();
            }
            writer.EndObject();
        }
        writer.EndObject();
    }
}

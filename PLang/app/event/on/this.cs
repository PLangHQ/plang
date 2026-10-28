using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace app.@event.on;

/// <summary>
/// An object's events, by name — <c>%!app.goal["/start"].on.start%</c>, <c>%!app.type.text.on.create%</c>: the core's,
/// and the ones the item's plang type declares (<c>[Events("closed")]</c>, a window's). Each event is made the first time
/// it is asked for, with lists that take bindings. An item nothing is bound on has its class's shared events
/// (<see cref="empty"/>), closed to bindings.
/// </summary>
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>Each event by its name, made with its two lists — the one place an event's class is named.</summary>
    private protected static readonly FrozenDictionary<string, System.Func<@this, binding.list.before, binding.list.after, global::app.@event.@this>> Made =
        new Dictionary<string, System.Func<@this, binding.list.before, binding.list.after, global::app.@event.@this>>(StringComparer.OrdinalIgnoreCase)
        {
            ["start"] = (events, before, after) => new start(events, before, after),
            ["load"] = (events, before, after) => new load(events, before, after),
            ["create"] = (events, before, after) => new create(events, before, after),
            ["set"] = (events, before, after) => new set(events, before, after),
            ["remove"] = (events, before, after) => new remove(events, before, after),
            ["write"] = (events, before, after) => new write(events, before, after),
            ["read"] = (events, before, after) => new read(events, before, after),
            ["ask"] = (events, before, after) => new ask(events, before, after),
            ["error"] = (events, before, after) => new error(events, before, after),
            ["hit"] = (events, before, after) => new hit(events, before, after),
            ["miss"] = (events, before, after) => new miss(events, before, after),
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    // the events this item's type declares besides the core's
    private readonly FrozenSet<string> _names;

    // each event a write made, by its name; made when the first one is added
    private ConcurrentDictionary<string, global::app.@event.@this>? _events;

    // the shared, closed events of the item's class: what a read answers for an event no write made
    private readonly empty? _shared;

    private protected @this(IEnumerable<string> names) => _names = names.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The events of <paramref name="item"/>: the core's and the ones its type declares, each taking bindings.</summary>
    internal @this(global::app.type.item.@this item) : this(empty.Of(item.GetType()))
        => this.item = item;

    private @this(empty shared) : this(shared.Declared) => _shared = shared;

    /// <summary>The item these are the events of; none on a class's shared events (<see cref="empty"/>).</summary>
    internal global::app.type.item.@this? item { get; }

    /// <summary>The events the item's type declares besides the core's (a window's <c>closed</c>).</summary>
    internal IReadOnlyCollection<string> Declared => _names;

    /// <summary>The event named <paramref name="name"/> as it stands: its own if a binding was added to it, else the
    /// class's shared one, closed, with empty sides. Makes nothing: a firing and a navigation (<c>%x.on.start%</c>) read
    /// here. Null when there is no event of that name.</summary>
    public virtual global::app.@event.@this? this[string name]
        => _events?.GetValueOrDefault(name) ?? _shared![name];

    /// <summary>The event named <paramref name="name"/> a binding is added to: made with lists that take bindings the
    /// first time. A name that is no event is refused (<see cref="NoEvent"/>), thrown: no binding is added for it.</summary>
    internal global::app.@event.@this Add(string name)
        => Made.ContainsKey(name) || _names.Contains(name)
            ? (_events ?? System.Threading.LazyInitializer.EnsureInitialized(ref _events,
                static () => new(StringComparer.OrdinalIgnoreCase))).GetOrAdd(name, static (named, events) => events.Event(named), this)
            : throw new global::app.error.AppException(NoEvent(name));

    /// <summary>The event named <paramref name="name"/>, made with its two lists: lists that take bindings.</summary>
    private protected virtual global::app.@event.@this Event(string name)
        => Made.TryGetValue(name, out var made) ? made(this, new binding.list.before(), new binding.list.after())
            : new declared(this, name, new binding.list.before(), new binding.list.after());

    /// <summary>Takes in <paramref name="maker"/>'s: each event the maker made, this item's event of that name inheriting
    /// it — what a sub app (<paramref name="app"/>) is born holding.</summary>
    internal void Inherit(@this maker, global::app.@this app)
    {
        foreach (var (name, @event) in maker._events ?? []) Add(name)!.Inherit(@event, app);
    }

    /// <summary>The names of the events an item has: the core's (<c>ask</c>, <c>create</c>, … <c>write</c>), every item
    /// the same ones, and those its type declares; before or after one is a binding's <c>When</c>, never part of its name.</summary>
    public IEnumerable<string> Names => Made.Keys.Concat(_names).Order(StringComparer.Ordinal);

    /// <summary>The start — what runs before and after the item starts.</summary>
    public start start => (start)this["start"]!;

    /// <summary>The load — what runs before and after the item is loaded.</summary>
    public load load => (load)this["load"]!;

    /// <summary>The create — what runs before and after a value of the item (a type) is born.</summary>
    public create create => (create)this["create"]!;

    /// <summary>The error — what answers when the item fails.</summary>
    public error error => (error)this["error"]!;

    /// <summary>The set — what runs before and after the item is set.</summary>
    public set set => (set)this["set"]!;

    /// <summary>The remove — what runs before and after the item is removed.</summary>
    public remove remove => (remove)this["remove"]!;

    /// <summary>The write — what runs before and after the item writes.</summary>
    public write write => (write)this["write"]!;

    /// <summary>The read — what runs before and after the item reads.</summary>
    public read read => (read)this["read"]!;

    /// <summary>The ask — what runs before and after the item asks.</summary>
    public ask ask => (ask)this["ask"]!;

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

    /// <summary>Whether a snapshot keeps anything of these events: an event that holds a binding a program made.</summary>
    internal bool IsSaved => _events?.Values.Any(@event => @event.IsSaved) == true;

    /// <summary>These events written: the item they are the events of, as what it is, then each event a snapshot keeps
    /// anything of, by its name — <c>{item, start: {before: [binding]}}</c>. Each event writes its own sides: here is where
    /// it lives, so it is written whole, never by its key.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("item");
        await new global::app.data.@this("", item, context: writer.Context).Output(writer);
        foreach (var (name, @event) in _events ?? [])
        {
            if (!@event.IsSaved) continue;
            writer.Name(name);
            await writer.Write(@event);
        }
        writer.EndObject();
    }

    /// <summary>
    /// The events a snapshot kept, read back where they are held: the item they are the events of, read as what it says
    /// it is, then each event reading its own sides onto the events the reading app holds for that item
    /// (<c>context.App.event.Add(item)</c>) — what it holds already, taken in from its maker at birth, keeps its bindings
    /// beside them. A row that doesn't read is the answer, as any read's is.
    /// </summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this events) return events;
        var context = data.Context;
        if (raw is not global::app.type.item.dict.@this record || record.Get("item", context) is not { } kept
            || global::app.View.Snapshot.Make(kept.Type ?? context.App.type.list[typeof(global::app.type.item.@this)], kept.Peek(), context)
                is not { } item)
        {
            data.Fail(new global::app.error.@this("a saved on's row names no item it is the events of", "SnapshotRowUnread", 400));
            return null;
        }

        events = context.App.@event.Add(item);
        foreach (var row in record.Rows(context))
        {
            if (string.Equals(row.Name, "item", StringComparison.OrdinalIgnoreCase)) continue;
            // a name that is no event is the read's answer, before anything is added for it
            if (events[row.Name] == null) { data.Fail(events.NoEvent(row.Name)); return null; }
            if (row.Peek() is not global::app.type.item.dict.@this sides)
            {
                data.Fail(new global::app.error.@this($"a saved on's '{row.Name}' holds no sides", "SnapshotRowUnread", 400));
                return null;
            }
            events.Add(row.Name).Read(sides, context);
        }
        return events;
    }
}

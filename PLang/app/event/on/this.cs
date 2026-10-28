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

    // the events this item's type declares besides the core's
    private readonly FrozenSet<string> _declared;

    // each event made so far, by its name; made when the first one is asked for
    private ConcurrentDictionary<string, global::app.@event.@this>? _events;

    private start? _start;
    private load? _load;
    private create? _create;
    private set? _set;
    private remove? _remove;
    private write? _write;
    private read? _read;
    private ask? _ask;
    private error? _error;

    private protected @this(IEnumerable<string> declared) => _declared = declared.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>The events of <paramref name="item"/>: the core's and the ones its type declares, each taking bindings.</summary>
    internal @this(global::app.type.item.@this item) : this(empty.Of(item.GetType()).Declared) => this.item = item;

    /// <summary>The item these are the events of; none on a class's shared events (<see cref="empty"/>).</summary>
    internal global::app.type.item.@this? item { get; }

    /// <summary>The events the item's type declares besides the core's (a window's <c>closed</c>).</summary>
    internal IReadOnlyCollection<string> Declared => _declared;

    /// <summary>The event named <paramref name="name"/>: one of the core's, or one the item's type declares; null when
    /// there is no event of that name.</summary>
    public global::app.@event.@this? this[string name]
        => Made.ContainsKey(name) || _declared.Contains(name)
            ? (_events ?? System.Threading.LazyInitializer.EnsureInitialized(ref _events,
                () => new(StringComparer.OrdinalIgnoreCase))).GetOrAdd(name, Event)
            : null;

    /// <summary>The event named <paramref name="name"/>, made with its two lists: lists that take bindings.</summary>
    private protected virtual global::app.@event.@this Event(string name)
        => Made.TryGetValue(name, out var made) ? made(new binding.list.before(), new binding.list.after())
            : new declared(name, new binding.list.before(), new binding.list.after());

    /// <summary>The names of the events an item has: the core's (<c>ask</c>, <c>create</c>, … <c>write</c>), every item
    /// the same ones, and those its type declares; before or after one is a binding's <c>When</c>, never part of its name.</summary>
    public IEnumerable<string> Names => Made.Keys.Concat(_declared).Order(StringComparer.Ordinal);

    /// <summary>The start — what runs before and after the item starts.</summary>
    public start start => _start ??= (start)this["start"]!;

    /// <summary>The load — what runs before and after the item is loaded.</summary>
    public load load => _load ??= (load)this["load"]!;

    /// <summary>The create — what runs before and after a value of the item (a type) is born.</summary>
    public create create => _create ??= (create)this["create"]!;

    /// <summary>The error — what answers when the item fails.</summary>
    public error error => _error ??= (error)this["error"]!;

    /// <summary>The set — what runs before and after the item is set.</summary>
    public set set => _set ??= (set)this["set"]!;

    /// <summary>The remove — what runs before and after the item is removed.</summary>
    public remove remove => _remove ??= (remove)this["remove"]!;

    /// <summary>The write — what runs before and after the item writes.</summary>
    public write write => _write ??= (write)this["write"]!;

    /// <summary>The read — what runs before and after the item reads.</summary>
    public read read => _read ??= (read)this["read"]!;

    /// <summary>The ask — what runs before and after the item asks.</summary>
    public ask ask => _ask ??= (ask)this["ask"]!;

    /// <summary>The event named <paramref name="name"/> as these events hold it — made by a bind or an ask; null when
    /// nothing asked for it. What a firing asks: it makes nothing.</summary>
    internal virtual global::app.@event.@this? Bound(string name) => _events?.GetValueOrDefault(name);

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

    /// <summary>These events written: the item they are the events of, as what it is, then each event that holds a
    /// binding a snapshot keeps, by its name, its sides by when they run — <c>{item, start: {before: [binding]}}</c>. A
    /// binding a program can't make again from what it wrote (a C# one) is left out: its maker binds it again.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("item");
        await new global::app.data.@this("", item, context: writer.Context).Output(writer);
        foreach (var (name, @event) in _events ?? [])
        {
            var sides = System.Enum.GetValues<When>()
                .Select(when => (when, saved: @event[when].Where(binding => binding.IsSaved).ToList()))
                .Where(side => side.saved.Count > 0).ToList();
            if (sides.Count == 0) continue;
            writer.Name(name);
            writer.BeginObject();
            foreach (var (when, saved) in sides)
            {
                writer.Name(when.ToString());
                writer.BeginArray(saved.Count);
                foreach (var binding in saved) await binding.Output(writer);
                writer.EndArray();
            }
            writer.EndObject();
        }
        writer.EndObject();
    }

    /// <summary>
    /// Events read back from what they wrote (<see cref="Write"/>): the item they are the events of, read as what it says
    /// it is, then each event's bindings, each made from its row on the side it runs on. A row that doesn't read is the
    /// answer, as any read's is.
    /// </summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this events) return events;
        var context = data.Context;
        if (raw is not global::app.type.item.dict.@this record || record.Get("item", context) is not { } kept
            || global::app.View.Snapshot.Make(kept.Type ?? context.App.type.list[typeof(global::app.type.item.@this)], kept.Peek(), context)
                is not { } item)
            return Declined(data, "a saved on's row names no item it is the events of");

        events = new @this(item);
        foreach (var row in record.Rows(context))
        {
            if (string.Equals(row.Name, "item", StringComparison.OrdinalIgnoreCase)) continue;
            if (events[row.Name] is not { } @event) { data.Fail(events.NoEvent(row.Name)); return null; }
            if (row.Peek() is not global::app.type.item.dict.@this sides)
                return Declined(data, $"a saved on's '{row.Name}' holds no sides");
            foreach (var side in sides.Rows(context))
            {
                if (!System.Enum.TryParse<When>(side.Name, ignoreCase: true, out var when)
                    || side.Peek() is not global::app.type.item.list.@this bindings)
                    return Declined(data, $"a saved on's '{row.Name}' holds '{side.Name}', which is no side of an event");
                foreach (var binding in bindings.Items(context))
                {
                    if (binding.Peek() is not global::app.type.item.dict.@this saved)
                        return Declined(data, $"a saved binding on '{row.Name}' is no record");
                    @event[when].Add(list => new binding.action.@this(list, @event, saved, context));
                }
            }
        }
        return events;
    }

    // a saved on's row that doesn't read: the read's answer
    private static @this? Declined(global::app.data.@this data, string message)
    {
        data.Fail(new global::app.error.@this(message, "SnapshotRowUnread", 400));
        return null;
    }
}

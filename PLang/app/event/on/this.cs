using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace app.@event.on;

/// <summary>
/// An item's moments — <c>%!app.goal["/start"].on%</c>, <c>%!app.type.text.on.create%</c>: what runs before it starts and
/// after (<see cref="before"/>, <see cref="after"/>), and each named moment — the core's (<c>load</c>, <c>create</c>,
/// <c>error</c> …) and the ones the item's plang type declares (<c>[Events("closed")]</c>, a window's) — itself an on with
/// its own before and after. <c>start</c> is the on itself. A named moment is made the first time an event is added to it.
/// An item nothing is bound on has its class's shared moments (<see cref="empty"/>), closed to events.
/// </summary>
[global::app.Attributes.PlangType("on")]
public class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The core's moments, every item the same ones.</summary>
    private protected static readonly FrozenSet<string> Core = new[]
    {
        "start", "load", "create", "set", "remove", "write", "read", "ask", "error", "hit", "miss",
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    // the moments this item's type declares besides the core's
    private readonly FrozenSet<string> _names;

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
        before = open ? new before(this) : before.None;
        after = open ? new after(this) : after.None;
    }

    /// <summary>The moments of <paramref name="item"/>: the core's and the ones its type declares, each taking events.</summary>
    internal @this(global::app.type.item.@this item) : this(empty.Of(item.GetType()))
        => this.item = item;

    private @this(empty shared) : this(null, "start", shared.Declared, open: true) => _shared = shared;

    /// <summary>The item these are the moments of; none on a class's shared moments (<see cref="empty"/>).</summary>
    internal global::app.type.item.@this? item { get; }

    /// <summary>The moment's name: <c>start</c> for the on itself, else its own (<c>load</c>, <c>error</c> …).</summary>
    public string Name { get; }

    /// <summary>What runs before the moment.</summary>
    public before before { get; }

    /// <summary>What runs after the moment.</summary>
    public after after { get; }

    /// <summary>The side that runs <paramref name="when"/> — what an event is added to.</summary>
    internal side this[When when] => when == When.before ? before : after;

    /// <summary>The moments the item's type declares besides the core's (a window's <c>closed</c>).</summary>
    internal FrozenSet<string> Declared => _names;

    /// <summary>The moment named <paramref name="name"/> as it stands: <c>start</c> the on itself, else its own if an event
    /// was added to it, else the class's shared one, closed, with empty sides. Makes nothing: a moment's run and a
    /// navigation (<c>%x.on.load%</c>) read here. Null when there is no moment of that name.</summary>
    public virtual @this? this[string name]
        => string.Equals(name, "start", StringComparison.OrdinalIgnoreCase) ? start
            : start._moments?.GetValueOrDefault(name) ?? start._shared![name];

    /// <summary>The moment named <paramref name="name"/> an event is added to: made with sides that take events the first
    /// time. A name that is no moment is refused (<see cref="NoEvent"/>), thrown: no event is added for it.</summary>
    internal @this Add(string name)
    {
        if (string.Equals(name, "start", StringComparison.OrdinalIgnoreCase)) return start;
        if (!Core.Contains(name) && !_names.Contains(name)) throw new global::app.error.AppException(NoEvent(name));
        return (start._moments ?? System.Threading.LazyInitializer.EnsureInitialized(ref start._moments,
            static () => new(StringComparer.OrdinalIgnoreCase))).GetOrAdd(name, static (named, moments) => moments.Moment(named), start);
    }

    /// <summary>The moment named <paramref name="name"/> of this start, made with sides that take events.</summary>
    private protected virtual @this Moment(string name) => new(this, name, _names, open: true);

    /// <summary>Takes in <paramref name="maker"/>'s: each side of each moment the maker made, this item's moment of that
    /// name inheriting it — what a sub app (<paramref name="app"/>) is born holding.</summary>
    internal void Inherit(@this maker, global::app.@this app)
    {
        before.Inherit(maker.before, app);
        after.Inherit(maker.after, app);
        foreach (var (name, moment) in maker._moments ?? [])
        {
            var made = Add(name);
            made.before.Inherit(moment.before, app);
            made.after.Inherit(moment.after, app);
        }
    }

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

    /// <summary>The refusal of <paramref name="name"/>, which is no moment: it says the moments this item has (the core's
    /// and its type's), and that before or after one is an event's When — so the name never binds.</summary>
    public global::app.error.@this NoEvent(string name)
        => new global::app.error.ActionError(
            $"'{name}' names no event — this item's events are {string.Join(", ", Names)}; before or after one is When " +
            "(Event=%!app.type.step.on.start%, When=before)", "EventNotFound", 404);

    /// <summary>One step down: the moment by its name (<c>.load</c>); a name that is no moment is refused
    /// (<see cref="NoEvent"/>).</summary>
    public override System.Threading.Tasks.ValueTask<global::app.data.@this> Get(global::app.data.@this parent, string key)
        => new(this[key] is { } moment
            ? new global::app.data.@this(key, moment, parent: parent)
            : parent.Context.Error(NoEvent(key)));

    // ---- a moment's run, at each of an item's levels ----

    /// <summary>Whether anything is bound on this moment, before or after it, at any of <paramref name="item"/>'s
    /// levels — so a door whose before is handed something it would have to make asks first.</summary>
    public bool IsBound(global::app.type.item.@this item, global::app.actor.context.@this context)
    {
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            if (IsBoundAt(level, context, When.before) || IsBoundAt(level, context, When.after)) return true;
        return false;
    }

    /// <summary>Whether anything is on this moment's side <paramref name="when"/> at <paramref name="level"/> alone — its
    /// own clauses or what the asker's app bound on it — so a door that would have to make what it hands an event asks
    /// first.</summary>
    internal bool IsBoundAt(global::app.type.item.@this level, global::app.actor.context.@this context, When when)
        => level.on[Name]?[when].Count > 0 || context.App.@event[level]?[Name]?[when].Count > 0;

    /// <summary>What is on this moment's before at <paramref name="level"/> alone — its own clauses, then what the asker's
    /// app bound on it — started on <paramref name="item"/>, something other than the level's own items (a load's .pr,
    /// before there is a goal), on <paramref name="result"/> (a plain success when none is given, made only when something
    /// is there). A failure or a Handled answer stops it and is the answer; null when nothing is there.</summary>
    internal async System.Threading.Tasks.ValueTask<global::app.data.@this?> BeforeAt(global::app.type.item.@this level,
        global::app.type.item.@this item, global::app.actor.context.@this context, global::app.data.@this? result)
    {
        global::app.data.@this? answer = null;
        if (level.on[Name]?.before is { Count: > 0 } clauses)
        {
            answer = await clauses.Start(item, result ?? context.Ok(), context);
            if (!answer.Success || answer.Handled) return answer;
        }
        if (context.App.@event[level]?[Name]?.before is { Count: > 0 } bound)
            answer = await bound.Start(item, answer ?? result ?? context.Ok(), context);
        return answer;
    }

    // What is on this moment's after at a level, on the result as it stands: what the asker's app bound on it, then the
    // level's own clauses.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> AfterAt(global::app.type.item.@this level,
        global::app.type.item.@this item, global::app.actor.context.@this context, global::app.data.@this result)
    {
        if (context.App.@event[level]?[Name]?.after is { Count: > 0 } bound) result = await bound.Start(item, result, context);
        if (level.on[Name]?.after is { Count: > 0 } clauses) result = await clauses.Start(item, result, context);
        return result;
    }

    /// <summary>
    /// Starts what is bound before <paramref name="item"/>'s moment at each of its levels, outermost first
    /// (<see cref="global::app.type.item.@this.Level"/>). A failure or a Handled answer stops it and is the answer;
    /// null when nothing was bound — nothing said, and nothing made. The first event is handed
    /// <paramref name="result"/> (what the moment is about to do, a set's value), else a plain success.
    /// </summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item,
        global::app.actor.context.@this context, global::app.data.@this? result = null)
    {
        // one pass, outermost level first: the levels are gone through until one's sides hold something, and from there
        // on they start — with nothing there, nothing awaited or made
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            if (IsBoundAt(level, context, When.before)) return Before(item, depth, context, result);
        return default;
    }

    // From the first level whose sides hold something, outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item,
        int depth, global::app.actor.context.@this context, global::app.data.@this? result)
    {
        global::app.data.@this? answer = null;
        for (; item.Level(depth, context) is { } level; depth++)
            if (await BeforeAt(level, item, context, answer ?? result) is { } answered)
            {
                answer = answered;
                if (!answer.Success || answer.Handled) return answer;
            }
        return answer;
    }

    /// <summary>
    /// Starts what is bound after <paramref name="item"/>'s moment at each of its levels, innermost first, every one, on
    /// <paramref name="result"/> as it stands; answers the result.
    /// </summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> After(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        // one pass, innermost level first: from the deepest level out until one's sides hold something, and from there on
        // they start — with nothing there, the result as it stands, nothing awaited
        var depth = 0;
        while (item.Level(depth + 1, context) != null) depth++;
        for (; depth >= 0; depth--)
            if (IsBoundAt(item.Level(depth, context)!, context, When.after)) return After(item, result, depth, context);
        return new(result);
    }

    // From the innermost level whose sides hold something, outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> After(global::app.type.item.@this item,
        global::app.data.@this result, int depth, global::app.actor.context.@this context)
    {
        for (; depth >= 0; depth--)
            result = await AfterAt(item.Level(depth, context)!, item, context, result);
        return result;
    }

    /// <summary>
    /// The failure <paramref name="failed"/> handed to what is bound before this moment (an error) on
    /// <paramref name="item"/>, level by level, in the order added: the first event that takes it answers — anything but
    /// the failure handed back (a retry's result, a recovery's, an ignore, or a new failure) — and the rest never see it.
    /// A failure no event takes stands as it was.
    /// </summary>
    public async System.Threading.Tasks.ValueTask<global::app.data.@this> Catch(global::app.type.item.@this item,
        global::app.data.@this failed, global::app.actor.context.@this context)
    {
        if (!IsBound(item, context)) return failed;
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
        {
            // the level's own clauses, then what the asker's app bound on it
            if (await Catch(level.on[Name]?.before, item, failed, context) is { } answer) return answer;
            if (await Catch(context.App.@event[level]?[Name]?.before, item, failed, context) is { } answered) return answered;
        }
        return failed;
    }

    // The first event on side that takes the failure answers; null when none does.
    private static async System.Threading.Tasks.ValueTask<global::app.data.@this?> Catch(side? side,
        global::app.type.item.@this item, global::app.data.@this failed, global::app.actor.context.@this context)
    {
        foreach (var @event in side ?? (IEnumerable<global::app.@event.@this>)[])
        {
            if (!@event.For(item, context)) continue;
            var answer = await @event.Start(item, failed, context);
            if (!ReferenceEquals(answer, failed)) return answer;
        }
        return null;
    }

    // ---- what a snapshot keeps ----

    /// <summary>Every event on these moments: the start's before then after, then each named moment's.</summary>
    internal IEnumerable<global::app.@event.@this> Items()
        => before.Concat(after).Concat((start._moments?.Values ?? []).SelectMany(moment => moment.before.Concat(moment.after)));

    // whether a snapshot keeps anything of this moment's own sides
    private bool IsSavedHere => before.IsSaved || after.IsSaved;

    /// <summary>Whether a snapshot keeps anything of these moments: a side that holds an event a program made.</summary>
    internal bool IsSaved => IsSavedHere || start._moments?.Values.Any(moment => moment.IsSavedHere) == true;

    /// <summary>These moments written: the item they are the moments of, as what it is, then each moment a snapshot keeps
    /// anything of, by its name, with its sides — <c>{item, start: {before: [event]}}</c>. Here is where the events live,
    /// so each is written whole, never by its key.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        writer.Name("item");
        await new global::app.data.@this("", item, context: writer.Context).Output(writer);
        foreach (var moment in new[] { this }.Concat(_moments?.Values ?? []))
        {
            if (!moment.IsSavedHere) continue;
            writer.Name(moment.Name);
            await moment.Sides(writer);
        }
        writer.EndObject();
    }

    // This moment's sides, each by its name, with the events a snapshot keeps — {before: [event]}; a side that keeps none
    // is left out.
    private async System.Threading.Tasks.ValueTask Sides(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        foreach (var side in new side[] { before, after })
        {
            if (!side.IsSaved) continue;
            var saved = side.Where(@event => @event.IsSaved).ToList();
            writer.Name(side.When.ToString());
            writer.BeginArray(saved.Count);
            foreach (var @event in saved) await writer.Write(@event);
            writer.EndArray();
        }
        writer.EndObject();
    }

    /// <summary>The events a snapshot kept of this moment, read back onto its sides: each row of a side the event it
    /// wrote. Each event the side took in from the maker at birth stands for one saved event of the same action — the
    /// same program's, which runs once — and absorbs at most one; every other saved event is added, so a step that bound
    /// twice comes back twice. A record that isn't this moment's sides is refused, thrown to the read boundary.</summary>
    private void Read(global::app.type.item.dict.@this record, global::app.actor.context.@this context)
    {
        foreach (var row in record.Rows(context))
        {
            side? side = string.Equals(row.Name, "before", StringComparison.OrdinalIgnoreCase) ? before
                : string.Equals(row.Name, "after", StringComparison.OrdinalIgnoreCase) ? after : null;
            if (side == null || row.Peek() is not global::app.type.item.list.@this rows)
                throw new global::app.error.DeclinedException(new global::app.error.@this(
                    $"a saved event '{Name}' holds '{row.Name}', which is no side of its events", "SnapshotRowUnread", 400));
            var inherited = side.OfType<inherited.@this>().ToList();
            foreach (var saved in rows.Items(context))
            {
                var @event = new call.@this(side,
                    saved.Peek() as global::app.type.item.dict.@this ?? throw new global::app.error.DeclinedException(
                        new global::app.error.@this($"a saved event on '{Name}' is no record", "SnapshotRowUnread", 400)), context);
                var taken = inherited.FindIndex(held => Equals(held.action, @event.action));
                if (taken >= 0) { inherited.RemoveAt(taken); continue; }
                side.Add(_ => @event);
            }
        }
    }

    /// <summary>
    /// The moments a snapshot kept, read back where they are held: the item they are the moments of, read as what it says
    /// it is, then each moment reading its own sides onto the moments the reading app holds for that item
    /// (<c>context.App.event.Add(item)</c>) — what it holds already, taken in from its maker at birth, keeps its events
    /// beside them. A row that doesn't read is the answer, as any read's is.
    /// </summary>
    public static @this? Create(object? raw, global::app.type.@this? type, global::app.data.@this data)
    {
        if (raw is @this on) return on;
        var context = data.Context;
        if (raw is not global::app.type.item.dict.@this record || record.Get("item", context) is not { } slot
            || global::app.View.Snapshot.Make(slot.Type ?? context.App.type.list[typeof(global::app.type.item.@this)], slot.Peek(), context)
                is not { } item)
        {
            data.Fail(new global::app.error.@this("a saved on's row names no item it is the moments of", "SnapshotRowUnread", 400));
            return null;
        }

        on = context.App.@event.Add(item);
        foreach (var row in record.Rows(context))
        {
            if (string.Equals(row.Name, "item", StringComparison.OrdinalIgnoreCase)) continue;
            // a name that is no moment is the read's answer, before anything is added for it
            if (on[row.Name] == null) { data.Fail(on.NoEvent(row.Name)); return null; }
            if (row.Peek() is not global::app.type.item.dict.@this sides)
            {
                data.Fail(new global::app.error.@this($"a saved on's '{row.Name}' holds no sides", "SnapshotRowUnread", 400));
                return null;
            }
            on.Add(row.Name).Read(sides, context);
        }
        return on;
    }
}

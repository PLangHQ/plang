namespace app.@event;

/// <summary>
/// One event of one object — a verb's (<c>start</c>, <c>create</c>, …) or an outcome's (<c>error</c>,
/// <c>hit</c>, <c>miss</c>): the bindings started before it and after it. One class per event
/// (<c>app/event/on/&lt;name&gt;.cs</c>); the item an event fires for is handed to its bindings when it starts,
/// never kept (one event serves every item of a type).
/// </summary>
[global::app.Attributes.PlangType("event")]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private protected @this(global::app.@event.on.@this on, string name, binding.list.before before, binding.list.after after)
    {
        this.on = on;
        Name = name;
        this.before = before;
        this.after = after;
    }

    /// <summary>The events this one is of — the item's that made it, or its class's shared ones.</summary>
    internal global::app.@event.on.@this on { get; }

    /// <summary>The event's name — the verb's own (<c>start</c> for <c>Start</c>), or the outcome's.</summary>
    public string Name { get; }

    /// <summary>What finds this event where it is pointed at (a running bound call's <c>%!event%</c>): the item its events
    /// are of, and its name — <c>{item, name}</c>, read back as the event of that name in the app's events for that item.
    /// A class's shared event has no item and no key: it is written whole.</summary>
    internal override System.Threading.Tasks.ValueTask<global::app.type.item.@this?> Key(global::app.actor.context.@this context)
        => new(on.item is { } item
            ? new global::app.type.item.dict.@this(context.App.type.list[typeof(global::app.type.item.dict.@this)])
                .Set(new global::app.data.@this("item", item, context: context)).Set("name", Name)
            : null);

    /// <summary>What is started before it.</summary>
    public binding.list.before before { get; }

    /// <summary>What is started after it.</summary>
    public binding.list.after after { get; }

    /// <summary>The side started <paramref name="when"/> it fires — what a binding is added to.</summary>
    internal binding.list.@this this[When when] => when == When.before ? before : after;

    /// <summary>Takes in <paramref name="maker"/>'s: each side inheriting the maker's side of the same when — what a sub
    /// app (<paramref name="app"/>) is born holding.</summary>
    internal void Inherit(@this maker, global::app.@this app)
    {
        before.Inherit(maker.before, app);
        after.Inherit(maker.after, app);
    }

    /// <summary>Whether a snapshot keeps anything of this event: a binding a program made, on either side.</summary>
    internal bool IsSaved => before.IsSaved || after.IsSaved;

    /// <summary>The event written: each side by its name, with the bindings a snapshot keeps — <c>{before: [binding]}</c>;
    /// a side that keeps none is left out.</summary>
    protected internal override async System.Threading.Tasks.ValueTask Write(global::app.type.format.IWriter writer)
    {
        writer.BeginObject();
        foreach (var (name, side) in new (string, binding.list.@this)[] { ("before", before), ("after", after) })
        {
            if (!side.IsSaved) continue;
            var saved = side.Where(binding => binding.IsSaved).ToList();
            writer.Name(name);
            writer.BeginArray(saved.Count);
            foreach (var binding in saved) await binding.Output(writer);
            writer.EndArray();
        }
        writer.EndObject();
    }

    /// <summary>The bindings a snapshot kept of this event, read back onto its sides: each row of a side the binding it
    /// wrote. A record that isn't this event's sides is refused, thrown to the read boundary.</summary>
    internal void Read(global::app.type.item.dict.@this record, global::app.actor.context.@this context)
    {
        foreach (var row in record.Rows(context))
        {
            binding.list.@this? side = string.Equals(row.Name, "before", StringComparison.OrdinalIgnoreCase) ? before
                : string.Equals(row.Name, "after", StringComparison.OrdinalIgnoreCase) ? after : null;
            if (side == null || row.Peek() is not global::app.type.item.list.@this rows)
                throw new global::app.error.DeclinedException(new global::app.error.@this(
                    $"a saved event '{Name}' holds '{row.Name}', which is no side of its bindings", "SnapshotRowUnread", 400));
            foreach (var saved in rows.Items(context))
                side.Add(list => new global::app.@event.binding.action.@this(list, this,
                    saved.Peek() as global::app.type.item.dict.@this ?? throw new global::app.error.DeclinedException(
                        new global::app.error.@this($"a saved binding on '{Name}' is no record", "SnapshotRowUnread", 400)), context));
        }
    }

    /// <summary>Whether anything is bound on this event, before or after it, at any of <paramref name="item"/>'s
    /// levels — so a door whose before is handed something it would have to make asks first.</summary>
    public bool IsBound(global::app.type.item.@this item, global::app.actor.context.@this context)
    {
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            if (IsBoundAt(level, context, When.before) || IsBoundAt(level, context, When.after)) return true;
        return false;
    }

    /// <summary>Whether anything is on this event's side <paramref name="when"/> at <paramref name="level"/> alone — its
    /// own clauses or what the asker's app bound on it — so a door that would have to make what it hands a binding asks
    /// first.</summary>
    internal bool IsBoundAt(global::app.type.item.@this level, global::app.actor.context.@this context, When when)
        => level.on[Name]?[when].Count > 0 || context.App.@event[level]?[Name]?[when].Count > 0;

    /// <summary>What is on this event's before at <paramref name="level"/> alone — its own clauses, then what the asker's
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
        if (context.App.@event[level]?[Name]?.before is { Count: > 0 } bindings)
            answer = await bindings.Start(item, answer ?? result ?? context.Ok(), context);
        return answer;
    }

    // What is on this event's after at a level, on the result as it stands: what the asker's app bound on it, then the
    // level's own clauses.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> AfterAt(global::app.type.item.@this level,
        global::app.type.item.@this item, global::app.actor.context.@this context, global::app.data.@this result)
    {
        if (context.App.@event[level]?[Name]?.after is { Count: > 0 } bindings) result = await bindings.Start(item, result, context);
        if (level.on[Name]?.after is { Count: > 0 } clauses) result = await clauses.Start(item, result, context);
        return result;
    }

    /// <summary>
    /// Starts what is bound before <paramref name="item"/>'s event at each of its levels, outermost first
    /// (<see cref="global::app.type.item.@this.Level"/>). A failure or a Handled answer stops it and is the answer;
    /// null when nothing was bound — nothing said, and nothing made. The first binding is handed
    /// <paramref name="result"/> (what the event is about to do, a set's value), else a plain success.
    /// </summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item,
        global::app.actor.context.@this context, global::app.data.@this? result = null)
    {
        // one pass, outermost level first: the levels are gone through until one's lists hold something, and from there
        // on they start — with nothing there, nothing awaited or made
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            if (IsBoundAt(level, context, When.before)) return Before(item, depth, context, result);
        return default;
    }

    // From the first level whose lists hold something, outward.
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
    /// Starts what is bound after <paramref name="item"/>'s event at each of its levels, innermost first, every
    /// one, on <paramref name="result"/> as it stands; answers the result.
    /// </summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this> After(global::app.type.item.@this item,
        global::app.data.@this result, global::app.actor.context.@this context)
    {
        // one pass, innermost level first: from the deepest level out until one's lists hold something, and from there on
        // they start — with nothing there, the result as it stands, nothing awaited
        var depth = 0;
        while (item.Level(depth + 1, context) != null) depth++;
        for (; depth >= 0; depth--)
            if (IsBoundAt(item.Level(depth, context)!, context, When.after)) return After(item, result, depth, context);
        return new(result);
    }

    // From the innermost level whose lists hold something, outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> After(global::app.type.item.@this item,
        global::app.data.@this result, int depth, global::app.actor.context.@this context)
    {
        for (; depth >= 0; depth--)
            result = await AfterAt(item.Level(depth, context)!, item, context, result);
        return result;
    }

    /// <summary>Every event is of the event type (<c>event</c>), whichever its class. Events are made by the event
    /// door's own table and the empty ones are shared across apps (on's static empties), so no one app's type is
    /// theirs to hold: it is built from the class.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    public override string ToString() => Name;
}

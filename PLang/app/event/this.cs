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
    private protected @this(string name, binding.list.before before, binding.list.after after)
    {
        Name = name;
        this.before = before;
        this.after = after;
    }

    /// <summary>The event's name — the verb's own (<c>start</c> for <c>Start</c>), or the outcome's.</summary>
    public string Name { get; }

    /// <summary>What is started before it.</summary>
    public binding.list.before before { get; }

    /// <summary>What is started after it.</summary>
    public binding.list.after after { get; }

    /// <summary>This event as <paramref name="on"/> holds it — the same event of another level; null at a level whose
    /// type doesn't have it (a declared event, on a level of another type).</summary>
    protected virtual @this? Of(global::app.@event.on.@this on) => on[Name];

    /// <summary>Whether anything is bound on this event, before or after it, at any of <paramref name="item"/>'s
    /// levels — so a door whose before is handed something it would have to make asks first.</summary>
    public bool IsBound(global::app.type.item.@this item, global::app.actor.context.@this context)
        => Bound(item, context, BeforeSide).Outer >= 0 || Bound(item, context, AfterSide).Outer >= 0;

    private static readonly System.Func<@this, binding.list.@this> BeforeSide = e => e.before;
    private static readonly System.Func<@this, binding.list.@this> AfterSide = e => e.after;

    // The outermost and innermost of the item's levels with something bound on one side; -1 when none has.
    private (int Outer, int Inner) Bound(global::app.type.item.@this item, global::app.actor.context.@this context,
        System.Func<@this, binding.list.@this> side)
    {
        int outer = -1, inner = -1;
        var sources = Sources(context);
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            for (var source = 0; source < sources; source++)
                if (At(level, source, context) is { } @event && side(@event).Count > 0)
                {
                    if (outer < 0) outer = depth;
                    inner = depth;
                    break;
                }
        return (outer, inner);
    }

    /// <summary>Whether anything is bound before this event at <paramref name="level"/> alone — on its own events or in
    /// the hold of an actor of the asker's app — so a door that would have to make what it hands a binding asks first.</summary>
    internal bool IsBoundBefore(global::app.type.item.@this level, global::app.actor.context.@this context)
        => IsBoundAt(level, context, BeforeSide);

    /// <summary>Whether anything is bound after this event at <paramref name="level"/> alone.</summary>
    internal bool IsBoundAfter(global::app.type.item.@this level, global::app.actor.context.@this context)
        => IsBoundAt(level, context, AfterSide);

    private bool IsBoundAt(global::app.type.item.@this level, global::app.actor.context.@this context,
        System.Func<@this, binding.list.@this> side)
    {
        var sources = Sources(context);
        for (var source = 0; source < sources; source++)
            if (At(level, source, context) is { } @event && side(@event).Count > 0) return true;
        return false;
    }

    /// <summary>What is bound before this event at <paramref name="level"/> alone — its own, then each actor's hold —
    /// started on <paramref name="item"/>, something other than the level's own items (a load's .pr, before there is a
    /// goal). A failure or a Handled answer stops it and is the answer; null when nothing is bound there.</summary>
    internal async System.Threading.Tasks.ValueTask<global::app.data.@this?> BeforeAt(global::app.type.item.@this level,
        global::app.type.item.@this item, global::app.actor.context.@this context, global::app.data.@this result)
    {
        if (!IsBoundBefore(level, context)) return null;
        var answer = result;
        var sources = Sources(context);
        for (var source = 0; source < sources; source++)
        {
            if (At(level, source, context)?.before is not { Count: > 0 } side) continue;
            answer = await side.Start(item, answer, context);
            if (!answer.Success || answer.Handled) return answer;
        }
        return answer;
    }

    // Where this event is bound at a level: the level's own events (what its program bound when it was read), then the
    // hold of each actor of the asker's app (what was bound while a program ran), in the actor list's order.
    private static int Sources(global::app.actor.context.@this context) => 1 + context.App.actor.list.CountRaw;

    // This event at a level, from one of its sources: the level's own (0), or the one an actor of the asker's app
    // holds for it; null where that source has nothing of it.
    private @this? At(global::app.type.item.@this level, int source, global::app.actor.context.@this context)
        => source == 0 ? Of(level.on)
         : context.App.actor.list[source - 1].Context.on.Of(level)?.Bound(Name);

    /// <summary>
    /// Starts what is bound before <paramref name="item"/>'s event at each of its levels, outermost first
    /// (<see cref="global::app.type.item.@this.Level"/>). A failure or a Handled answer stops it and is the answer;
    /// null when nothing was bound — nothing said, and nothing made. The first binding is handed
    /// <paramref name="result"/> (what the event is about to do, a set's value), else a plain success.
    /// </summary>
    public System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item,
        global::app.actor.context.@this context, global::app.data.@this? result = null)
    {
        // nothing bound at any level: nothing to start, so nothing awaited or made
        var outer = Bound(item, context, BeforeSide).Outer;
        return outer >= 0 ? Before(item, outer, context, result) : default;
    }

    // From the first level with a binding outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item,
        int depth, global::app.actor.context.@this context, global::app.data.@this? result)
    {
        var answer = result ?? context.Ok();
        var sources = Sources(context);
        for (; item.Level(depth, context) is { } level; depth++)
            // the level's own first, then each actor's hold
            for (var source = 0; source < sources; source++)
            {
                if (At(level, source, context)?.before is not { Count: > 0 } side) continue;
                answer = await side.Start(item, answer, context);
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
        // nothing bound at any level: the result as it stands, nothing awaited
        var inner = Bound(item, context, AfterSide).Inner;
        return inner >= 0 ? After(item, result, inner, context) : new(result);
    }

    // From the innermost level with a binding outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> After(global::app.type.item.@this item,
        global::app.data.@this result, int depth, global::app.actor.context.@this context)
    {
        var sources = Sources(context);
        for (; depth >= 0; depth--)
        {
            var level = item.Level(depth, context)!;
            // each actor's hold first, then the level's own: the reverse of before
            for (var source = sources - 1; source >= 0; source--)
                if (At(level, source, context)?.after is { Count: > 0 } side) result = await side.Start(item, result, context);
        }
        return result;
    }

    /// <summary>Every event is of the event type (<c>event</c>), whichever its class. Events are made by the event
    /// door's own table and the empty ones are shared across apps (on's static empties), so no one app's type is
    /// theirs to hold: it is built from the class.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    public override string ToString() => Name;
}

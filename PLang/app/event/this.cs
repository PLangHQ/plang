namespace app.@event;

/// <summary>
/// One event of one object — a verb's (<c>start</c>, <c>create</c>, …) or an outcome's (<c>error</c>,
/// <c>hit</c>, <c>miss</c>): the bindings started before it and after it. One class per event
/// (<c>app/event/on/&lt;name&gt;.cs</c>); the item an event fires for is handed to its bindings when it starts,
/// never kept (one event serves every item of a type).
/// </summary>
public abstract class @this : global::app.type.item.@this
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

    /// <summary>This event as <paramref name="on"/> holds it — the same event of another level.</summary>
    protected virtual @this Of(global::app.@event.on.@this on) => on[Name]!;

    /// <summary>Whether anything is bound on this event, before or after it, at any of <paramref name="item"/>'s
    /// levels — so a door whose before is handed something it would have to make asks first.</summary>
    public bool IsBound(global::app.type.item.@this item, global::app.actor.context.@this context)
    {
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            if (Of(level.on) is { } at && (at.before.Count > 0 || at.after.Count > 0)) return true;
        return false;
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
        // nothing bound at any level: nothing to start, so nothing awaited or made
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            if (Of(level.on).before.Count > 0) return Before(item, depth, context, result);
        return default;
    }

    // From the first level with a binding outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item,
        int depth, global::app.actor.context.@this context, global::app.data.@this? result)
    {
        var answer = result ?? context.Ok();
        for (; item.Level(depth, context) is { } level; depth++)
        {
            var side = Of(level.on).before;
            if (side.Count == 0) continue;
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
        var depth = 0;
        while (item.Level(depth, context) != null) depth++;
        // nothing bound at any level: the result as it stands, nothing awaited
        while (--depth >= 0)
            if (Of(item.Level(depth, context)!.on).after.Count > 0) return After(item, result, depth, context);
        return new(result);
    }

    // From the innermost level with a binding outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this> After(global::app.type.item.@this item,
        global::app.data.@this result, int depth, global::app.actor.context.@this context)
    {
        for (; depth >= 0; depth--)
        {
            var side = Of(item.Level(depth, context)!.on).after;
            if (side.Count > 0) result = await side.Start(item, result, context);
        }
        return result;
    }

    /// <summary>Every event is of the event type (<c>app.event</c>), whichever its class.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    public override string ToString() => Name;
}

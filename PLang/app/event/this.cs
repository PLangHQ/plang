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
        for (var depth = 0; item.Level(depth, context) is { } level; depth++)
            if (IsBoundAt(level, context, side))
            {
                if (outer < 0) outer = depth;
                inner = depth;
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
        foreach (var @event in Out(level, context))
            if (side(@event).Count > 0) return true;
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
        foreach (var @event in Out(level, context))
        {
            if (@event.before is not { Count: > 0 } side) continue;
            answer = await side.Start(item, answer, context);
            if (!answer.Success || answer.Handled) return answer;
        }
        return answer;
    }

    // This event where it is bound at a level, outward: the level's own events (what its program bound when it was
    // read), then each actor's hold in the asker's app (what was bound while a program ran), in the actor list's order.
    private protected Outward Out(global::app.type.item.@this level, global::app.actor.context.@this context)
        => new(this, level, context.App.actor.list);

    // The same places inward: each actor's hold, last actor first, then the level's own events.
    private protected Inward In(global::app.type.item.@this level, global::app.actor.context.@this context)
        => new(this, level, context.App.actor.list);

    // The places of one level, outward, walked without allocating; each answers the event by its name if a binding
    // made it there.
    private protected struct Outward
    {
        private readonly @this _event;
        private readonly global::app.type.item.@this _level;
        private readonly global::app.actor.list.@this _actors;
        private readonly int _count;
        // whether the level's own events were asked, and the next actor whose hold is
        private bool _owned;
        private int _next;

        internal Outward(@this @event, global::app.type.item.@this level, global::app.actor.list.@this actors)
        {
            _event = @event;
            _level = level;
            _actors = actors;
            _count = actors.CountRaw;
            _owned = false;
            _next = 0;
            Current = null!;
        }

        public readonly Outward GetEnumerator() => this;

        public @this Current { get; private set; }

        public bool MoveNext()
        {
            if (!_owned)
            {
                _owned = true;
                if (_level.on.Bound(_event.Name) is { } own) { Current = own; return true; }
            }
            while (_next < _count)
                if (_actors[_next++].Context.on.Of(_level)?.Bound(_event.Name) is { } held) { Current = held; return true; }
            return false;
        }
    }

    // The places of one level, inward, walked without allocating.
    private protected struct Inward
    {
        private readonly @this _event;
        private readonly global::app.type.item.@this _level;
        private readonly global::app.actor.list.@this _actors;
        // the next actor whose hold is asked, and whether the level's own events were
        private int _next;
        private bool _owned;

        internal Inward(@this @event, global::app.type.item.@this level, global::app.actor.list.@this actors)
        {
            _event = @event;
            _level = level;
            _actors = actors;
            _next = actors.CountRaw - 1;
            _owned = false;
            Current = null!;
        }

        public readonly Inward GetEnumerator() => this;

        public @this Current { get; private set; }

        public bool MoveNext()
        {
            while (_next >= 0)
                if (_actors[_next--].Context.on.Of(_level)?.Bound(_event.Name) is { } held) { Current = held; return true; }
            if (_owned) return false;
            _owned = true;
            if (_level.on.Bound(_event.Name) is not { } own) return false;
            Current = own;
            return true;
        }
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
        var outer = Bound(item, context, BeforeSide).Outer;
        return outer >= 0 ? Before(item, outer, context, result) : default;
    }

    // From the first level with a binding outward.
    private async System.Threading.Tasks.ValueTask<global::app.data.@this?> Before(global::app.type.item.@this item,
        int depth, global::app.actor.context.@this context, global::app.data.@this? result)
    {
        var answer = result ?? context.Ok();
        for (; item.Level(depth, context) is { } level; depth++)
            foreach (var @event in Out(level, context))
            {
                if (@event.before is not { Count: > 0 } side) continue;
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
        for (; depth >= 0; depth--)
            foreach (var @event in In(item.Level(depth, context)!, context))
                if (@event.after is { Count: > 0 } side) result = await side.Start(item, result, context);
        return result;
    }

    /// <summary>Every event is of the event type (<c>event</c>), whichever its class. Events are made by the event
    /// door's own table and the empty ones are shared across apps (on's static empties), so no one app's type is
    /// theirs to hold: it is built from the class.</summary>
    protected internal override global::app.type.@this Type => new(typeof(@this));

    public override string ToString() => Name;
}

# event-action — the expected C# (decisions 3–6)

The goal's start path only. Actions, a goal's `load`/`error` by name, IsSystem and the `on.event` path come next. NEW marks a member that doesn't exist; anything else is quoted with its file:line.

## The actor holds its events

```csharp
// actor/this.cs — NEW: the events this actor bound; %!app.event% reads the running actor's
public global::app.@event.list.@this @event { get; }
```

## event/this.cs — what's bound (NEW; replaces binding/this.cs)

```csharp
namespace app.@event;

/// <summary>What a program bound: run before or after the event it names, for the item it names, by the actor that
/// bound it, whose events hold it.</summary>
[global::app.Attributes.PlangType("event")]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    private protected @this(string name, When when, global::app.type.item.@this item, global::app.actor.@this actor)
    {
        Name = name;
        When = when;
        Item = item;
        Actor = actor;
    }

    /// <summary>The event it runs on: start, load, error.</summary>
    public string Name { get; }

    /// <summary>Before the event, or after it.</summary>
    public When When { get; }

    /// <summary>What it is bound on: one goal, or the goal type (each goal).</summary>
    public global::app.type.item.@this Item { get; }

    /// <summary>The actor that bound it.</summary>
    public global::app.actor.@this Actor { get; }

    /// <summary>Whether it runs for <paramref name="item"/>: the one it is bound on, or each item of the type it is bound on.</summary>
    internal virtual bool For(global::app.type.item.@this item)
    {
        if (ReferenceEquals(item, Item)) return true;
        return ReferenceEquals(item.Type, Item);
    }

    /// <summary>Runs it for <paramref name="item"/> on the result as it stands; inside its own call it doesn't run again
    /// (binding/this.cs:66–72).</summary>
    internal async Task<data.@this> Start(global::app.type.item.@this item, data.@this result, actor.context.@this context)
    {
        if (!context.TryEnterEvent(this)) return result;
        try { return await Handle(item, result, context); }
        finally { context.ExitEvent(this); }
    }

    private protected abstract Task<data.@this> Handle(global::app.type.item.@this item, data.@this result, actor.context.@this context);
}
```

## event/call/this.cs — what on.event binds (was binding/action/this.cs)

```csharp
namespace app.@event.call;

/// <summary>What on.event binds: a goal call, run when its event fires. It points to the on.event step that made it.</summary>
public sealed class @this : global::app.@event.@this
{
    internal @this(global::app.goal.step.action.@this action, string name, When when, global::app.type.item.@this item,
        global::app.actor.@this actor) : base(name, when, item, actor)
    {
        this.action = action;
    }

    /// <summary>The on.event step that made it; its Action is the goal call it runs.</summary>
    internal global::app.goal.step.action.@this action { get; }

    // unchanged from binding/action/this.cs:37–73: finds the goal in its own app, runs the call with this as %!event%
    private protected override async Task<data.@this> Handle(global::app.type.item.@this item, data.@this result,
        actor.context.@this context) { … }
}
```

## event/list/this.cs — the actor's events (NEW; replaces event/on/list/)

```csharp
namespace app.@event.list;

/// <summary>An actor's events (%!app.event%): every event it bound, in bind order, and each item's lists, made on the
/// item's first run.</summary>
public sealed class @this : global::app.type.item.list.@this<global::app.@event.@this>
{
    private readonly object _gate = new();

    // each item's lists, made on its first run: none until then
    private readonly System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.@this, on.@this> _on =
        new(System.Collections.Generic.ReferenceEqualityComparer.Instance);

    internal @this(global::app.type.@this type) : base(type) { }

    /// <summary>The item's lists: matched against every event on the first ask, the same lists after that.</summary>
    internal on.@this this[global::app.type.item.@this item]
    {
        get
        {
            if (_on.TryGetValue(item, out var on)) return on;
            lock (_gate)
            {
                if (_on.TryGetValue(item, out on)) return on;
                on = new on.@this(Type);
                foreach (var @event in Items())
                    if (@event.For(item)) on.Add(@event);
                _on[item] = on;
                return on;
            }
        }
    }

    /// <summary>Binds <paramref name="event"/>: it joins the actor's events, then every item's lists already made that it
    /// is for.</summary>
    internal void Add(global::app.@event.@this @event)
    {
        lock (_gate)
        {
            base.Add(@event);
            foreach (var (item, on) in _on)
                if (@event.For(item)) on.Add(@event);
        }
    }
}
```

`Items()` is `list/this.Generic.cs:28`; `base.Add(item)` is `list/this.cs:400`. `on.unbind` is the same walk, leaving each list.

## event/on/ — one item's lists for one actor (NEW body)

```csharp
namespace app.@event.on;

/// <summary>One item's events for one actor: what runs before and after it starts, made on the item's first run.</summary>
public sealed class @this
{
    internal @this(global::app.type.@this type)
    {
        before = new(type);
        after = new(type);
    }

    public before before { get; }

    public after after { get; }

    // the side an event runs on (today's event/this.cs:42)
    internal global::app.type.item.list.@this<global::app.@event.@this> this[When when] => when == When.before ? before : after;

    internal void Add(global::app.@event.@this @event)
    {
        this[@event.When].Add(@event);
    }
}

/// <summary>What runs before an item starts: each event in bind order. A failure or a Handled answer stops it and is the
/// answer.</summary>
public sealed class before : global::app.type.item.list.@this<global::app.@event.@this>
{
    internal before(global::app.type.@this type) : base(type) { }

    public async ValueTask<data.@this> Start(global::app.type.item.@this item, actor.context.@this context)
    {
        var answer = context.Ok();
        foreach (var @event in Items())
        {
            answer = await @event.Start(item, answer, context);
            if (!answer.Success || answer.Handled) return answer;
        }
        return answer;
    }
}

/// <summary>What runs after an item starts: each event in bind order, whatever the others answer; one that fails makes
/// the result its error (binding/list/after.cs:18–28).</summary>
public sealed class after : global::app.type.item.list.@this<global::app.@event.@this>
{
    internal after(global::app.type.@this type) : base(type) { }

    public async ValueTask<data.@this> Start(global::app.type.item.@this item, data.@this result, actor.context.@this context)
    {
        foreach (var @event in Items())
        {
            var answer = await @event.Start(item, result, context);
            if (!answer.Success) result = answer;
        }
        return result;
    }
}
```

## on.event binds (module/on/event.cs:55–80)

```csharp
public async Task<data.@this<global::app.@event.@this>> Start()
{
    // :57–68 unchanged for now: the path reached, the event's name, the item it's on (the path's shape is open)
    …
    return data.@this<global::app.@event.@this>.From(await When.Use(when => Action.Use(_ =>
    {
        var @event = new global::app.@event.call.@this(__action!, named.Name, when.Value, item, Context.Actor!);
        Context.Actor!.@event.Add(@event);
        return Task.FromResult<data.@this>(Context.Ok<global::app.@event.@this>(@event));
    })));
}
```

`Scope` and its `Use` go.

## goal.Start (goal/this.cs:356–381)

```csharp
public virtual async Task<data.@this> Start(actor.context.@this context, IEnumerable<data.@this>? parameters)
{
    // :358–376 unchanged: cancelled, broken, the frame pushed
    …
    var on = context.Actor.@event[this];
    var answer = await on.before.Start(this, context);
    var result = answer is { Success: false } or { Handled: true } ? answer : frame.Leave(await Step.Start(context));
    return await on.after.Start(this, result, context);
}
```

`Enter` (`:397–401`), `Leave` (`:405–414`) and `Elsewhere` (`:117`) go; `Start` becomes virtual.

## The goal frame leaves (call/goal/this.cs:38)

```csharp
// today: => Goal?.Leave(result) ?? result;
internal override global::app.data.@this Leave(global::app.data.@this result)
{
    if (!result.Returned) return result;
    result.ReturnDepth--;
    if (result.ReturnDepth <= 0) result.Returned = false;
    return result;
}
```

## A goal of the parent app (NEW: parent/goal/this.cs)

```csharp
namespace app.parent.goal;

/// <summary>A goal of the app that hosts this one (%!app.parent.goal["Claude"]%): its run goes up the link with the
/// call's parameters, and what comes back is its result. The parent runs its own events.</summary>
public sealed class @this : global::app.goal.@this
{
    private readonly global::app.parent.@this _parent;

    internal @this(global::app.parent.@this parent, string name, global::app.type.list.@this types) : base(types)
    {
        _parent = parent;
        Name = name;
    }

    public override Task<data.@this> Start(actor.context.@this context, IEnumerable<data.@this>? parameters)
    {
        return _parent.Run(this, parameters, context);
    }
}
```

`parent/this.cs:113` makes it: `new global::app.parent.goal.@this(parent, key, data.Context.App.type.list)`. `parent.Run` (`:48`) takes the goal and the call's parameters, instead of a name and the caller's frame (`:55–64` read `context.call.Scope`'s arguments).

## For the coder to check

- `goal.Type` is the same object `%!app.type.goal%` reaches, so "each goal" matches by reference.
- The base list's public `Add(item)` stays a second way in; the event list must not let an event in without joining the made lists.
- Inside its own call an event answered `context.Ok()` (`binding/this.cs:69`); here it answers the result as it stands, so an after doesn't lose it.

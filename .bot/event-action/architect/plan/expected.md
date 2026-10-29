# event-action — the expected C# (decisions 3–13)

What the coder's code should look like, for comparing when a stage reports done. NEW marks a member that doesn't exist; anything else is quoted with its file:line. Names the coder proposes (on.unbind's parameter, `%!x.on%`'s reach) are left as they are today.

## What's bound — `event/this.cs` (NEW; replaces binding/this.cs)

```csharp
namespace app.@event;

/// <summary>What a program bound: run before or after a moment of the item it is bound on, by the actor that bound
/// it, whose events hold it.</summary>
[global::app.Attributes.PlangType("event")]
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    /// <summary>The moment it runs on: start, load, error, create …</summary>
    public string Name { get; }

    /// <summary>Before the moment, or after it.</summary>
    public When When { get; }

    /// <summary>What it is bound on: a type (each goal), one goal, a catalog action, a module, a channel.</summary>
    public global::app.type.item.@this Item { get; }

    /// <summary>The actor that bound it.</summary>
    public global::app.actor.@this Actor { get; }

    /// <summary>Runs it for <paramref name="item"/> on the result as it stands. Each kind has its own.</summary>
    public abstract Task<data.@this> Start(global::app.type.item.@this item, data.@this result, actor.context.@this context);
}
```

Kinds: `event/call/` (on.event's goal call; its `Start` is today's `binding/action/this.cs:37–73`), `event/clause/`, `event/code/` (C#), `event/inherited/`.

## The actor's events — `event/list/this.cs` (NEW; replaces event/on/list/)

```csharp
namespace app.@event.list;

/// <summary>An actor's events (%!app.event%): every event it bound, in bind order, and each item's moments, made on the
/// item's first run.</summary>
public sealed class @this : global::app.type.item.list.@this<global::app.@event.@this>
{
    private readonly object _gate = new();

    // each item's moments for this actor, made on its first run: none until then
    private readonly System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.@this, on.@this> _on = new();

    /// <summary>The item's moments: made on the first ask from every event that applies, the same after that.</summary>
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
                    if (item.Is(@event.Item.Type)) on.Add(@event);   // bound on a type: each item of it (the path rule for system/ goals is the coder's)
                _on[item] = on;
                return on;
            }
        }
    }

    /// <summary>Binds <paramref name="event"/>: it joins the actor's events, then the moments it belongs in — the one
    /// item's it is bound on, or every made item's that Is the type it is bound on.</summary>
    internal void Add(global::app.@event.@this @event)
    {
        lock (_gate)
        {
            base.Add(@event);
            … placement by what the path reached (decision 7): one item → this[item].Add(@event); a type → each made item
              that Is it; a module → each of its actions
        }
    }
}
```

`Items()` is `list/this.Generic.cs:28`; `base.Add` is `list/this.cs:400`; `item.Is(type)` is `item/this.cs:395`. The maps use their keys' own equality (a type by name, `type/this.cs:610`).

## An item's moments — `event/on/this.cs` (NEW body)

```csharp
namespace app.@event.on;

/// <summary>An item's moments for one actor: what runs before and after it starts, and each named moment (load, error,
/// create …), itself an on with its own before and after.</summary>
public sealed class @this
{
    public before before { get; }

    public after after { get; }

    /// <summary>A named moment: load, error, create, set, write … (start is this one).</summary>
    public @this this[string name] { get … }    // made when an event of that name is added
}
```

## Running a side — `event/on/before.cs`, `after.cs`

```csharp
public sealed class before : global::app.type.item.list.@this<global::app.@event.@this>
{
    public async ValueTask<data.@this> Start(global::app.type.item.@this item, actor.context.@this context)
    {
        var answer = context.Ok();
        foreach (var @event in Items())
        {
            using var running = context.Enter(@event);   // NEW: null when it's already running in this flow
            if (running == null) continue;
            answer = await @event.Start(item, answer, context);
            if (!answer.Success || answer.Handled) return answer;
        }
        return answer;
    }
}

public sealed class after : global::app.type.item.list.@this<global::app.@event.@this>
{
    public async ValueTask<data.@this> Start(global::app.type.item.@this item, data.@this result, actor.context.@this context)
    {
        foreach (var @event in Items())
        {
            using var running = context.Enter(@event);
            if (running == null) continue;
            var answer = await @event.Start(item, result, context);
            if (!answer.Success) result = answer;
        }
        return result;
    }
}
```

## goal.Start — `goal/this.cs:356–381`

```csharp
public virtual async Task<data.@this> Start(actor.context.@this context, IEnumerable<data.@this>? parameters)
{
    // :358–376 unchanged: cancelled, broken, the frame pushed
    var on = context.Actor.@event[this];
    var answer = await on.before.Start(this, context);
    var result = answer is { Success: false } or { Handled: true } ? answer : frame.Leave(await Step.Start(context));
    return await on.after.Start(this, result, context);
}
```

`Enter` (`:397`), `Leave` (`:405`) and `Elsewhere` (`:117`) go.

## The goal frame leaves — `call/goal/this.cs:38`

```csharp
internal override global::app.data.@this Leave(global::app.data.@this result)
{
    if (!result.Returned) return result;
    result.ReturnDepth--;
    if (result.ReturnDepth <= 0) result.Returned = false;
    return result;
}
```

## A goal of the parent app — `parent/goal/this.cs` (NEW)

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

`parent/this.cs:113` makes it; `parent.Run` (`:48`) takes the goal and the call's parameters instead of a name and the caller's frame (`:55–64`).

## on.event binds — `module/on/event.cs:74–79`

```csharp
var @event = new global::app.@event.call.@this(__action!, named.Name, when.Value, item, Context.Actor!);
Context.Actor!.@event.Add(@event);
return Task.FromResult<data.@this>(Context.Ok<global::app.@event.@this>(@event));
```

`Scope` and its `Use` go.

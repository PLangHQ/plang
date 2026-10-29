# event-action — the expected C# for stage 2 (decisions 3–40)

What stage 2's code should look like, to compare when it reports done. NEW marks a member that doesn't exist; anything else is quoted with its file:line. Names the coder proposes stay theirs to bring.

## The one question — `Has`, answered by the thing bound on (39)

```csharp
// type/item/this.cs — NEW, item's locked base: an event bound on this item runs for itself
internal virtual bool Has(@this item) => Equals(this, item);

// type/this.cs — NEW: an event bound on a type runs for each item of it (each goal, each action)
internal override bool Has(global::app.type.item.@this item) => item.Is(this);          // item.Is(type): item/this.cs:395

// module/this.cs — NEW: an event bound on a module runs for each of its actions
internal override bool Has(global::app.type.item.@this item) => _action.Values.Contains(item);   // _action: module/this.cs:57

// goal/pattern/this.cs — NEW kind (37), made by goal.list.Find for a name with * or ?: the goal answers (42)
internal override bool Has(global::app.type.item.@this item) => item.Is(this);

// type/item/this.cs — NEW (42): is this item named by the pattern? An item on its own isn't
internal virtual bool Is(global::app.goal.pattern.@this pattern) => false;

// goal/this.cs — NEW (42): a goal is named by a pattern when its path matches (the one Glob, moved out of permission/this.cs:117)
internal override bool Is(global::app.goal.pattern.@this pattern) => pattern.Matches(Path);
```

A variable equals another by its name (42), so the base `Has` answers for it. No `is` check and no `ReferenceEquals` anywhere.

## The actor's events — pull and push (3, 10, 34)

```csharp
// event/list/this.cs — the actor's events (%!app.event%), every event in bind order
private readonly object _gate = new();
private readonly System.Collections.Concurrent.ConcurrentDictionary<global::app.type.item.@this, on.@this> _on = new();   // loaded lists, owned by the actor's events (43)

// pull: an item's lists, loaded on the first ask with every event that applies, the same after that
internal on.@this this[global::app.type.item.@this item]
{
    get
    {
        if (_on.TryGetValue(item, out var on)) return on;
        lock (_gate)
        {
            if (_on.TryGetValue(item, out on)) return on;
            on = new on.@this(item);
            foreach (var @event in Items())
                if (@event.Item.Has(item)) on.Add(@event);
            _on.Add(item, on);
            return on;
        }
    }
}

// push: an on.event step adds its event; it joins every loaded list it applies to, never an unloaded one
internal void Add(global::app.@event.@this @event)
{
    lock (_gate)
    {
        base.Add(@event);
        foreach (var (item, on) in _on)
            if (@event.Item.Has(item)) on.Add(@event);
    }
}
```

`on.Add(event)` puts it on its moment's side (`Name`, `When`). A variable name's list is keyed by the name (24), loaded on its first set only when the name has events.

## Running (5, 28, 31, 32)

```csharp
// goal.Start — its own list
var on = context.Actor.@event[this];
var answer = await on.before.Start(this, context);
var result = answer is { Success: false } or { Handled: true } ? answer : frame.Leave(await Step.Start(context));
return await on.after.Start(this, result, context);

// an action — its catalog action's list (28); its own clauses run around it (11)
var on = context.Actor.@event[Module[Name] ?? this];

// event/on/side.cs — only when the side holds an event: under event.setting.goal=app, a run inside a system/ goal skips it
if (events.Length == 0 || await context.Actor!.@event.Skips(context)) return result;
```

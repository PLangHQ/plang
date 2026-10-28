# 10a: the C# I expect (snapshot 217)

Written before the coder reports, to compare against its commit. Members marked NEW don't exist today. OBP pattern: saved data maps by a view and its tags; one door (adding to the list is the only way to bind).

## on.event binds by adding to the list (`module/on/event.cs:73–76`)

```csharp
// before
var hold = Context.Actor!.on;
… hold.Bind(item, named.Name, when.Value,
    (side, actor) => new binding.action.@this(side, hold[item][named.Name]!, call, actor, scope.Value)) …

// after
var @event = Context.Actor!.on[item][named.Name]!;
… @event[when.Value].Add(list => new binding.action.@this(list, @event, __action, Context.Actor, scope.Value)) …
```

The binding holds the on.event action that made it (`__action`), never the goal.call inside it.

## The event selects a side (`event/this.cs`)

```csharp
[Snapshot] public binding.list.before before { get; }
[Snapshot] public binding.list.after after { get; }

/// <summary>The bindings started <paramref name="when"/> it fires.</summary>
internal binding.list.@this this[When when] => when == When.before ? before : after;   // NEW
```

## on folds own (`event/on/this.cs`; `own.cs` deleted)

```csharp
public class @this : type.item.@this
{
    /// <summary>The item these are the events of.</summary>
    internal type.item.@this item { get; }                              // NEW, untagged

    internal @this(type.item.@this item) : this(item.on.Declared) => this.item = item;

    public virtual @event.@this? this[string name] => …own's indexer, its lists open…;
    internal virtual @event.@this? Bound(string name) => _events.GetValueOrDefault(name);

    // NEW: its own write; the view writes the item as what finds it
    protected internal override async ValueTask Write(IWriter writer)
    {
        writer.BeginObject();
        writer.Name("item"); await item.Output(writer);
        foreach (var (name, @event) in _events) { writer.Name(name); await @event.Output(writer); }
        writer.EndObject();
    }
}

/// <summary>The events of an item nothing is bound on: one per class, shared, closed.</summary>
public sealed class empty : @this { … }                                 // NEW, replaces Empty, None, No*
```

## The action binding saves its action (`event/binding/action/this.cs`)

```csharp
/// <summary>The on.event action that bound it; what it runs is that action's Action, read when it fires.</summary>
[Snapshot] internal goal.step.action.@this action { get; private set; }  // NEW, replaces _call

internal override bool IsSaved => true;                                  // NEW virtual on binding, false there
```

## Where it lives

```csharp
// actor/this.cs:72, above Context
[Snapshot] internal @event.on.list.@this on { get; }

// event/on/list/this.cs: each on carries its item, so the list is the saved member
[Snapshot] internal IReadOnlyList<on.@this> list { get => [.. _held.Values]; private set => …each keyed by its item… }
```

## Gone

`own`, `own.Bind`, both `on.list.Bind`, `on.Empty`/`None`/`NoStart`…`NoError`, `item.Own()` (renamed; the coder sends the name), `binding.action._call`.

## Not traced; I compare against the coder

- Where `IsSaved` is asked. I expect one place. I haven't traced where the walk writes a binding list's rows (`binding.list` is not an item).
- What `item` writes when the item is a type (`app.type.goal`).
- How the action binding reads its action's `Action` when it fires.

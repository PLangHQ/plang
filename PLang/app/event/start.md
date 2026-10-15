# The event system — a C# developer's guide

An event is a hook the runtime fires around something it does: a goal starts,
a value is born, a variable is set, a cache misses, a failure happens. A
program binds a goal to the hook, and the runtime starts that goal when the
hook fires. This page covers the event system for the developer working on
Runtime2 in C#. It explains what an event is, the core events, how a binding
attaches to one, how a module declares its own, and where each piece lives.

```
app.@event.@this        PLang/app/event/this.cs          namespace app.event       one event
app.@event.on.@this     PLang/app/event/on/this.cs       namespace app.event.on    an item's events
app.@event.binding      PLang/app/event/binding/this.cs  namespace app.event.binding  a binding
app.@event.When         PLang/app/event/When.cs          namespace app.event       before | after
```

> **Namespace note.** The folder is `event` but the C# identifier is `@event`,
> because `event` is a C# keyword. The path-equals-namespace rule still holds
> (`PLang/app/event/` is `app.event`); the `@` only escapes the keyword.

> **Audience.** These docs are for the developer working *on* Runtime2 in C#.
> Everything here answers three questions: **what is this, where is the source,
> and how does the plang I write reach this C#?** The root of the runtime is
> described in `PLang/app/start.md`.

---

## 1. What an event is

An event is one named hook on one item. `start` is the hook on a goal, a step,
an action, and the app. `write` is the hook on a channel's write. Each event
has two sides, and a binding attaches to one side:

- **`before`** runs before the thing the event wraps.
- **`after`** runs after it, on the result.

An event class holds two lists, `before` and `after`. Each list holds the
bindings in the order they were added.

```csharp
public abstract class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public string Name { get; }
    public binding.list.before before { get; }
    public binding.list.after after { get; }
}
```

> **One class per event.** The core events are one class each under
> `PLang/app/event/on/` (`start.cs`, `write.cs`, …). The event carries no
> state about the item it fires for. The item is handed to its bindings when
> the event starts, so one event serves every item of its type.

### Before can stop it, after cannot

The two sides fold differently. The rule is in the list classes:

| Side | Starts | A failure or a `Handled` answer | Otherwise |
|---|---|---|---|
| `before` | each binding that fires for the item, in order added | stops the list and is the answer; what the event wraps does not run | the result passes on |
| `after` | each binding that fires, innermost level first, every one | does not stop the rest | the result passes on |

`before` is handed the result it is about to act on (for a `set`, the value
about to be stored), or a plain success if there is none. A `before` binding
that answers `Handled` is the answer: the thing it wraps is skipped, and its
result stands in. `after` is handed what the wrapped thing produced.

---

## 2. The shape: `start` in `Launch()`

`app.@this` fires `start` around the goal it runs. The call site is the
private `Launch()` in `PLang/app/this.cs`:

```csharp
// User code executes under the User actor's context — the app starts running
// through its on.start: a before that fails or cancels is the answer and the
// goal doesn't start; every after runs on the result.
var user = actor.list.User.Context;
var answer = await on.start.Before(this, user);
var result = answer is { Success: false } or { Handled: true } ? answer : await goal.Start(user);
return await on.start.After(this, result, user);
```

Read it as the template every event follows:

1. `on.start.Before(this, user)` — the bindings before `start` run. If one
   fails or answers `Handled`, `answer` is that result and the goal does not start.
2. `goal.Start(user)` runs only when `before` let it through.
3. `on.start.After(this, result, user)` — every `after` binding runs on the
   result, whether the goal ran or `before` stopped it.

`on` is the item's accessor (`PLang/app/type/item/this.cs`, `item.on`). Here
`this` is the app, so `on.start` is the app's `start`.

---

## 3. The core events

The core events are the files in `PLang/app/event/on/`. The class is
`app.@event.on.<name>`, and `on/this.cs` (`Made`) is the one place that names
each by its string.

| Event | File | Fires when |
|---|---|---|
| `start` | `on/start.cs` | a goal, a step, an action, or the app starts. Before: handed the item. After: handed the result. |
| `load` | `on/load.cs` | a goal is loaded. Before: handed the `.pr` it is read from (there is no goal yet). After: handed the goal. |
| `create` | `on/create.cs` | a value is born through its type (`type.Create`). Not fired by an internal re-type. |
| `set` | `on/set.cs` | a variable is set. Before: handed the value about to be stored. After: handed the stored one. |
| `remove` | `on/remove.cs` | a variable is removed. Handed the value it holds. |
| `read` | `on/read.cs` | a channel reads. After: handed what was read. |
| `write` | `on/write.cs` | a channel writes. Before: handed the data about to be written. After: handed the write's result. |
| `ask` | `on/ask.cs` | a channel asks. After: handed the answer. |
| `error` | `on/error.cs` | the thing it is on fails. Answered by the `on.error` clauses bound on its `before` side. |
| `hit` | `on/hit.cs` | a cache answers with what it holds. |
| `miss` | `on/miss.cs` | a cache holds nothing for the key. |

The `Made` table names eleven events (`start`, `load`, `create`, `set`,
`remove`, `write`, `read`, `ask`, `error`, `hit`, `miss`). Two more files,
`on/own.cs` and `on/declared.cs`, are classes, not events:

- **`own`** is the item's own set of events, given to it by its first
  binding. It is not a hook. See §5.
- **`declared`** is the event a module names with `[Events(...)]`. See §6.

### Worked example: `error`

`error` is the one event whose `before` side has a special fold. `Catch` in
`on/error.cs` hands the failure to each `before` binding in turn. The first
binding that answers with anything other than the failure handed back wins, and
the rest never see it. A failure no binding takes stands as it was.

```csharp
foreach (var binding in Of(level.on).before)
{
    if (!binding.For(item, context)) continue;
    var answer = await binding.Start(item, failed, context);
    if (!ReferenceEquals(answer, failed)) return answer;
}
```

---

## 4. Bindings

A binding is what a program attaches to an event. `binding/this.cs` is the
class. It holds three things:

| Member | Meaning |
|---|---|
| `Actor` | the actor that bound it |
| `Scope` | `binding.Scope.actor` (fires only while the binding's actor runs) or `Scope.app` (fires for every actor). `binding/Scope.cs` |
| `For(item, context)` | whether it fires for this item in this context. `Scope` takes the actor, the filter takes the item. |

`When` (`PLang/app/event/When.cs`) is the side: `before` or `after`.

A binding does not fire inside its own handler. `Start` calls
`context.TryEnterEvent(this)`. If the binding is already running, it answers a
plain success. A handler can fire the event it is bound on without looping.

`binding/list/` holds the two sides. `before` and `after` are the two list types.
An own event's lists take bindings. The shared empty event's lists are closed:
`Add` throws. `Remove` is the binding's own verb (`binding.@this.Remove()`).

### The three kinds of binding

All three derive from `binding.@this` and differ only in what `Handle` does:

| Kind | File | Handles by |
|---|---|---|
| plain | `binding/this.cs` | calling the handler the caller passed in |
| goal call | `binding/action/this.cs` | starting the program's action (`on.event`, a goal call). It holds `%!event%` on the call frame for as long as the call runs. |
| clause | `binding/clause/this.cs` | minting the clause's handler with the firing's context, then handing it the action it is a clause of |

The clause binding is what `on.error`, `on.cache`, and `on.timeout` become.
Those are actions, not events. The module that owns the clause (for example
`PLang/app/module/on/error.cs`) implements `IClause` and binds the clause onto
the action before it:

```csharp
action.Own().Bind("error", global::app.@event.When.before, side =>
    new global::app.@event.binding.clause.@this(side, clause, action, fire));
```

So a clause is a binding whose handler is a module action, and the binder is
the module's `IClause`. It is never started as a step.

---

## 5. Items, levels, and `own`

`item.on` returns the events for an item (`on.Of(GetType())`). An item nothing
is bound on has its class's shared empty events, which are closed to bindings.
The first binding gives the item its own `own` events. An `own` is made the
first time each event is asked for.

Events are per level. `Before` and `After` walk the item's levels with
`item.Level(depth, context)`. `before` starts outermost first. `after` starts
innermost first. The binding sets are read at each level as they stand.

---

## 6. Declared events and module-owned events

A plang type can declare events beyond the core's. The declaration is the
attribute `PLang/app/Attributes/EventsAttribute.cs`:

```csharp
[PlangType("window"), Events("opened", "titled", "focused", "minimized", "maximized",
    "restored", "closed", ...)]
```

The attribute names the events. `Events` makes a `declared` event for each name
(`on/declared.cs`). A declared event binds like any other
(`on %window% closed, call X`). The module that owns the type raises it. A
declared event has no class of its own: `declared` is the one class for all of
them.

> **Rule.** New needs go to the edge, not the core. A module owns its own types,
> and a module declares its own events with `[Events(...)]`. Reach for those
> before adding anything to `on/`. The core event set and `app.@event` are
> locked: an addition to them is a proposal to the architect, not a change made
> in a module.

The current declarations are `PLang/app/module/window/type/window/this.cs` and
`PLang/app/module/screen/type/element/this.cs` (`click`).

---

## 7. Find ...

| You want | Where |
|---|---|
| the event door on an item | `item.on`, `PLang/app/type/item/this.cs` |
| the events an item has | `on.@this`, `PLang/app/event/on/this.cs` (`this[name]`, `Names`) |
| one event's shape (before, after, Before, After) | `PLang/app/event/this.cs` |
| a core event's class | `PLang/app/event/on/<name>.cs` |
| the lists that fold the two sides | `PLang/app/event/binding/list/before.cs`, `after.cs` |
| a binding and its firing filter | `PLang/app/event/binding/this.cs` (`For`, `Start`, `Remove`) |
| a clause binding | `PLang/app/event/binding/clause/this.cs` |
| the `IClause` a module implements | `PLang/app/module/IClause.cs` |
| a type's declared events | `PLang/app/Attributes/EventsAttribute.cs` |
| where a core event fires from | `Launch()` in `PLang/app/this.cs`, and the callers of `on.<name>` |
| a plang `on` line in a `.pr` | `PLang/app/event/serializer/Reader.cs` (an event is reached by path, never written as a value) |

---

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, the collection-node pattern, and the reading fundamentals |
| `../module/start.md` | the module system, which declares the clauses and the events a module owns |
| `../type/item/start.md` | the value apex, `item.@this`, and `item.on` |
| `../goal/start.md` | the goal a binding runs, and `on.start` around the run |

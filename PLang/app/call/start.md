# The call stack and variable memory — a C# developer's guide

A call is one frame on the call stack: a goal's, a step's or an action's, or a
frame that only binds names. The stack is the chain of frames a context is
running now. Variable memory is the store a run reads and writes its names in,
and a read walks the frames before it reaches the store. This page covers both
for the developer working on Runtime2 in C#. It explains the frame, the stack,
frame-scoped state, and how a name reaches its value.

```
app.call.@this          PLang/app/call/this.cs                    namespace app.call          one frame
app.call.list.@this     PLang/app/call/list/this.cs               namespace app.call.list     the stack
context.call            PLang/app/actor/context/this.cs           the stack a context runs on
context.Variable        PLang/app/type/item/variable/list/this.cs namespace app.type.item.variable.list
                                                                  (global alias Variables)  variable memory
```

> **Audience.** These docs are for the developer working *on* Runtime2 in C#.
> The root of the runtime is described in `../start.md`. The type system that
> variables hold values of is `../type/start.md`.

> **Locked core.** The call stack and variable memory are on the locked-core
> list. This page describes their current surface. Additions go through the
> architect, not into this folder on a local judgement.

> **Alias.** `call` is not a global alias. `app.module.goal.Call` (the
> `goal.call` action handler) collides with it. Write `app.call.@this` fully
> qualified, or add a per-file alias. The variable store is the exception: it
> has a global alias, `Variables` (`PLang/app/GlobalUsings.cs`).

---

## 1. One call is one frame

`app.call.@this` is one frame. It holds what the frame is and what it runs:
its goal, its step, its action, its event, its format, its errors and its
children. It is a plang value, and it is a structure, so plang navigates its
members. `IsLeaf` is `false`.

```csharp
namespace app.call;

public partial class @this : global::app.type.item.@this, IAsyncDisposable
{
    public override bool IsLeaf => false;
    public virtual global::app.goal.@this? Goal { get; }
    public virtual global::app.goal.step.@this? Step { get; }
    public global::app.goal.step.action.@this? Action { get; }
    public @this? Caller { get; }
    public child.list.@this Children { get; }
}
```

The frame's members split three ways:

| Member | What it is | Plang |
|---|---|---|
| `Goal`, `Step`, `Action` | what the frame runs | `%!call.current.Goal%` and the like |
| `Caller`, `Children`, `Chain`, `Depth` | the tree: up, down, the chain outward, the length | `%!call.current.Depth%` |
| `Errors`, `Handled`, `Error` | errors the frame saw; `Error` is the one in play, null once recovered | `%!error%` reads `call.Error` |
| `Event`, `Format` | frame-scoped state (§3) | `%!event%`, `%!app.type.format%` |
| `Tags`, `Diffs`, `StartedAt`, `CompletedAt`, `Duration`, `Id` | what the call setting asks for (§5) | `Tags` is a dict |

The frame writes one flat form as its `Output`: id, depth, goal address, step
text, `module.name` of the action, times, handled flag, tags, errors, caller and
child ids by name, and the variable changes it saw. It does not write its
variables or its event. Everything else is one navigation away.

A frame is created only by the stack (§2). Its constructor is `internal`, and
its life ends at `DisposeAsync`, which restores the stack's current frame.

### The three kinds of frame

Every frame is an `app.call.@this`. Three subclasses set how it behaves.

| Kind | Source | Deepens the chain | Timed and diffed | Keeps writes |
|---|---|---|---|---|
| goal, step or action (the base) | `call/this.cs` | yes | yes, when the setting says so | only names it was born with |
| binding (a loop's `%item%`, a handler's `%error%`) | `call/binding/this.cs` | no | no | only names it was born with |
| isolated (a tool's goal, a shortcut, a task's run) | `call/isolated/this.cs` | yes | yes | every write |

> **Keeper.** A write to a name lands in the frame that *keeps* it. The base
> frame keeps the names it was born with (a goal call's parameters). The
> binding frame keeps the same way. The isolated frame keeps every name, so a
> write under it never reaches its caller. A name no frame keeps lands in the
> context's variable memory (§4).

---

## 2. The stack is `app.call.list.@this`

`app.call.list.@this` (`PLang/app/call/list/this.cs`) is the stack of one
context. It holds the current frame and nothing else. Each frame holds its own
state, and frames link to each other through `Caller` and `Children`.

- **`Current`** is the innermost frame on this async branch. It is an
  `AsyncLocal`, so a parallel branch has its own current and never disturbs the
  others. The stack is per context: a context's calls are born with it.
- **`Root`** is the first frame pushed in this run. A frame that starts an empty
  chain becomes the root again, so `Root` always belongs to the live run.
- **`Goal`, `Step`, `Scope`, `Error`, `Format`, `Event`** walk outward from
  `Current` and answer the first frame that holds the thing. These are the
  plang faces: `%!goal%`, `%!step%`, `%!error%`, `%!event%`.
- **`Audit`** is every error observed in the run, at every frame. It survives
  the frames that recorded it.

### Push and pop

Each push returns a frame that is `IAsyncDisposable`. Use `await using`, and
the frame pops when its scope ends.

```csharp
await using var frame = context.call.Push(action);   // a step's action: goal, step, action
```

| Call | Pushes | Use |
|---|---|---|
| `Push(goal, names)` | a goal's own frame, born with the goal call's parameters | a goal starting |
| `Push(step)` | a step's frame, with its goal | each step |
| `Push(action, variables)` | an action's frame, with its step and goal | each action |
| `Push(names, held)` | a binding frame | a loop's item, a handler's error |
| `Isolate(names, held, caller)` | an isolated frame | a tool's goal, a task's run |

`Push` checks the depth first. A push past `MaxDepth` (default 1500) throws
`CallStackOverflowException`. The stack files it as a `CallStackOverflow` error
with status 500. Depth counts goal, step and action frames. A binding frame does
not count.

> **Pop.** `DisposeAsync` restores `Current` to the frame it replaced, but only
> if the frame is still current. A parallel branch that has its own current is
> left alone.

---

## 3. Frame-scoped state: format, settings, event

Three things are set per frame and hold for the goals a frame calls. They are
not app-wide.

**Format.** A goal sets its format with `SetFormat`, which writes it on the
nearest frame that runs a goal (not one of its steps or actions). It holds for
that goal and what it calls, and goes when the goal returns. `Format` reads the
nearest frame that holds one. When no running goal set one, the app's own format
is in play. The type page covers how the format is read and set
(`../type/start.md` §6). In short, `FormatOf(context)` answers the call's format
first, then the app's, then plain text.

**Settings.** What a stack captures is `app.call.setting.@this`
(`PLang/app/call/setting/this.cs`), read as `%!app.call.setting%`. The stack reads
it through the context's settings, once per push. Each option turns on one tier
of a call's data:

| Option | Type | What it turns on |
|---|---|---|
| `Timing` | bool | `StartedAt`, `CompletedAt`, `Duration` on every call |
| `Diff` | `diff` (`Enabled`, `Deep`) | the variable changes a call makes; `Deep` keeps a deep copy of the value before |
| `Tags` | bool | a hint for exporters; a call's own tags are written either way |
| `History` | bool | keeps the calls that returned, under their caller |
| `Frame` | `frame` (`Max`) | how many returned calls `History` keeps |

An open diff scope turns `Diff` on for as long as it is open, whatever the
setting says. The `--callstack={…}` run option sets these values for the run.

**Event.** The event in play is on the frame whose bound call is running. It is
an `internal` setter on `app.call.@this.Event`, set only while that call runs.
`%!event%` reads `call.list.Event`, which walks outward and returns the first
one found. Its value is the running event, its properties are `item` and
`result`.

---

## 4. Variable memory is `context.Variable`

Each context owns one variable memory: `context.Variable`, of type
`app.type.item.variable.list.@this`. It is the store, not a value, and it does
not claim the `variable` type name. Its global alias is `Variables`.

```csharp
public async ValueTask<data.@this> Set(string name, object? value)
public ValueTask<data.@this> Get(string name)
public data.@this? Peek(string name)
public async ValueTask<item.@this> Value(string name)
public async ValueTask<data.@this<T>> Get<T>(string name)
```

| Member | What it does |
|---|---|
| `Get(name)` | the Data `name` holds, frames first (inner first), then memory, then the outer memory of a task. `NotFound` when nothing is bound. |
| `Set(name, value)` | binds `name`. Lands in the frame that keeps the name, else in this memory. A new Data is minted, never mutated in place. |
| `Peek(name)` | the same lookup, in memory, with no async. For `--debug` displays. |
| `Value(name)` | the value `name` holds, opened through its own door. |
| `Get<T>(name)` | the typed view: `Data<T>`. |
| `Ensure(name, value)` | what `name` holds, or the value a birth gives it, stored in one step. |
| `Own(name)` | what `name` holds, as this run's own to change in place. A copy, when the value is read from beyond the frame that keeps it. |
| `Replace(name, expected, value)` | stores `value` only if `name` still holds `expected`. |
| `Remove(name)` | removes it, through the variable type's `on.remove`. |
| `Contains(name)`, `list`, `GetNames()`, `GetAll()` | presence, the variables as a plang list, the names, the pairs. |
| `Snapshot()` | the user-visible variables as a dict, for failure diagnostics. |

> **Root names only.** The store takes root names. A name is the root of a
> variable: `user`, not `user.name`. A write deeper in is the variable's own
> (`Variable.Set`, §5). The store's doc comment says it: "a write deeper in is
> the variable's own."

### What a read sees

`Get` checks, in order:

1. The current frame and its callers, inner first. An inner frame's name shadows
   an outer one.
2. This memory's own names.
3. For a task's context, its parent context's memory. That case is `Outer`: it
   applies when the root frame has a caller, and it reads `context.Parent`.
4. `NotFound`, built by the context.

### What a write does

`Set` runs these steps in order:

1. If the value is a Data that holds variables, it is settled first. A template
   renders here. A reference binds the value it names, not the marker.
2. The `set` event's `before` handlers run. A refusal or a handled answer is the
   write's answer, and nothing is written.
3. `Bind` writes the name into the frame that keeps it, else into this memory.
4. If the value changed, the `set` event's `after` handlers run, and the call
   stack records the change for its diff (`call.Record`).

`Set` never mutates a Data in place. It mints a new one. A value a caller
already holds stays as it was.

---

## 5. A name reaches its value through the variable parser

A plang path like `%user.name%` is a variable with hops. The store does not
parse it. Ask the parser, or the variable.

| You have | You call | You get |
|---|---|---|
| the text of a `%…%` | `new parser.@this(text).Variable` | the one variable the text is, or null |
| a path from a value | `new parser.@this(text).Path()` | the code (its hops), or null |
| a variable | `variable.Start(context)` | the value it holds |
| a variable | `variable.Set(value, context)` | writes it; every hop but the last reaches the parent |

The hop for `!` is `Binding`. A root name that starts with `!` is a system name
(`%!app.call%`, `%!event%`). Those names are kept by the store with the same
rules as any other name, but the store's enumerations skip them.

> **Skipped names.** `list`, `GetNames`, `GetAll`, `GetChangedSince`, `Clear`,
> `Clone`, `Snapshot` and `ToDictionary` leave out names that start with `!`.
> `list` also leaves out a name the parser does not read back whole.

The variable parser's rules live in `PLang/app/type/item/variable/parser/this.cs`.
The variable type itself is `PLang/app/type/item/variable/this.cs`. Read the
type page (`../type/item/start.md`) for the value side.

---

## 6. Children and history

`Children` (`PLang/app/call/child/list/this.cs`) is the list of frames a frame
pushed. It owns its own lock.

- With `History` off, a frame removes itself from its caller's children when it
  pops. The list stays bounded by the live depth.
- With `History` on, `Add` evicts the oldest child once the count passes
  `Frame.Max`. The list keeps the last returned calls.

`Position` (`PLang/app/call/Position.cs`) is a snapshot record of one frame: its
action, its goal, its step index, its action index and its id. It is a resume
point, not a live frame. A frame's `Position` is null unless it runs an action.

---

## 7. Errors on a frame

`call.Record(error, context)` is the one door. A frame stamps what the error
does not carry yet: its chain, its step, its context, and a snapshot of the
variables when the error keeps them (`Keeps`, for an assertion or under
`--debug`). It then files the error on the frame and in the run's audit. An
error is recorded once per frame, by instance identity.

`call.Error` is the error in play: the first frame outward that holds an error
it has not recovered. `Handled` on the frame marks a recovered error, and
`Error` then reads null for that frame.

---

## Find ...

| You want | Plang | C# | Source |
|---|---|---|---|
| the stack a context runs on | `%!app.call%` | `app.call(ctx)`, which returns `context.call` | `PLang/app/this.cs` (`call(...)`) |
| the current frame | `%!call.current%` | `Current` | `PLang/app/call/list/this.cs` |
| the goal in play | `%!goal%` | `list.Goal` | `PLang/app/call/list/this.cs` |
| the error in play | `%!error%` | `list.Error` | `PLang/app/call/list/this.cs` |
| the event in play | `%!event%` | `list.Event` | `PLang/app/call/list/this.cs` |
| the format in play | `%!app.type.format%` | `list.Format`, `FormatOf` | `PLang/app/call/list/this.cs`, `PLang/app/type/root/this.cs` |
| what the stack captures | `%!app.call.setting%` | `list.Setting` | `PLang/app/call/setting/this.cs` |
| a frame's kind | — | `binding.@this`, `isolated.@this` | `PLang/app/call/binding/this.cs`, `PLang/app/call/isolated/this.cs` |
| the variable memory | `%!app.variable%` | `context.Variable` (`Variables`) | `PLang/app/type/item/variable/list/this.cs` |
| a name's value | `%name%` | `Get(name)`, `Peek(name)`, `Value(name)` | same file |
| a path into a value | `%name.part%` | `parser.@this`, `variable.Start` | `PLang/app/type/item/variable/parser/this.cs`, `PLang/app/type/item/variable/this.cs` |
| the error snapshot | — | `Snapshot()` | `PLang/app/type/item/variable/list/this.Snapshot.cs` |

## The tree from here

| Doc | Covers |
|---|---|
| `../start.md` | the app root, the collection-node pattern, and the reading fundamentals |
| `../type/start.md` | the type system, and the format in play (§6) |
| `../type/item/start.md` | the value apex, and the variable type's place in it |
| `../module/start.md` | the module system, which dispatches the actions a frame runs |
| `../goal/start.md` | a goal and its steps |
| `../actor/start.md` | a context and its actor |

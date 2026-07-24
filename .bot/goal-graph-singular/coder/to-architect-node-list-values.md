# to architect — node collections (`action.list`, `step.list`, `parameter.list`) must become `list<T>` values; blocked on born-with-context

Branch `goal-graph-singular`. This came out of the typed-value-set-reader work (your
`typed-value-set-reader-answer.md`). Ingi has ruled the shape; I'm stuck on ONE lifecycle wall and he
wants your ruling before I cut the hot-path code.

## How we got here

The live failure: `set %goal.step[i].action% = %compileResult.actions%` (a `clr(json)` array onto the
`step.Action` slot) lands an **untyped generic `list.@this`**, not a typed `action.list` — so a nested
`goal.call` param never dispatches. I built the read path you designed:

- `clr.Read → kind.Read → json.Read` bridges the json into the declared type's own reader (the `.pr`
  path), element kind handed down.
- the generic **list reader now loops the element's reader** via `kind` (`list<action>` → each element
  reads through `Typed("action")`, so a `goal.call` param rides `@schema:data` and dispatches). ✅
- `ContainerFamily` extended to recognize concrete `IReadOnlyList<T>` so `App.Type[action.list.@this]`
  resolves to `{Name:"list", Kind:"action"}`. ✅

Then the assignment fails:

```
ArgumentException: Object of type 'app.type.item.list.this' cannot be converted to
                   type 'app.goal.step.action.list.this'.
  at app.type.item.this.Set (prop.SetValue)
```

The elements are correct (real `action` reader, goal.call dispatched). The **container type** is wrong:
the reader produces a generic `list.@this`; the slot is the bespoke `action.list.@this`.

## Root cause — the reader can't produce the node, because the node isn't a value

`ITypeReader.Read` returns `app.type.item.@this`. But the node collections are bare CLR lists, **not
items**:

```csharp
// app/goal/step/action/list/this.cs
public sealed class @this : IReadOnlyList<Action>        // NOT app.type.item.@this
// app/goal/step/list/this.cs
public sealed class @this : IReadOnlyList<Step>          // NOT app.type.item.@this
// app/goal/step/action/parameter/list/this.cs
public sealed class @this : IReadOnlyList<Data>          // NOT app.type.item.@this
```

So a reader **cannot return one**. This is *exactly* why the `.pr` step reader hand-rolls a raw
`List<action>` then wraps it (Ingi: *"I find it strange that pr creates first List<action> just to
create action.list"*):

```csharp
// app/goal/step/serializer/Reader.cs
var actions = new List<action>();
while (reader.NextElement()) actions.Add((action)_action.Read(ref reader, kind, ctx));
...
Action = new action.list.@this(actions),   // the node can't be read directly, so build raw + wrap
```

## Ingi's ruling (verbatim intent)

- *"it should be always same type in the flow, if making it `list<action>` is that solution we need to
  do that, having if statement checking if it is one type or another is always bad."*
- On the node keeping its behavior: *"Run on an action belongs to an action, not to step … item are
  responsible for them selfs"* — i.e. the node keeps its own `Run`; it does NOT move to `step`.
- *"yes, this is it, cost is low. and step.list should also"* — convert `action.list` AND `step.list`.

So: **`action.list : list<action>`, `step.list : list<step>`, `parameter.list : list<data>`** — real
`list.@this` subclasses the reader returns directly. The node keeps its `Run`/`IndexOf`; the reader
just fills it. This kills the double-build in the `.pr` reader AND the value-set mismatch, one path, no
type-`if`.

**Precedent already exists** — `action.property.list.@this : app.type.item.list.@this`
(`app/goal/step/action/property/list/this.cs`), a node that IS a list value, exposing typed rows via
`Items.Select(d => d.Clr<Property>())`. This is the shape to copy.

**Scope confirmed with Ingi:** in = `action.list`, `step.list`, `parameter.list` (the `[Store]`
sequence nodes that ride readers). OUT = `goal.list` (it's the goal **registry** — a path-keyed
`ConcurrentDictionary` cache + name index + `Setup`, reached as `app.Goal`, never read from `.pr` or
Set from json). Also out = `callstack/*`, `error/trail`, `warning/list` (runtime infra, never
reader-produced).

## Where I'm stuck — born-with-context vs context-free POCOs

`list.@this` is **born-with-context**: every ctor demands a non-null `actor.context` (it needs it to
born child row `Data` and navigate). But `step`/`goal` carry **no context property at all**, and
`action` has only a nullable *catalog* `Context` (usually null at runtime). Context arrives at **Run**
(`context.Step = this`, `goal.Run(context)`), never at construction.

The nodes are held as **empty-list field defaults** that run at field-init time, with no context in
scope. Every one of these stops compiling the moment the node ctor requires context:

```csharp
// app/goal/step/this.cs:49       — step has no Context field
private global::app.goal.step.action.list.@this _action = new(new List<ActionEl>());
    ...
    set => _action = value ?? new(new List<ActionEl>());          // :56 fallback

// app/goal/step/this.cs:101 (Nest) and :191 (Merge) — rebuilds, step has no context
_action = new global::app.goal.step.action.list.@this(nested);
_action = new global::app.goal.step.action.list.@this(from.Action.list);

// app/goal/this.cs:43 / :49      — goal has no Context field
private global::app.goal.step.list.@this _step = new(new List<step>());
    set => _step = value ?? new(new List<step>());

// app/goal/step/action/this.cs:29 / :45
public parameter.list.@this Parameter { get; init; } = new();
public step.list.@this Child { get; set; } = new(new List<step>());
```

**Every genuine read/deserialize site DOES have context** (`ctx.Context` in the readers,
`d.Get(...).Context` in `Create`, the builder). The gap is *only* these imperative empty-defaults +
`Nest`/`Merge` rebuilds. The context they'd need is always "the actor/app context the goal belongs
to" — known at **load**, but never **stored** on `step`/`goal`.

## The two ways I see to close it — Ingi rejected both as I framed them, wants your ruling

**A. Store context on the nodes at load.** Give `step`/`goal` a `Context` stamped by the goal-load
path; empty-defaults go nullable + lazy (`_action ??= new(Context)`). Correct per "runtime objects
reach Context," but it spreads: a new `Context` on `step`/`goal` and a stamping pass wherever goals
load (GoalMapper / goal.list.Add / readers).

**B. Deferred context on the node-lists.** The three node types get a context-less empty ctor;
`_context` stays null until the goal enters a context at Run, stamped via the existing
`list.@this.Context` setter. Localized to the three types + one base ctor — but it's a **late-stamp**
(context after birth), which the value model frowns on. Defensible only because a `.pr` POCO genuinely
doesn't know its context until it executes.

Ingi's response to A/B was *"no, I dont accept that"* — so neither framing is right. I think the
question underneath is: **what is the lifecycle of a loaded-from-.pr goal graph w.r.t. context?** Is a
`step`/`action`/`goal` a context-bearing runtime object (→ stamp at load, A-like), or is the whole
graph context-free until it's run through one (→ the list's born-with-context rule shouldn't apply to
these structural nodes the same way)? Right now `step`/`goal` are firmly the latter, which is what
collides with `list.@this` being firmly the former.

## What's already landed (uncommitted, working)

- list reader dispatches elements via `kind` (`list/serializer/Reader.cs`).
- `ContainerFamily` recognizes `IReadOnlyList<T>` (`type/list/this.cs`).
- `item.Read`/`clr.Read`/`kind.Read`/`json.Read` chain (the Read-not-Create path).
- `list.@this<T>` unsealed + an `Elements` accessor (the typed-rows surface the nodes will use).
- A proof test `ClrJsonActionsArray_GoalCallParam_ReadsAsTypedGoalCall` (currently red — blocked on
  exactly this: the value reads as a generic `list.@this`, can't assign to the `action.list` slot).

Nothing here is committed; happy to reshape once you rule on the context lifecycle.

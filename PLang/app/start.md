# The app tree — a C# developer's guide to Runtime2

This is where you start if you are going to work on the Runtime2 C# codebase.
It teaches you how to *read* the code: how a path on disk tells you a namespace,
how to find the C# that runs a line of plang, and the handful of shapes that
repeat everywhere. Once these click, the whole tree reads the same way.

> **Audience.** These docs are for the developer working *on* Runtime2 in C#.
> The developer *writing* `.goal` files is a later pass. Everything here answers
> three questions: **what is this, where is the source, and how does the plang I
> write reach this C#?**

**The docs live inside the code.** This page is `PLang/app/start.md`, sitting
next to `PLang/app/this.cs`. Every folder's `start.md` is that folder's overview
— the same reason a plang folder's own docs live in `start.md` (because
`Start.goal` is the entry point). So the doc for a concept is in the concept's
own folder, beside its source. This page is both the app root's doc and the
index for the tree.

---

## 1. The one root

There is one root object: `app`. Everything in the runtime hangs off it. There
are no service layers, no dependency injection, no parameter threading — you
navigate the object graph to what you need.

```
app.@this          PLang/app/this.cs          namespace app
```

Open `PLang/app/this.cs` and you are looking at the whole runtime's spine. A few
things to notice immediately:

```csharp
namespace app;

public sealed partial class @this : global::app.type.item.@this, IAsyncDisposable, ...
```

- **`app` is itself an `item`.** It extends `app.type.item.@this` — the apex of
  the value lattice (see §6). The root of the runtime is a value like any other,
  which is why `%!app.home%` and friends navigate the same way every value does.
- **`@this` is the class name.** Every folder's primary class is literally named
  `@this`, living in `this.cs`. More on that in §3.
- It is `partial` — the source generator and hand-written code share the class.

`app` owns app-level state directly (id, created/updated, environment, culture,
shutdown, the key/value store) and exposes every concept as a navigable member.

---

## 2. Path = namespace = plang name

**This is the single most useful rule in the codebase.** A file's path, its C#
namespace, and the plang name of the thing it implements are the same string
with different separators.

```
PLang/app/module/file/read.cs     the file on disk
        app.module.file           the C# namespace (declared at the top of the file)
            file.read             the plang action
```

The folder path under `PLang/app/` *is* the namespace (`/` → `.`), and the plang
vocabulary rides on top of it. So when you see a line of plang, you can walk
straight to the C# that runs it, and vice-versa.

### Finding the source for a line of plang

A built step records which module and action it uses. Here is a real one — the
step `- read "/system/builder/llm/decider.json", write to %decider%` compiles to
a `.pr` row whose `code` block says:

```json
{ "module": "file", "name": "read", "property": [ ... ] }
```

`module: file` + `name: read` → namespace `app.module.file`, action `read` →
the file **`PLang/app/module/file/read.cs`**, whose class carries
`[Action("read")]`. That is the whole lookup. (§Module system covers dispatch in
detail: `Documentation/app/module/start.md`.)

### The casing rule: nodes lowercase, verbs PascalCase

Within `PLang/app/`, **lowercase, singular folders are plang vocabulary**
(`actor`, `goal`, `module`, `type`, `data`, `error`, `event`, `channel`,
`variable`, `path`, `file`, …) and **PascalCase folders are C# infrastructure**
(`Attributes`, `Utils`).

The same split runs through member names:

- **Lowercase members are what plang navigates** — properties. `app.module`,
  `app.goal`, `app.type`, `app.error` are lowercase because a plang path walks
  them: `%!app.module.list%` ↔ `app.module.list` ↔ the `list` property on the
  module node.
- **PascalCase members are verbs (methods) and C# plumbing plang never walks** —
  `app.Start()`, `app.module.Get(name)`, `app.FileSystem`, `app.AbsolutePath`.
  The uppercase marks the line where C# executes, not a path plang can navigate.

So `%!app.module%` ↔ `app.module` (the property) ↔ `PLang/app/module/this.cs`,
but `app.Module(name)` is a C# verb — you won't find it on a plang path.

One keyword carve-out: `app/type/item/path/file/filesystem/Default/` stays
PascalCase because `default` is a C# keyword.

---

## 3. The `/this.cs` + `@this` pattern

Every folder's primary class is named `@this` and lives in `this.cs`. The folder
*is* the concept; `this.cs` is "the concept itself."

```
PLang/app/this.cs              app.@this          the app
PLang/app/module/this.cs       app.module.@this   the module registry
PLang/app/goal/this.cs         app.goal.@this     a goal
PLang/app/type/item/this.cs    app.type.item.@this the value apex
```

`@this` is a C# identifier using the `@`-escape so the contextual keyword `this`
can be a type name. You read `app.module.@this` as "the module, itself."

### Reading `@this` in code — the aliases

Writing `global::app.goal.step.@this` everywhere would be unbearable, so
`PLang/app/GlobalUsings.cs` defines short aliases for the ones used across files:

```csharp
global using Step    = app.goal.step.@this;
global using Channel = app.channel.@this;
global using path    = app.type.item.path.@this;      // lowercase: matches the plang name
global using dict    = app.type.item.dict.@this;
global using Clr     = app.type.clr.@this;
global using Debug   = app.module.debug.@this;
```

Note the lowercase aliases (`path`, `dict`) — they match the plang concept names
and, unlike PascalCase `Path`, don't collide with `System.IO.Path`. Within a
parent namespace you write the child directly: inside `namespace app.goal`, the
step is `step.@this`.

A few names **cannot** be global aliases because of collisions (listed with their
reasons in `GlobalUsings.cs`): `App`, `Goal`, `Action`, `CallStack`, `Builder`.
For those, use the fully-qualified form or a per-file `using`.

---

## 4. The collection-node pattern: `app.X`, `X.list`, `.current`

This is the shape that repeats for nearly every concept. **`app.X` is the
collection node, not a wrapper around one element.** Each concept exposes its
collection at `app.X`, owned once by `app`:

```
app.X                select-and-enumerate node for concept X
app.X["name"]        select one element by name
app.X.list           enumerate the collection
app.X.current        the element execution is currently inside   (only some X)
```

- **`.list`** is always the collection. There are no plural names anywhere in the
  API — the intent "all the goals" is spelled `app.goal.list`, never `goals`.
- **`.current`** exists only for concepts execution flows *through* — `goal`,
  `actor`, `error`, `test`. A concept nothing is ever *inside* (`type`,
  `channel`, `event`, `module`, `format`) has **no** `.current`.

The collection never lives on the element, and it is never a flat `App<Plural>`
property. The rule of thumb, stated throughout the codebase:

> **Registry = selection + lifecycle; all behaviour lives on the element.**

A type-switch inside a registry (`if (x is SomeSubtype)`) is misplaced behaviour
— it belongs on the element as a virtual member. The registry only *finds* and
*owns the lifetime of* its elements.

You can see this on `app` itself (`PLang/app/this.cs`):

```csharp
public global::app.module.@this module { get; }              // app.module  -> the registry
public global::app.type.current.@this<Goal, goal.list.@this> goal { get; }  // app.goal -> has .current
public global::app.type.root.@this type { get; }             // app.type    -> no .current
```

`type.current.@this<TElement, TList>` is the generic that gives a concept both a
`list` and a `current`; a concept with no current uses `type.@this<T, TList>`.

---

## 5. What `app` holds

The navigable (lowercase) members are the concepts; the PascalCase members are
C# plumbing. The current set, from `PLang/app/this.cs`:

| Member | Source | What it is |
|---|---|---|
| `module` | `app/module/` | action registry — every loaded module and its actions |
| `goal` | `app/goal/` | loaded goals + the running one (`.current`) |
| `type` | `app/type/` | the type system: plang names ⇄ CLR types, kinds, formats |
| `actor` | `app/actor/` | execution identities (System, User) + the current one |
| `error` | `app/error/` | the run's errors; `.current` is the one in play |
| `test` | `app/test/` | the test run (`--test`) |
| `variable` | `app/type/item/variable/` | the memory a run navigates |
| `shortcut` | `app/shortcut/` | app shortcuts (goals under `shortcut/`) |
| `store` | `app/store/` | the app's SQLite store (`.data/data.sqlite`) |
| `parent` | `app/parent/` | the app that started this plang, if any |
| `call(ctx)`, `trace(ctx)`, `data(ctx)`, `@event(ctx)` | — | context-scoped views (`%!app.call%`, `%!app.trace%`, `%!app.data%`, `%!app.event%`) |
| `Code` | `app/module/code/` | type-keyed provider registry (the runtime escape-hatch) |
| `FileSystem` | `app/type/item/path/file/filesystem/` | the disk a path's verbs reach after their gate |
| `Cache` | `app/module/cache/` | pluggable step cache (default in-memory) |
| `Debug`, `Build` | `app/module/debug/`, `app/module/build/` | mode controllers; `null` = off |
| `Mode` | — | derived: `Build` / `Test` / `Run` (never stored twice) |

`app.Start()` is the bootstrap: load identity, resolve the goal file, run it, and
show a failed run once through `/system/error/Show`. Read `Start()` → `Launch()`
in `this.cs` for the full lifecycle.

---

## 6. How `item.list` works — the value lattice and native collections

Every value in the runtime is an `item`: `app.type.item.@this`
(`PLang/app/type/item/this.cs`) is the apex, roughly C#'s `object`. `number`,
`text`, `bool`, `dict`, `list`, `path`, `datetime`, `null` — each is a
`: item.@this`. That is why a `Data<T>` slot can constrain `where T : item` and a
raw CLR value can never ride a value slot.

The native collection value is `app.type.item.list.@this<T>`
(`PLang/app/type/item/list/this.cs`) — a plang `list`, holding `Data` end to end.
This is the type behind every `.list` you navigate.

**A domain collection is one of these.** When a concept needs its own list, it
does not expose a bare `List<T>` — that would scatter its add/lock/evict
discipline across files (the "naked collection" smell). Instead it has an `X.list`
type with private backing and its own `Add`, exposed under a singular name. The
module registry is the clearest example (`PLang/app/module/this.cs`):

```csharp
// the app's module holds every loaded module as a native plang list:
public global::app.type.item.list.@this<@this> list => Root._modules!;
```

So `%!app.module.list%` → `app.module.list` → this `list<module>` → each element
a `module` you can navigate further (`app.module.file.read`). The same shape is
`app.goal.list`, `app.error.list`, `app.actor.list`, and so on — one native list
type, reused.

This is also why there are no plurals: the collection is always reached by
navigating to `.list`, never by a pluralized name.

---

## 7. Everything travels as `Data`

`Data` (`app.data.@this`, `PLang/app/data/this.cs`) is the universal carrier.
Every value circulates as `Data`: variables, file contents, HTTP responses,
errors. `Data<T>` is the typed form — a slot that carries a known value type (and
the lazy `%var%` resolution that fills it). An action's result is a `Data`; a
failure is a `Data` whose `Error` is set and `Success` is false.

Two rules you will meet constantly:

- **Data is the wire shape, not an envelope.** The serialized form *is* `Data`'s
  own shape with `[Out]`-tagged members. Don't build a parallel wrapper type to
  carry what `[JsonIgnore]` hides — tag the members instead.
- **A courier forwards `Data`; only a leaf opens it.** A handler that declares a
  typed `Data<T>` slot may read the value; a layer that merely passes `Data`
  through (variable memory, the callstack, a channel) must not crack it open.

The full model lives in `Documentation/Runtime2/data-spec.md` and
`Documentation/v0.2/good_to_know.md`; this page only places `Data` in the graph.

---

## 8. Cheat-sheet — plang → C# → source

| You have | You want | How |
|---|---|---|
| a plang step (`- read "x.txt"`) | the handler | build it, read the `.pr` `code` row: `module`+`name` → `PLang/app/module/<module>/<name>.cs` |
| a plang path (`%!app.goal.list%`) | the member | split on `.`: each segment is a lowercase property → `PLang/app/goal/list/this.cs` |
| a C# type `app.module.file.@this` | the folder | `/`-join the namespace under `PLang/`: `PLang/app/module/file/this.cs` |
| an alias (`Step`, `path`, `dict`) | the real type | look it up in `PLang/app/GlobalUsings.cs` |
| a concept's collection | the list type | `app.X.list` → `app/X/list/this.cs`, a `type.item.list.@this<X>` |

---

## The tree from here

Each doc lives in the folder it describes, beside the source:

| Doc | Covers |
|---|---|
| `PLang/app/module/start.md` | the module system: discovery, the action handler pattern, dispatch |
| `PLang/app/module/file/start.md` | the `file` module, worked example end to end |

Deeper subsystems (`type`, `data`, `goal`, `channel`, `event`, `actor`) get their
own `start.md` as this tree grows. Until then, the per-subject pages under
`Documentation/v0.2/` that are still accurate — `object_pattern_formal.md`,
`obp-smells.md`, `data-spec.md` — remain the detailed reference, and the source
itself (`PLang/app/**/this.cs`) is always the final word.

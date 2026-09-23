# Births pass — a value knows its App; the run knows who is running

Designed with Ingi 2026-09-23, rewritten after coder's prototype (`coder/proto-running-context.md`, `7d86272f6`). **Released to coder 2026-09-23.**

> **You (coder) own this.** The rules and the order are settled with Ingi. Shapes, names not fixed below, and the commit split are yours. Code below is direction; NEW marks what does not exist yet.

## Why

A run reaches the shared program and writes its context onto it. The generated parameter binding stamps the shared row (`PLang.Generators/Emission/Action/this.cs:370-377`, `data.Context = context`); `Data.Value()` stamps what it materializes (`data/this.cs:324`); context-aware values resolve `%var%` and check permissions with the context they STORE. Two actors running one goal at once read each other's variables and permissions — proven by coder's reds (System's handler read User's `%x%`; a literal path checked as System, its loader).

And a value that needs more than its name can't reach the registry (the type object has no context, `type/this.cs:593`; static fallback `:163`).

## What the prototype taught (`proto-running-context.md`)

Values that read the running context instead of a stored one turned all six reds green. It also showed four things the first design got wrong:

1. **A context is two things.** The ACTOR (permissions, variables, later culture) is whoever is running. The APP (root, type registry, formats) is where the value lives — a birth fact. Reading the App from the running context moved paths to another app's root (145 cross-app reads) and forced program nodes to need an actor (322 sites at `goal.PrPath` → `path.Parent`).
2. **A synchronous `AsyncLocal` set leaks to the caller.** Setting it in the App constructor made every later flow "run as System".
3. **TUnit doesn't flow `AsyncLocal` from a `[Before(Test)]` hook** to the test body without `TestContext.AddAsyncLocalValues()`.
4. **Most reads outside a run need only the App.** Of 666 non-cross-app sites: ~450 are `Combine`/`Parent`/`Relative`/`MimeType`/root/`Clr` (App only); ~100 are file verbs through `Authorize` (App only when in-root); ~34 are `%var%` resolution (the running actor).

## The design

### 1. The running context lives on the App — no static

An `AsyncLocal` doesn't have to be static: the value lives in the async flow, the field is only the key. The root App holds it; a child app hands up to its `Parent` (`app/this.cs:78`), so a parent-loaded goal run inside a child app (the test runner) sees the child's run.

```csharp
// app/this.cs — NEW
private readonly AsyncLocal<actor.context.@this?> _running = new();
public actor.context.@this Context
{
    get => Parent?.Context ?? _running.Value
           ?? throw new InvalidOperationException("No run in progress");   // named error — yours
    internal set { if (Parent != null) Parent.Context = value; else _running.Value = value; }
}
```

- **Only the run doors set it, and only inside async methods:** `action.Run(context)` (`goal/step/action/this.cs:159`) and `goal.Run(context)` (`goal/this.cs:~285`), first line: `context.App.Context = context;`. App boot sets System's in `Start()` (`app/this.cs:482`, async) — never in the constructor. Verify every door that runs under a context (app entry, builder, goal.call on another actor, event.on, test run) goes through `goal.Run`/`action.Run`; one that doesn't sets it the same way.
- **An empty slot is a loud error** naming the value that asked. No fallback to a stored context (the prototype's was for mapping only).
- **Thread safety is by construction:** each run sets the slot in its own flow; parallel runs each see their own; a task started inside a run inherits it; a set inside a run never flows back to the caller. Work started outside any run (timers, listener callbacks) sees whoever started it — it must come in through a run door (event.on already does).
- **C# tests:** one TUnit-wide hook sets the test app's User context into the test body (coder: `[BeforeEvery(Test)]` + `AddAsyncLocalValues()`, or what TUnit offers). Tests that call handlers directly keep working.
- The comment at `app/this.cs:494-495` ("there is no global current actor") is updated to the fact: the current context is per flow, on the App.

### 2. What stores a context today stores its App instead

Path family (`path`; `file`/`url`/`directory` delegate to it), `source`, `list`, `dict`, `computed`, `clr`, **and `Data`**. One rule, no exceptions:

```csharp
// NEW on each: the birth fact, and the running context through it
private readonly app.@this _app;                    // root, registry, formats — set at birth, non-null
public app.@this App => _app;                        // where the value lives
public actor.context.@this Context => _app.Context;  // who is running: actor, variables
```

- **Constructors keep taking a context** and keep its App — none of the ~600 creation sites change. No context-aware value has an implicit conversion in (`path` has only `string` out, `path/this.cs:333`), so every birth already has a context in hand.
- **`Context` setters die** — every post-birth stamp: `data/this.cs:113-126` setter, `:324` (`contextual.Context = _context`), `Copy`'s stamp; list/dict setters that walk and stamp elements (`list/this.cs:157-176`, `dict/this.cs:95-110`) and their element stamps (`list :134, :147, :335, :345, :495, :503`; `dict :123, :279, :291`); `path`/`source` `{ get; set; }` → getter; `file`/`url`/`directory` `set => Path.Context = value`; `_context = null!`.
- **Two questions, two members:** `x.App` = where the value lives (root, registry, formats); `x.Context` = who is running. Inside values and Data, `Context.App` reads become `App` (101 `Context.App` reads in 49 files under `PLang/app`; handlers holding their run's explicit context are fine as they are). Example: `path/file/this.Derivation.cs:20` `new @this(dir, Context)` → the derived path is born with this path's App, not the running one.
- A Data with no value (the sentinels: `NotFound`, `new Data(name)`) may have no App; its `Context` is never asked. Shape that yours.

### 3. In-root is decided by the App, before any actor

`Authorize` (`path/this.Authorize.cs:27-33`) asks for the actor first. Reorder: `IsInRoot()` (`:110-124`, reads only the App: its root, its os folder, its parent apps) returns Ok before the actor is asked. Reading anything in-root then needs no actor at all: `.pr` loads, the module catalog under `os/` (Ingi: os/system reads run as the running actor — User in the builder — and pass as in-root), the settings store (`/.db/system.sqlite`, `app/this.cs:538`). Only out-of-root paths ask the running actor.

### 4. A run never writes to the shared program — the owner hands out the run's copy

The program graph is shared by every run. With values reading the running context, a direct read of a row no longer mixes actors' variables — but `Data.Value()` still keeps its answer on the row it was called on (`data/this.cs:322-326`). A file row read once would keep the loaded content on the shared row: the next run, any actor, is served without `Authorize` and never sees the file change.

So the copy can't be optional (Ingi: "what happens when we forget Copy"):

- **`action[name]` returns the run's own copy** of the parameter (value shared, property bag copied, born with the row's App). Replaces `GetParameter`. There is no runtime door to the row itself; structure readers (build, validate, the `.pr` writer) use the `Parameter`/`Default` lists, which they own.
- The generator calls `action[name]` and never stamps (`__ResolveData`'s stamp dies). Typed slots take their view from that copy.
- `Copy()` replaces `ShallowClone`; the deep `Clone()` keeps its name.
- Step 1 as already built (the generator copies with the run's context) lands first; `action[name]` absorbs the copy once §1-2 exist.

### 5. Program nodes stay context-free

Goal, step, action, modifier and their lists store no context; they reach the registry through their App (a birth fact, already true). `goal.Path` is a path value — with §2 it answers `Parent`/`Relative` from its own App, no actor needed.

## Tests (red first, then green)

1. The six `SharedProgramTests` with coder's corrected premises (typed slot through `list.split Value`; list/dict literals written and read through the goal's own `.pr` writer/reader).
2. **Concurrent:** one goal run by System and User at the same time; each run sees its own `%x%` and its own permission checks.
3. **Literal file, two actors:** User holds the read grant, a second actor does not; the second is denied, not served from a cache. Then the file changes between runs; the second run reads the new content.
4. **Child app:** a parent-loaded goal run in a child app resolves `%x%` in the child's run and paths against the path's own App.
5. **Empty slot:** a value asked for its actor outside any run fails loud with its name.
6. **No leak to the caller:** creating an App (and a second App) leaves the caller's slot unchanged.

## Demolition — what must not survive

- Statics: the unused `IContextAccessor` / `actor.context.@thisAccessor` (`actor/context/this.cs:561-581`) and `IVariablesAccessor` / `variable.list.@thisAccessor` (`variable/list/this.cs:617-632`) — tests only; die with their tests. No static slot is added.
- `GetParameter`; `ShallowClone` (→ `Copy`); `__ResolveData`'s stamp.
- Every `Context` setter and post-birth context stamp listed in §2; `_context = null!`.
- `Context.App` reads inside values and Data (→ `App`).
- `type.@this`'s `Context` and `Promote()` with its throw; `ClrType`'s static fallback (`type/this.cs:163`); the static primitive table, `GetPrimitiveOrMime`, `ClrFromMime` (#14).
- The 34 `Type => new(...)` overrides and `new app.type.@this` outside the registry — a value asks `App.Type[...]`.
- `test.Create` as a static factory (#24); `action.ReturnTypeName` as a string (#15).

**Stays:** program nodes context-free with their App; explicit `context` parameters on constructors and `Run(context)`; implicit conversions in both directions; `ICreate`'s static members (C#-mandated); `Clone()` (deep); one context per actor and the variable overlay for forks.

## Order — each step compiles and is its own commit

1. **The slot** on the App, the run doors, boot in `Start()`, the TUnit hook; tests 5-6. Adds no value behavior yet — green on its own. (`Authorize` in-root first can land here or as its own commit; it is independent.)
2. **App as birth fact + the shared-row fix, together** — values and Data keep their App, `Context` through it; setters and stamps die; `Context.App` → `App`; `action[name]` hands out the run's copy (absorbing the step-1 tree's `Copy(context)`), no generator stamp, `Copy` / `As<T>(answer)` (internal, like `As<T>()`); tests 1-4. The no-stamp change can't land before this: without it a `%var%` source or path keeps its reader's context and resolves/authorizes as the wrong actor (coder's run of the step-1 tree: #20's `VariableHoldingAName_Selects`, two foreach, one render, two strict-image tests). The step that clears the prototype's four clusters — six suites by name after it.
3. **The type object is the registry's own** — values ask `App.Type[...]`; `Promote()`, the static fallback and primitive statics die; the menu template gets choice values (#27).
4. **#15 and #24.**

Six suites by name after each step; tests move with the code they exercise.

## OBP validation

| Surface | Check |
|---|---|
| `app.Context` | the App owns "who runs in me now"; instance `AsyncLocal`, no static; set only by the run doors |
| value/Data `App` | birth fact, private, non-null, never stamped |
| value/Data `Context` | a getter through the App — no stored copy, no setter (no late stamp, no flat copy) |
| `x.App` vs `x.Context` | two questions, two members; `Context.App` inside values would be a second way to the App |
| `action[name]` | the owner hands out the run's parameter; no verb+noun; no door to the shared row at run time |
| `Copy()` / `Clone()` | two honest operations: new wrapper over the same value / deep copy |
| `Authorize` | in-root answered by the App (the path's own), the actor only when needed |
| registry `Type[...]` | selection + lifecycle; each type object minted once |

# Births pass — every value is born with its context; the type object is the registry's own

Designed with Ingi 2026-09-23. **Draft — Ingi is not done; not sent to coder.** Numbers from coder's inventory (`coder/births-inventory.md`, `dff9f80a7`, a Roslyn pass over the real compilation).

> **You (coder) own this** once it is released. Rules and order are settled with Ingi; shapes and commit split are yours. Code below is direction; NEW marks what does not exist.

## Why

A plang value is usually born without its context, and where a context exists it is stamped on later.

- **The type object can't reach the registry.** Every item builds a bare copy of its own type (`Type => new("goal", typeof(@this))`, 34 overrides). Asked for more than its name — a choice's values, a record's fields — it reaches for the registry from inside itself (`Promote()`), has no context, and throws (`type/this.cs:593`) or falls back to a static table (`type/this.cs:163` → `GetPrimitiveOrMime`). This is why the menu can't show a choice's values.
- **Context is nullable or stamped.** `list`/`dict` declare `_context = null!` (`list/this.cs:176`, `dict/this.cs:136`); Data's context is set after birth in many places.
- **A run writes its context onto the shared program graph** — a live concurrency and security bug, below.

Ingi: "items should have their own context — it's just a reference."

## The design: an ambient context, set by the one run door

Every place that runs something under a context goes through one door — `goal.Run(context)` / a held call's `Run(context)` → `action.Run(context)`: the app entry (`app/this.cs:531`, User), the builder (`build/this.cs:115`, User), `goal.call` on another actor (`goal/call.cs:115`), `event.on` (`event/on.cs:49`, the target actor), the test runner (`test/run.cs:206`, the child app's User). In .NET an `AsyncLocal` set inside an async method is seen by everything it calls and is gone when it returns — the caller keeps its own. So:

- **`action.Run(context)` (and `goal.Run(context)`) set the ambient context at entry.** Every actor switch sets it by construction; there is no second place to forget. App boot sets it to the System context; the test app sets it for C# tests.
- **A value takes the ambient context in its constructor and stores it:** private, non-nullable, never stamped. The 237 implicit conversions and ~390 direct `new`s need no call-site change — they are born under the running action's context. Implicit conversions INTO an item therefore **stay** (their only problem was having no context).
- **A value is owned by the actor that is running when it is born.** A value computed while actor B runs belongs to B — B's variables, B's permissions.
- **Explicit `T.Create(raw, context)` stays** for the rare site that deliberately mints a value for another context (e.g. setting up a child app's bindings from the parent).
- **Program nodes** (goal, step, action, modifier, their lists) stay context-free by law: they store no context and reach the registry through their App (a birth fact). A value they hand out is born under the asker's ambient.
- The existing, unused `AsyncLocal` accessor (`actor/context/this.cs:564-581`, `IContextAccessor`) becomes the ambient's home (named in the pass).

## The shared-row rule — and the live bug it fixes (first commit)

The program graph is shared by every run. Today a run writes onto it. The generated parameter binding (`PLang.Generators/Emission/Action/this.cs:370-377`):

```csharp
var data = action?.GetParameter(name, context);   // the row inside the program graph — shared by every run
data.Context = context;                            // ← this run's context written onto the SHARED row
return data;                                       // a plain-Data slot gets the shared row itself
```

and `Data.Value()` keeps what it materializes on that same row (`data/this.cs:321-326`). Two runs of one goal under different actors at the same time: B's stamp overwrites A's, and A's handler resolves `%x%` in B's variables and checks path permissions as B. Typed slots copy the row's context at the same moment (`As<T>`, `data/this.cs:575-576`), so they share the window. (From reading the code; the first commit proves it with a test.)

**Rule: a run never writes to the shared graph.** Each run takes its own Data over the row's value, born under the running context; materialization is kept on that copy:

```csharp
var path = action["Path"].Copy();     // this run's own Data over the shared value, born under the running context
```

- `action[name]` — the action selects its parameter by name (parameters first, then defaults). **Replaces `GetParameter`** (verb+noun).
- `Copy()` — a new Data over the same value (value shared, property bag copied). **Replaces `ShallowClone`**; the deep `Clone()` keeps its name.
- `__ResolveData`'s stamp dies; plain-Data slots get a copy like typed slots.
- **Test:** one goal run concurrently under two actors; each run sees its own variables and its own permission checks.

## Demolition — what must not survive

- `__ResolveData`'s `data.Context = context` on the shared row; `GetParameter`; `ShallowClone` (→ `Copy`).
- Every post-birth context stamp on a value or Data: `clone.Context = _context` (`data/this.cs:719`), `contextual.Context = _context` (`data/this.cs:324`), list/dict `Context` setters that walk and stamp elements, `list.Row` stamping, `_context = null!`.
- `type.@this`'s `Context` and `Promote()` with its throw; `ClrType`'s static fallback (`type/this.cs:163`); the static primitive table, `GetPrimitiveOrMime`, `ClrFromMime` (open-items #14).
- The 34 `Type => new(...)` overrides (a value asks the registry through its context; a program node through its App); `new app.type.@this` outside the registry.
- `test.Create` as a static factory (#24) — the test collection mints its tests.
- `action.ReturnTypeName` as a string (#15) — the return type is the type object.

**Stays:** program nodes context-free with their birth facts; readers; the typed ask; the result doors; implicit conversions in both directions; `ICreate`'s static members (C#-mandated); `Clone()` (deep).

## Order — each step compiles and is its own commit

1. **The shared-row fix** — `action[name]`, `Copy()`, no stamp on the shared row; the two-actor concurrency test first (red), then green.
2. **The ambient** — set in `action.Run`/`goal.Run`, at boot, in the test app.
3. **Values born with the ambient** — the private non-nullable context in the value constructors; the stamps and `null!` die. The list's behaviour moves to a context-free base the program lists derive; the value list adds its context (Ingi confirmed the direction).
4. **The type object is the registry's own** — values ask through their context, program nodes through their App; `Promote()`, the static fallback and primitive statics die; the menu template gets choice values.
5. **#15 and #24.**
Six suites by name after each; tests move with the code they exercise.

## OBP validation

| Surface | Check |
|---|---|
| value context | private, non-nullable, set at birth from the ambient — no late stamp |
| the ambient | set by the one run door every execution goes through; scoped by async flow, never leaks back to a caller |
| program graph | read-only for runs; no context field; birth facts only |
| `action[name]` | the owner selects its own parameter; no verb+noun |
| `Copy()` / `Clone()` | two honest operations: new wrapper over the same value / deep copy |
| registry `Type[...]` | selection + lifecycle; each type object minted once |

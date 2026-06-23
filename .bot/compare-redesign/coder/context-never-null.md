# Context is never null — the context owns Data creation

**Branch:** compare-redesign
**Author:** codeanalyzer (interactive, with Ingi)
**Goal:** Make `actor.context.@this Context` a non-null invariant everywhere. Remove every
`Context?` / `context.@this?` declaration, every `if (context == null)` guard, every
`?.Context` / `?? _context` coalesce. Stricter, and simpler — no defensive null handling.

---

## The fundamental (read this first)

"Context is never null" is **not true today**, and not because of stray `?` noise. Values and
Data are minted **context-less at the source** in ~650 call sites, via static factories that
take no context:

- `data.@this.Ok()`, `Ok(value)`, `Ok(value, type)` (this.Result.cs:72-73)
- `data.@this.FromError(error)`, `FromError<T>(error)` (this.Result.cs:74-75)
- `data.@this.Null()`, `NotFound()`, `Uninitialized()` (this.cs:635-643)
- `data.@this<T>.Ok(...)`, `<T>.FromError(...)`, `<T>.Uninitialized(...)` (this.cs:1026-1031)

The `Context` getter papers over the resulting nulls with a `!`:
```csharp
get => _context ?? (_type as module.IContext)?.Context!;   // this.cs:116 — the lie
```
That `!` asserts non-null over a field that genuinely is null on those paths. So the real work
is **not** "strip the `?`" — it's "make it impossible to mint a Data without a context," then
the strip falls out for free.

## The mechanism (Ingi's call)

**The context owns creation.** Move the factories off `data.@this` (static, context-less) onto
`context.@this` (instance, context-stamped):

```csharp
// on actor.context.@this:
public data.@this Ok()                          => new("")                 { Context = this };
public data.@this Ok(object? value, type? t=null)=> new("", value, t)      { Context = this };
public data.@this Null(string name = "")        => new(name, type.@null…)  { Context = this };
public data.@this NotFound(string name = "")     => …                       { Context = this };
public data.@this Error(IError error)            => new("") { Error = error, Context = this };
public data.@this<T> Ok<T>(T value, type? t=null) => new("", value, t)      { Context = this };
public data.@this<T> Error<T>(IError error)      => new() { Error = error } /* Context = this */;
```

A Data now cannot exist without a context — `Context` is non-null **by construction**, not by
assertion. Call sites change `data.@this.Ok(x)` → `Context.Ok(x)` and
`data.@this.FromError(e)` → `Context.Error(e)`.

Delete the static context-less factories once migration is complete. (If a few genuinely
context-less internal mints remain — see "Two real no-context paths" — they go through the
headless context, not a static factory.)

---

## Migration map

### A. Call-site sweep (~650 sites, mostly mechanical)

- **Action handlers** — have `Context` in scope (`Action.GetParameter(name).As<T>(Context)`).
  `return data.@this.Ok(x)` → `return Context.Ok(x)`. The bulk of `module/**`.
- **Type `Convert` hooks** — receive a `context` parameter. `data.@this.Ok(x)` → `context.Ok(x)`.
  (`type/*/this.Convert.cs`, ~dozens.)
- **Engine/goal paths** (`goal/list`, `GoalCall`, `step/*`) — context is reachable via the
  goal/step/app; thread it.

Where a site truly has no context in scope, **stop and look** — that is the spot the `!` was
hiding. Thread the real context from its caller. Do not invent a fresh empty context, do not
reach into `App.System.Context`, do not add a fallback — if threading is impossible, escalate it
as a design problem (see "No headless" below).

### B. Once every Data is born with a context — strip the nullability

These become pure simplification (do them only AFTER A, or the build breaks):

- `data/this.cs:116` — getter loses the `!`: `get => _context ?? (_type as module.IContext)?.Context`
  → ideally just `_context`, with `_context` non-null from construction. Field type
  `actor.context.@this` (not `= null!`).
- **Propagation guards collapse to unconditional** (`if (_context != null) child.Context = _context`
  → `child.Context = _context`):
  - `type/list/this.cs:79,102,240,250,381,398`
  - `type/dict/this.cs:96,227,239`
  - `data/this.cs:299,343,581,755`
- **Delete the producer-bug `throw`** at `type/this.cs:729` (`type.@this has no Context`) — the
  type system now enforces what that throw was checking.
- **Drop `?.Context` / `Context?.` / `?? _context`** reads (36 + 31 sites) — straight `.Context`.
- **Flip declarations** `actor.context.@this?` → `actor.context.@this` (~70 decls): params,
  fields, the `ReadContext.Context`, `IContext.Context`, etc.
- Default params `context.@this? context = null` (25 sites): drop the `= null` default; the
  caller must pass one. (A handful that genuinely need an optional context become an overload
  pair, not a nullable param.)

## No headless, no fallback (Ingi's ruling)

**There is never a headless / context-less creation.** If code creates Data, it already holds a
real context — full stop. There is **no** sentinel context, **no** `IsHeadless` flag, and code
**must not** reach into `App.System.Context` to fabricate one. `App.System.Context` is not a
static reach-target and is not allowed to become one.

The `context.Ok()/Null()/Error()` ownership model enforces this directly: a creation site that
holds no context simply *cannot* call a factory — it stops compiling. That compile error is the
finding. Resolve it by threading the real context from the caller, never by inventing one.

The two places that today carry meaning in `null` are therefore **bugs to remove**, not carriers
to add:

1. **Wire context-less ctor + the `_context == null` branch** (`Wire.cs:211`; the parameterless
   `Wire()` and `Wire(view, sign:false)` with no context). A `Wire` is a per-actor serializer —
   it is always constructed with the actor's context (the per-actor `plang` serializer already
   does this at plang/this.cs:65/70/75; the context-less construction at plang/this.cs:84 and the
   parameterless ctor are the offenders). Thread context into those construction sites; the
   `_context == null` fail-closed branch then becomes unreachable and is deleted. The fail-closed
   behavior is preserved for free: there is no longer any way to construct a verifying `Wire`
   without an actor, so an unverifiable payload can't be unwrapped because no context-less Wire
   exists to unwrap it.

2. **Signature hashing / snapshot serialize** (Store view). Signing is an actor act — the signing
   actor's context is in scope at the call. Thread it into the Wire used for canonical hashing.
   Do not special-case "no actor."

**Coder:** if you hit a Data-creating site where threading a real context is genuinely
impossible, that is a design problem to escalate to Ingi — not a place to add a fallback. Log it;
don't silence it.

---

## Acceptance

- Grep clean: no `actor.context.@this?` / `context.@this?` declarations in production
  (test fixtures may still construct contexts, but never hold a nullable one as the invariant).
- No `Context == null` / `_context == null` / `?.Context` / `?? _context` in production.
- The `!` at the `Context` getter (this.cs:116) is gone.
- The producer-bug `throw` at type.cs:729 is deleted (or, if kept, never reachable).
- Wire transport verify still fails closed for an unverifiable Out-view payload — now because no
  context-less verifying `Wire` can be constructed at all (the `_context == null` branch is gone).
  The existing Wire fail-closed test must stay green.
- No context-less `Wire` construction remains (no parameterless ctor use, no `sign:false` without
  a context); signature hashing / snapshot serialize thread the signing actor's context.
- No production reach into `App.System.Context` as a context fallback for arbitrary code.
- A runtime `%secret%` still prints literally (no behavior change from the context sweep).
- Full C# suite green; `plang --test` from `Tests/` green (clean rebuild — stale-binary trap).

## Risks / sequencing

- **Order matters:** A (every Data born with context) must land before B (strip nullability),
  or the build is broken mid-way. Can be one PR; A then B within it.
- The ~650-site sweep is mechanical but large — the value is catching the handful of sites with
  *no* context in scope. Those are findings, not chores: each is a place the codebase was
  faking the invariant. Log each one and how it was resolved.
- Keep `data.@this<T>.Error` context-stamped too — the typed error path (`From`, ShallowClone's
  fail propagation) must not reintroduce a context-less Data.

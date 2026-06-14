# v9 — born-right read + variable-as-a-normal-type (the type builds its own value)

Session built `typeRef.Build(value)` — the type owns its value construction — and
routed the read/build paths through it, born at the declared kind in one step.
Replaces the old lift→Judge→`clr`-label tangle on every path that has a context.

## What landed (all committed + pushed)

Each step was one file, built + suite-verified, then committed:

1. **`typeRef` gets context at read** (`Wire.cs`) — the type's JsonConverter has no
   actor scope, so the entity was born context-less; Wire stamps `_context` so the
   type can reach `App.Type` to build its value.
2. **Families return born-native wrappers, not raw CLR** (`number`/`text`/`bool`/
   `date`/`datetime`/`duration`/`time`/`binary` `this.Convert.cs`). Flipped one
   first (`number`) to prove the caller-tail is empty — it is (zero broken callers,
   suites at baseline). A `.NET` edge that wants raw CLR uses `.Clr<T>()`.
3. **`type.Build(value)`** (`type/this.cs`) — the type-owned value builder: kind-aware,
   context from the entity itself. `5 + {number,int} → number(int)`, one step. Named
   `Build` not `Create` (static `Create(name)` already makes type ENTITIES).
4. **wire read → `Build`** (`Wire.cs`) — the declared-type value read calls
   `typeRef.Build(value)`; dropped the `kindDiffers` `clr` label (Build honors kind);
   kept `nameDiffers` (signing-gated). **~35 real failures fixed** (Modules −31,
   Runtime −4, …), zero real regressions.
5. **ctor → `Build`** and **`Declare` → `Build`** (`data/this.cs`) — same, when a
   context is in scope; `Judge` is the no-context fallback.
6. **`variable` became a normal type** — moved the name-parsing out of `Resolve`
   into a standard `Convert(value,kind,ctx)` hook (`variable/this.cs`); `Build` and
   `Deserialize` dropped their `IRawNameResolvable` special-case and route variable
   through the normal family dispatch (`App.Type["variable"] → variable.Convert`).
   `Resolve` is a one-line bridge for the remaining users.

## Where `Judge` stands

`Judge` is **demoted to the no-context fallback only**. Everything with a scope is
born-right via `Build`. `Judge` is NOT deletable cheaply — see below.

## The blocker (and the real next move) — DO NOT retry as plumbing

Instrumented the ctor's no-context fallback (screamer). The no-context typed
construction is **pervasive**: `variable 99, text 71, hash 50, identity 40, …
50+ types`, hundreds of constructions — test fixtures (`Data.Ok(value, type)` with
no scope), static factories, build-time entities. So "give everything context" is
**not feasible** — there's no scope to hand most of these.

**The only way to delete `Judge`:** stop building eagerly at construction. Store
the raw value + declared type and **build at the first `Value()`** — where the Data
has been wired into an actor and always has context. Then the no-context ctor case
disappears, `Build` is universal, `Judge` + its `Resolve` branch + (mostly) the
marker delete. This is the **lazy-source / born-typed-deferred refactor** (its own
run). Captured in `todos.md` 2026-06-14 "Construction builds EAGERLY".

Note (Ingi): the laziness that *matters* is already intact — file/url = `source`,
dict/list = type-on-read (Stage 11). Only cheap already-in-hand C# values build
eagerly, where deferring a constant buys nothing. So it's a purity deviation, not a
cost — but it IS the thing keeping `Judge` alive.

## Other todos surfaced (in `todos.md`)

- **`FromWireShape` is a parallel wire reader** (reaches into a parsed dict's
  `value`/`type` keys by hand — a second reconstruction next to `Wire`). Exists
  because the `.pr` loader parses to native dicts then hand-rebuilds params instead
  of `Deserialize<Goal>` through the `Wire` converter. Real fix: load `.pr` as
  `Deserialize<Goal>`; then `FromWireShape` + `WireSlot`/`IsWireShape`/`TypeFromWire`
  delete.
- The `IRawNameResolvable` marker still has indirect users (the source-gen getter
  emission, builder Render) — fully retiring it touches `PLang.Generators`.

## State

All green-relative-to-baseline; ~35 net failures cleared this session. Tree clean.
Pick up at the lazy-build refactor (the Judge-killer) when ready.

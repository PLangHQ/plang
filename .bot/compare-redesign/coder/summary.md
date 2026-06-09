# Coder — compare-redesign

## Version: v5 — Stage 2 async-door cutover (sprint mode). **Production code COMPILES.**

Ingi's call: **full sprint, build red mid-flight; go through everything, don't stop, commit/push
between.** This session drove the entire Stage 2 door cutover.

### MILESTONE (committed, pushed)
- **`PLang` + `PlangConsole` compile clean — 2130 → 0 `error CS`.** The all-or-nothing part of the
  sprint (the async `Value()` door + every Data-receiver `.Value` migration across ~120 production
  files) is structurally complete.

### The door (PLang/app/data/this.cs)
- `public virtual ValueTask<object?> Value()` — the single public async read door. No public sync
  `.Value` property.
- `internal virtual object? Materialize()` — the sync in-memory core (factory + parse-rung). Sync
  surfaces that can't `await` read through it. `DynamicData` overrides it. `ParseRaw()` = inner parse.
- `public virtual void SetValue(object?)` — the write side (was the setter).
- `Data<T>.Value()` typed async; `Peek()` = current rung, no parse (distinct from Materialize: see below).
- **OBP (Ingi caught this):** the sync read is the **verb** `Materialize()` (internal plumbing), NOT
  a `CurrentValue`/`Materialized` noun-twin of `Value` (smell #4 / verb+noun).
- **Three levels:** `Peek()` = current rung, no parse; `Materialize()` = parse (sync, no I/O);
  `await Value()` = async I/O read (Stage 3) + parse. They coincide once materialised.

### Source generator (PLang.Generators/Emission/{Property/Data,Property/Code,Action})
Emits `Peek()` for param-slot diagnostics + presence guards, `Materialize()` for `[Code]` injection.
Collapsed all 956 generated errors.

### The recipe used across ~120 files
1. Async method: `await X.Value()` (typed door returns T; clean for typed params).
2. Sync surface (serializer, predicate, lambda-in-sync-delegate, build-time, `INavigator`): `X.Materialize()`.
3. Guard-reorder where a param is guarded: `var v = await X.Value(); if (!X.Success) return X;`.
4. Write `X.Value = v` → `X.SetValue(v)`.
5. `data.Value is/as T` → `Materialize() is/as T` (sync) or `await Value() is/as T` (async).
6. `Data<bool>` truthiness: `(await X.Value())?.Value == true` / `(X.Materialize() as @bool.@this)?.Value`.
7. Typed `.Value.Member` in sync: `(X.Materialize() as ConcreteType)!.Member`.
Watch: `await` cannot go inside a sync lambda (`items.Find(i => …)`) — hoist the value to a local first.

### NOT done — the rest of the sprint
- **`PLang.Tests` — 1736 `error CS` across 169 test files.** Same `.Value` migration in test code
  (most `[Test]` are `async Task`, so `await X.Value()`). Mechanical, follows the recipe. The
  compiler error list is the worklist: `dotnet build PLang.Tests 2>&1 | grep "error CS"`.
- **Stages 3–6** (reference types `file`/`directory`/`url`, narrow-on-examination, per-type `Compare`
  → `Comparison` enum, the `data.Compare` async entry, consumers + demolition of the old mediator).
  The door is async-*shaped* but `Value()` still sync-completes everything — real async I/O reads,
  narrowing, and the typed compare are Stages 3–6. The ~140 CompareRedesign test stubs stay red
  until those land.
- Navigation chain is still **sync** (reads via `Materialize()`); making `GetChild`→`Variable.Get`
  →`Resolve` a `ValueTask` chain is the deferred nav-async sub-step.

### Next
1. Migrate `PLang.Tests` (1736 sites) with the recipe → both projects compile.
2. Stages 3–6 (per architect stage files) → land green at the 2→6 boundary; CompareRedesign stubs pass.
3. Run both suites.

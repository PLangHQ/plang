# Coder v6 — implement Stage 2.1 (make the door actually async)

Architect's `stage-2.1-materialize-to-door.md`, 3 parts. My audit caught the 3-way gap; architect
folded it in. Now building.

## Coupling finding (refines the architect's sequencing)
The architect said "A+C together, B separate." Tracing the code, **B forces C**: making
`Variable.Get`/`GetChild` async (B) forces `Data.As<T>` async (As<T> calls `Variable.Get` for `%var%`
resolution), and a sync source-gen getter cannot call an async `As<T>` — so the lazy getter (C) must
land with B. Net: **A is the separable, mechanical part; B+C are coupled** (via `Variable.Get → As<T>`).
(There is an alternative design — a *lazy-child* `GetChild` that stays sync and defers the read to
`child.Value()`, avoiding the Variable.Get/As<T> ripple entirely. Decide at B; flag if I take it.)

## Sequence
1. **Part A — handler reads → `await Value()`** (272 `.Materialize()` across 60 `app/module/` files;
   flip sync `Run()`→`async`). Self-contained, behaviour-preserving today, forward-compatible with C.
   Drives `grep .Materialize() PLang/app/module` toward zero. **← start here.**
2. **Part B+C together** — nav chain → `ValueTask` (`GetChild`/`GetChildValue`/`Variable.Get`/`Resolve`
   + the 3 navigators + await-once gate + sync-surface handling) AND the `GetParameter<T>` lazy getter +
   source-gen. C's getter emission overlaps deferred **Stage 8** (non-null optional params) — do the
   getter rewrite once; coordinate before churning it.
3. **Gate** — `grep -rn "\.Materialize()" PLang/app/module PLang/app/variable/navigator` → zero;
   `Materialize()` made `internal`/`private`.

## Baseline (don't regress)
Production (PLang+PlangConsole) compiles 0 errors. PLang.Tests compiles 0 errors but has ~130 CS8974
(silent method-group `.Value`) + ~8 DataTest runtime failures (carried from the door cutover; separate
worklist). Part A must keep production green.

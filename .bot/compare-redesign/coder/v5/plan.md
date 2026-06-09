# Coder v5 — implement the typed value model

First implementation version (v1–v4 were reviews). Architect's 7-stage plan is settled;
test-designer stubbed ~140 tests (125 C# + 15 `.goal`). Baseline: `dotnet build PLang.Tests`
clean (0 errors, 606 pre-existing warnings).

## Shape of the work

- **Stage 1** — `Comparison` enum. Standalone, nothing reads it. **Green-able alone.** ✅ done.
- **Stages 2–6** — ONE GREEN UNIT (architect's gate). The build is red until all land:
  - Stage 2: async/lazy `ValueTask Value()` door; remove public sync `.Value`; `_raw`→rung;
    `Peek()`; `.`/`!` resolver; navigation goes async (`ValueTask`); `GetParameter<T>` lazy;
    ~42 handler sites migrate `await → guard → use`; `data.Type → return _type`; no generic `ToRaw`.
  - Stage 3: `file`/`directory`/`url` reference types; narrow-on-examination; `path` demolition.
  - Stage 4: per-type `Compare` → `Comparison` (rank + coerce-into-own-kind), unify `AreEqual`/`Order`.
  - Stage 5: `data.Compare` async entry (caller-order, name→family routing).
  - Stage 6: consumers (operators/assert/two-phase sort/list ops) + boundary mapping;
    delete old mediator/`IEquatableValue`/`IOrderableValue`/`ScalarComparer`; `Compare`→`Diff`.
- **Stage 7** — full public-surface typing behind a build gate; converges after 2–6.

## Why 2–6 can't be incrementally green

The door change (sync `.Value` property → async `ValueTask Value()`) ripples through navigation,
every handler that reads a param, the source generator's lazy getters, and the compare mediator.
The architect explicitly carved 2–6 as one green unit, green only at the 2→6 boundary. Landing it
half-done leaves the build red — not a valid handoff state.

## Plan

1. Land Stage 1 (done) — commit as a clean green increment.
2. Drive Stages 2–6 as the one green unit, in dependency order, holding the build red mid-flight,
   committing only at the green boundary (or checkpointing clearly if the session ends mid-unit).
3. Stage 7 last, behind the gate.

Both C# and PLang tests are the deliverable for each stage's contract.

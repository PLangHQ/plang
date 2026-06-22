
## tester — v1 — 2026-06-22
**Target:** /workspace/plang/CLAUDE.md (new bullet under "## Running plang Tests")
**Why:** ~half of ~4,099 C# test methods hand-construct an action record and call `.Run()`/`TestAction.RunAsync`, or poke an internal type's API surface — testing handlers in a vacuum and skipping born-typing, `%var%` resolution, `Data<T>` wiring, source-gen guards, and dispatch (the things that actually break in production). The `variable` pilot showed these collapse to goal-run tests with line-for-line coverage parity, the only residue being genuine build-time floor. Without a written rule, bots and contributors keep adding the bad version. Full guide written at `Documentation/v0.2/writing-tests.md`; this is the enforcing pointer.
**Proposed change:**
```
- **Default to goal-run tests, not handler-in-a-vacuum unit tests.** PLang is tested
  the way it's used: build a `.pr` deterministically and run it through the engine —
  `Make.Goal → RealGoalLoad.ViaChannel → engine.RunGoalAsync`, asserting on observable
  state (output channel / variable / returned `Data` / raised error). Do **not**
  `new SomeAction{...}.Run()` or `TestAction.RunAsync(...)` — that bypasses build +
  dispatch and proves nothing about the author's path. Make the engine with
  `TestApp.Create("/app")` (never `new app.@this(...)` — it skips in-memory settings).
  Collapse type/value matrices into one `[Arguments]` test. A C# unit test is allowed
  ONLY on the named floor: source generator, build-time validation (`ValidateBuild`),
  and internal-state mechanisms a goal can't observe (lazy-deserialize, wire
  byte-determinism). When replacing unit tests, prove parity with a coverage line-set
  diff (lost lines ⊆ floor). Full guide: `Documentation/v0.2/writing-tests.md`.
```
**Footer:** Filed at user request ("write up instructions on how to write tests so we won't start doing the bad version"), not a reviewer-bot self-initiated proposal.

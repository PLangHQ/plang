# Tester v7 plan — runtime2-channels

## What I'm validating
Coder v7 made a 2-line conceptual change to `PLang/App/Channels/Channel/Events/this.cs`:
- **B1 fix:** dropped `static` from `_active` AsyncLocal so each `Channel.Events.@this` instance owns its own recursion-guard slot.
- **L1 fix:** `Enter` now copies the parent set into a fresh HashSet and `Releaser.Dispose` restores the parent reference (copy-on-write).

Coder admits: *"the failure modes they prevent ... have no current callsite to exercise"* — no new tests added.

That's exactly the false-green shape I hunt: a fix landed without a test that would fail if the fix were reverted.

## Steps
1. Confirm baseline matches coder's claim (C# 2760/2760, plang 205 + 6 fixture-fails).
2. Run the deletion test on B1 and L1: revert each fix, see if any test fails.
3. Read every Stage8 test for behaviours that touch the recursion guard.
4. Decide: are these untestable hazards, or did the coder skip writing tests that should exist?
5. Write findings + verdict.

## Hypotheses going in
- **B1 (static `_active`):** would need a test with two `Channel.Events.@this` instances where binding ids collide / cross-channel re-entry happens in same async flow. None of the Stage8 tests does this. Likely catchable; should be tested.
- **L1 (mutable inherited set):** would need a binding handler that fans out parallel child writes (`Task.WhenAll(ch.WriteAsync(...), ch.WriteAsync(...))`). None of the Stage8 tests does this. Catchable in principle, but only matters once a real callsite uses parallel fan-out from inside a binding.

## Output
- `result.md` — full findings with deletion-test outcomes
- `verdict.json` — pass/fail
- `test-report.json` at branch root
- update `summary.md`

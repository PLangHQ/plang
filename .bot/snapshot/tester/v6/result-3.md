# tester v6, third pass — snapshot: PASS

Tip `c1e1a9248` (includes `d4c2abe81`, the identity-race fixes `818ffe898`/`517972c1b`, marker clean-up `828662b18`, auditor fixes). Built with `./dev.sh build` first.

## Counts
- C#: 5647 total (Modules 1483, Types 1037, Wire 641, Data 871, Generator 191, Runtime 1424), **0 red**.
- plang: 312 total, 311 pass, 1 skipped (`DurationPartsAndCompare`), 0 stale. **5 of 5 full runs and 5 of 5 plan-folder runs (6/6) green**; `git status` clean afterwards (no `.pr` rewritten, no leftover marker).
- Baseline: `coder/baseline-failures.txt` now exists; no red name at all, so none new.

## Finding A (flaky ResumeTakesInTheBinding): closed
Was red in 6 of 8 folder runs; now 0 of 10 parallel runs. The cause named by the architect (identity race) matches the symptom: the binding missing in the resumed sub app only when other tests ran in the process.

## Mutations (announced, reverted, `git status` clean)
- `call/action/this.cs:77` `at + 1` → `at`: red (`SnapshotResume_InsideACall_…`; FreshAppGoesOn and EditedStopPoint both fail).
- same line → `return result`: red (`SnapshotResume_InsideACall_…`).
- `call/this.cs:75` `[Snapshot]` removed from `Event`: red (`CallStack_RoundTrip_TheEventRunningInAFrame_…`, `ARunSavedInsideABoundCall_…`, EventComesBack).
Not re-run: coverage, the ReadOnly source in EditLeavesTheProgram (unchanged from pass 2).

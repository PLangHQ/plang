# tester v5 on event-action (tip d07ab731f) — PASS

Pattern first: one door / the value answers (12); a birth fact, not a search (11).

## Run (clean tree, build first)
- C#: Modules 1500, Types 1042, Wire 641, Data 877 (+12 probes), Generator 191, Runtime 1460 — green. plang `--test`: 323 total, 322 pass, 0 fail, 0 stale, 1 skipped (baseline). Tree clean. No new red names.
- Probes P1–P7 pass again (variable events in one store, nested, case, remove, first ask, 50 concurrent).
- Bytes per set unchanged from v4: nothing bound 3,464 / 3,681 (1,000 names); watch hit 4,424; miss 3,809; type event 4,296. (Times were taken under load, 2-3x the quiet ones; read bytes.)

## Mutations (reverted, tree clean)
1. `Remove` drops a list whether or not empty (89): `AnEventBoundDuringARun_AfterAnUnbind_RunsAtThatRunsAfter` red; `ListTests` and `SystemEventsTests` stay green — one test guards it, which is enough but only one.
2. `call.goal.Leave` no longer counts return levels down (stage 7): `TaskTests.AReturnOfTwoLevels_EndsItsGoalAndItsCaller_TheNextGoesOnWithTheValue` red; the older `GoalReturnTests` (6) stay green, so they do not exercise the Leave path any more (they test the depth set, not the count down) — fine, the new test covers it.

## Notes (minor, not blocking)
- Pattern 11: `Moments(variable)` still scans `_events` on a set with no list (carried from v4).
- Stages 8 (`goal.App` birth fact, `Find(name, caller)`) and the Sub option are covered by CallFindsItsAppTests and OnEventTests/SavedBindingsTests; I read them but did not mutate them.

Verdict: PASS.

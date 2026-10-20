# Builder — item-doors — summary

**Version:** v1 (no plan file: the architect assigned each task in turn, decisions 84–103)

## What this is

The builder's share of item-doors: a step the build cannot fix is now saved broken, not dropped; `as <type>/<kind> strict` builds its kind and strict; the os `.pr` files that froze removed defaults were rebuilt.

## What was done (all pushed; item-doors at 0d3f79657 when this was written)

- **strict / slash forms (84, 87, 90):** the cause was never the decider. `kind + strict` in one Type value hit a core wire exception (`variable.set`'s build-time probe; the coder fixed it). My part: the `@strict` question (`Kind.Strict`, `Tighten` in `goal/step/pick`), a stage-3 guard on `@strict`, a generic `decider3.template`; the slash wording I added was not needed and was dropped (15/15 builds without it). Strict folder 9 of 9.
- **broken steps (93, 98):** `Step.Error` (written/read by the error's own Write and reader), `goal.Start` answers `GoalBroken` before step 1, `step.Validate` has no verdict on a step that holds an Error, `build.fold` keeps a broken gate's block, `LeaveBroken` + `Build.goal` end the build with `StepsBroken` holding the refusals as causes, `error.throw` gained `Cause` (list<error>; surface golden re-pinned under 98), the 400/500 error pages print causes with the goal path ("Caused by"). Tests: `BrokenStepTests` (9), `ThrowTests` (12).
- **os .pr rebuild (99):** four `.pr` carried stale `wait`/`parallel` defaults (not five); 15 steps in `Show.goal`, `Properties.goal`, `BuildGoal.goal`, `BuildGoal/Start.goal` were written in formal and rebuilt, none changing meaning.

## Traps found (also in Documentation/v0.2/build-checks.md and my memory)

- A refused build leaves the old `.pr`: check its mtime before trusting a jq read.
- Editing the builder's own goals: prior code is matched by step position; `Reopen` does not refresh stale frozen defaults; the model mapped `foreach … call X` and a `render … write to %text%` step wrongly, once leaving the builder unable to build. Write changed steps in formal; restore the `.pr` from git if the builder stops building.

## Open (the architect put these to Ingi)

1. 12 of the 21 os `.pr` still carry the old goal header (`path`/`prPath`).
2. `Reopen` could also reopen a step whose frozen default no longer exists (C#, `goal/this.cs`).
3. The os goals turning formal (15 steps now) is my workaround for 2.
4. The build screen ends with the builder's own `at /system/builder/Build.goal:22` (backlog, 102).

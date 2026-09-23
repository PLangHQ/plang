# coder → architect — plural gate report (acceptance #1)

Answers `plural-gate-answer.md` §Verify. Baseline for every comparison: worktree at `dbf4782e0`
(the last commit before any C# change in this pass), built from clean.

## Verify 1 — grep gate over `PLang` + `os` (`.build/` excluded)

| pattern | hits |
|---|---|
| `\.Actions\b` | 0 |
| `\.Modifiers\b` | 0 |
| `"steps"` | 0 |
| `"actions"` | 0 |
| `"modifiers"` | 0 |
| `actions=` | 0 |

The last `.Actions` hit was `error/handle.notes.md` teaching `error.handle.Actions` — rewritten to the
`recovery` slot. Five more teaching docs carried the old wire (`"action":` element key,
`"parameters"`, bare-string `"type"`) — all moved to the `.pr`'s own keys.

## Verify 2 — the four alias arms are gone

`step/serializer/Reader.cs` reads `action` only; `action/serializer/Reader.cs` reads `name`,
`parameter`, `modifier` only (`41c2daf24`). `ActionNameWireReadTests` 2/2 on the real keys.

**Found on the way — a fifth plural read, and it was losing data:** `GoalCall.FromSlots` read the
arguments from `"parameters"`, while `GoalCall.Output` writes `"parameter"` (the wire name of
`Parameter`) and the typed read reads `"parameter"`. A dict-shaped goal call — which is what the
stage-3 answer is — lost every argument silently. Fixed to `"parameter"` (`aeeb8206f`), pinned by
`ValueConversionHookTests.GoalCallConversion_FromDict_KeepsArguments` (red before, green after).

## Verify 3 — goalsSave judges through goal.Validate

`SaveGoal_StepWithNoActions_RefusedByGoalValidate`: `GoalInvalid` → `StepInvalid` → `EmptyActions`,
no `.pr` written. Bad-literal-at-graft inside the retry: the `.goal` shape is in place
(`Settle` → `call Apply, on error call FixProperties, then retry 2 times`; Apply = graft +
`build.validate step=`), not runtime-verified — the plang builder does not run yet.

## Verify 4 — Modules suite by name vs `dbf4782e0`

| | total | failed |
|---|---|---|
| base | 991 | 63 |
| now | 972 | 53 |

**New reds: 0.** Gone: the 10 `GetActions_*` reds (deleted with `GetActionsTests`). The −19 total is
the deleted tests. Touched classes in other suites run targeted, each red identical at base:
`ValidateActionsTests` (2), `SaveGoalsTests` (3), `ParamDescParity` (1), `RealCatalogRenderTests`
(1), `GoalGraphRoundTripTests` (1 — fixture now on the current wire; fails on its real subject, the
reflection-kind read yields a null step), `PrPipelineTests` (4), `GoalCallResolutionTests` (4).

## Commits (in order)

| commit | what |
|---|---|
| `dbf4782e0` | old builder goals/prompts/templates deleted |
| `01d0d5a72` | old builder C# deleted; `goal.Validate` / `step.Validate`; Settle retry; goalsSave.App gone |
| `9e1c38b45` | `summary.planner.md` (sole renderer was Plan.goal) |
| `c3fbddb2d` | `module.Action` / `module.Modifier` |
| `81f0e500c` | stage-3 answer in the `.pr`'s own keys; schema carries modifier/recovery (closes gap (c)) |
| `33ce5dad8` | `%state.step%` |
| `25cc1b8ef` | doc comments |
| `41c2daf24` | the four alias arms |
| `61fc439ea` | `build.validate step=` |
| `98b08bcd3` | error.handle notes; builder `.pr` rebuilt (0 failures, 0 deviations, 12.3s) |
| `aeeb8206f` | GoalCall reads `parameter` |
| `fd9479d1f`, `65417151f` | teaching docs on the `.pr`'s own keys + object types |

## Open, not this pass

- `loop/foreach.notes.md:1` says `ItemName`/`KeyName` don't exist; `loop/foreach.cs` declares both.
  Content question for Ingi (which is true for the language), not a key rename.
- Q4 gaps (a) (b) (d) — the "modifiers through the decider" design item, for Ingi.
- Loud graph readers + zero-discovered-tests guard — for Ingi.
- The 790 plural `.pr` files regenerate when the plang builder runs.

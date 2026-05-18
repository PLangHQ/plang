# Baseline — before any rename work

Captured on `app-lowercase` branch, off `runtime2` commit `199b4997`.

## C# tests (`dotnet run --project PLang.Tests`)

- Total: **2752**
- Passed: **2752**
- Failed: **0**
- Skipped: 0
- Duration: ~17s

Note: validator probe spam ("File not found: .build/validator.pr" etc.) is expected — those are intentional negative-path test fixtures, not failures.

## PLang tests (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`)

- Pass: **203**
- Fail: **6** (all pre-existing fixtures named to be failures):
  - `_fixtures_sensitive/sensitivefail.fixture.goal` (×4 occurrences)
  - `_fixtures_fail/failsvar.fixture.goal` (×2 occurrences)
- Many "Untested branches" reported in conditional tests — pre-existing coverage gaps, not regressions.

Net: the 6 `[Fail]` entries are intentional negative fixtures. Any new failure after rename = my regression.

## One stderr stack to note

```
Failed to deserialize List`1 to this: The JSON value could not be converted to
  App.Goals.Goal.Steps.Step.Actions.Action.this.
  Path: $[0] | LineNumber: 0 | BytePositionInLine: 3.
```

Surfaces during test discovery. Pre-existing on `runtime2`. Note the namespace `App.Goals.Goal.Steps.Step.Actions.Action.this` — this is a serialized type discriminator. After rename this exact string changes to `app.goals.goal.steps.step.actions.action.this`. Anyone reading old serialized blobs would break — but Ingi confirmed `.pr.json` doesn't carry C# type names. This error is from somewhere else; flag for investigation if it changes shape after rename.

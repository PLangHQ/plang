# codeanalyzer — runtime2-callstack — v3

## What this is

Verifies coder/v2 cleanup commit `be77dc12` against v2's findings (5 v1
carry-forward + 3 new MINORs + 2 NITs).

## Outcome

**PASS.** Branch ready to merge.

- 8/8 MINORs verified fixed in code (App.Run + Goal.RunAsync overflow catch,
  Step.Context restore in finally, Errors._current instance AsyncLocal,
  Diffs.Add lock, ThreadStatic→AsyncLocal, DictionaryNavigator Count
  consistency, SerializableCallStack deletion).
- 2 rejections accepted with notes:
  - **Tags flag**: doc reframed as exporter hint instead of enforcing the
    gate. Reasonable — explicit user `- tag` shouldn't be silenced by
    default flags.
  - **AsCanonical infra-resolve**: coder closed the asymmetry the opposite
    direction (removed `SubstitutePrimitive`'s carve-out via `c4381135`
    instead of adding one to AsCanonical). Either direction works; pinned
    by `AsT_PlainDataTarget_DictWithInfraVar_ResolvesAtCanonicalWalk`.
    (Coder's "stale state" framing is slightly off — the v2 snapshot did
    show the asymmetry — but the fix is fine.)
- 1 NIT deferral accepted: Goal frame `Action.Step = Steps[0]` keeps the
  pin because `ContainsGoal` cycle detection reads `Action.Step.Goal.PrPath`;
  comment now explains "anchor, not currently-running step".

## Files written

- `.bot/runtime2-callstack/codeanalyzer/v3/result.md`
- `.bot/runtime2-callstack/codeanalyzer/v3/summary.md`
- `.bot/runtime2-callstack/codeanalyzer/v3/verdict.json`

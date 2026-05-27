# Coder handoff: enrich test run output in results.json

**From:** builder
**Branch:** `fix-stepvartypes-incremental`
**Severity:** medium — features the webui needs to surface meaningful test-run detail. Two related additions; ship together.

## What's wanted

After every `plang --test` run, two extra fields per run in `Tests/.test/results.json`:

```json
{
  "runs": [
    {
      "path": "Modules/Foo/Bar.test.goal",
      "entryGoal": "TestBar",
      "status": "Pass",
      "durationMs": 50.8,
      ...existing fields stay...

      "output": "<captured stdout from output.write etc.>",

      "stepDurations": [
        { "stepIndex": 0, "stepText": "set %x% = 1", "ms": 0.2 },
        { "stepIndex": 1, "stepText": "if %x% equals 1, call Foo", "ms": 12.4 },
        { "stepIndex": 2, "stepText": "assert %x% equals 1", "ms": 0.1 }
      ]
    }
  ]
}
```

## Why each one

**`output`** — the test's `output.write` / `write out` stream. Captured today on `Run.CapturedOutput` (`PLang/app/tester/Run.cs:28`) but never serialised. Without it the webui can't show what a failing test printed before it failed, which is half the value of seeing the failure at all.

**`stepDurations`** — per-step wall-clock for the test run, so the webui can show "this step took 12ms" alongside the source. Useful for finding slow steps, useful for confirming a test actually exercised what we think it did. Just top-level steps for now (the goal's own step list); sub-goal call durations roll up under the calling step rather than being broken out. If a step's `stepText` would be a long line, truncating is fine — webui can fall back to looking up the source step by index.

## How to verify

- Run `plang --test` against `Tests/Simple` (or any small directory). Open the resulting `Tests/.test/results.json`. Each run entry should carry both new fields.
- `output` should be non-empty for tests that call `write out` / `output.write`; empty string is fine for tests that don't.
- `stepDurations.length` should equal the goal's source step count. Sums close to `durationMs` (within a few ms — there's per-test overhead that's not step-attributable).
- For tests that fail mid-way, `stepDurations` should include the steps that ran and stop at the failing step (don't fabricate zero-duration entries for unreached steps).

## Scope notes

- Existing field names stay exactly as they are (`durationMs`, `error`, `expected`, etc.) — the webui already keys off them.
- No JSON schema versioning needed; the webui treats missing fields as null and renders accordingly.
- Don't change the console rendering of `report.cs` — that's a separate audience.
- Sub-goal per-step breakdown is deliberately out of scope. If a slow test needs deeper investigation, that's a follow-up handoff.

Once shipped, I'll wire the webui display.

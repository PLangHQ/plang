# Tester summary — builder-ergonomics

**Version:** v1
**Verdict:** FAIL

## What this is

The `builder-ergonomics` branch worked through a 7-priority friction list
(`user-feedback.md`) from a coder who'd been authoring PLang test goals. There's no
`coder/` folder — work was tracked via `tester-handoff.md` instead. Shipped:

- **C#**: foundational-channel snapshot mechanism removed, replaced by a per-channel
  `IsExecuting` recursion guard (the fix for the `ChannelNotFound`-in-sub-goals bug).
  P4 root-cause-first error chaining in `Conversion.cs`.
- **PLang**: builder output routed through a named `"builder"` goal-channel;
  confidence-per-step (P6) in the four LLM passes; `list<T>` schemas; always-on
  EmitSummary; Plan.llm verb rule. New reproduction goal `UnknownVerb.test.goal`.

## What was done (this version)

Clean rebuild (stale-binary protocol). Ran both suites. Mutation-tested the channel
guard. Read the committed `UnknownVerb` `.pr`. Five findings; verdict FAIL.

- **C# suite:** 3376/3377 — 1 fail.
- **PLang suite:** 233/234 — 1 fail.

### Findings (full detail: `v1/result.md`, `../test-report.json`)

1. **CRITICAL false-green — `UnknownVerb.test.goal`.** The P6 reference reproduction
   asserts nothing, its committed `.pr` mis-compiled `compress …` to `variable.set`
   (the exact silent verb-drop P6 exists to catch), and confidence is `null` on every
   step. Two clean rebuilds hard-failed (`ValidationErrors` / `BuilderPlannerFailed`) —
   the documented `⚠ planner VeryLow` warnings never appear.
2. **MAJOR flaky — `Normalize_PropertyLookupCache…` (the 1 C# failure).** Asserts on
   `Tagged.CacheSize`, a process-wide static dict, under parallel TUnit. Passes alone,
   fails in suite.
3. **MAJOR missing-coverage — channel recursion guard.** Mutation-confirmed: deleting
   `InvokeGoal`'s `_executing.Value = true` leaves all 7 channel tests green. The guard
   is never armed through a real goal write — only via reflection flips.
4. **MINOR (env) — `UploadFile.test.goal` (the 1 PLang failure).** 502 from an external
   endpoint; not a code regression.
5. **MINOR — interactive `y/n/a` prompt leaks into the C# run** (a permission-gate test
   not redirecting stdin).

### Code example — the false green (finding 1)

```
# Tests/ConfidenceCheck/UnknownVerb.test.goal  (reports [Pass])
- set %original% = "hello world"
- compress %original%, write to %archived%   # committed .pr: action = variable.set (!)
- write out %archived%                        # no assert anywhere
```

The "unknown-verb reproduction" ships bytecode where the unknown verb was silently
dropped, runs green, and verifies nothing.

## What to do next

Hand back to coder. The two fixes that close the verdict:
- Make `UnknownVerb` a real assertion (read trace → assert step-1 confidence VeryLow, or
  assert the rendered warning line); stop committing a `variable.set` `.pr` for it.
- Fix the cache test to assert behaviour it owns (specific key reuse), not the global
  `CacheSize`, and stop the global `ClearCacheForTests()` from flaking parallel neighbours.
- Add an end-to-end channel-guard test (goal-channel body writes its own name → expect
  `ChannelNotFound`, not recursion).

(`UploadFile` 502 is environmental; the `y/n/a` leak is hygiene — both worth a separate ticket.)

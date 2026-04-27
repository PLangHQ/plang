# v1 Summary — Test-Designer: PLang Test Module Test Suite

## What this is

The test contract for the v1 PLang test module (architect plan `.bot/runtime2-test-module/architect/v1/plan.md`). This module replaces the current `system/test.goal` (which uses PLang `foreach` and silently skips 86 of 143 tests) with a proper runner: per-test App isolation, semaphore-throttled parallel execution, per-test timeout, AfterAction-subscribed coverage, JUnit XML output.

Test-designer session only — no production code. All test bodies are `Assert.Fail("Not implemented")` (C#) or `throw "not implemented"` (PLang). The coder implements the production code to make the tests pass; these signatures define "done."

## What was done

112 tests across 33 files, split into 14 approved batches. Each batch was reviewed with Ingi before writing. Every file compiles; the PLang.Tests project builds clean.

**Breakdown:**

| # | Batch | C# | PLang | Area |
|---|---|---|---|---|
| 1 | `Testing` class | 10 | 0 | IsEnabled, results/coverage/currentTest, config fields, JSON apply |
| 2 | `Results` + `Coverage` | 10 | 0 | aggregation, thread-safe Add, merge |
| 3 | `[RequiresCapability]` | 5 | 0 | reflection, real-handler smoke |
| 4 | `Variables.Snapshot()` | 8 | 0 | scope chain, by-ref, thread-safety |
| 5 | `AssertionError.Variables` | 5 | 1 | captures on fail only, all-handlers smoke |
| 6 | `AfterAction` widening | 6 | 0 | (Action, Data) payload, no back-compat |
| 7 | `condition.if` branch_index | 6 | 2 | 0=true, 1=false, chain position, error skip |
| 8 | `test.discover` | 10 | 2 | recursive walk, freshness via Goal.Hash, tag extraction, sub-goal traversal, filters |
| 9 | `test.tag` | 4 | 2 | CurrentTest write, outside-test no-op, accumulate |
| 10 | `test.run` | 10 | 4 | isolation, parallel, timeout, AfterAction sub, merge, stale/skipped preserved |
| 11 | `test.report` | 8 | 3 | .test/ per-goal-dir, format=json default / junit, XML escape, coverage tables, failure render |
| 12 | `system/test.goal` | 0 | 4 | E2E rewrite verification, no-foreach regression |
| 13 | Per-test metadata | 4 | 0 | builder version + Goal.Hash capture, drift flag |
| 14 | Edge/security | 7 | 1 | negative/zero config, recursive run, path traversal, ANSI strip, nested Data, Icelandic names |
| **Total** | | **93** | **19** | **112** |

**Key design decisions (with Ingi's input):**

- **No separate `Config` class** — config fields live directly on `Testing`.
- **Results is split from Testing** (own test file); Coverage likewise. Two different things.
- **No backward-compat** for AfterAction widening — all subscribers updated same commit.
- **`.test/` output is relative to the discovery path**, not CWD. Test at `Tests/Foo/Bar.test.goal` writes `Tests/Foo/.test/`.
- **Format is single-select**: `--test={"format":"json"|"junit"}`, default `json`. Console always writes.
- **Freshness uses existing `Goal.Hash`** (SHA-256 over Name + Steps.Text), not raw file bytes. Comment-only edits don't trigger stale.
- **Auto-tags from `[RequiresCapability]` traverse sub-goals** via static `goal.call` chains.
- **`test.tag` outside a test is a no-op**, not an error. Lets shared goals work in production.
- **Timeout is a distinct status** (`TestStatus.Timeout` ≠ Fail). Separates "hung" from "assertion wrong" in CI dashboards.
- **Filtered-out tests are `Skipped`, not removed** — CI visibility.
- **`exclude` wins over `include`** on tag conflict.
- **Failure is data, not exception** — `test.run` never throws for child-test failures. Keeps the main loop parallel-safe.

## Code example — the pattern

One C# test (from `TestingClassTests.cs`):

```csharp
// Quiet mode by default — output.write is captured and shown only on failure.
[Test]
public async Task NewInstance_Verbose_DefaultIsFalse()
{
    await Task.Yield();
    Assert.Fail("Not implemented");
}
```

One PLang test (from `Tests/TestModule/Condition/`):

```
TestConditionIfRecordsBranchIndexElseBranch
/ Verifies condition.if publishes the else-branch index when nothing else matches.
/ Setup: set %x% = 0. Then: if %x% > 10 do A; else if %x% > 5 do B; else do C.
/ After the if step, inspect %__data__!branchIndex% — should be 2 (else position).
- throw "not implemented"
```

Both carry a comment explaining the intent — the comment is the spec. The coder reads them to understand what the test must verify.

## Files modified

- `PLang.Tests/App/Testing/*.cs` — 14 test files (new).
- `Tests/TestModule/**/*.goal` — 19 PLang test goals (new), organized by feature subfolder.
- `.bot/runtime2-test-module/test-designer/v1/plan.md` — the test plan, 14 batches.
- `.bot/runtime2-test-module/test-designer/v1/verdict.json` — PASS verdict.
- `.bot/runtime2-test-module/report.json` — session record.

Bot summary file at `.bot/runtime2-test-module/test-designer/summary.md` updated with v1 entry.

## What to do next

Hand off to **coder**. The coder implements:

1. `Testing` class upgrade (flat config fields, Results, Coverage, CurrentTest).
2. `Results`, `Coverage`, `TestRun` types.
3. `[RequiresCapability]` attribute + application to http.* and llm.*.
4. `Variables.Snapshot()` method.
5. `AssertionError.Variables` field + capture wiring in all 9 assert handlers.
6. `AfterAction` payload widening to `(Action, Data)` — update all call sites.
7. `condition.if` branch_index publishing.
8. `test.discover`, `test.tag`, `test.run`, `test.report` handlers.
9. `system/test.goal` rewrite (no foreach).
10. Per-test metadata (builder version + Goal.Hash in TestRun, surfaced in results.json).

Every test file I wrote lists its spec. When all 112 tests pass, the module is done.

## Five open questions from plan.md §9 — all resolved

Recorded in plan.md §9 as "Decisions (resolved with Ingi 2026-04-17)":

1. Results/Coverage split: **separate files**.
2. `CurrentTest` thread-safety: **skip** — per-App isolation covers it.
3. JUnit XML variant: **Gradle-compatible superset**.
4. Builder version: **version of the builder that produced the .pr** — surfaced for drift notification.
5. Capability auto-tagging: **traverses sub-goals**.

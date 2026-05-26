# tester — fix-stepvartypes-incremental

**Version:** v1
**Verdict:** FAIL

## What this is

Branch `fix-stepvartypes-incremental` carries the `%var%` slot-description fix plus surrounding scaffolding: OpenAI cost/cached-token accounting, per-step Stopwatch timings, output capture via BeforeWrite event binding, and a Run-entity property rename `CapturedOutput → Output`. The bulk of the diff came in via merge from `purge-systemio-from-actions` (already tested upstream); only 8 C# files are unique to this branch. Codeanalyzer v1 and v2 PASSed.

## What was done

1. Pulled `origin/fix-stepvartypes-incremental`. No `coder/` folder exists → no baseline-tests.md → cannot diff PLang failures against pre-coder state. Flagged as process gap.
2. Clean-rebuilt PlangConsole (succeeded). Built PLang.Tests → **FAILED** with 2 compile errors: `ReportActionTests.cs:56` and `EdgeCaseTests.cs:129` reference the now-renamed `run.CapturedOutput` property. The whole C# suite is unrunnable.
3. Ran `plang --test` from `Tests/` against the (working) PlangConsole binary: 195 pass / 22 fail / 217 total. Without a baseline I cannot label any as a coder regression vs pre-existing; many failures are "File not found: .../.build/*.pr" suggesting stale artifacts.
4. Test-quality pass over the 8 branch-unique C# files identified nine findings (2 critical missing-coverage, 1 critical build-break, 4 major missing-coverage / weak-assertion, 2 minor).

**Key findings:**

- **F1 (critical, build-break):** Property rename `CapturedOutput → Output` not propagated to callers — `PLang.Tests` won't compile. Codeanalyzer's PASS verdicts were issued without a green build.
- **F2 (critical):** New BeforeWrite event-binding output-capture path in `test/run.cs` has zero tests.
- **F3 (critical):** New per-step Stopwatch timings (`Timings`, `Timing`, the `IsEntryGoalStep` filter) have zero tests.
- **F4 (major):** OpenAI cost math has only a `Cost == null` test — no positive arithmetic, no cached-tokens, no longest-prefix-wins, no multi-call accumulation. Any of the three rate multipliers could be swapped without test failure.
- **F6 (major, weak-assertion):** The headline `%var%` slot-description fix (dropped trailing `"string"`) is only verified by `.Contains("%var%")` which passes both old and new strings — a deletion test would not fail. Comment in `GetActionsTests.cs:72` still describes the OLD format.

Full list in `.bot/fix-stepvartypes-incremental/test-report.json`. Detailed plan in `v1/plan.md`.

## Code example — what a real assertion would look like

The headline `%var%` fix is "covered" by:

```csharp
// GetActionsTests.cs:78 — passes for BOTH "%var%" and "%var% string"
await Assert.That(nameParam!.Value!.ToString()).Contains("%var%");
```

Should be:

```csharp
// equality test — catches a revert
await Assert.That(nameParam!.Value!.ToString()).IsEqualTo("%var%");
// or, when defaults can append " = ..."
await Assert.That(nameParam!.Value!.ToString()).DoesNotContain("string");
```

## Process gaps

- No `coder/` folder on this branch → no `summary.md`, no `plan.md`, no `baseline-tests.md`. PLang test failures cannot be triaged as regressions vs pre-existing.
- Codeanalyzer (v1 and v2) reviewed the renamed property without checking whether dependents had been updated; PASS verdicts were issued against an uncompilable test project.

## Next

```
run.ps1 coder stepvartypes-incremental "Fix build break (Run.CapturedOutput → Output not propagated to ReportActionTests.cs:56 and EdgeCaseTests.cs:129); add tests for output capture, per-step Timings, OpenAI cost math (positive arithmetic + CachedTokens + longest-prefix), tighten GetActionsTests %var% slot assertion to IsEqualTo; write coder/v<N>/baseline-tests.md so PLang failures are triageable." -b fix-stepvartypes-incremental
```

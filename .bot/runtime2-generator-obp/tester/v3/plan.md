# Tester v3 plan — verify v3 closes v2's 7 toothlessness findings honestly

## Context

Coder v3 set out to close 7 findings from codeanalyzer v2, **all of which were about test toothlessness**, not production bugs. The whole point of v3 is the regression-prevention layer. So my job is unusually meta: I'm hunting false greens in tests that were specifically rewritten to no longer be false greens. Codeanalyzer v3 verified every claim and gave PASS + 1 NIT. I will not rubber-stamp; I will independently apply the deletion test to the 7 production fixes and to the new tests' assertions.

This is the first tester pass on this branch, so the bot starts cold against `v3` (matching coder/codeanalyzer's version per workflow).

## What changed in v3 (delta vs v2)

Production code (3 small deltas):
- `PLang/App/Data/this.cs` — `ResolveDepthLimit = 32` constant + depth-bound clause in `AsT_Impl` for expanding-cycle protection (~6 lines).
- `PLang.Generators/this.cs` — two `WithTrackingName(...)` calls + two constants (`ActionInfoTrackingName`, `ActionInfoFilteredTrackingName`); orchestrator now uses `LinePositionSpan(StartLine/StartChar, EndLine/EndChar)` instead of `(line, char + 1)`.
- `PLang.Generators/Discovery/this.cs` — `DiagnosticInfo` widened from 5 fields to 7 (`StartLine, StartChar, EndLine, EndChar` instead of `Line, Character`).

Test code (5 files):
- `PLang.Tests/Generator/NoDeadEmissionTests.cs` — heuristic rewritten + cross-file scan + convention pin + 5 heuristic regression tests.
- `PLang.Tests/Generator/IncrementalCacheTests.cs` — 2 `CSharpGeneratorDriver` cache-hit tests + ~190 lines of MinimalSource bootstrap.
- `PLang.Tests/Generator/GeneratorValidationTests.cs` — `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier`.
- `PLang.Tests/App/AppRunScaffoldingTests.cs` — `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate`.
- `PLang.Tests/App/DataTests/DataAsTResolutionTests.cs` — 3 cycle tests strengthened with value assertions + 1 expanding-cycle test added.
- `PLang.Tests/PLang.Tests.csproj` — `Microsoft.CodeAnalysis.CSharp 4.13.0` added as a regular `PackageReference`.

## My approach — false-green hunting per finding

### Test quality scrutiny (the core job)

For each new/modified test, apply:

1. **Deletion test on the production fix.** Mentally remove the fix code; would the test now fail?
2. **Mutation test on the assertion.** Could the assertion still pass if the production code did the wrong thing in a sibling way?
3. **Setup-vs-execution check.** Is the test exercising the actual production path or a mock-shaped substitute?

Specifically per finding:

| # | Test under scrutiny | What I look for |
|---|---|---|
| #39 | `PipelineCache_Rerun…` + `PipelineCache_ActionClassChanged…` | Driver actually runs to the `ActionInfoFiltered` step (predicate fires on MinimalSource? `path:` is set? generator emits to those tracking names?). The `ContainsKey` assertion has to actually be executable — if the predicate skips MinimalSource, `TrackedSteps` won't contain the key and the test fails for the wrong reason. Also: the negative-space test asserts `anyModified || anyNew` — what if a re-run produces only `Cached` because the syntax tree is identical at the cached level? |
| #40 | `NoGeneratedHandlerDeclaresAnUnreadPrivateField` + 5 heuristic | The 5 heuristic tests guard `HasReadOf` synthetically — good. But the live test `NoGeneratedHandlerDeclaresAnUnreadPrivateField` only fails if the live tree has a dead field. With the live tree clean, the test passes vacuously today. The synthetic regression tests are the load-bearing layer — verify they're independent of the live tree. Also: `PrivateFieldDecl` regex with `[\w\.<>\?,\s:@\[\]]+?` — does it actually match the live generated fields? If the regex matches zero lines, the test passes vacuously regardless of the live tree's cleanliness. |
| #40 (Pattern B) | `NoGeneratedHandlerExposesUnusedPublicMethod` | `LoadAllCallableSources()` reads ~thousands of files and concatenates into one string. The regex `\bMethodName\s*\(` matches anywhere in that mass — including inside string literals and comments. False positive (test green when method is dead) is the risk: if a method name happens to appear in a comment or a sql/regex/log string anywhere in the codebase, it counts as "called." Verify whether any of the actual generated public method names are common enough to be falsely matched. |
| #41 | `AsT_ExpandingCycle_DepthBoundReturnsGracefully` | `Contains('%')` — if the depth-bound returns the **fully resolved** string before the limit trips, the assertion would fail. Also: does `await Assert.That(result).IsNotNull()` plus `Value).IsNotNull()` plus `Contains('%')` actually catch a regression where the bound is removed? Without bound the test would StackOverflow → process crash → test-runner reports FAIL. So the test is honest if the bound is removed; I need to check the test would also fail for *wrong* depth-bound behavior (e.g., bound at 1 returns "%a%" immediately). |
| #42 | `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate` | The test pre-cancels CTS, pushes via `PushCancellation`, then asserts `step.RunAsync` throws `OperationCanceledException`. Verify (a) `context.CancellationToken` actually reflects `PushCancellation`'s token and (b) the test's step has at least one Action so the foreach is entered. With zero actions, `ThrowIfCancellationRequested` is never called. |
| #43 | 3 cycle tests upgraded to value assertions | Specific values asserted — good. But verify the values asserted match the *current* implementation's behavior, not the implementation as the coder mis-remembered. If the production code returns `null` instead of `"%a%"`, do the assertions fail? Trace through the code path. |
| #44 | `EveryGeneratedPrivateFieldUsesDoubleUnderscorePrefix` | Convention pin. Does it actually fail if a single non-`__` field is emitted? Verify by mental mutation. |
| #45 | `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier` | The test creates a `BadHandler` partial class with `public partial int RawIntProperty`. Does the predicate match on `[App.modules.Action]` attribute? The MinimalSource doesn't define `App.modules.IAction` etc — just the `ActionAttribute`. Verify the diagnostic actually fires. Asserts `spanWidth > 1` — what if the fallback path uses `Location.None` (which has span 0)? Then `spanWidth = 0`, test fails for the *right* reason. But what if the fallback is `Location.Create` with start==end? Then `spanWidth = 0`, same outcome. The test is safe-by-construction; verify the diagnostic actually triggers. |

### Also triage the "pre-existing" PLang failures

Coder reports 169 pass / 48 fail. Coder claims all 48 are pre-existing infrastructure failures. I'll spot-check 2-3 of them to confirm they're not v3-induced regressions.

### Coverage analysis

Run coverage on the changed production files (`PLang/App/Data/this.cs`, `PLang.Generators/this.cs`, `PLang.Generators/Discovery/this.cs`) and report whether the new lines are reached by the new tests.

## Validation steps

1. Build — `dotnet build PLang.sln -c Debug`.
2. Run full TUnit suite — `dotnet run --project PLang.Tests` and capture totals.
3. Run coverage (Coverlet or equivalent) on PLang.Tests against changed files. Save to `coverage.json`.
4. Apply deletion test to each finding's fix mentally and where feasible empirically (e.g., comment out the depth-bound, run the cycle test, expect StackOverflow).
5. Spot-check 2-3 PLang test failures.
6. Write `test-report.json` + `verdict.json` + `summary.md`.

## Verdict criteria

- **Approved** if all 7 findings have honest tests AND no critical false greens found AND no regressions surfaced beyond what's claimed pre-existing.
- **Needs-fixes** if any new test is structurally vacuous, any production fix lacks a backing test, or new regressions surfaced.

## Out of scope

- Code-style review of the production fixes (codeanalyzer's domain).
- Triaging all 48 PLang failures end-to-end (claimed pre-existing; spot-check only).
- Re-relitigating closed v1/v2 findings.

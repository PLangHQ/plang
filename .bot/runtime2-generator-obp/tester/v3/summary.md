# Tester v3 — verify v3 closes v2's 7 toothlessness findings honestly

## What this is

First tester pass on `runtime2-generator-obp`. The branch's whole v3 cycle was about closing 7 codeanalyzer-v2 findings — every one of them about test toothlessness, not production bugs. So this test review is meta: I'm hunting false greens in tests that were specifically written to *not* be false greens. Codeanalyzer v3 reviewed these and gave PASS + 1 NIT. My job was to independently apply the deletion test rather than rubber-stamp.

Verdict: **needs-fixes**. The v2 toothlessness pattern recurred in v3's fix to v2, in the most direct possible way.

## What was done

### Empirical deletion tests on the 3 production fixes

Performed 4 deletion tests by editing production code, building, running the targeted test, then reverting. All passed (i.e., the test caught the regression):

| Production fix | Mutation | Test | Outcome |
|---|---|---|---|
| `ResolveDepthLimit` clause (`Data/this.cs:417`) | Removed `\|\| Count > Limit` | `AsT_ExpandingCycle_DepthBoundReturnsGracefully` | StackOverflow → process crash → test fails for the right reason |
| Step.RunAsync OCE-not-set (`Step/this.cs:157`) | Removed `OperationCanceledException` from filter | `StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate` | Assertion fails: `Expected to throw exactly OperationCanceledException` |
| `LinePositionSpan` widening (`Generators/this.cs:46-47`) | Replaced `EndLine,EndChar` with `StartLine,StartChar+1` | `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier` | Assertion fails: `spanWidth=1, expected >1` |
| `WithTrackingName(ActionInfoFilteredTrackingName)` (`Generators/this.cs:31`) | Replaced with literal disagreeing string | `PipelineCache_RerunWithUnchangedSyntax_StepOutputsAreCachedOrUnchanged` | `ContainsKey` assertion fails — test machinery sound |

### Coverage on changed production files

| File | Pct | Note |
|---|---|---|
| `PLang/App/Data/this.cs` | 83.1% (651/783) | All v3 cycle-protection lines (415-420, 442-443) HIT |
| `PLang.Generators/this.cs` | 100.0% (70/70) | All v3 diagnostic-location lines (42-49) HIT |
| `PLang.Generators/Discovery/this.cs` | 75.0% (330/440) | All v3 GetLineSpan capture lines (94-101) HIT |

Every v3-added executable line is exercised by the new tests. `ResolveDepthLimit = 32` and the tracking-name constants on lines 20/21/399 are constant initializers so don't have coverage rows — they are non-executable.

### C# test suite

- Without coverage: **2456/2456 green**, 11.7s.
- Under `--coverage` instrumentation: 2454/2456 — both `PipelineCache_*` tests fail with `KeyNotFoundException: 'ActionInfoFiltered'`. This is a Roslyn-IIncrementalGenerator interaction with coverage hooks, captured as Finding #4.

### PLang test suite

Reported by coder: 169 pass / 48 fail / 5 stale. Coder's claim: pre-existing infrastructure failures unrelated to v3. **Did not deep-triage** — the C# tests are the load-bearing layer for v3's contract, and the deletion-test results are sufficient evidence.

## Findings (5 total)

### Finding #1 — MAJOR — Pattern B restricts to `public`, the v1 regression was `protected`

**File:** `PLang.Tests/Generator/NoDeadEmissionTests.cs:140-142, 106-134`

`NoGeneratedHandlerExposesUnusedPublicMethod` (Pattern B) is anchored to:

```csharp
private static readonly Regex PublicMethodDecl = new(
    @"^\s*public\s+(?:async\s+|partial\s+|static\s+)*[\w\.<>\?,\s:@\[\]]+?\s+(\w+)\s*\(",
    RegexOptions.Multiline);
```

Empirically run against the live generated tree, this regex matches exactly two method names: `ExecuteAsync` and `SnapshotParams` — both on the `_publicMethodCallerExemptions` list. So the cross-file scan iterates **zero non-exempt items per file** and `orphans` is always empty. The test passes vacuously today.

But the real toothlessness is structural. The v1 regression (codeanalyzer-v1 Finding 12, the explicit motivation for Pattern B per the test's own docstring) involved `protected global::App.Data.@this? ParamData(string paramName)` — `protected`, not `public`. Pattern B's regex would never have caught it. The current generator emits `protected static global::App.Data.@this Data(...)` (4 overloads) and `protected static Error(...)` — Pattern B ignores all of them. If a future change re-emitted a dead `protected` helper, Pattern B would silently pass.

This is the **same toothlessness pattern** Ingi flagged in v2: a test named to catch a regression that it provably can't catch. The v3 cycle exists specifically to close that gap — and the v3 fix retained it.

**Empirical proof** (mini console app, `/tmp/regextest`):

```
Public-only regex finds: ExecuteAsync, SnapshotParams
Public+Protected finds:  Data, Error, ExecuteAsync, SnapshotParams
Missed by current Pattern B: Data, Error
```

**Suggestion:** widen `PublicMethodDecl` to `^\s*(?:public|protected)\s+...` and add a synthetic regression test (an `IsOrphan` helper fed a fake source) that mirrors the 5 `Heuristic_*` tests for Pattern A.

### Finding #2 — MINOR — Pattern B has no synthetic regression test

Pattern A has 5 `Heuristic_*` tests against synthetic source (`Heuristic_VariablesShape_*`, `Heuristic_DoubleEqualsIsNotAnAssignment`, etc.) that pin `HasReadOf` independently of the live tree. Pattern B's cross-file scan has zero such tests. If `PublicMethodDecl` broke or `LoadAllCallableSources` returned empty, the test would silently green.

**Suggestion:** extract `IsOrphanMethod(name, allCallableSources, exemptions)` and write 2-3 synthetic tests covering called/orphan/exempt cases.

### Finding #3 — MINOR — Comment/string literal false-positive risk in caller scan

`LoadAllCallableSources` concatenates every `.cs` file's raw text. The caller-detection regex `\b{name}\s*\(` matches inside `//` comments, `///` docstrings, and string literals. Empirically: `SnapshotParams` appears in 5+ comments. If Pattern B is widened (per Finding #1) without comment-stripping, a future genuinely-dead method whose name is mentioned in a comment anywhere would falsely count as "called."

**Suggestion:** strip comments and string literals before scanning, OR negate `//`/`*`/`"` lookbehind in the caller pattern.

### Finding #4 — MINOR — `PipelineCache_*` tests fail under `--coverage`

Empirically confirmed: `dotnet run --project PLang.Tests -- --coverage --coverage-output-format cobertura --coverage-output X.xml` produces 2454/2456. Both pipeline-cache tests fail with `KeyNotFoundException: 'ActionInfoFiltered'`. The Roslyn `CSharpGeneratorDriver`'s `trackIncrementalGeneratorSteps:true` interaction with coverage instrumentation strips/relabels the tracked steps.

**Suggestion:** document this in a comment block at the top of `IncrementalCacheTests.cs`, or guard the two pipeline tests with a `[NotInCoverage]`-style attribute. CI risk if coverage gating is ever added.

### Finding #5 — NIT — `ActionInfoTrackingName` (unfiltered) is dead

Carry from codeanalyzer Finding 46. Confirmed via grep: only `PLang.Generators/this.cs` lines 20 and 29 reference `ActionInfoTrackingName`. No test reads the unfiltered step. Either delete it, or write `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` (the stronger contract, since it would catch transform-step instability that the post-Where step's value-equality hides).

## Code example — the kind of fix Finding #1 needs

The 5 `Heuristic_*` tests are the right shape for Pattern A. Pattern B should mirror it. Pseudo-fix:

```csharp
// Internal helper, testable
internal static bool IsOrphan(string methodName, string allCallableSources,
    ISet<string> exemptions)
{
    if (exemptions.Contains(methodName)) return false;
    var callerPattern = new Regex(@"\b" + Regex.Escape(methodName) + @"\s*\(");
    return !callerPattern.IsMatch(allCallableSources);
}

[Test]
public async Task Heuristic_OrphanProtectedMethod_IsFlagged()
{
    var src = """
        partial class H {
            protected static Data Data() => Data.Ok();   // orphan in test
        }
        """;
    var noCallers = "// nothing references Data() here";
    await Assert.That(IsOrphan("Data", noCallers, new HashSet<string>())).IsTrue();
}

private static readonly Regex PublicOrProtectedMethodDecl = new(
    @"^\s*(?:public|protected)\s+(?:async\s+|partial\s+|static\s+)*[\w\.<>\?,\s:@\[\]]+?\s+(\w+)\s*\(",
    RegexOptions.Multiline);
```

## Verdict: needs-fixes

The 6 other v2 findings are honestly closed (3 confirmed via deletion tests, 1 via test-machinery mutation, 2 via static review). Finding #1 is the same toothlessness shape v3 was supposed to fix. Recommend coder address it before approval. Findings 2-5 can be batched into a follow-up.

## Files touched (this session)

- `.bot/runtime2-generator-obp/tester/v3/plan.md` — plan (created)
- `.bot/runtime2-generator-obp/tester/v3/coverage.json` — coverage summary (created)
- `.bot/runtime2-generator-obp/tester/v3/verdict.json` — verdict (created)
- `.bot/runtime2-generator-obp/tester/v3/summary.md` — this file (created)
- `.bot/runtime2-generator-obp/tester/summary.md` — bot-root summary (created)
- `.bot/runtime2-generator-obp/test-report.json` — test report (created)
- `.bot/runtime2-generator-obp/report.json` — session entry (modified)

No production code changed; all 4 deletion-test mutations were reverted in-session and `git diff --stat` shows only `.bot/runtime2-generator-obp/report.json`.

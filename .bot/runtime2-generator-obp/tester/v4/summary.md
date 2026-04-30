# Tester v4 — verify coder v4 honestly closes tester v3's 5 findings

## What this is

Second tester pass on `runtime2-generator-obp`. Coder v4 responded to v3's `needs-fixes` verdict — specifically the MAJOR `Pattern B regex restricted to public` toothlessness that recurred from v2 (the same shape Ingi originally flagged). v4 added 10 tests across 2 test files. **No production code modified.**

Verdict: **approved**. All 5 findings are honestly closed. 4 empirical deletion tests demonstrate that each new test's claimed contract is actually pinned.

## What was done

### Pass 1 — structural inspection of all 10 new tests

Walked through each new test in the plan, asking: "If this test's claimed contract broke, would the test fail?" All 10 are structurally honest. The vacuous-pass risk on the unfiltered cache-hit test is guarded by `Assert.That(infoSteps.Length).IsGreaterThan(0)`.

### Pass 2 — empirical deletion tests on the 4 contracts

| # | Mutation | Expected to fail | Actual outcome |
|---|---|---|---|
| A | Narrow `PublicOrProtectedMethodDecl` regex back to `public`-only | `PublicOrProtectedMethodDecl_MatchesProtectedDeclaration` | ✓ Failed: `Expected to be 1 but found 0` |
| B | `IsOrphanMethod` always returns `false` | `Heuristic_OrphanProtectedMethod_IsFlagged` (and 3 `Strip_*` tests, which depend on `IsOrphanMethod`) | ✓ Failed: 4 failures, all asserting `IsOrphanMethod(...)` returned `false` instead of `true` |
| C | `StripCommentsAndStrings` is identity (`return src;`) | All 3 `Strip_*` tests | ✓ Failed: 3 strip tests + `Heuristic_OrphanProtectedMethod_IsFlagged` (whose input contains `ParamData()` in a comment that the test assumes will be stripped) |
| D | `WithTrackingName(ActionInfoTrackingName)` → literal `"DisagreeingActionInfoName"` | `PipelineCache_RerunWithUnchangedSyntax_UnfilteredStepOutputsAreCachedOrUnchanged` | ✓ Failed: `Expected to contain key ActionInfo but key ActionInfo not found` |

Every mutation produced exactly the expected failure. Every revert restored 2466/2466 green.

### Pass 3 — secondary toothlessness investigation

**Live tree verification.** Pattern B widened to `public|protected` now matches `protected static Data()` (3 overloads) and `protected static Error()` in every generated handler. Coder claimed user partials provide real callers. Verified:

- `grep -rE '\bData\s*\(' PLang/App/modules/ --include="*.cs" -l` → 10+ files (count, unique, foreach, get, any, range, etc.)
- `grep -rE '\bError\s*\(' PLang/App/modules/ --include="*.cs" -l` → 10+ files (get, add, range, sort, mock/verify, error/throw, app/run, etc.)

Confirmed by the live cross-file test passing — `Data` and `Error` are not flagged as orphans because their user-partial callers register through the (correctly stripped) caller corpus.

**Cross-product (regex + IsOrphanMethod) end-to-end.** The live `NoGeneratedHandlerExposesUnusedPublicOrProtectedMethod` test exercises both helpers in their integrated form and is currently green — so the cross-product is empirically pinned by the live test on the actual generated tree. The synthetic `Heuristic_*` tests pin each helper independently. Architecture mirrors Pattern A precedent.

**Test totals.**
- Without coverage: **2466/2466 green**, 11.7s. (was 2456 in v3 → +10 v4 tests)
- Under `--coverage`: not re-run; v3 confirmed 2 PipelineCache tests fail under instrumentation. v4 added a 3rd PipelineCache test (`UnfilteredStepOutputsAreCachedOrUnchanged`) which presumably also fails the same way — coder's caveat comment block at the top of `IncrementalCacheTests.cs` correctly documents this.

## Findings

**0 critical, 0 major, 0 minor.**

One observation, NOT a needs-fixes finding:

### Observation — Strip integration into `LoadAllCallableSources` is not directly pinned

The 3 `Strip_*` tests pin `StripCommentsAndStrings` as a unit. The live cross-file test calls `LoadAllCallableSources()`, which calls `StripCommentsAndStrings()`. If a future change removed the strip call from `LoadAllCallableSources` (without changing the helper), no test would fail — `Data(`/`Error(` have plenty of real callers in user partials, so the live test stays green; the strip tests stay green because they call the helper directly. A genuinely-dead method whose name appeared only in a comment would silently pass.

**Why I'm not filing it as a finding:**
- Pattern A has the identical architecture gap — `HasReadOf` is pinned by 5 `Heuristic_*` tests but the integration into `NoGeneratedHandlerDeclaresAnUnreadPrivateField` is not pinned. Precedent-aligned.
- The helper is the actual contract surface; the live test is a corpus-scan harness.
- Closing it would require either (a) refactoring `LoadAllCallableSources` to take a source-reading function so a synthetic test can drive it, or (b) introducing a known-only-in-comment string in the corpus and asserting it stays orphan. Both are larger refactors than the gap warrants.

If `Pattern A`'s analogous gap were ever to be filed as a finding, this one should join it; otherwise both stay observation-only.

## Verdict: approved

All 5 v3 findings closed. All new contracts deletion-tested. The v3 toothlessness pattern (regex looks correct in isolation but cannot match the regression it claims to catch) is gone — the v4 regex assertions explicitly pin `protected` matching with the actual `ParamData()` shape as input.

The coder's own deletion tests in `coder/v4/summary.md` (regex narrow → `MatchesProtectedDeclaration` fails; tracking-name swap → `UnfilteredStepOutputs` test fails) match my independently-run results on the same mutations. Tests A, B, C, D from my plan all produced expected failures.

## Code example — what an honest pinning looks like

The v3 pattern was: regex documented as "catches `protected ParamData()`" with `^\s*public\s+...` body. The v4 pattern is two-part:

```csharp
// Helper pinned independently of the live tree.
private static readonly Regex PublicOrProtectedMethodDecl = new(
    @"^\s*(?:public|protected)\s+...",
    RegexOptions.Multiline);

// And a test that actually feeds the regression shape and asserts it matches.
[Test]
public async Task PublicOrProtectedMethodDecl_MatchesProtectedDeclaration()
{
    var src = "    protected static Data ParamData(string name) => Data.Ok();\n";
    var matches = PublicOrProtectedMethodDecl.Matches(src);
    await Assert.That(matches.Count).IsEqualTo(1);
    await Assert.That(matches[0].Groups[1].Value).IsEqualTo("ParamData");
}
```

The deletion test confirms the assertion bites: narrow `(?:public|protected)` to `(?:public)` and this test fails. That is the kind of empirical demonstration the v2/v3 cycle lacked.

## For v3 review (already in v4_review_summary equivalent above)

5 findings filed in v3, 5 honestly closed in v4. Coder's empirical deletion tests in v4 summary match my independent verification. Architectural learning: when a test claims to catch a specific regression shape, the test must exhibit the regression shape as input and assert the helper bites. Both Pattern A's `Heuristic_*` tests and Pattern B's `Heuristic_*` + regex assertions now follow this pattern.

## Files touched (this session)

- `.bot/runtime2-generator-obp/tester/v4/plan.md` — plan (created)
- `.bot/runtime2-generator-obp/tester/v4/coverage.json` — coverage summary (created)
- `.bot/runtime2-generator-obp/tester/v4/verdict.json` — verdict (created)
- `.bot/runtime2-generator-obp/tester/v4/summary.md` — this file (created)
- `.bot/runtime2-generator-obp/tester/summary.md` — bot-root summary (modified, append v4 line)
- `.bot/runtime2-generator-obp/test-report.json` — test report (modified)
- `.bot/runtime2-generator-obp/report.json` — session entry (modified)

No production code or test code changed. All 4 deletion-test mutations were reverted in-session and `git diff --stat` shows only `.bot/runtime2-generator-obp/report.json` modified at end.

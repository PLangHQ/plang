# Tester — runtime2-generator-obp

## v3 (2026-04-30) — needs-fixes

First tester pass on this branch. Verified coder-v3's closures of codeanalyzer-v2's 7 toothlessness findings via 4 empirical deletion tests on the production fixes (depth-bound, Step OCE, diagnostic span, pipeline-cache machinery — all caught the regression as expected). C# tests 2456/2456 green; coverage on changed files 75-100% with all v3-added executable lines HIT.

Surfaced 1 MAJOR + 3 MINOR + 1 NIT findings. The MAJOR (Finding #1) is that `NoGeneratedHandlerExposesUnusedPublicMethod` (Pattern B) restricts to `public` methods, but the v1 `__paramData/ParamData()` regression it claims to catch involved a `protected` method — same toothlessness pattern Ingi flagged in v2 has recurred in v3. The current generator emits `protected Data()` and `Error()` helpers that Pattern B doesn't even examine. See [v3/summary.md](v3/summary.md).

## v4 (2026-04-30) — approved

Coder v4 honestly closed all 5 v3 findings. Pattern B's regex widened to `public|protected`, `IsOrphanMethod` and `StripCommentsAndStrings` extracted as testable helpers, 9 new tests added in `NoDeadEmissionTests.cs` (3 regex assertions + 3 IsOrphanMethod synthetic + 3 strip tests) plus 1 new test in `IncrementalCacheTests.cs` (pre-Where cache-hit, activating the previously-dead `ActionInfoTrackingName` constant). 4 empirical deletion tests confirmed each new contract bites: regex narrow → `MatchesProtectedDeclaration` fails; `IsOrphanMethod` neutered → 4 synthetic tests fail; `StripCommentsAndStrings` identity → strip tests fail; tracking-name swap → unfiltered cache-hit test fails. C# 2466/2466 green (was 2456; +10 v4 tests). No production code modified. Recommended security analyst next. See [v4/summary.md](v4/summary.md).

## v7 (2026-05-01) — approved

Cumulative catch-up review of coder v5 + v6 + v7 (last tester pass was v4). v7 is the big delta — Variable + IRawNameResolvable migration replacing `[VariableName]`. 5 deletion tests on load-bearing surfaces: IRawNameResolvable carve-out → 35 tests fail (strongly load-bearing); Variable implicit operator → 49+ tests fail (critical); Variable.ToString → 1 test (lightly pinned); WasPercentWrapped → 3 tests pin value but no consumer; variable.set CopyProperties → 0 C# but 10 plang TestReport tests fail. C# 2550/2550 green and plang 166/166 green — both better than the coder/v7 summary's mid-flight counts because commit 4 closed the 4 ListAdd stubs and 10 TestReport regressions. 4 minor findings: missing C# coverage for variable.set CopyProperties (only plang integration pins it); IRawNameResolvable contract trap untested (codeanalyzer NIT-4 too); PLNG001_VariableNameAttribute_NowReportsDiagnostic misnamed (overlaps RawScalar coverage); WasPercentWrapped value-only pinning. None block. Recommend security analyst next. See [v7/summary.md](v7/summary.md).

# Tester — runtime2-generator-obp

## v3 (2026-04-30) — needs-fixes

First tester pass on this branch. Verified coder-v3's closures of codeanalyzer-v2's 7 toothlessness findings via 4 empirical deletion tests on the production fixes (depth-bound, Step OCE, diagnostic span, pipeline-cache machinery — all caught the regression as expected). C# tests 2456/2456 green; coverage on changed files 75-100% with all v3-added executable lines HIT.

Surfaced 1 MAJOR + 3 MINOR + 1 NIT findings. The MAJOR (Finding #1) is that `NoGeneratedHandlerExposesUnusedPublicMethod` (Pattern B) restricts to `public` methods, but the v1 `__paramData/ParamData()` regression it claims to catch involved a `protected` method — same toothlessness pattern Ingi flagged in v2 has recurred in v3. The current generator emits `protected Data()` and `Error()` helpers that Pattern B doesn't even examine. See [v3/summary.md](v3/summary.md).

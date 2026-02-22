# Data Envelope Architecture — Tester Summary

**v1** — Analyzed coder's 62 Engine.Types tests. All 1229 C# tests pass. Found 1 critical issue: Kind(null)/Mime(null) crash with unhandled ArgumentNullException. Found 1 false green: Name() test uses Uri (works) but misses HashSet<string> which produces invalid "hashset`1". BuilderNames() and ComplexSchemas() have zero tests. Verdict: needs-fixes. See [v1/summary.md](v1/summary.md).

**v2** — Analyzed Phase 2 tests (23 new, 1324 total pass). Found a real bug: Add() doesn't update _allKinds/_mimeToKind, so KindOf() can't find dynamically added kinds — verified experimentally. v1 findings carry forward unfixed with escalated impact. Methods.cs context stamping untested. Verdict: needs-fixes. See [v2/summary.md](v2/summary.md).

**v3** — Verified all v2 critical/major fixes (Add→KindOf, null guards, backtick, BuilderNames/ComplexSchemas). Analyzed Phase 3 (Data partial class split + Out view). 1349 tests pass. Clean structural refactor with no behavior change. 3 minor findings only. Verdict: approved for auditor. See [v3/summary.md](v3/summary.md).

**v4** — Analyzed Phase 4 envelope pipeline (17 new tests, 1366 total pass). Found critical code bug: Decompress() has no exception handling around GZip decompression and JSON deserialization — corrupt data at the transport boundary crashes instead of returning Error. All Decompress error paths untested. Round-trip tests are solid but standalone Decompress test has weak assertions. Verdict: needs-fixes. See [v4/summary.md](v4/summary.md).

**v5** — Verified all v4 fixes (1372 tests pass). Decompress exception handling added with distinct catch for InvalidDataException and JsonException. All 4 error paths tested. Multi-level nesting verified. Properties-not-preserved documented. 1 minor carry-forward only. Verdict: approved for auditor. See [v5/summary.md](v5/summary.md).

**v6** — Verified all 8 auditor fixes (1372 tests pass, 0 regressions). Code fixes are structurally correct. Deep analysis surfaced: (1) CRITICAL: zip bomb protection untested, (2) MAJOR: thread safety untested, (3) MAJOR: Data.Merge() used in production Actions loop but zero tests. One finding (RehydrateNestedData false positives) withdrawn after architecture clarification — the heuristic is correct by design since the pipeline always operates on Data layers. 1 critical, 2 major, 3 minor. Verdict: needs-fixes. See [v6/summary.md](v6/summary.md).

**v7** — Reviewed coder v5 security hardening (1384 tests pass). v6 zip bomb, Merge, StatusCode findings resolved. New findings: (1) MAJOR: ResolveVariablesInPath cycle detection completely untested, (2) MAJOR: GetChild depth error not tested through MemoryStack. Verdict: needs-fixes. See [v7/summary.md](v7/summary.md).

**v8** — Reviewed coder v5 PLang tests + coder v6 cross-concern fixes (1390 tests pass). Decimal precision, Clone context, fromJson error key, GetChild integration all verified. v7 finding #2 resolved, #1 (cycle detection) still open since v7. One new observation: `JsonDepthExceeded` catch in fromJson is unreachable (STJ limit < ours). Blocking: cycle detection still zero coverage. Verdict: needs-fixes. See [v8/summary.md](v8/summary.md).

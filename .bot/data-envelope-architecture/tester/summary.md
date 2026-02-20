# Data Envelope Architecture — Tester Summary

**v1** — Analyzed coder's 62 Engine.Types tests. All 1229 C# tests pass. Found 1 critical issue: Kind(null)/Mime(null) crash with unhandled ArgumentNullException. Found 1 false green: Name() test uses Uri (works) but misses HashSet<string> which produces invalid "hashset`1". BuilderNames() and ComplexSchemas() have zero tests. Verdict: needs-fixes. See [v1/summary.md](v1/summary.md).

**v2** — Analyzed Phase 2 tests (23 new, 1324 total pass). Found a real bug: Add() doesn't update _allKinds/_mimeToKind, so KindOf() can't find dynamically added kinds — verified experimentally. v1 findings carry forward unfixed with escalated impact. Methods.cs context stamping untested. Verdict: needs-fixes. See [v2/summary.md](v2/summary.md).

**v3** — Verified all v2 critical/major fixes (Add→KindOf, null guards, backtick, BuilderNames/ComplexSchemas). Analyzed Phase 3 (Data partial class split + Out view). 1349 tests pass. Clean structural refactor with no behavior change. 3 minor findings only. Verdict: approved for auditor. See [v3/summary.md](v3/summary.md).

**v4** — Analyzed Phase 4 envelope pipeline (17 new tests, 1366 total pass). Found critical code bug: Decompress() has no exception handling around GZip decompression and JSON deserialization — corrupt data at the transport boundary crashes instead of returning Error. All Decompress error paths untested. Round-trip tests are solid but standalone Decompress test has weak assertions. Verdict: needs-fixes. See [v4/summary.md](v4/summary.md).

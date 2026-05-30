# Tester v2 review of coder v2 — summary

Verdict: **fail** — "6 of 7 v1 findings honestly fixed; the new F1 Promote() throw is deletion-confirmed uncovered and its named test verifies the opposite."

3 findings in v3 scope:

- **F1-RESIDUAL (MAJOR)** — `type.@this.Promote()` throws fail-loud on unstamped non-primitive reads, but no test exercises it. Deletion of the throw → 3694/3694 still pass. Worse, `DataType_OnUnstampedData_ThrowsHard_NoSilentFallback` reads `ClrType` (which never calls Promote) and asserts a silent null return — the name says "throws hard" but the body verifies the opposite.

- **N1 (MINOR)** — `GetPrimitiveOrMime_ExternalFallbackCallSites_AllRemoved` uses `if (!File.Exists(path)) continue;` — if the BaseDirectory→repo-root relative walk breaks (CI layout change), both files are skipped and the test asserts nothing → green vacuously.

- **N2 (MINOR)** — F7's `Capture.goal` does `set %captured% = "hello from channel accessor"` — a hardcoded literal identical to the written value. If the channel delivered the wrong data, `%captured%` would still equal the expected string. Reachability is pinned; value-flow is not.

F8-RESIDUAL is a process flag (no `baseline-tests.md`); not a code finding.

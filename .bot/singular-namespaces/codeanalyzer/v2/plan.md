# Code Analyzer v2 — plan

**Task:** "pull, coder fixed, also do fresh eye look" — re-review the coder's response to v1 findings, plus a fresh-eye pass.

## Approach
1. Pull; diff `6b4c3e5e0..HEAD` (coder's fix commit + generator commit).
2. Re-verify each v1 finding: door symmetry (#1), Promote re-walk (#2), dead enumerators (#3).
3. Fresh eye: the *new* code the fix introduced — the memoized `_catalogByName` cache, the `Promote()` rewire, the generator nullable-partial change.
4. Ground every claim empirically: clean rebuild of PLang.Tests, run the fold-contract tests in isolation AND in the full suite, quantify any flakiness (stale-binary trap discipline).

## Findings → v2/report.md. Verdict → v2/verdict.json.

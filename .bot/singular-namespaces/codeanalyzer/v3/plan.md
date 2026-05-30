# Code Analyzer v3 — plan

**Task:** "pull, new from coder" — re-review the coder's fix for v2 findings.

## Approach
1. Pull; diff `7b9a5ee8a..HEAD` (the two v2-fix commits).
2. Read the collision-resolution change; reason about safety (recursion / Context-null during Rank).
3. Verify the v2 false-positive: does `goal/list.All` actually have callers? (broaden the grep my v2 used).
4. Clean rebuild (source changed → stale-binary discipline); run the formerly-flaky test **in isolation ×8** (the exact failure mode), plus Types namespace + full suite.
5. Verdict on whether the non-determinism is gone.

## Findings → v3/report.md. Verdict → v3/verdict.json.

# Tester v2 Plan — Re-run Tests After Coder v3 Fixes

## Context
Coder v3 fixed 3 PLang test failures found by tester v1:
1. **ErrorRetryOnly** — Changed ambiguous `set %var% = %var% + 1` to `add 1 to %var%, write to %var%` to eliminate builder non-determinism
2. **ErrorGoalFirst** — Removed retry count assertion (GoalFirst skips retries when error goal succeeds — correct behavior)
3. **ConditionCompound** — Reverted .build/ to runtime2 baseline (pre-existing NullRef, not a regression)

## Plan
1. Build C# solution (PLang + PLang.Tests)
2. Run all 1511 C# tests
3. Run all 68 PLang tests
4. Compare results against v1 (65 pass / 3 fail)
5. Verify the 3 previously-failing tests individually
6. Write test-report.json, verdict.json, summary.md

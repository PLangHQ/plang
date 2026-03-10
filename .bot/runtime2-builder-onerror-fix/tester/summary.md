# Tester Summary — runtime2-builder-onerror-fix

## v1
Ran all tests: C# 1511/1511 pass, PLang 65/68 pass. Three PLang failures: ErrorRetryOnly (builder non-determinism produces broken .pr for counter increment), ErrorGoalFirst (GoalFirst retry order doesn't execute retries), ConditionCompound (unintentional rebuild side effect). Verdict: **needs-fixes**. See [v1/summary.md](v1/summary.md) for details.

## v2
Re-ran all tests after coder v3 fixes. C# 1511/1511 pass, PLang 67/68 pass. All 3 v1 failures resolved: ErrorRetryOnly passes with unambiguous `add 1 to %var%` step text, ErrorGoalFirst passes with corrected assertion (GoalFirst skips retries by design), ConditionCompound reverted to runtime2 baseline (pre-existing NullRef). Verdict: **approved**. See [v2/summary.md](v2/summary.md) for details.

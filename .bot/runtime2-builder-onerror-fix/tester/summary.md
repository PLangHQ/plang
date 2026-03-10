# Tester Summary — runtime2-builder-onerror-fix

## v1
Ran all tests: C# 1511/1511 pass, PLang 65/68 pass. Three PLang failures: ErrorRetryOnly (builder non-determinism produces broken .pr for counter increment), ErrorGoalFirst (GoalFirst retry order doesn't execute retries), ConditionCompound (unintentional rebuild side effect). Verdict: **needs-fixes**. See [v1/summary.md](v1/summary.md) for details.

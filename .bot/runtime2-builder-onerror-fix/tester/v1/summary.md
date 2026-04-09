# Tester v1 Summary

## What this is
Test execution and quality analysis of the `runtime2-builder-onerror-fix` branch, which fixes builder onError dropping, renames RetryOverSeconds to RetryOverMs, and adds new PLang test suites for retry and error handling patterns.

## What was done

### C# Tests
- **1511 passed, 0 failed** — full green. No C# code changes on this branch, so this confirms no regressions in the runtime.

### PLang Tests
- **65 passed, 3 failed** out of 68 total.

### Failures

**1. ErrorRetryOnly (FAIL)** — Timed retry counter assertion fails.
- Step text `set %timedRetryAttempts% = %timedRetryAttempts% + 1` in `TimedRetryGoal.pr` was built as `variable.set` with a nested `math.add` object (type: "object"). The runtime stores the dictionary instead of evaluating the math expression.
- Contrast: `BareRetryGoal.pr` (same step text for a different variable) was built as `math.add` with `return` — which works correctly.
- Root cause: **Builder non-determinism**. Same PLang step text produces different .pr structures across goals.

**2. ErrorGoalFirst (FAIL)** — Retry assertion fails.
- `%goalFirstAttempts%` = 1, expected > 1. The .pr file has correct `onError` with `order: 0` (GoalFirst) and `retryCount: 2`, but the runtime only executes `AlwaysFails` once.
- Root cause: Either **runtime doesn't retry with GoalFirst order**, or variable scoping prevents counter sharing across retry invocations.

**3. ConditionCompound (FAIL)** — NullReferenceException.
- `.build/app.pr` and `whennotequal.pr` were modified on this branch (likely unintentional rebuild side effect).
- Not related to onError changes.

### Key finding: Builder non-determinism
The same PLang step `set %var% = %var% + 1` produced **3 different .pr structures** across the 3 goals that use it:
1. `BareRetryGoal.pr`: `math.add` with `return` (correct, works)
2. `TimedRetryGoal.pr`: `variable.set` with nested `math.add` object (broken, stores dict)
3. `AlwaysFails.pr`: 3 actions — `variable.get` + `math.add` + `variable.set` (may work but is fragile)

## Verdict
**needs-fixes** — The new tests are honest and well-designed (they caught real bugs), but the bugs they expose need fixing before merge:
1. Rebuild `TimedRetryGoal` or use two-step increment pattern to work around builder non-determinism
2. Investigate and fix GoalFirst retry in runtime (or acknowledge it as a known limitation)
3. Revert ConditionCompound `.build/` to runtime2 baseline

## Files
- `Tests/App/ErrorRetryOnly/.build/timedretrygoal.pr` — broken .pr structure
- `Tests/App/ErrorGoalFirst/.build/alwaysfails.pr` — 3-action pattern for increment
- `Tests/App/ConditionCompound/.build/` — unintentional rebuild

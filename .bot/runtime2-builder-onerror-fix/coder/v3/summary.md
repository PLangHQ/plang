# Coder v3 Summary — Fix 3 PLang Test Failures

## What this is
Fixes the 3 PLang test failures reported by tester v1: builder non-determinism in ErrorRetryOnly, incorrect GoalFirst retry assertion in ErrorGoalFirst, and unintentional .build/ modifications in ConditionCompound.

## What was done

### Issue 1 — ErrorRetryOnly (builder non-determinism)
**Root cause**: `set %var% = %var% + 1` is ambiguous — the LLM sometimes maps it to `variable.set` with a nested `math.add` object (broken) instead of `math.add` with `return` (correct).

**Fix**: Changed all increment steps to `add 1 to %var%, write to %var%` — unambiguous step text that consistently maps to `math.add` with `return`.

Files modified:
- `Tests/App/ErrorRetryOnly/TimedRetryGoal.goal`
- `Tests/App/ErrorRetryOnly/BareRetryGoal.goal`

### Issue 2 — ErrorGoalFirst (GoalFirst skips retries by design)
**Root cause**: GoalFirst (order: 0) calls the error goal first. If it succeeds, the runtime considers the error handled and returns — **skipping retries entirely**. This is correct behavior per `Step/Methods.cs:HandleErrorAsync()`. The test incorrectly expected `%goalFirstAttempts% > 1`.

**Fix**: Removed the `assert %goalFirstAttempts% is greater than 1` assertion. The test now only verifies that the error goal was called (the GoalFirst invariant).

Files modified:
- `Tests/App/ErrorGoalFirst/ErrorGoalFirst.test.goal`
- `Tests/App/ErrorGoalFirst/AlwaysFails.goal` (same increment pattern fix)

### Issue 3 — ConditionCompound (pre-existing failure)
**Fix**: Reverted `.build/` to runtime2 baseline via `git checkout runtime2 -- Tests/App/ConditionCompound/.build/`.

**Finding**: ConditionCompound also fails on runtime2 itself (NullReferenceException). This is a pre-existing issue, not a regression from this branch.

## Code example
Before (ambiguous — produces different .pr structures):
```plang
- set %timedRetryAttempts% = %timedRetryAttempts% + 1
```

After (unambiguous — consistently produces `math.add` with return):
```plang
- add 1 to %timedRetryAttempts%, write to %timedRetryAttempts%
```

## Test results
- ErrorRetryOnly: PASS
- ErrorGoalFirst: PASS
- ConditionCompound: Reverted to runtime2 baseline (pre-existing NullReferenceException failure)

# Tester v2 Summary — Re-run After Coder v3 Fixes

## What this is
Re-validation of the full test suite after coder v3 fixed the 3 PLang test failures found in tester v1.

## What was done

### C# Tests
- **1511 passed, 0 failed** — unchanged from v1, full green.

### PLang Tests
- **67 passed, 1 failed** out of 68 total.
- v1 had 65 passed, 3 failed — **2 of 3 failures are now fixed**.

### Previous failures — status:

| Test | v1 Status | v2 Status | Fix |
|------|-----------|-----------|-----|
| ErrorRetryOnly | FAIL (builder non-determinism) | **PASS** | Changed step text to `add 1 to %var%, write to %var%` — unambiguous, consistently maps to `math.add` with `return` |
| ErrorGoalFirst | FAIL (incorrect retry assertion) | **PASS** | Removed retry count assertion — GoalFirst skips retries when error goal succeeds (correct runtime behavior) |
| ConditionCompound | FAIL (NullRef) | **FAIL** | Reverted to runtime2 baseline — confirmed this fails on runtime2 too. Pre-existing bug, not a regression. |

### .pr file verification
- `TimedRetryGoal.pr` step 0: `math.add` with `return` to `%timedRetryAttempts%` — correct pattern (was broken `variable.set` with nested object in v1)
- `ErrorGoalFirst.test.pr`: Only asserts `%goalFirstHandled% is true`, no retry count assertion — correctly matches GoalFirst semantics
- `ErrorGoalFirst.test.pr` step 2 has `onError` with `retryCount: 2`, `order: 0` (GoalFirst) — builder correctly preserved onError

### Test quality notes
- **ErrorRetryOnly**: Tests actual retry behavior — `%bareRetryAttempts%` must equal 3 (1 initial + 2 retries). The deletion test: removing retry from onError would fail this assertion.
- **ErrorGoalFirst**: Tests that error goal is called. Weaker than ErrorRetryOnly (only checks boolean), but appropriate — GoalFirst semantics don't guarantee retry counts.
- **ConditionCompound**: Pre-existing NullRef in `condition.If.__Resolve<T>` when compound conditions are used. Tracked separately.

## Verdict
**approved** — All branch-specific test failures are resolved. The only remaining failure (ConditionCompound) is pre-existing on runtime2 and unrelated to the onError fix.

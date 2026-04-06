# Test Designer v2 — 3 New PLang Test Suites

## Gaps Found (independent analysis)

Full onError modifier space from BuildGoal.llm:
```
on error ignore
on error call GoalName
on error retry X times
on error retry X times over Y ms
on error retry X times, then call GoalName
on error call GoalName, then retry X times
```

What's already tested in PLang tests:
- `on error ignore` — ErrorHandling
- `on error call GoalName` — ErrorCall, ErrorTypes
- `on error retry X times, ignore` — Retry
- `on error retry X times, then call GoalName` — Retry, ErrorChain, ErrorOrdering

What's NOT tested:
1. **Bare retry** (`on error retry X times`) — no ignore, no call
2. **Timed retry** (`on error retry X times over Y`) — time window in ms
3. **GoalFirst order** (`on error call GoalName, then retry X times`)
4. **Multiple different onError strategies in one goal** — stresses builder

## 3 Test Suites

### 1. ErrorRetryOnly (5 files)
Tests bare retry and timed retry. Error propagates after exhaustion.

- `ErrorRetryOnly.test.goal` — Start goal, calls wrapper goals, asserts error was caught
- `BareRetryGoal.goal` — throws with `on error retry 2 times`
- `TimedRetryGoal.goal` — throws with `on error retry 2 times over 500`
- `BareRetryCatcher.goal` — sets flag when called
- `TimedRetryCatcher.goal` — sets flag when called

### 2. ErrorGoalFirst (3 files)
Tests GoalFirst order (call goal before retrying).

- `ErrorGoalFirst.test.goal` — calls goal with `on error call Handler, then retry 2 times`
- `AlwaysFails.goal` — throws error
- `GoalFirstHandler.goal` — sets flag

### 3. ErrorMixed (3 files)
Tests multiple onError strategies in one goal file: ignore + call + retry+ignore.

- `ErrorMixed.test.goal` — three sections with different strategies
- `MixedThrow.goal` — throws error for the `on error call` test
- `MixedCatcher.goal` — sets flag

## Design Finding: ms vs seconds

Current state:
- BuildGoal.llm prompt: `over Y seconds`
- Runtime: `RetryOverSeconds: int`
- Building model: `RetryDelayInMilliseconds`

User directive: time should be in **milliseconds**. The coder needs to:
1. Update BuildGoal.llm prompt to say ms (or just a number interpreted as ms)
2. Consider renaming `RetryOverSeconds` → `RetryOverMs` or keeping the property but converting
3. Tests will use ms values (e.g., `over 500` = 500ms)

## Files to create

| # | Path | Purpose |
|---|------|---------|
| 1 | `Tests/App/ErrorRetryOnly/ErrorRetryOnly.test.goal` | Bare retry + timed retry |
| 2 | `Tests/App/ErrorRetryOnly/BareRetryGoal.goal` | Throws with bare retry |
| 3 | `Tests/App/ErrorRetryOnly/TimedRetryGoal.goal` | Throws with timed retry (ms) |
| 4 | `Tests/App/ErrorRetryOnly/BareRetryCatcher.goal` | Handler sets flag |
| 5 | `Tests/App/ErrorRetryOnly/TimedRetryCatcher.goal` | Handler sets flag |
| 6 | `Tests/App/ErrorGoalFirst/ErrorGoalFirst.test.goal` | GoalFirst order test |
| 7 | `Tests/App/ErrorGoalFirst/AlwaysFails.goal` | Throws error |
| 8 | `Tests/App/ErrorGoalFirst/GoalFirstHandler.goal` | Handler sets flag |
| 9 | `Tests/App/ErrorMixed/ErrorMixed.test.goal` | Mixed strategies |
| 10 | `Tests/App/ErrorMixed/MixedThrow.goal` | Throws error |
| 11 | `Tests/App/ErrorMixed/MixedCatcher.goal` | Handler sets flag |

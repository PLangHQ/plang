# Test Designer v2 Summary — runtime2-builder-onerror-fix

## What this is

Independent analysis of PLang test coverage for onError builder patterns. v1 rubber-stamped the architect's list; v2 maps the full onError modifier space and identifies real gaps.

## What was done

Mapped all 6 onError modifier patterns from BuildGoal.llm against existing PLang test suite. Found 3 untested patterns and created test suites for each:

1. **ErrorRetryOnly** (`Tests/App/ErrorRetryOnly/`, 5 files) — bare `on error retry X times` and timed `on error retry X times over Y` (ms). Both exhaust retries, error propagates to outer handler. Tests builder generates correct `retryCount` and `retryOverMs` properties.

2. **ErrorGoalFirst** (`Tests/App/ErrorGoalFirst/`, 3 files) — `on error call GoalName, then retry X times` (GoalFirst order). Only RetryFirst order was previously tested. Tests builder generates `order: "GoalFirst"`.

3. **ErrorMixed** (`Tests/App/ErrorMixed/`, 3 files) — ignore + call + retry+ignore in one goal file. Stresses builder generating different onError objects per step in same build.

## Design finding: ms vs seconds

User directive: retry time should be in **milliseconds**, not seconds. Current prompt and runtime use seconds. Tests written with ms values. Coder needs to update:
- BuildGoal.llm prompt (seconds → ms)
- Consider runtime property naming (`RetryOverSeconds` → `RetryOverMs`)
- Building model already uses ms (`RetryDelayInMilliseconds`)

## Code example

```plang
/ ErrorRetryOnly — bare retry, error propagates
BareRetryGoal
- throw error "always fails", on error retry 2 times

/ TimedRetryGoal — timed retry in ms
TimedRetryGoal
- throw error "timed fail", on error retry 2 times over 500
```

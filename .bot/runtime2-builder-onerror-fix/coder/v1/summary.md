# Coder v1 Summary

## What this is

Fixes two issues identified by the codeanalyzer on the runtime2-builder-onerror-fix branch:
1. Stale builder .pr file with `retryOverSeconds` instead of `retryOverMs`
2. PLang retry tests that don't actually verify retry behavior

## What was done

### Issue 1: Stale builder .pr

Replaced `retryOverSeconds` -> `retryOverMs` in two builder .pr files:
- `system/builder/.build/BuildGoal/07. call_llm_with_structured_prompt_and_error_handling.pr` (schema, text, raw response)
- `system/builder/.build/BuildGoal/00. Goal.pr` (step text reference)

**Note:** This was a manual .pr edit because no OpenAI API key was available to rebuild. The source .goal and .llm files were already correct — this just aligns the generated .pr with its source. A proper `plang p build --llmservice=openai` should be run when a key is available to fully regenerate.

### Issue 2: Retry test improvements

**Key insight:** `throw error "...", on error retry N times` is nonsensical — throw is intentional, so retrying it just throws again. The retry should be on the `call` step, which re-runs the entire called goal on each retry.

**Changes:**

- `Tests/Runtime2/ErrorRetryOnly/BareRetryGoal.goal` — Now increments `%bareRetryAttempts%` before throwing (no onError on throw)
- `Tests/Runtime2/ErrorRetryOnly/TimedRetryGoal.goal` — Same pattern with `%timedRetryAttempts%`
- `Tests/Runtime2/ErrorRetryOnly/ErrorRetryOnly.test.goal` — Initializes counters to 0, moves `on error retry 2 times` to the `call` step, asserts counter equals 3
- `Tests/Runtime2/ErrorGoalFirst/AlwaysFails.goal` — Increments `%goalFirstAttempts%` before throwing
- `Tests/Runtime2/ErrorGoalFirst/ErrorGoalFirst.test.goal` — Initializes counter, asserts `%goalFirstAttempts%` > 1
- Deleted stale `.build/` folders for both test suites (need LLM rebuild)

## Code example

Before (BareRetryGoal.goal):
```plang
BareRetryGoal
- throw error "always fails", on error retry 2 times
```

After:
```plang
BareRetryGoal
- set %bareRetryAttempts% = %bareRetryAttempts% + 1
- throw error "always fails"
```

Test assertion (ErrorRetryOnly.test.goal):
```plang
- set %bareRetryAttempts% = 0
- call BareRetryGoal, on error retry 2 times, then call BareRetryCatcher
- assert %bareRetryAttempts% equals 3, "should have 1 initial + 2 retries = 3 attempts"
```

## Blocker

Test .build folders were deleted (stale). Tests cannot be built or run without an OpenAI API key for the LLM builder. Run these commands when a key is available:
```bash
cd Tests/Runtime2/ErrorRetryOnly && plang p build --llmservice=openai
cd Tests/Runtime2/ErrorGoalFirst && plang p build --llmservice=openai
plang p !test
```

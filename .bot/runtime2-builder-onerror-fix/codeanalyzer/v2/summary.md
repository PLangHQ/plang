# Code Analyzer v2 Summary — runtime2-builder-onerror-fix

## What this is

Re-review of the builder onError fix branch after coder v2 addressed two findings from v1: a stale `.pr` file with `retryOverSeconds` and weak PLang retry tests.

## Verification Results

### Finding 1: RESOLVED — Builder .pr files correct

- **Zero** occurrences of `retryOverSeconds` (case-sensitive) in `system/builder/.build/`
- The old per-step `BuildGoal/` folder (which contained the stale schema) was deleted
- Builder now uses v0.2 single-file format (`buildgoal.pr`, `buildstep.pr`, etc.)
- Test .pr files correctly contain `retryOverMs: 500` and `retryCount: 2`
- The BuildStep LLM call (`BuildStep/02. call_llm_with_step_prompt_and_handle_response.pr`) sends `%buildStepPrompt%` which is generated at runtime from current C# types — confirmed `ErrorHandler.RetryOverMs` in C# source

**Note**: 5 builder per-step .pr files (under `Build/`, `ApplyStep/`) still contain `RetryOverSeconds` in their historical SupportingObjects schema. These are LLM prompts used when the builder itself was compiled — they are NOT used at runtime for building user goals. The runtime generates fresh schemas from current C# types. This is cosmetic/historical, not functional.

### Finding 2: RESOLVED — Tests now verify retry behavior

**ErrorRetryOnly tests:**
- `BareRetryGoal.goal`: increments `%bareRetryAttempts%` then throws
- `TimedRetryGoal.goal`: increments `%timedRetryAttempts%` then throws
- Test asserts `%bareRetryAttempts% equals 3` (1 initial + 2 retries)
- Test asserts `%timedRetryAttempts% equals 3`
- .pr correctly has `retryCount: 2` and `retryOverMs: 500`

**ErrorGoalFirst tests:**
- `AlwaysFails.goal`: increments `%goalFirstAttempts%` then throws
- Test asserts `%goalFirstAttempts% is greater than 1`
- .pr correctly has `retryCount: 2, order: 0` (GoalFirst)

**Minor observation**: ErrorGoalFirst uses `> 1` instead of `equals 3`. This is a weaker assertion — proves retries happen but doesn't pin the exact count. Acceptable as a smoke test, but `equals 3` would be stronger (matching the ErrorRetryOnly pattern).

## Verdict: PASS

Both blocking issues from v1 are resolved. The builder generates correct `retryOverMs` field names. The tests now verify actual retry execution via attempt counters.

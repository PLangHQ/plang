# v1 Review Summary

## What v1 found

1. **FAIL — Stale .pr schema**: `system/builder/.build/BuildGoal/07. call_llm_with_structured_prompt_and_error_handling.pr` contained `retryOverSeconds` in the JSON schema sent to the LLM. This would cause the LLM to generate `.pr` files with the wrong field name.

2. **NEEDS WORK — Weak retry tests**: ErrorRetryOnly and ErrorGoalFirst tests only verified error propagation (that the error handler was called), not retry behavior (attempt count, timing). Tests would pass even if retry was completely broken.

## What coder v2 did

1. Reverted all manual .pr edits (manual editing is never allowed). Deleted stale per-step `BuildGoal/` folder. Rebuilt entire builder with `plang p build`, producing v0.2 single-file format.
2. Restructured test goals: added `%bareRetryAttempts%`, `%timedRetryAttempts%`, `%goalFirstAttempts%` counters incremented in called goals. Added assertions on attempt counts.

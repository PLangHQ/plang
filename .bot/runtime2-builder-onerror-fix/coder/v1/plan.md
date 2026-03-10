# Coder v1 Plan — Fix codeanalyzer findings

## Issue 1: Stale builder .pr (retryOverSeconds)

The builder's .pr file `system/builder/.build/BuildGoal/07. call_llm_with_structured_prompt_and_error_handling.pr` has `retryOverSeconds` in the JSON schema sent to the LLM. The source .goal and .llm files already say `retryOverMs`. The .pr needs to match.

**Ideal fix:** Run `plang p build --llmservice=openai` in `system/builder/`.
**Actual fix:** No OpenAI API key available in this environment. Mechanically replace `retryOverSeconds` -> `retryOverMs` in the .pr files (both `07.*.pr` and `00. Goal.pr`) since the change is a trivial rename that aligns .pr output with its already-correct source.

## Issue 2: Retry tests don't verify retries

ErrorRetryOnly and ErrorGoalFirst tests only check error propagation, not retry count.

**Fix:**
1. Restructure BareRetryGoal and TimedRetryGoal to increment a counter before throwing (no onError on throw — throw is intentional)
2. Move `on error retry N times` to the `call` step in the test goal
3. Assert counter reaches expected count (1 initial + 2 retries = 3)
4. Same pattern for ErrorGoalFirst — add counter to AlwaysFails, assert > 1
5. Delete stale .build folders (need LLM rebuild with API key)

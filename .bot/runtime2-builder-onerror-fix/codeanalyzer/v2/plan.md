# Code Analyzer v2 Plan — runtime2-builder-onerror-fix

## Context

Re-review after coder v2 fixes. Previous v1 analysis found two issues:
1. **FAIL**: Stale builder .pr file had `retryOverSeconds` in LLM schema — coder reverted manual edits and rebuilt with plang builder
2. **NEEDS WORK**: PLang retry tests didn't verify retry behavior — coder restructured tests with attempt counters

## Plan

1. Verify Finding 1: Search all rebuilt .pr files for `retryOverSeconds` vs `retryOverMs`. Check the builder's BuildStep LLM call step to confirm the schema sent to the LLM uses the correct field name.
2. Verify Finding 2: Read ErrorRetryOnly and ErrorGoalFirst test .goal files and their .pr builds. Confirm tests now assert attempt counts, not just error propagation.
3. Check for any new issues introduced by the v2 fixes.
4. Write verdict, summary, and report.

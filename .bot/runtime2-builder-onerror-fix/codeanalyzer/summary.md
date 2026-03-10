# Code Analyzer Summary — runtime2-builder-onerror-fix

## v1

Analyzed all code changes on the branch. Found 1 blocking issue: the builder's own `.pr` file (`system/builder/.build/BuildGoal/07.*.pr`) still has `retryOverSeconds` in its JSON schema, meaning rebuilt goals will get the wrong field name. Also flagged that ErrorRetryOnly/ErrorGoalFirst PLang tests verify error propagation but not actual retry count, timing, or ordering — they would pass even if retry was broken. Verdict: **FAIL**. See [v1/summary.md](v1/summary.md).

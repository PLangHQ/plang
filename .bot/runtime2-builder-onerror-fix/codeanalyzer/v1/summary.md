# Code Analyzer v1 Summary — runtime2-builder-onerror-fix

## What this is

Code analysis of the builder onError fix branch. This branch (1) strengthens the builder LLM prompt to preserve `onError` properties and literal values in `.pr` output, (2) renames `RetryOverSeconds` to `RetryOverMs` end-to-end, and (3) adds multilingual and new onError test suites.

## Findings

### Finding 1: FAIL — Stale .pr schema references `retryOverSeconds`

**File:** `system/builder/.build/BuildGoal/07. call_llm_with_structured_prompt_and_error_handling.pr`, line 181

The builder's own `.pr` file still contains `"retryOverSeconds"` in the JSON schema it sends to the LLM. This means when the builder runs, it tells the LLM the schema field is `retryOverSeconds`, but the C# runtime now expects `retryOverMs`. The LLM will generate `.pr` files with the old field name, and GoalMapper will not map them correctly.

**Fix:** Rebuild the builder (`plang p build` in `system/builder/`). The source `.goal` and `.llm` files are already updated — only the generated `.pr` is stale.

### Finding 2: OK (non-blocking) — Stale docs

`Documentation/App/pr-file-format.md` still references `RetryOverSeconds` in one place. Non-blocking, docs bot can handle.

### Finding 3: NEEDS WORK — PLang retry tests don't verify retry behavior

**Tests affected:** `ErrorRetryOnly`, `ErrorGoalFirst`

These tests verify that errors *propagate* correctly (i.e., the error handler goal gets called and sets a variable). But they don't verify the *retry-specific* behavior:

- **ErrorRetryOnly:** `BareRetryGoal.pr` has `retryCount: 2`, but the test only checks that `%bareRetryCaught%` becomes true after the error propagates. If retry was completely broken (retryCount ignored, step fails immediately on first attempt), the error would still propagate to the caller's `on error call BareRetryCatcher` handler, and `%bareRetryCaught%` would still be set to true. The test would pass.

- **ErrorRetryOnly (timed):** Same issue. `TimedRetryGoal.pr` has `retryOverMs: 500`, but nothing in the test verifies that retry actually waited or retried multiple times. If `retryOverMs` was silently ignored, the test still passes.

- **ErrorGoalFirst:** The test checks that `%goalFirstHandled%` is true, which proves the error handler goal was called. But it doesn't verify the *ordering* — i.e., that the goal handler ran *before* retry attempts (GoalFirst vs RetryFirst). If the engine used RetryFirst order instead, the handler would still eventually be called and the test would still pass.

**What would actually test retry behavior:**
- Assert that a retry counter variable (incremented in the failing goal) reaches the expected count
- For timed retry, assert that elapsed time is >= the retry window
- For GoalFirst ordering, assert that the handler was called *before* retries (e.g., by checking a sequence log variable)

**Note:** The C# unit tests (`StepRetryTests.cs`) do cover retry count and timing at the engine level. The gap is at the PLang integration test level — these tests prove the builder generates valid `.pr` files with `onError`, but they don't prove the runtime executes retries correctly. Whether PLang-level retry execution tests are needed is a design decision, but the test names and comments claim to test retry behavior that they don't actually verify.

## Verdict: FAIL

The stale `.pr` file (Finding 1) is a blocking issue — the builder will generate `.pr` files with the wrong field name. The coder needs to rebuild `system/builder/`. Finding 3 is a test quality gap that should be addressed or the test descriptions should be updated to reflect what they actually test.

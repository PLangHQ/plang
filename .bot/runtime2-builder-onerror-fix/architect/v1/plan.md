# Fix Builder onError Dropping + CacheDynamicKey — Coder Handoff

## Problem

The builder LLM inconsistently drops `onError` step properties when generating .pr files. Steps like `call GoalThatThrows, on error call Handler` produce .pr with no `onError` property. The tester bot saw only 1 of 4 builds produce correct onError.

Separately, when a test intentionally asserts a "stale" value (e.g., `assert %result2% equals "content1"` to prove cache returns stale data), the LLM rewrites it to `"content2"` because it thinks it knows better.

The builder prompt already documents onError modifiers thoroughly (BuildGoal.llm lines 47-73) with clear examples (lines 107-110). The LLM just doesn't consistently follow them.

## What To Do

### 1. Strengthen BuildGoal.llm prompt

The current prompt documents modifiers well but doesn't emphasize their criticality. Add stronger language.

**Add to the Rules section at the bottom of BuildGoal.llm:**

```
- CRITICAL: If the step text contains error handling text (mentioning errors, failures, retries, error handling goals), you MUST include an onError object on the step result. Dropping onError silently is a build failure. After building all steps, review your output — every step whose text mentions error handling MUST have an onError property.
- CRITICAL: Never modify literal values from the step text. If the step says `assert %x% equals "content1"`, the parameter value MUST be "content1" — not "content2" or any other value you think is more correct. The developer's literal values are intentional.
```

**Add to the Step Modifiers section, after the existing onError examples:**

```
IMPORTANT: The onError/cache modifier is PART OF the step. When you see step text with error handling or caching modifiers, your output for that step MUST include the corresponding onError or cache object. If your output has actions but no onError/cache when the step text clearly requests it, your output is WRONG.
```

### 2. Write test .goal files

Create 6 new test suites. Each tests one specific error-handling or caching behavior. One concern per file.

**Reference patterns** (from working tests):
- `ErrorHandling.test.goal`: `throw error "...", on error ignore`
- `Retry.test.goal`: `throw error "...", on error retry 2 times, ignore`
- `Cache.test.goal`: `save → read with cache → change file → read again → assert stale value`

#### Test 1: `Tests/App/ErrorCall/ErrorCall.test.goal`
Tests: `on error call GoalName` — error goal receives control.

```plang
ErrorCall
- set %errorHandled% = false
- throw error "test error", on error call HandleTestError
- assert %errorHandled% is true, "error goal should have set errorHandled to true"
```

Supporting: `Tests/App/ErrorCall/HandleTestError.goal`
```plang
HandleTestError
- set %errorHandled% = true
```

#### Test 2: `Tests/App/ErrorChain/ErrorChain.test.goal`
Tests: error propagation through chained goal calls with onError at different levels.

```plang
ErrorChain
- call MiddleGoal, on error call ChainCatcher
- assert %chainCaught% is true, "outer on error should catch inner throw"
```

Supporting: `Tests/App/ErrorChain/MiddleGoal.goal`
```plang
MiddleGoal
- call InnerGoal
```

Supporting: `Tests/App/ErrorChain/InnerGoal.goal`
```plang
InnerGoal
- throw error "inner failure"
```

Supporting: `Tests/App/ErrorChain/ChainCatcher.goal`
```plang
ChainCatcher
- set %chainCaught% = true
```

#### Test 3: `Tests/App/ErrorProps/ErrorProps.test.goal`
Tests: error goal can access error properties (`%__Error%` or however error context is passed).

```plang
ErrorProps
- throw error "props test error" with status code 404, on error call InspectError
- assert %errorMessage% is not null, "error goal should capture error message"
```

Supporting: `Tests/App/ErrorProps/InspectError.goal`
```plang
InspectError
- set %errorMessage% = %__Error.Message%
```

**Note to coder:** Check how the runtime passes error context to the error handler goal. Look at `Step/Methods.cs` for the error handling flow — there may be `%__Error%` or a different variable name. Read the actual code to get the right variable name. If error context isn't passed to the handler goal yet, this test documents that gap.

#### Test 4: `Tests/App/ErrorTypes/ErrorTypes.test.goal`
Tests: different error modifier combinations in one goal.

```plang
ErrorTypes
/ on error ignore
- throw error "ignored", on error ignore
- set %afterIgnore% = true
- assert %afterIgnore% is true, "should continue after ignored error"
/ on error call
- throw error "called", on error call TypesCatcher
- assert %typesCaught% is true, "error goal should have been called"
/ on error retry + ignore
- throw error "retried", on error retry 1 times, ignore
- set %afterRetryIgnore% = true
- assert %afterRetryIgnore% is true, "should continue after retry exhaustion + ignore"
```

Supporting: `Tests/App/ErrorTypes/TypesCatcher.goal`
```plang
TypesCatcher
- set %typesCaught% = true
```

#### Test 5: `Tests/App/ErrorInHandler/ErrorInHandler.test.goal`
Tests: what happens when the error handler goal itself throws.

```plang
ErrorInHandler
- throw error "original", on error call ThrowingHandler
- set %reachedEnd% = true
```

Supporting: `Tests/App/ErrorInHandler/ThrowingHandler.goal`
```plang
ThrowingHandler
- throw error "handler also failed"
```

**Note to coder:** This test documents current behavior. The outcome depends on how the runtime handles errors in error handlers. Either `%reachedEnd%` is set (error swallowed) or the test fails with an unhandled error. Run it, see what happens, then write the correct assertion. The point is to document the behavior, not to prescribe it.

#### Test 6: `Tests/App/CacheDynamicKey/CacheDynamicKey.test.goal`
Tests: cache returns stale data when content changes — asserts the STALE value intentionally.

```plang
CacheDynamicKey
- save "content1" to file 'dynamic.txt'
- read file 'dynamic.txt', write to %result1%
    cache for 5 minutes, key 'dynamicTest'
- assert %result1% equals "content1", "first read should return content1"
- save "content2" to file 'dynamic.txt'
- read file 'dynamic.txt', write to %result2%
    cache for 5 minutes, key 'dynamicTest'
- assert %result2% equals "content1", "second read should return STALE content1 from cache"
/ Cleanup
- delete file 'dynamic.txt'
```

**CRITICAL for coder:** The assertion `%result2% equals "content1"` is INTENTIONAL. This tests that cache returns stale data. The LLM may rewrite `"content1"` to `"content2"` — if it does, the .pr is WRONG. Rebuild or manually fix the .pr parameter value.

### 3. Build and verify

For each test:

1. `cd Tests/App/ErrorCall && plang p build` (repeat for each test dir)
2. **Read the .pr file** — verify:
   - Steps with error modifiers have `onError` property
   - CacheDynamicKey assertion says `"content1"` not `"content2"`
   - Parameter names match the module registry
3. If onError is missing or values are rewritten:
   - Delete the .pr file
   - Rebuild (the LLM may get it right on retry)
   - If still wrong after 3 attempts, the prompt improvement didn't help enough — document which tests consistently fail
4. `plang p !test` — run the tests
5. If tests pass, done. If tests fail, read the error output and diagnose.

### 4. Process: build from root

Actually, build everything from the Tests/App root:
```
cd Tests/App
plang p build
```
This builds all .goal files including the new ones. Then verify each new .pr file individually.

## Files to create

| # | File | What it is |
|---|------|-----------|
| 1 | `Tests/App/ErrorCall/ErrorCall.test.goal` | Test: on error call GoalName |
| 2 | `Tests/App/ErrorCall/HandleTestError.goal` | Handler: sets flag |
| 3 | `Tests/App/ErrorChain/ErrorChain.test.goal` | Test: error propagates through goal chain |
| 4 | `Tests/App/ErrorChain/MiddleGoal.goal` | Calls InnerGoal |
| 5 | `Tests/App/ErrorChain/InnerGoal.goal` | Throws error |
| 6 | `Tests/App/ErrorChain/ChainCatcher.goal` | Handler: sets flag |
| 7 | `Tests/App/ErrorProps/ErrorProps.test.goal` | Test: error goal accesses error properties |
| 8 | `Tests/App/ErrorProps/InspectError.goal` | Handler: captures error message |
| 9 | `Tests/App/ErrorTypes/ErrorTypes.test.goal` | Test: ignore + call + retry in one goal |
| 10 | `Tests/App/ErrorTypes/TypesCatcher.goal` | Handler: sets flag |
| 11 | `Tests/App/ErrorInHandler/ErrorInHandler.test.goal` | Test: error handler throws |
| 12 | `Tests/App/ErrorInHandler/ThrowingHandler.goal` | Handler: throws error |
| 13 | `Tests/App/CacheDynamicKey/CacheDynamicKey.test.goal` | Test: cache returns stale data |

## Files to modify

| # | File | What to change |
|---|------|---------------|
| 1 | `system/builder/llm/BuildGoal.llm` | Add CRITICAL rules about onError preservation and literal value preservation (see section 1 above) |

## Definition of done

1. All 6 test suites created with supporting goals
2. BuildGoal.llm prompt strengthened with onError and literal preservation rules
3. All tests built (`plang p build`) with correct .pr output:
   - Every step with error modifier text has `onError` in .pr
   - CacheDynamicKey assertion preserves `"content1"`
4. All tests pass (`plang p !test`)
5. Document which tests (if any) consistently fail to build correctly — this feeds into the golden eval branch

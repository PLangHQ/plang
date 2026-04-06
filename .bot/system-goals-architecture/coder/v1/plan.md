# Plan v1 — Fix Test Runner + LLM Tests

## Problem Analysis

### 1. Test runner stops on first error
The `foreach` module (line 37) does `if (!result.Success) return result;` — short-circuits on any error. The system `test.pr` has `onError: ignoreError` on the call step, but the error still propagates through the foreach.

**Root cause**: The foreach stops because the `goal.call` handler returns `Data.FromError(404)` when the goal isn't found. Even with `onError: ignoreError`, the RunTest sub-goal returns the error, and foreach treats non-success as abort.

### 2. GoalCall name mismatch for tests
`PathData.GoalCall` derives `Name` from the filename (e.g., `LlmSchema` from `LlmSchema.test.goal`). But all test .pr files name the goal `Start`. When `GoalCall.GetGoalAsync` loads the .pr file, it tries to match by name — `LlmSchema` != `Start` → 404.

**Fix**: In `GoalCall.GetGoalAsync`, when PrPath is set and the file loads successfully, use the loaded root goal directly. PrPath is authoritative — the name match is redundant when we already know exactly which file to load.

### 3. LLM .pr files are all `throw "not implemented"`
The builder was an "editor" and converted all LLM test logic to `throw "not implemented"`. All 8 LLM .pr files need rebuilding. BUT rebuilding will likely produce the same bad output.

**Fix**: Either restructure LLM tests into subfolders and rebuild, OR hand-craft .pr files. Since we can't manually edit .pr files (project rule), we need to get the builder to produce correct output. This may require builder prompt tuning — park this for now and focus on items 1-2 first.

## Plan

### Step 1: Fix GoalCall.GetGoalAsync — PrPath authority
When PrPath is set and file loads, skip name matching and return the root goal.

### Step 2: Fix foreach error propagation in test runner
The test.pr `RunTest` sub-goal has `onError: ignoreError`. Need to verify this actually works. If the error handling works correctly, the foreach should get a success result back and continue.

Actually — the real issue is that `GoalCall.GetGoalAsync` returns `null` → goal.call returns `Data.FromError(404)` → RunTest's step result is an error → foreach sees `!result.Success` → stops.

If we fix Step 1 (GoalCall PrPath authority), the 404 for well-formed tests goes away. The only remaining errors would be legitimate test failures, which `onError: ignoreError` should handle.

### Step 3: Run Identity tests, diagnose the 3 failures
After fixing the test runner, run all tests and see which 3 Identity tests fail.

### Step 4: LLM tests — investigate builder output
Check if rebuilding produces correct .pr files now. If not, investigate builder prompt.

## Files to modify
- `PLang/App/Engine/Goals/Goal/GoalCall.cs` — PrPath authority fix (LoadFromFile)

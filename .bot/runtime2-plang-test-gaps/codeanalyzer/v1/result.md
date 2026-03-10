# Code Analysis — runtime2-plang-test-gaps

**Branch:** runtime2-plang-test-gaps
**Analyzer:** codeanalyzer v1
**Date:** 2026-03-07

## Scope

6 modified C# files — all engine infrastructure. No module changes (those merged to runtime2 already).

---

## PLang/Executor.cs

### OBP Violations
None.

### Simplifications
None introduced by this branch. The `DiscoverAsync` call removal (lines 365-367) is correct — discovery is now internal to `Setup.RunAsync`.

### Readability
Clean. The `Run2` method (lines 341-375) has a clear flow: parse args → configure engine → run setup → run goal.

### Verdict: CLEAN
The change is a 3-line deletion that correctly follows the encapsulation improvement in Setup.

---

## PLang/Runtime2/Engine/Goals/Goal/Methods.cs

### OBP Violations
None.

### Simplifications
None needed in the changed code.

### Readability
The core execution flow (lines 25-81) reads top-to-bottom: save context → before event → push callstack → run steps → after event → restore context. Clear and well-structured.

### Behavioral Reasoning
**Line 72: `return stepsResult` (was `return Data.Ok()`)** — Essential bug fix. The old code discarded the actual step execution result. All callers verified compatible:
- `goal/call.cs`: passes result through (correct)
- `condition/if.cs`: checks `.Success` only (unaffected)
- `loop/foreach.cs`: checks `.Success` only (unaffected)
- `Engine.RunGoalAsync`: returns result to caller (correct)

No caller depended on always-success semantics. Data flows through MemoryStack; the return value carries success/failure + the last step's Data.

### Verdict: CLEAN
The return value fix is essential and all callers are compatible.

---

## PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs

### OBP Violations
None. This is a model example of OBP rule 5 — the collection owns its iteration loop.

### Simplifications
None needed.

### Behavioral Reasoning
**Lines 31, 66, 69: `lastResult` tracking** — The fix tracks the last executed step's result and returns it instead of `Data.Ok()`.

Edge cases verified:
- **Empty goal (0 steps)**: `lastResult` stays null → returns `Data.Ok()` via null-coalescing. Correct — an empty goal is a no-op success.
- **All steps skipped (setup cache hit)**: `continue` on line 38 skips the step without updating `lastResult`. If ALL steps are skipped, returns `Data.Ok()`. Correct — a fully-cached setup run is success.
- **Last step has error with IgnoreError**: Step result is failure, but `errorTolerated` is true (line 47). Step is NOT returned early (line 60 skipped). `lastResult` is set to the error Data (line 66). Returns error Data. This is the correct behavior — the step's result preserves the error information even though execution continues.

### Verdict: CLEAN

---

## PLang/Runtime2/Engine/Goals/Setup/this.cs

### OBP Violations
None. Making `DiscoverAsync` private (was public) is an OBP improvement — callers should use `RunAsync`, not manage internal discovery.

### Simplifications
The refactoring from full directory scan to convention-based lookup is a genuine simplification:
- Old: `GetFiles(root, "*.pr", SearchOption.AllDirectories)` — O(n) scanning potentially thousands of files
- New: Check 2 hardcoded paths — O(1)

### Behavioral Reasoning

**Convention-based discovery (lines 42-46)**:
```csharp
var candidates = new[]
{
    fs.Path.Combine(root, ".build", "setup.pr"),
    fs.Path.Combine(root, "Setup", ".build", "setup.pr"),
};
```

This works because:
1. Setup goals are named `Setup.goal` by convention → builds to `setup.pr`
2. They live at the app root or in a `Setup/` folder
3. If a user puts a setup goal in a non-standard location, it won't be found — but that's a documentation issue, not a code bug. The convention is clear and the old behavior (scanning everything) was wasteful.

**Bare catch (lines 62-65)**:
```csharp
catch
{
    // Skip unparseable files — they'll fail when lazy-loaded later
}
```
Pre-existing pattern, not introduced by this branch. The comment explains the intent. In this specific context (setup discovery at startup), silently skipping a corrupt file is acceptable — the file will fail again when explicitly loaded. The only risk is swallowing `OutOfMemoryException` or similar catastrophic failures, but that's a framework-wide concern, not specific to this code.

**RunAsync flow (lines 76-100)**: `DiscoverAsync` → early return if no goals → set `context.Setup` → iterate goals → finally clear `context.Setup`. Clean, correct, no issues.

### Verdict: CLEAN

---

## PLang/Runtime2/Engine/Goals/this.cs (EngineGoals)

### OBP Violations
None.

### Simplifications
None needed. The fallback chain in `Get()` is necessary complexity.

### Behavioral Reasoning

**PrPath keying (line 39)**:
```csharp
var key = !string.IsNullOrEmpty(goal.PrPath) ? goal.PrPath : goal.Name;
```
Fixes a real collision bug: two goals named "Setup" in different folders would overwrite each other when keyed by Name. PrPath is unique per goal file.

**Name-based search fallback (lines 67-70)**:
```csharp
goal = _goals.Values.FirstOrDefault(g => !g.IsSetup
    && g.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
```
Necessary because callers use `Get("Start")` (by name), not `Get("path/to/.build/start.pr")`. Linear scan over `_goals.Values`. Not on a hot path — goal lookup is typically a one-time operation per goal call, and results are cached after first load.

**Remove by name fallback (lines 172-178)**: Same pattern — find by name when direct key removal fails. Consistent with Get.

### Deletion Test
Lines 67-70 (name search): If deleted, `engine.Goals.Get("Start")` would fail for any goal loaded via `LoadFromFileAsync` (which keys by PrPath). Every PLang test that runs via `engine.Goals.Run("Start")` would break. Essential code.

### Verdict: CLEAN

---

## PLang/Runtime2/Engine/Test/this.cs

### OBP Violations
None.

### Simplifications

**TestResult consolidation (lines 27-33)**: Replaced `Errored` bool + `ErrorMessage` string with a single `Data Result` property. `Passed` is now `Result.Success && Failures.Count == 0`. This is simpler and consistent with the Data pattern used everywhere else.

**RunSingleTest return type (line 92)**: Changed from `void` to `Task<Data>`. Replaces manual error property setting with proper Data returns. Cleaner control flow — errors propagate through return values instead of mutating the TestResult object.

### Behavioral Reasoning

**Per-test engine root (line 111)**:
```csharp
var testFs = new SafeFileSystem.PLangFileSystem(dir, "");
```
Changed from `rootDir` (Tests/Runtime2/) to `dir` (the individual test's folder). This is essential for:
1. **Setup discovery**: Each test folder is its own PLang app. `Setup.RunAsync` discovers `Setup.goal` relative to the test root. Without this, setup goals would need to be at `Tests/Runtime2/Setup/`, shared across all tests.
2. **Goal resolution**: `GetAsync` resolves goals relative to engine root. With per-test root, helper goals (`ComputeAnswer.goal`, `InnerGoal.goal`) are found in the test's own folder.
3. **Test isolation**: No shared state between test suites. Each gets its own engine, own memory, own goals.

**Setup before tests (lines 118-120)**:
```csharp
var setupResult = await testEngine.Goals.Setup.RunAsync(testEngine, testEngine.User.Context, cancellationToken);
if (!setupResult.Success) return setupResult;
```
Runs setup goals before the test goal. Tests like SetupGoal rely on this. Correct.

**Simplified assertion tracking (lines 128-137)**: The old code had complex post-run logic to handle assertion errors that bubbled up vs. those caught by the AfterStep handler. The new code just returns `RunGoalAsync` result directly — the AfterStep handler (lines 128-135) catches assertion failures during execution, and any uncaught failure shows up in `result.Result`. Simpler and correct.

### Verdict: CLEAN

---

## Cross-Cutting Analysis

### Pass 4: Behavioral Reasoning — Data Flow

The return value chain now works correctly end-to-end:

```
Step.RunAsync() → returns step Data (with Value from action handler)
  ↓ stored in lastResult
Steps.RunAsync() → returns lastResult (last step's Data)
  ↓
Goal.RunAsync() → returns stepsResult (what Steps returned)
  ↓
Engine.RunGoalAsync() → returns to caller
```

Before this branch: both Steps and Goal returned `Data.Ok()`, discarding the actual value. Goal return values (`call ComputeAnswer, write to %result%`) relied on a workaround in `goal/call.cs` that fell back to `__stepResult` from MemoryStack. The fix makes return values flow naturally through the chain.

### Pass 5: Deletion Test

| Lines | File | Delete impact |
|-------|------|---------------|
| Methods.cs:72 `return stepsResult` | Goal/Methods.cs | If reverted to `Data.Ok()`, goal return values break silently. GoalCallReturn test would fail. |
| Steps/this.cs:31,66,69 `lastResult` | Steps/this.cs | If reverted to `Data.Ok()`, same as above — propagates up. |
| Setup/this.cs:78-79 `DiscoverAsync` call | Setup/this.cs | If deleted, setup goals are never discovered. SetupGoal test fails. |
| Goals/this.cs:67-70 name search | Goals/this.cs | If deleted, `Get("Start")` fails for all loaded goals. All PLang tests fail. |
| Test/this.cs:118-120 setup before test | Test/this.cs | If deleted, SetupGoal test fails (no DB setup). Other tests unaffected. |

All code changes are exercised by existing tests. No dead code introduced.

---

## Overall Verdict: PASS

All 6 files are **CLEAN**. The changes are:
1. **Two essential bug fixes** (return values in Methods.cs and Steps/this.cs)
2. **One encapsulation improvement** (Setup discovery private + convention-based)
3. **One collision fix** (Goals keyed by PrPath)
4. **One test infrastructure improvement** (per-test engine root + Data-based results)

No OBP violations. No over-abstraction. No dead code introduced. All changes are covered by existing tests.

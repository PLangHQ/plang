# Code Analysis v2 — runtime2-plang-test-gaps

**Branch:** runtime2-plang-test-gaps
**Analyzer:** codeanalyzer v2
**Date:** 2026-03-09
**Scope:** Full branch diff — all 7 runtime C# files + 8 test files, final state after coder v1+v2 and tester v1-v3.

---

## PLang/Executor.cs

### OBP Violations
None.

### Simplifications
None. The `Run2` change (lines 366-372) is a clean 6-line addition: run setup → early return if "setup" command → run main goal.

### Readability
Clean. The setup-only early return at line 371 reads naturally: "if the user asked for setup and we have setup goals, we're done."

### Behavioral Reasoning
**Context consistency**: `engine.Context` at line 367 resolves to `engine.User.Context` (verified at `Engine/this.cs:158`). This matches the test runner at `Test/this.cs:119` which passes `testEngine.User.Context`. Consistent.

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Methods.cs

### OBP Violations
None.

### Simplifications
None needed.

### Behavioral Reasoning
**Line 72: `return stepsResult`** — Essential fix. The old code returned `Data.Ok()`, discarding the actual step execution result. Now return values propagate correctly through:
```
Steps.RunAsync() → Goal.RunAsync() → Engine.RunGoalAsync() → caller
```

All callers verified compatible (same analysis as v1 — no new callers added).

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/Steps/this.cs

### OBP Violations
None. Exemplary OBP rule 5 — collection owns its iteration.

### Behavioral Reasoning
**`lastResult` tracking (lines 31, 66, 69)**: Correctly returns the last executed step's result. Edge cases:
- Empty goal → `lastResult` stays null → `Data.Ok()` via null-coalescing ✓
- All steps cached (setup) → all `continue` → `Data.Ok()` ✓
- Error with IgnoreError → `lastResult` set to error Data but execution continues ✓

### Verdict: CLEAN

---

## PLang/App/Goals/Goal/this.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
**PrPath computed property (lines 41-54)**: Dense string manipulation — `LastIndexOfAny`, `LastIndexOf`, substring slicing. The logic (convert `/Foo.goal` → `/.build/foo.pr`) is correct but takes effort to parse visually.

Not a finding per se — the property is computed once, the logic is correct, and a helper method would just move the same code elsewhere. But noting it for awareness.

### Behavioral Reasoning
**PrPath null guard (line 45)**: `string.IsNullOrEmpty(Path)` — returns null if Path is null or empty. This was the tester v2 finding #10 fix. Correct.

**`init` setter on PrPath (line 53)**: Empty init-only setter with comment. This is a serialization compatibility trick — `System.Text.Json` won't deserialize `PrPath` from JSON (since it's read-only), and the `init` prevents callers from setting it directly. Clever but justified — the alternative would be `[JsonIgnore]` which changes the serialization contract.

### Deletion Test
**Lines 41-54 (PrPath property)**: If deleted, `Goals.Add()` throws for every goal (line 36 checks `goal.PrPath`). All tests would fail. Essential code.

### Verdict: CLEAN

---

## PLang/App/Goals/Setup/this.cs

### OBP Violations
None. `DiscoverAsync` is correctly private — callers go through `RunAsync`.

### Simplifications
Convention-based discovery (2 hardcoded paths) is a genuine improvement over directory scanning.

### Behavioral Reasoning

**Bare catch (lines 62-65)**:
```csharp
catch
{
    // Skip unparseable files — they'll fail when lazy-loaded later
}
```
This swallows ALL exceptions including `OutOfMemoryException`. The intent (skip corrupt .pr files at startup) is valid, but the mechanism is too broad.

**Finding #1 (Minor)**: Consider catching `Exception` instead of bare `catch`, and logging or at minimum excluding `OutOfMemoryException`, `StackOverflowException`, `ThreadAbortException`. However, this is pre-existing code from before this branch and the pattern is common in startup discovery. Not a regression.

**Setup iteration (lines 86-93)**: The `foreach (var goal in Goals)` iterates the `Goals` LINQ query. Since `Goals` reads from `_goals.AllIncludingSetup` (the `ConcurrentDictionary.Values`), this evaluates the query each time. But the iterator is created once at foreach start and the collection is only modified during `DiscoverAsync` which completed before this loop. Safe.

### Verdict: CLEAN

---

## PLang/App/Goals/this.cs (EngineGoals)

### OBP Violations
None.

### Simplifications

**Get() method (lines 47-91)** — This is a 44-line method with a multi-stage fallback chain: strip extension → try PrPath key → try path index → search by Name → try 4 variations each through 3 lookups. It works, but it's the most complex method in the changed files.

Each variation loop iteration (lines 79-89) does up to 3 lookups: `_goals.TryGetValue`, `_byPath.TryGetValue`, and `_goals.Values.FirstOrDefault`. The `FirstOrDefault` is O(n) and runs for each of 4 variations if earlier lookups fail. For a small goal collection this is negligible, but the code structure could be clearer.

Not flagging as a finding — the complexity is necessary to support the various ways goals are referenced (by name, by path, with/without extension, with forward/back slashes). But noting it for awareness.

### Behavioral Reasoning

**PrPath enforcement (lines 36-37)**:
```csharp
if (string.IsNullOrEmpty(goal.PrPath))
    throw new ArgumentException(...);
```
This throws, which is correct for `Add()` — it's a programming error (violation of precondition), not a runtime data error. The throw is caught by `LoadFromFileAsync` line 307's `catch (Exception ex)` → `Data.FromError(Error.FromException(ex))`. So a .pr file without `path` returns a Data error to the caller rather than crashing the engine. Correct error propagation.

**Names property (line 197)**:
```csharp
public IEnumerable<string> Names => _goals.Values.Where(g => !g.IsSetup).Select(g => g.Name);
```
Correctly filters out setup goals. Tester v2 finding #9 fix verified.

**Get() excludes setup goals (lines 57, 61, 65-66, 81, 83, 85-86)**: Every lookup path has `!g.IsSetup` check. Consistent.

**Remove by name fallback (lines 173-180)**: When `_goals.TryRemove(name)` fails (because _goals is keyed by PrPath, not Name), it falls back to searching by Name. Finds the entry, removes by its PrPath key, and cleans up `_byPath`. Correct.

### Pass 5: Deletion Test

| Lines | Delete impact |
|-------|--------------|
| 36-37 (PrPath enforcement) | If deleted, goals without Path silently enter the collection. No immediate test failure, but PrPath-keyed lookups would break unpredictably. `Add_ThrowsWhenNoPrPath` and `Add_ThrowsWhenPathIsEmptyString` tests catch this. |
| 65-66 (Name search fallback) | If deleted, `Get("Start")` fails for goals added via `Add()` or `LoadFromFileAsync`. Virtually all PLang tests break. |
| 197 (Names filter) | If deleted (reverted to unfiltered), `Names_ExcludesSetupGoals` test catches it. |

### Verdict: CLEAN

---

## PLang/App/Test/this.cs

### OBP Violations
None.

### Simplifications
**TestResult (lines 27-33)**: Clean consolidation — `Data Result` replaces `bool Errored` + `string ErrorMessage`. `Passed` is now `Result.Success && Failures.Count == 0`. Simpler.

### Behavioral Reasoning

**Per-test engine root (line 111)**:
```csharp
var testFs = new SafeFileSystem.PLangFileSystem(dir, "");
```
Uses `dir` (the test's own folder) instead of `rootDir` (Tests/App/). Essential for:
1. Setup goal discovery works per-test
2. Helper goals resolve relative to test folder
3. Full isolation between test suites

**Setup before tests (lines 118-120)**:
```csharp
var setupResult = await testEngine.Goals.Setup.RunAsync(testEngine, testEngine.User.Context, cancellationToken);
if (!setupResult.Success) return setupResult;
```
Runs setup before each test. Uses `testEngine.User.Context` which is consistent with `engine.Context` in Executor.cs (both resolve to `User.Context`).

**Finding #2 (Observation — NOT a bug)**: The `RunSingleTest` method takes `rootDir` as a parameter (line 94) but doesn't use it. It was the original engine root before the per-test root change. The parameter is still passed by the caller (line 76) but is now dead. Not a correctness issue — just dead parameter.

### Deletion Test
| Lines | Delete impact |
|-------|--------------|
| 118-120 (setup before test) | If deleted, SetupGoal test fails. Other tests unaffected. |
| 128-135 (assertion tracking) | If deleted, assertion failures aren't tracked. Tests that rely on assertion failure detection would report PASS incorrectly. |

### Verdict: NEEDS WORK (minor — dead parameter)

---

## C# Test Files Analysis

### GoalsTests.cs
All 30+ goal creations now include `Path = "/GoalName.goal"`. 8 new tests added for PrPath keying and enforcement. Tests are honest — they test the actual behavior, not implementation details.

### SetupTests.cs
3 `DiscoverAsync` tests correctly rewritten to go through `RunAsync`. 2 new convention discovery tests added. JSON in test data now includes `"path"` field. All correct.

### EngineTests.cs, StartGoalTests.cs, StepErrorHandlingTests.cs, StepRetryTests.cs
Mechanical changes — adding `Path` to goal object initializers. No behavioral changes.

### ConditionHandlerTests.cs, ForeachTests.cs
Same mechanical `Path` addition. 1-2 lines each.

### Verdict: CLEAN (all test files)

---

## Cross-Cutting Analysis

### Pass 4: Full Return Value Data Flow

The return value chain is now correct end-to-end:
```
Step.RunAsync() → Data with action result
  ↓ stored in lastResult (Steps/this.cs:66)
Steps.RunAsync() → lastResult ?? Data.Ok()
  ↓
Goal.RunAsync() → stepsResult (Methods.cs:72)
  ↓
Engine.RunGoalAsync() → returns to caller
```

This enables goal return values (`call ComputeAnswer, write to %result%`) to work naturally.

### Pass 4: Clone/Copy Family Audit

Goal has `Path` (existing property, now enforced). No new properties were added to Goal, Step, or PLangContext. The `PrPath` computed property derives from `Path` — no clone issue possible. Clean.

### Pass 4: JSON Numeric Boxing

No new generic cast paths or JSON deserialization changes in the runtime source. The test JSON strings added `"path"` as a string field — no boxing risk. Clean.

### Pass 5: Deletion Test — Dead Parameter

**`Test/this.cs` line 94**: `string rootDir` parameter on `RunSingleTest` is unused. If removed from the signature and call site (line 76), no test breaks and no behavior changes.

---

## Findings Summary

| # | File | Severity | Description |
|---|------|----------|-------------|
| 1 | Setup/this.cs:62-65 | Minor (pre-existing) | Bare `catch` swallows all exceptions including catastrophic ones. Consider `catch (Exception)` at minimum. Not a regression. |
| 2 | Test/this.cs:94 | Minor | `rootDir` parameter is dead — not used after per-test root change. Harmless but untidy. |

Both findings are minor and neither affects correctness. Finding #1 is pre-existing. Finding #2 is cosmetic.

---

## Overall Verdict: PASS

All 7 runtime source files and 8 test files are **clean or near-clean**. The branch delivers:

1. **Return value propagation** (Methods.cs + Steps/this.cs) — essential bug fix, correctly implemented
2. **PrPath keying with enforcement** (Goals/this.cs + Goal/this.cs) — collision fix, robust with good test coverage
3. **Convention-based setup discovery** (Setup/this.cs) — simplification from O(n) scan to O(1) lookup
4. **Per-test engine isolation** (Test/this.cs) — enables setup goals and helper goals in test suites
5. **Names setup filter** (Goals/this.cs:197) — correctness fix validated by tester v2/v3

No OBP violations. No over-abstraction. No security concerns. Two minor findings (one pre-existing, one cosmetic dead parameter). All changes are covered by C# and PLang tests.

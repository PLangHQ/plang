# Phase 1.7: PLang Testing App

## Context

PLang tests today are "print and eyeball it" — no assertions, no pass/fail, no counts. Every module added means more manual verification. C# has 1023 tests with TUnit giving structured pass/fail. PLang needs the same foundation so every future module gets proper PLang-level tests from day one.

The user wants:
- `plang !test` to discover and run `*.test.goal` files
- An `assert` module for assertions (equals, true, false, notNull, contains, etc.)
- Minimal C# — the test runner itself should be PLang, C# just provides the `!test` entry point
- `Goal.IsTest` set automatically if a goal uses the assert module

---

## What We're Building

### 1. Assert Module (`assert/*`)

Action handlers following the standard `[Action]` pattern:

| Handler | Parameters | Behavior |
|---------|-----------|----------|
| `equals` | Expected: object, Actual: object, Message: string? | Fail if not equal |
| `notEquals` | Expected: object, Actual: object, Message: string? | Fail if equal |
| `isTrue` | Value: object, Message: string? | Fail if not truthy |
| `isFalse` | Value: object, Message: string? | Fail if truthy |
| `isNull` | Value: object?, Message: string? | Fail if not null |
| `isNotNull` | Value: object?, Message: string? | Fail if null |
| `contains` | Value: object, Container: object, Message: string? | Fail if container doesn't contain value |
| `greaterThan` | A: object, B: object, Message: string? | Fail if A <= B |
| `lessThan` | A: object, B: object, Message: string? | Fail if A >= B |

**On success:** `Data.Ok(true)` — no output, test continues silently.

**On failure:** `Data.Fail(new AssertionError(...))` with clear message showing expected vs actual.

**New error type:** `AssertionError : Error` with `Expected`, `Actual`, `UserMessage` properties + a key like `"AssertionFailed"`.

**Reference:** Runtime1's `PLang/Modules/AssertModule/Program.cs` has `IsEqual` and `Contains`. The App version will be cleaner — no ObjectValue unwrapping, use TypeMapping.ConvertTo for comparisons, return Data.Fail instead of IError?.

**Files to create:**
- `PLang/App/modules/assert/equals.cs`
- `PLang/App/modules/assert/notEquals.cs`
- `PLang/App/modules/assert/isTrue.cs`
- `PLang/App/modules/assert/isFalse.cs`
- `PLang/App/modules/assert/isNull.cs`
- `PLang/App/modules/assert/isNotNull.cs`
- `PLang/App/modules/assert/contains.cs`
- `PLang/App/modules/assert/greaterThan.cs`
- `PLang/App/modules/assert/lessThan.cs`
- `PLang/App/modules/assert/types.cs`
- `PLang/App/Errors/AssertionError.cs`

### 2. Test Runner (`!test` flag)

**How `!debug` works today (same pattern for `!test`):**
1. `plang !test` → CommandLineParser parses `!test` → `parameters["test"] = true`
2. `Executor.Run2()` checks `parameters.TryGetValue("test", ...)` → calls `TestMode.Apply(app)`
3. `TestMode.Apply` is a C# class that:
   - Sets `app.IsTestMode = true`
   - Discovers all `*.test.goal` files (via `fileSystem.Directory.GetFiles(rootDir, "*.test.goal", SearchOption.AllDirectories)`)
   - Loads each as a Goal
   - Runs each goal, tracking pass/fail via events
   - Prints summary at end

**Test discovery:** Find all `*.test.goal` files under the current directory recursively. Each file is one test suite.

**Test execution flow:**
1. For each `*.test.goal` file, load and run the Start goal
2. If any assert action fails → that step failed. With `on error ignore`, it's counted as failure but test continues. Without it, test stops.
3. After all test goals run, print summary.

**The test runner is mostly C# (like DebugMode)** because it needs to:
- Discover files before any PLang runs
- Register events on the app to intercept assert failures
- Print the summary with proper exit codes

But it's minimal C# — the actual test logic is all PLang.

**Tracking results:** TestMode registers a `beforeGoal` and `afterStep` event:
- `beforeGoal`: Record which test goal is starting
- `afterStep`: If step failed with `AssertionError`, record it as an assertion failure
- After all goals complete, print:
  ```
  Test run summary: 3 passed, 1 failed, 4 total

  FAILED: Tests/App/Math/Math.test.goal
    [2] assert %sum% equals 9 — Expected: 9, Actual: 8
  ```

**Exit code:** 0 if all pass, 1 if any fail.

**Files to create/modify:**
- `PLang/App/Core/TestMode.cs` (new — like DebugMode.cs)
- `PLang/App/Core/App.cs` (add `IsTestMode` property)
- `PLang/App/Context/PLangAppContext.cs` (add `IsTestMode`)
- `PLang/Executor.cs` (add `!test` handling in `Run2()`)

### 3. Goal.IsTest Property

**Optional enhancement:** Add `Goal.IsTest` flag set during build validation if any step uses the `assert` module. This allows the app to know a goal is a test goal even without the `*.test.goal` naming convention. But `*.test.goal` is the primary discovery mechanism.

**File:** `PLang/App/Core/Goal.cs` — add `public bool IsTest { get; set; }`

### 4. Rewrite Existing Tests as `*.test.goal`

Convert the 5 existing PLang test suites from "print output" to proper assertions:

**Example — `Tests/App/Math/Math.test.goal`:**
```plang
Start
- add 5 and 3, write to %sum%
- assert %sum% equals 8
- subtract 3 from 10, write to %diff%
- assert %diff% equals 7
- multiply 6 by 7, write to %product%
- assert %product% equals 42
- divide 10 by 4, write to %quotient%
- assert %quotient% equals 2.5
- get absolute value of -42, write to %absVal%
- assert %absVal% equals 42
- round 3.14159 to 2 decimal places, write to %rounded%
- assert %rounded% equals 3.14
- get min of 5 and 3, write to %minimum%
- assert %minimum% equals 3
- get max of 5 and 3, write to %maximum%
- assert %maximum% equals 5
```

Files to create:
- `Tests/App/Loop/Loop.test.goal`
- `Tests/App/ListOps/ListOps.test.goal`
- `Tests/App/Math/Math.test.goal`
- `Tests/App/Convert/Convert.test.goal`
- `Tests/App/ErrorHandling/ErrorHandling.test.goal`

Keep existing `Start.goal` files for manual/demo usage. The `.test.goal` files are the real tests.

### 5. C# Unit Tests for Assert Module

- `PLang.Tests/App/Modules/assert/AssertTests.cs`
- Test each handler: pass case, fail case, type coercion, null handling

---

## Implementation Order

1. **AssertionError** — new error type
2. **Assert module handlers** — 9 handlers + types.cs
3. **C# tests** for assert module
4. **TestMode.cs** — discovery, execution, reporting
5. **Wire `!test` in Executor.Run2** and App
6. **Write `.test.goal` files** — convert existing 5 test suites
7. **Build and run** — `plang build` then `plang !test`
8. **Goal.IsTest** — optional, add if time permits

---

## Verification

1. `dotnet build PlangConsole/PlangConsole.csproj` — 0 errors
2. `dotnet run --project PLang.Tests` — all tests pass (including new assert tests)
3. `cd Tests/App && plang build` — all test.goal files build successfully
4. `cd Tests/App && plang !test` — discovers and runs all *.test.goal, prints summary like:
   ```
   Test run summary: Passed!
     total: 5 suites, 40+ assertions
     failed: 0
     passed: 5
   ```
5. Intentionally break one assertion to verify failure reporting works

---

## Files Summary

**New files:**
- `PLang/App/Errors/AssertionError.cs`
- `PLang/App/modules/assert/equals.cs` (+ 8 more handlers + types.cs)
- `PLang/App/Core/TestMode.cs`
- `PLang.Tests/App/Modules/assert/AssertTests.cs`
- `Tests/App/*/*.test.goal` (5 test files)

**Modified files:**
- `PLang/App/Core/App.cs` — add IsTestMode
- `PLang/App/Context/PLangAppContext.cs` — add IsTestMode
- `PLang/App/Core/Goal.cs` — add IsTest
- `PLang/Executor.cs` — add !test handling in Run2()

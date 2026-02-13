# Phase 1.7: PLang Testing Engine

## Context

PLang tests today are "print and eyeball it" — no assertions, no pass/fail, no counts. Every module added means more manual verification. C# has 1023 tests with TUnit giving structured pass/fail. PLang needs the same foundation so every future module gets proper PLang-level tests from day one.

The user wants:
- `plang p !test` to discover and run `*.test.goal` files
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

**Reference:** Runtime1's `PLang/Modules/AssertModule/Program.cs` has `IsEqual` and `Contains`. The Runtime2 version will be cleaner — no ObjectValue unwrapping, use TypeMapping.ConvertTo for comparisons, return Data.Fail instead of IError?.

**Files to create:**
- `PLang/Runtime2/modules/assert/equals.cs`
- `PLang/Runtime2/modules/assert/notEquals.cs`
- `PLang/Runtime2/modules/assert/isTrue.cs`
- `PLang/Runtime2/modules/assert/isFalse.cs`
- `PLang/Runtime2/modules/assert/isNull.cs`
- `PLang/Runtime2/modules/assert/isNotNull.cs`
- `PLang/Runtime2/modules/assert/contains.cs`
- `PLang/Runtime2/modules/assert/greaterThan.cs`
- `PLang/Runtime2/modules/assert/lessThan.cs`
- `PLang/Runtime2/modules/assert/types.cs`
- `PLang/Runtime2/Errors/AssertionError.cs`

### 2. Test Runner (`!test` flag)

**How `!debug` works today (same pattern for `!test`):**
1. `plang p !test` → CommandLineParser parses `!test` → `parameters["test"] = true`
2. `Executor.Run2()` checks `parameters.TryGetValue("test", ...)` → calls `TestMode.Apply(engine)`
3. `TestMode.Apply` is a C# class that:
   - Sets `engine.IsTestMode = true`
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
- Register events on the engine to intercept assert failures
- Print the summary with proper exit codes

But it's minimal C# — the actual test logic is all PLang.

**Tracking results:** TestMode registers a `beforeGoal` and `afterStep` event:
- `beforeGoal`: Record which test goal is starting
- `afterStep`: If step failed with `AssertionError`, record it as an assertion failure
- After all goals complete, print:
  ```
  Test run summary: 3 passed, 1 failed, 4 total

  FAILED: Tests/Runtime2/Math/Math.test.goal
    [2] assert %sum% equals 9 — Expected: 9, Actual: 8
  ```

**Exit code:** 0 if all pass, 1 if any fail.

**Files to create/modify:**
- `PLang/Runtime2/Core/TestMode.cs` (new — like DebugMode.cs)
- `PLang/Runtime2/Core/Engine.cs` (add `IsTestMode` property)
- `PLang/Runtime2/Context/PLangAppContext.cs` (add `IsTestMode`)
- `PLang/Executor.cs` (add `!test` handling in `Run2()`)

### 3. Goal.IsTest Property

**Optional enhancement:** Add `Goal.IsTest` flag set during build validation if any step uses the `assert` module. This allows the engine to know a goal is a test goal even without the `*.test.goal` naming convention. But `*.test.goal` is the primary discovery mechanism.

**File:** `PLang/Runtime2/Core/Goal.cs` — add `public bool IsTest { get; set; }`

### 4. Rewrite Existing Tests as `*.test.goal`

Convert the 5 existing PLang test suites from "print output" to proper assertions:

**Example — `Tests/Runtime2/Math/Math.test.goal`:**
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
- `Tests/Runtime2/Loop/Loop.test.goal`
- `Tests/Runtime2/ListOps/ListOps.test.goal`
- `Tests/Runtime2/Math/Math.test.goal`
- `Tests/Runtime2/Convert/Convert.test.goal`
- `Tests/Runtime2/ErrorHandling/ErrorHandling.test.goal`

Keep existing `Start.goal` files for manual/demo usage. The `.test.goal` files are the real tests.

### 5. C# Unit Tests for Assert Module

- `PLang.Tests/Runtime2/Modules/assert/AssertTests.cs`
- Test each handler: pass case, fail case, type coercion, null handling

---

## Implementation Order

1. **AssertionError** — new error type
2. **Assert module handlers** — 9 handlers + types.cs
3. **C# tests** for assert module
4. **TestMode.cs** — discovery, execution, reporting
5. **Wire `!test` in Executor.Run2** and Engine
6. **Write `.test.goal` files** — convert existing 5 test suites
7. **Build and run** — `plang p build` then `plang p !test`
8. **Goal.IsTest** — optional, add if time permits

---

## Verification

1. `dotnet build PlangConsole/PlangConsole.csproj` — 0 errors
2. `dotnet run --project PLang.Tests` — all tests pass (including new assert tests)
3. `cd Tests/Runtime2 && plang p build` — all test.goal files build successfully
4. `cd Tests/Runtime2 && plang p !test` — discovers and runs all *.test.goal, prints summary like:
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
- `PLang/Runtime2/Errors/AssertionError.cs`
- `PLang/Runtime2/modules/assert/equals.cs` (+ 8 more handlers + types.cs)
- `PLang/Runtime2/Core/TestMode.cs`
- `PLang.Tests/Runtime2/Modules/assert/AssertTests.cs`
- `Tests/Runtime2/*/*.test.goal` (5 test files)

**Modified files:**
- `PLang/Runtime2/Core/Engine.cs` — add IsTestMode
- `PLang/Runtime2/Context/PLangAppContext.cs` — add IsTestMode
- `PLang/Runtime2/Core/Goal.cs` — add IsTest
- `PLang/Executor.cs` — add !test handling in Run2()

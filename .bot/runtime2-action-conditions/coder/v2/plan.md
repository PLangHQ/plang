# Coder v2 Plan — Fix Auditor Findings

## Scope
Address all 4 auditor findings + security finding #4 (non-IComparable silent fallback).

## Changes

### 1. If.Run() — try/catch around evaluator calls (auditor #1)
**File:** `PLang/App/modules/condition/if.cs`
- Wrap evaluator calls in `try { ... } catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)`
- Return `Data.FromError(new ValidationError(..., Context, "EvaluationError"))` with rich message including operator, left/right types and values, plus FixSuggestion
- Use `ValidationError` (not `ServiceError`) — this is input validation: bad operator or incompatible types from the builder
- Pass `Context` to get step text, file, line, call stack in the formatted error

### 2. Compare.Run() — same pattern (auditor #2)
**File:** `PLang/App/modules/condition/compare.cs`
- Same try/catch pattern with same rich error

### 3. WiderNumericType fallback to decimal (auditor #3)
**File:** `PLang/App/modules/condition/providers/DefaultEvaluator.cs`
- Change `if (ai < 0) ai = 0; if (bi < 0) bi = 0;` to use `order.Length - 1` (decimal) instead of 0 (byte)

### 4. Compare() returns error for non-IComparable (security #4)
**File:** `PLang/App/modules/condition/providers/DefaultEvaluator.cs`
- Change `return 0;` to `throw new ArgumentException($"Type '{left.GetType().Name}' does not support comparison")` in the private Compare method
- This will be caught by the try/catch in If.Run()/Compare.Run() from fix #1/#2

### 5. Tests
**Files:** `IfHandlerTests.cs`, `CompareHandlerTests.cs`, `DefaultEvaluatorTests.cs`

- **If.Run() unsupported operator** — assert `Success == false`, `Error.Key == "EvaluationError"`
- **Compare.Run() unsupported operator** — same pattern
- **Compare doesn't set __condition__** — negative test (auditor #4)
- **WiderNumericType unknown type** — test with ushort, verify widens to decimal
- **Non-IComparable comparison throws** — test via Evaluate with non-comparable type
- **Strengthen weak assertions** — add Error.Key checks to existing `Run_GoalExecutionFails_PropagatesError` (tester finding #3)

## Build & verify
- `dotnet build PLang.sln` — compile
- `dotnet run --project PLang.Tests` — all tests pass

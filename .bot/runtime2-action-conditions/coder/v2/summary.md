# Coder v2 Summary — Fix Auditor Findings

## What this is
Fixes the auditor's FAIL verdict on the action-based conditions implementation. The auditor found that `If.Run()` and `Compare.Run()` could throw unhandled exceptions from the evaluator, violating the "behavior methods never throw" contract.

## What was done

### Fix 1: If.Run() try/catch (auditor #1 — major)
**File:** `PLang/App/modules/condition/if.cs`
- Wrapped evaluator calls in `try/catch` for `NotSupportedException | ArgumentException | OverflowException`
- Returns `Data.FromError(ValidationError)` with rich diagnostic message including operator, operand types/values, and fix suggestion
- Uses `ValidationError` with `Context` parameter — gets step text, file, line, call stack automatically from the error formatter

### Fix 2: Compare.Run() try/catch (auditor #2 — major)
**File:** `PLang/App/modules/condition/compare.cs`
- Same try/catch pattern with same rich error

### Fix 3: WiderNumericType fallback (auditor #3 — minor)
**File:** `PLang/App/modules/condition/providers/DefaultEvaluator.cs:150-151`
- Changed fallback from index 0 (byte) to `order.Length - 1` (decimal)
- Unknown numeric types now widen safely instead of narrowing to byte

### Fix 4: Non-IComparable throws (security #4)
**File:** `PLang/App/modules/condition/providers/DefaultEvaluator.cs:62`
- Changed `return 0` to `throw new ArgumentException` for non-IComparable types in comparison operators
- Caught by the try/catch from fix #1/#2, converted to `EvaluationError`

### Fix 5: Tests (7 new)
- `IfHandlerTests.Run_UnsupportedOperator_ReturnsEvaluationError` — verifies error key and message content
- `IfHandlerTests.Run_IncompatibleComparisonTypes_ReturnsEvaluationError` — non-IComparable via If
- `IfHandlerTests.Run_GoalExecutionFails_PropagatesError` — strengthened with Error.Key assertion
- `CompareHandlerTests.Run_UnsupportedOperator_ReturnsEvaluationError` — same pattern
- `CompareHandlerTests.Run_DoesNotSetConditionSignal` — negative test (auditor #4)
- `CompareHandlerTests.Run_NonComparableType_ReturnsEvaluationError` — non-IComparable via Compare
- `DefaultEvaluatorTests.Evaluate_NonComparableType_GreaterThan_ThrowsArgumentException` — direct evaluator test
- `DefaultEvaluatorTests.Evaluate_UnknownNumericType_DoesNotThrow` — ushort outside standard set

## Code example

The error message pattern for If.Run():
```csharp
catch (Exception ex) when (ex is NotSupportedException or ArgumentException or OverflowException)
{
    return Data.FromError(EvaluationError(ex));
}

private ValidationError EvaluationError(Exception ex)
{
    var leftType = Left?.GetType().Name ?? "null";
    var rightType = Right?.GetType().Name ?? "null";
    var message = Operator != null
        ? $"Condition evaluation failed: '{Left}' ({leftType}) {Operator} '{Right}' ({rightType}) — {ex.Message}"
        : $"Condition evaluation failed: IsTruthy('{Left}' ({leftType})) — {ex.Message}";

    return new ValidationError(message, Context, "EvaluationError")
    {
        Exception = ex,
        FixSuggestion = "Check that operator '...' is supported (...) and that both operands are compatible types."
    };
}
```

## Test results
- 1595 total (1588 existing + 7 new), 0 failures, 0 skipped

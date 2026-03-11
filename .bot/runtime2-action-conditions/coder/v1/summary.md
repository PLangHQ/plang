# v1 Summary -- Action-Based Conditions Implementation

## What this is

Implements action-based conditions for PLang Runtime2. Replaces the single `bool Condition` parameter on `condition.if` with `Left/Operator/Right` evaluation, adds a pluggable evaluator interface, a `condition.compare` action, and sub-step execution logic (indented steps after conditions). All 69 C# tests pass, plus all 1580 existing tests (zero regressions).

## What was done

### Production code (5 files)

1. **`PLang/Runtime2/modules/condition/providers/IEvaluator.cs`** -- Interface: `Evaluate(left, op, right)` and `IsTruthy(value)`
2. **`PLang/Runtime2/modules/condition/providers/DefaultEvaluator.cs`** -- All operators (==, !=, >, <, >=, <=, contains, startswith, endswith, in, isEmpty, NOT, AND, OR), type normalization (int/long/double widening, string-to-number), case-insensitive string comparison, IsTruthy for null/bool/numeric/string/collection/object
3. **`PLang/Runtime2/modules/condition/if.cs`** -- Refactored: `bool Condition` -> `Left/Operator/Right`. No operator = truthy check. With operator = evaluator. Stores `__condition__` in MemoryStack for sub-step signaling.
4. **`PLang/Runtime2/modules/condition/compare.cs`** -- New `[Action("compare")]`: pure bool evaluation, no branching. For compound AND/OR intermediates.
5. **`PLang/Runtime2/Engine/Goals/Goal/Steps/this.cs`** -- Sub-step logic: indented steps default to NOT executing. Condition steps signal via `__condition__` in memory. Only `true` signal proves children. Non-condition steps don't trigger skipping.

### Test implementations (5 files, 69 new + 6 updated)

- DefaultEvaluatorTests: 49 tests (all operators, type normalization, null handling, IsTruthy)
- IfHandlerTests: 8 tests (truthy/falsy, operator delegation, goal branching, error propagation)
- CompareHandlerTests: 3 tests (true/false results, type verification)
- StepsSubStepTests: 9 tests (skip/execute, nested, consecutive, non-condition passthrough)
- ConditionHandlerTests: 6 existing tests updated (`Condition` -> `Left`)

### Key discovery: Data.Merge loses non-list values

`Actions.RunAsync` merges action results via `Data.Merge`, which casts Value to `List<Data>`. When `If.Run()` returns `Data.Ok(true)`, the bool is lost. Solution: If stores its condition result as `__condition__` in MemoryStack, Steps reads it from there. Same pattern as `__stepResult`, `__error__`, etc.

## Code example

The core pattern -- `If.Run()` evaluates and signals:
```csharp
bool result = Operator == null
    ? evaluator.IsTruthy(Left)
    : evaluator.Evaluate(Left, Operator, Right);

Context.MemoryStack.Set("__condition__", result);
```

Steps reads the signal:
```csharp
if (HasIndentedChildren(i))
{
    var conditionSignal = context.MemoryStack.Get("__condition__");
    if (conditionSignal != null)
    {
        context.MemoryStack.Remove("__condition__");
        if (conditionSignal.Value is not true)
            skipBelowIndent = step.Indent;
    }
}
```

### Codeanalyzer v1 fixes applied

- `DefaultEvaluator` sealed
- `WiderNumericType` array moved to `static readonly` field
- `Contains`/`In` now normalize types per element (fixes boxed int vs long mismatch)
- `HasIndentedChildren` changed from `public` to `internal`
- Duplicate XML summary on `Steps.RunAsync` merged

## Next steps

- Builder prompt update (`BuildGoal.llm`) to generate Left/Operator/Right instead of bool Condition
- PLang pipeline tests (need builder)
- `Libraries.GetProvider<T>()` for pluggable evaluators (added to todos)

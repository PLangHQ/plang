# Coder v1 Plan — Action-Based Conditions Implementation

## Goal

Implement the architect's action-based conditions design and make all 69 C# test stubs pass. Defer PLang pipeline tests (need builder prompt changes + LLM).

## Implementation Order

### 1. Create `providers/IEvaluator.cs`
- Interface: `bool Evaluate(object? left, string op, object? right)`, `bool IsTruthy(object? value)`
- Namespace: `App.modules.condition.providers`

### 2. Create `providers/DefaultEvaluator.cs`
- Port from runtime1's `ConditionEvaluator` adapted for runtime2
- All operators: ==, !=, >, <, >=, <=, contains, startswith, endswith, in, isEmpty, NOT, AND, OR
- `IsTruthy`: null->false, bool, int/long/double/decimal 0->false, empty string->false, empty ICollection->false, object->true
- `NormalizeTypes`: int<->long<->double<->decimal widening, string->number conversion
- String comparison: case-insensitive by default
- No System.IO, no Newtonsoft -- pure logic class, no dependencies

### 3. Modify `condition/if.cs`
- Replace `bool Condition` with `object? Left`, `string? Operator`, `object? Right`
- No operator -> truthy check via `IEvaluator.IsTruthy(Left)`
- With operator -> `IEvaluator.Evaluate(Left, Operator, Right)`
- GoalIfTrue/GoalIfFalse unchanged -- branch on evaluation result
- No goals set -> return `Data.Ok(bool)` (sub-step mode)
- Evaluator resolution: `new DefaultEvaluator()` for now (GetProvider<T> is a future feature)
- Sets `__condition__` in Variables so Steps.RunAsync can detect condition results

### 4. Create `condition/compare.cs`
- `[Action("compare")]` with `Left`, `Operator` (required), `Right`
- Pure evaluation: returns `Data.Ok(bool)`, no branching
- Uses same evaluator

### 5. Modify `Steps/this.cs` -- Sub-step execution logic
- Mental model: **indented steps default to NOT executing**. They must be "proven true" by a parent condition.
- After each step, check if `__condition__` was set in memory (condition step signal)
- If signal present and value is `true` -> children execute; if `false` -> skip
- If no signal (non-condition step) -> children execute normally (no skip)
- Reset when back at parent indent level
- Thread-safe: all state is local to the method invocation

### 6. Implement all 69 C# test stubs
- DefaultEvaluatorTests: 49 tests
- IfHandlerTests: 8 tests
- CompareHandlerTests: 3 tests
- StepsSubStepTests: 9 tests

## Key Design Decisions

- **No GetProvider<T> yet**: Libraries doesn't have this method. Use `new DefaultEvaluator()` directly.
- **Sub-step model**: Indented steps default to "don't execute" -- they must be proven true by a parent condition. The If handler sets `__condition__` in Variables. Steps reads it for sub-step control. Non-condition steps (no `__condition__` signal) don't trigger skipping.
- **Data.Merge loses bool values**: Actions.RunAsync merges results via Data.Merge, which converts Values to List<Data>. The bool from If.Run() is lost in merge. Solution: If stores condition result in Variables (`__condition__`), Steps reads it from there.
- **Source generator**: The If record properties change from `bool Condition` to `object? Left` etc.

## Files

| File | Action |
|------|--------|
| `PLang/App/modules/condition/providers/IEvaluator.cs` | Create |
| `PLang/App/modules/condition/providers/DefaultEvaluator.cs` | Create |
| `PLang/App/modules/condition/if.cs` | Modify |
| `PLang/App/modules/condition/compare.cs` | Create |
| `PLang/App/Goals/Goal/Steps/this.cs` | Modify |
| `PLang/App/Goals/Goal/Steps/this.cs` | Modify (sub-step logic) |
| `PLang.Tests/App/Modules/condition/DefaultEvaluatorTests.cs` | Implement stubs |
| `PLang.Tests/App/Modules/condition/IfHandlerTests.cs` | Implement stubs |
| `PLang.Tests/App/Modules/condition/CompareHandlerTests.cs` | Implement stubs |
| `PLang.Tests/App/Modules/condition/StepsSubStepTests.cs` | Implement stubs |
| `PLang.Tests/App/Modules/condition/ConditionHandlerTests.cs` | Update (Condition -> Left) |

## Not In Scope
- Builder prompt changes (`BuildGoal.llm`) -- separate task
- PLang pipeline tests (need builder)
- `Libraries.GetProvider<T>()` -- future feature (added to todos)

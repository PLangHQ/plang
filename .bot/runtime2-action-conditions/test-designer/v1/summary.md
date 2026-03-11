# v1 Summary — Test Suites for Action-Based Conditions

## What this is

Test suites defining the behavioral contract for the architect's action-based conditions design. 90 tests across C# unit tests and PLang pipeline tests covering: DefaultEvaluator (all operators, truthiness, type normalization, null handling), condition.if handler (truthy checks, operator delegation, goal branching, sub-step mode), condition.compare handler (pure evaluation), Steps.RunAsync sub-step logic (skipBelowIndent), and full PLang pipeline integration including multi-action conditions via file.exists.

## What was done

### C# Tests (69 tests in 4 files)

- `PLang.Tests/Runtime2/Modules/condition/DefaultEvaluatorTests.cs` — 49 tests: all operators (==, !=, >, <, >=, <=, contains, startswith, endswith, in, isEmpty, NOT, AND, OR), type normalization (int/long, int/double, string/int), null handling, IsTruthy for every type
- `PLang.Tests/Runtime2/Modules/condition/IfHandlerTests.cs` — 8 tests: truthy/falsy checks, operator delegation, goal branching, sub-step mode, error propagation
- `PLang.Tests/Runtime2/Modules/condition/CompareHandlerTests.cs` — 3 tests: returns Data.Ok(bool), type verification
- `PLang.Tests/Runtime2/Modules/condition/StepsSubStepTests.cs` — 9 tests: skip logic, nested conditions, consecutive conditions, deeply nested, non-condition false doesn't trigger skip

### PLang Tests (21 tests in 21 directories)

Each directory under `Tests/Runtime2/Condition*` has a `.test.goal` plus supporting `.goal` files:
- 8 basic operator tests (truthy, falsy, >, <, ==, !=, >=, <=)
- 3 string operator tests (contains, startswith, endswith)
- 3 sub-step tests (true, false, nested)
- 3 compound/logical tests (AND, OR, NOT)
- 1 else branch test
- 3 action-based condition tests (file exists, file not exists, file exists with sub-steps)

## Code example

C# test pattern (all follow this — `Assert.Fail` stub for coder to implement):
```csharp
[Test]
public async Task Evaluate_GreaterThan_LeftBigger_ReturnsTrue()
{
    Assert.Fail("Not implemented");
}
```

PLang test pattern:
```plang
Start
/ Test: greater than operator calls goal when condition is true
- set %x% = 10
- if %x% > 5, call WhenGreater
- assert %result% equals "greater", "GoalIfTrue should have been called for x > 5"
```

## Key design decisions

- Independently added tests the architect didn't suggest: `>=`, `<=`, `startswith`, `endswith`, null handling, type normalization, non-condition-false-doesn't-skip, action-based conditions via file.exists
- Explicitly documented what's NOT tested and why (db-based conditions, GetProvider<T>, concurrent execution)
- Sub-step PLang tests use indented steps directly — this validates both the builder understanding indentation AND the runtime skip logic

## Next step

Run **coder** to implement the production code and make all 90 tests pass.

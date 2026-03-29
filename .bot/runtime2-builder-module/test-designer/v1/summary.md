# v1 Summary — Builder Module Test Suite

## What this is

Test contract for Piece 8: Builder Module — a native v2 builder that parses `.goal` files, manages `.pr` files, reflects action metadata for the LLM, and validates LLM output. 49 test stubs (43 C# + 6 PLang) that define "done" for the coder bot.

## What was done

Translated the architect's plan into comprehensive test stubs across 8 C# test files and 6 PLang test directories. Added independent edge cases beyond the architect's ~43 estimate:

- **Tab-to-space conversion** in GoalFile (classic parser edge case)
- **Blank line boundary handling** (off-by-one trap for line-based parsers)
- **Errors/Warnings conditional replacement** in Step.Merge (subtle semantic)
- **Cacheable=false flag** reflection (defaults to true, need to test explicit false)
- **Empty folder** edge case for getGoals
- **Building.IsEnabled guard** as cross-cutting concern

### Files created

**C# test stubs (43 tests):**
- `PLang.Tests/Runtime2/Modules/builder/GoalFileTests.cs` — 12 tests
- `PLang.Tests/Runtime2/Modules/builder/MergeTests.cs` — 7 tests
- `PLang.Tests/Runtime2/Modules/builder/GetActionsTests.cs` — 5 tests
- `PLang.Tests/Runtime2/Modules/builder/GetTypeInfoTests.cs` — 2 tests
- `PLang.Tests/Runtime2/Modules/builder/GetGoalsTests.cs` — 4 tests
- `PLang.Tests/Runtime2/Modules/builder/ValidateActionsTests.cs` — 5 tests
- `PLang.Tests/Runtime2/Modules/builder/AppTests.cs` — 3 tests
- `PLang.Tests/Runtime2/Modules/builder/SaveGoalsTests.cs` — 3 tests
- `PLang.Tests/Runtime2/Modules/builder/MergeStepTests.cs` — 1 test
- `PLang.Tests/Runtime2/Modules/builder/BuildingGuardTests.cs` — 1 test

**PLang test stubs (6 tests):**
- `Tests/Runtime2/Builder/GetActions/BuilderGetActions.test.goal`
- `Tests/Runtime2/Builder/GetTypeInfo/BuilderGetTypeInfo.test.goal`
- `Tests/Runtime2/Builder/ParseGoal/BuilderParseGoal.test.goal`
- `Tests/Runtime2/Builder/MergeStep/BuilderMergeStep.test.goal`
- `Tests/Runtime2/Builder/ValidateValid/BuilderValidateValid.test.goal`
- `Tests/Runtime2/Builder/ValidateInvalid/BuilderValidateInvalid.test.goal`

## Code example

C# test stub pattern (all follow this):

```csharp
[Test]
public async Task Parse_SingleGoalWithSteps_ReturnsOneGoal()
{
    // Single goal with two steps — basic happy path
    Assert.Fail("Not implemented");
}
```

PLang test stub pattern:

```plang
Start
/ Test that builder.getActions returns a list of all registered module actions
- throw "not implemented"
```

## Next steps

Run the **coder** bot to implement the builder module and make these tests pass.

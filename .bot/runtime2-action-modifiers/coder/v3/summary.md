# Coder v3 Summary — Tests for Data.IsVariable, HasVariableReference, and ValidateBuild

## What this is

Tests for three pieces of new code on the action-modifiers branch that had 0% coverage: `Data.IsVariable`, `Data.HasVariableReference`, and `variable.set.ValidateBuild()`.

## What was done

Added 18 tests across 2 files:

- **PLang.Tests/App/Memory/DataTests.cs** — 7 tests for `IsVariable` (standard var, short name, empty `%%`, embedded var, trailing text, non-string, null) + 7 tests for `HasVariableReference` (embedded, multiple, single, no vars, empty `%%`, non-string, null)
- **PLang.Tests/App/Modules/variable/settests.cs** — 4 tests for `ValidateBuild()` (literal "this" error, variable reference skip, type mismatch error, valid type match)

All 2145/2146 tests pass (1 pre-existing LLM snapshot failure).

## Code example

```csharp
[Test]
public async Task IsVariable_StandardVariable_ReturnsTrue()
{
    var d = new Data("x", "%var%");
    await Assert.That(d.IsVariable).IsTrue();
}

[Test]
public async Task ValidateBuild_TypeMismatch_ReturnsError()
{
    var parameters = new List<Data>
    {
        new Data("Value", "not a number", global::App.Data.Type.FromName("int"))
    };
    var result = global::App.modules.variable.Set.ValidateBuild(parameters);

    await Assert.That(result).IsNotNull();
    await Assert.That(result!).Contains("type=int");
}
```

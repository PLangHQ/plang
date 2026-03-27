# UI Module Test Stubs — v1 Summary

## What this is

Test contract for the UI module (piece 6) — template rendering via Fluid/Liquid. Defines the behavioral spec through 34 test stubs (29 C# + 5 PLang) that the coder bot will implement against.

## What was done

- Created `PLang.Tests/Runtime2/Modules/ui/RenderTests.cs` with 29 C# test stubs covering:
  - Core render behavior (inline, file, missing file, null, empty, syntax error)
  - Variable resolution (memory stack access, explicit param override/alias, scoped var skip)
  - Custom tags & partials (callGoal execute/error, include render/inherit vars)
  - Provider & path resolution (custom provider, goal-relative path, absolute path)
  - Complex data types (dot-navigation, list iteration, null values, Data wrapper unwrap, undefined vars)
  - callGoal edge cases (non-string return, goal not found, arguments)
  - Include edge cases (missing partial, nested relative path resolution)
  - Security (HTML auto-escaping)
- Created 5 PLang integration test goals in `Tests/Runtime2/Ui/`:
  - `RenderFile/` — file template with variables
  - `RenderInline/` — inline content rendering
  - `RenderWithParams/` — explicit parameters
  - `RenderCallGoal/` — callGoal tag
  - `RenderInclude/` — partial includes

## Key design decision

- `Render.Parameters` is `List<Data>?` (not `Dictionary<string, object?>`), consistent with `Action.Parameters` and `GoalCall.Parameters`.

## Code example

```csharp
[Test]
public async Task Render_NullDotNavigation_NoException()
{
    // {{ user.name }} where user is null does not throw — renders empty
    Assert.Fail("Not implemented");
}
```

```plang
Start
/ Test: render a file template with variables from memory stack
- throw "not implemented"
```

## Next steps

Run the **coder** bot to implement the production code and make these tests pass.

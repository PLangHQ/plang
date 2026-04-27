# v6 Summary: Builder Data<T> Unwrapping

## What this is

The builder sends action parameter descriptions to the LLM. Since all action properties were wrapped in `Data<T>`, the LLM was seeing type names like `"@this"` instead of `"path"`, `"bool"`, etc. This also broke valid value discovery (e.g., Actor's `user|service|system` options) and meant .pr parameters lacked type stamps needed for runtime resolution.

## What was done

### 1. `GetTypeName()` unwraps `Data<T>` (TypeMapping.cs:207-208)
Added `Data.@this<>` check before `List<>`/`Dict<>` in the generic handler, plus plain `Data.@this` → `"object"` mapping.

### 2. `GetValidValues()` unwraps `Data<T>` (TypeMapping.cs:260-261)
Added `Data<T>` unwrapping after nullable unwrap, before enum/ValidValues check.

### 3. `NormalizeParameterTypes()` stamps missing types (DefaultBuilderProvider.cs:388-401)
When the LLM omits `type` on a parameter, looks up the action schema via `modules.GetActionType()`, reflects its properties, and stamps the correct PLang type name. Only fills in missing types, never overrides LLM-provided ones.

### 4. Tests (6 new)
- `GetTypeName_DataOfPath_ReturnsPath`
- `GetTypeName_DataOfBool_ReturnsBool`
- `GetTypeName_DataOfListString_ReturnsListString`
- `GetTypeName_PlainData_ReturnsObject`
- `GetValidValues_DataOfActor_ReturnsValues`
- `Describe_DataWrappedProperty_ShowsInnerTypeName`

## Files modified

| File | Change |
|------|--------|
| `PLang/App/Utils/TypeMapping.cs` | `Data<T>` unwrapping in `GetTypeName()` and `GetValidValues()` |
| `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` | Type stamping in `NormalizeParameterTypes()` |
| `PLang.Tests/App/Utility/TypeMappingTests.cs` | 5 new tests |
| `PLang.Tests/App/Engine/Modules/DescribeTests.cs` | 1 new test + fake action |

## Code example

The core change in `GetTypeName()`:

```csharp
if (type.IsGenericType)
{
    var generic = type.GetGenericTypeDefinition();
    if (generic == typeof(Data.@this<>))                    // NEW
        return GetTypeName(type.GetGenericArguments()[0]);   // NEW
    if (generic == typeof(List<>) || generic == typeof(IList<>))
    ...
}
```

## Status

All tests pass (2062/2064 — 2 pre-existing LLM module failures unrelated to this work). Ready for end-to-end verification with `plang build`.

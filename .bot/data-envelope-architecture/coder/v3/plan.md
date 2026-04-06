# Phase 3: Data Partial Class Split + Out View

## Goal

Split the monolithic `Data.cs` (432 lines, 4 classes + 4 concerns) into focused partial class files. Add `Out` view for transport serialization. Prepare the surface for Phase 4 envelope pipeline.

## What Changes

### 1. Data.cs partial class split

Current `Data.cs` contains: Type class, Data class (core + result + navigation mixed), Data<T>, DynamicData.

Split into:

**`Data.cs`** (core) — stays as main file:
- `Type` sealed class (unchanged)
- `Data` class becomes `partial class Data`:
  - Fields: `_value`, `_type`, `_context`
  - Properties: `Name`, `Context`, `Path`, `Parent`, `IsInitialized`, `Created`, `Updated`, `Properties`, `Value`, `Type`
  - Methods: `GetValue<T>()`, `GetValue(System.Type)`, `IsEmpty`, `Null()`, `ToString()`
  - Constructor
  - Static helpers: `CleanName`, `BuildPath`, `UnwrapJsonElement` and friends
- `Data<T>` class (stays — tightly coupled to core)
- `DynamicData` class (stays — tightly coupled to core)

**`Data.Result.cs`** — extracted from Data:
- `Handled` property
- `Error` property
- `Warnings` property
- `Success` property
- `implicit operator bool`
- `Ok()`, `Ok(value, type)`, `FromError(error)` static factories
- `Merge(Data other)` method

**`Data.Navigation.cs`** — extracted from Data:
- `GetChild(string path)` public method
- `GetChildValue(string key)` private method

**`Data.Envelope.cs`** — new, stubs for Phase 4:
- `Signature` property (`byte[]?`, `[JsonIgnore]`, `[Out]`)
- `Verified` property (`bool?`, `[JsonIgnore]`)
- Comment stubs noting Phase 4 pipeline methods

### 2. View.cs — Out view

- Add `Out` to View enum
- Add `[Out]` attribute class (same pattern as `[Store]`, `[LlmBuilder]`, `[Debug]`)

### 3. Properties tagging

- Tag `Data.Properties` with `[Out]` — only serialized when Data leaves the system
- Tag `Data.Signature` with `[Out]` (in Data.Envelope.cs)

### 4. Tests

- All existing 1305+ tests must pass unchanged (partial class split is purely organizational)
- New tests for:
  - `Signature` and `Verified` properties (null by default, settable)
  - `Out` view attribute exists and is applied correctly
  - `Properties` has `[Out]` attribute

## Files Modified

| File | Action |
|------|--------|
| `PLang/App/Memory/Data.cs` | Trim to core + mark `partial` |
| `PLang/App/Memory/Data.Result.cs` | New — result concern |
| `PLang/App/Memory/Data.Navigation.cs` | New — navigation concern |
| `PLang/App/Memory/Data.Envelope.cs` | New — envelope stubs |
| `PLang/App/View.cs` | Add `Out` enum value + attribute |
| `PLang.Tests/App/Memory/DataTests.cs` | Add envelope + Out attribute tests |

## Risk

Low. Partial class split is a zero-behavior-change refactor. The `Out` attribute and envelope stubs are additive. All existing tests should pass without modification.

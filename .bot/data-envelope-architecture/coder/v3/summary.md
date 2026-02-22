# Phase 3: Data Partial Class Split + Out View

## What this is

Data.cs was a monolithic 432-line file mixing four concerns: core data model, result/error handling, path navigation, and (future) transport/envelope. Phase 3 splits it into focused partial class files and adds the `Out` view for transport serialization — preparing the surface for Phase 4's envelope pipeline.

## What was done

### Partial class split

`Data.cs` → 4 files, same `partial class Data`:

| File | Concern | What moved |
|------|---------|-----------|
| `PLang/Runtime2/Engine/Memory/Data.cs` | Core | Name, Value, Type, Path, Parent, Context, Properties, constructor, GetValue, IsEmpty, Null, ToString, UnwrapJsonElement helpers, CleanName, BuildPath. Type class, Data\<T\>, DynamicData stay here. |
| `PLang/Runtime2/Engine/Memory/Data.Result.cs` | Result/Error | Handled, Error, Warnings, Success, implicit bool, Ok(), FromError(), Merge() |
| `PLang/Runtime2/Engine/Memory/Data.Navigation.cs` | Navigation | GetChild(), GetChildValue() |
| `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` | Envelope | Signature (byte[]?), Verified (bool?), Phase 4 pipeline stubs |

### Out view

- Added `Out` to `View` enum in `PLang/Runtime2/Engine/View.cs`
- Added `[Out]` attribute (same pattern as `[Store]`, `[LlmBuilder]`, `[Debug]`)
- Tagged `Data.Properties` with `[Out]` — only serialized when Data leaves the system
- Tagged `Data.Signature` with `[Out]` — only relevant on the wire

### Tests

8 new tests in `PLang.Tests/Runtime2/Memory/DataTests.cs`:
- Signature defaults to null, can be set
- Verified defaults to null, can be set true/false
- Signature has [Out] attribute
- Properties has [Out] attribute
- Out view exists in View enum

All 1313 tests pass (1305 existing + 8 new), 0 failures.

## Code example

The partial class split — each file declares the same class with its own concern:

```csharp
// Data.cs (core)
public partial class Data
{
    [Out]
    public Properties Properties { get; set; }
    // ... core fields, constructor, Value, Type, GetValue, helpers
}

// Data.Result.cs
public partial class Data
{
    public IError? Error { get; set; }
    public bool Success => Error == null;
    public static Data Ok() => new("");
    public static Data FromError(IError error) => new("") { Error = error };
    // ...
}

// Data.Envelope.cs
public partial class Data
{
    [Out]
    public byte[]? Signature { get; set; }
    public bool? Verified { get; set; }
    // Phase 4: Wrap(), Compress(), Encrypt(), Decrypt(), Decompress(), Unwrap()
}
```

## Files modified

- `PLang/Runtime2/Engine/Memory/Data.cs` — trimmed to core, marked `partial`
- `PLang/Runtime2/Engine/Memory/Data.Result.cs` — new
- `PLang/Runtime2/Engine/Memory/Data.Navigation.cs` — new
- `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` — new
- `PLang/Runtime2/Engine/View.cs` — added `Out` enum value + `[Out]` attribute
- `PLang.Tests/Runtime2/Memory/DataTests.cs` — 8 new tests

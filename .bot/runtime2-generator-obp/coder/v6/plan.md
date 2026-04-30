# v6 plan — close auditor/v1 finding #1 (+ cheap nits #2, #3)

## Scope
Honest closure of auditor finding #1 (Data<T> emission ServiceError contract gap) plus the two cheap nits (#2 misnamed sensitive test, #3 sensitive FinalValue null-guard). Leave nit #4 as documented follow-up. Hand off the [VariableName] / raw-primitive migration as a structured todos entry for a future branch.

## What's actually broken
`Data.AsT_Impl<T>` returns `FromError(ServiceError)` on cycle and depth-trip (added in v2). Legacy property emission catches this through `__Resolve<T>` setting `__resolutionError`, which `ExecuteAsync` checks at line 232 *before* `await Run()`. Data<T> property emission catches none of it — the getter assigns the FromError-Data directly to the backing field. Run() executes with `.Value == default(T)`.

**Auditor's suggested fix is necessary but not sufficient.** Setting `__resolutionError` from the Data<T> getter is dead code on its own — line 232 fires before Run, and the Data<T> getter only fires *during* Run. We also need a post-Run check.

## Changes

### 1. Capture in property getter — `PLang.Generators/Emission/Property/Data/this.cs`

Four emission branches (IsPlainData / IsNullable / DefaultValue / else). For each, after the `As<T>(Context)` assignment to `{Backing}` and before `{SetFlag} = true`:

```
if (!{Backing}.Success) __resolutionError = {Backing};
```

`__resolutionError` is `Data?` (base type); `Data<T>` is assignable to `Data` so this is implicit. The `__d.IsEmpty ? new Data<T>(...default...) : As<T>(...)` branch in line 40 is unaffected — the default-construction path produces Success=true, so the check only fires on the As<T> path.

### 2. Surface in ExecuteAsync — `PLang.Generators/Emission/Action/this.cs:231-234`

Change:
```csharp
if (__resolutionError != null) return __resolutionError;

return await Run();
```
to:
```csharp
if (__resolutionError != null) return __resolutionError;

var __runResult = await Run();
if (__resolutionError != null) return __resolutionError;
return __runResult;
```

This is the missing half. Pre-Run check stays (covers legacy path's eager validation reads). Post-Run check covers Data<T> getters that fire during Run().

**Rationale for not eager-resolving Data<T> at top of ExecuteAsync:** would defeat the whole lazy-resolution design. Most handlers don't access every parameter every run; eager resolution would burn cycles and could mask handler-specific resolution-order semantics.

### 3. Tests — `PLang.Tests/Generator/Matrix/DataWrapped/`

Add a non-pass-through handler in `Handlers.cs` to avoid the existing `Run() => Body` pattern that already happens to surface FromError (which would silently pass without the fix):

```csharp
[global::App.modules.Action("datawrappedstringuses")]
public partial class DataWrappedStringUses : global::App.modules.IContext
{
    public partial global::App.Data.@this<string> Body { get; init; }
    public Task<global::App.Data.@this> Run()
    {
        // Touches .Value — without the fix, this proceeds with default(string)=null
        // and the FromError on Body is invisible to the caller.
        var len = Body.Value?.Length ?? 0;
        return Task.FromResult(Data(len));
    }
}
```

Add three tests in `DataWrappedTests.cs`:
- `DataWrappedStringUses_CyclicVarReference_HandlerReturnsCycleServiceError`  
  → seeds `a=%b%, b=%a%`, Body=`%a%`, asserts `result.Data.Error.Key == "VariableResolutionCycle"` and the error is a `ServiceError` (not the misleading `Data(len)` success result with `len=0`).
- `DataWrappedStringUses_ExpandingCycle_HandlerReturnsDepthServiceError`  
  → seeds `a=X-%b%, b=Y-%a%`, asserts `Error.Key == "ResolveDepthExceeded"`.
- `DataWrappedStringUses_NormalResolution_StillRunsCleanly`  
  → seeds normal var, asserts `result.Data.Value == 5` (length of "hello") to pin that the post-Run check doesn't break the success path.

### 4. Nit #2 — rename misnamed sensitive test

`PLang.Tests/Generator/Matrix/Snapshot/SnapshotTests.cs:105` — rename `SnapshotOnError_SensitiveProperty_UnaccessedStillMasksPrValue` to `SnapshotOnError_SensitiveProperty_NullPrValue_StaysNull` (auditor's suggestion). The unaccessed-AND-sensitive contract is left uncovered intentionally — every existing sensitive handler touches every prop, so a non-touching test would be artificial.

### 5. Nit #3 — tighten sensitive FinalValue null-guard

In **both** `Data/this.cs:60` and `Legacy/this.cs:82`:
```csharp
// Before:
$"{SetFlag} ? (object?)\"******\" : null"
// After:
$"{SetFlag} ? ({Backing} != null ? (object?)\"******\" : null) : null"
```

Mirrors the existing PrValue null-guard pattern.

### 6. Hand-off — `Documentation/Runtime2/todos.md`

Append an entry: "Migrate handlers off `[VariableName]` and raw-primitive partials to `Data<T>` (or successor wrapper)." Frame the design problem (`[VariableName]` semantically wants the literal name, not the resolved value — needs a wrapper that exposes both, or a different contract). Scope: ~20 handlers in `App/modules/list/`, `App/modules/loop/`, `App/modules/variable/`. Outcome of that migration enables deletion of `PLang.Generators/Emission/Property/Legacy/this.cs` and the `[VariableName]` attribute.

## Build / verify
- `dotnet run --project PLang.Tests` — expect 2468 + 3 = 2471 green.
- Snapshot the generated emission for one Data<T> handler before/after to sanity-check the property change.

## Out of scope (deferred)
- Auditor nit #4 (full-typename attribute matching across Discovery).
- The actual [VariableName] migration (the todos.md hand-off is the structured pointer).

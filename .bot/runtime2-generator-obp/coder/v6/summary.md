# v6 — close auditor/v1 finding #1 (+ nits #2, #3)

## What this is

Response to auditor/v1 review of v5. Auditor verdict was **PASS** (1 minor + 3 nit, no critical or major). Three findings closed in v6:

1. **#1 (minor, cross-file)** — `Data<T>` property emission did not surface `As<T>`'s ServiceError (cycle / depth-trip from v2). Legacy emission honored the contract via `__Resolve<T>` setting `__resolutionError`; `Data<T>` emission assigned `FromError-Data` straight to the backing field, where Run() read `.Value == default(T)` and proceeded.
2. **#2 (nit, review-gap)** — `SnapshotOnError_SensitiveProperty_UnaccessedStillMasksPrValue` was misnamed; the test pins the PrValue null-guard, not unaccessed-and-sensitive masking.
3. **#3 (nit, contract)** — Sensitive `FinalValue` masked to `"******"` even when the resolved backing value was null, conflating "absent" with "redacted".

Nit #4 (full-typename attribute matching in Discovery) left as documented follow-up — auditor explicitly said fixing it alone creates a different inconsistency.

## Auditor scope correction

The auditor's stated fix for #1 was "single-line addition per branch" in `Data/this.cs` to set `__resolutionError`. That alone is **dead code**: line 232 of `ExecuteAsync` checks `__resolutionError` *before* `await Run()`, but `Data<T>` getters only fire *during* Run. The Legacy path works because raw-scalar validations (`RawScalarValidations`) eagerly read the property before line 232, populating `__resolutionError`. `Data<T>` handlers have no such eager step.

The real fix is two-part:
1. **Capture in getter** (auditor's suggestion): each of the 4 `Data<T>` emission branches (IsPlainData / IsNullable / DefaultValue / else) sets `__resolutionError = {Backing}` when `As<T>` returned a non-Success Data.
2. **Post-Run check in ExecuteAsync** (the missing half): replace `return await Run();` with `var __runResult = await Run(); if (__resolutionError != null) return __resolutionError; return __runResult;`.

Confirmed with Ingi before implementing.

## What was done

### Production code

- `PLang.Generators/Emission/Property/Data/this.cs:32,36,40,44` — added `if (!{Backing}.Success) __resolutionError = {Backing};` after each `As<T>(Context)` call. Backing is `Data<T>?`, assignable to `Data?`.
- `PLang.Generators/Emission/Property/Data/this.cs:60` — sensitive `FinalValue` tightened from `{SetFlag} ? "******" : null` to `{SetFlag} ? ({Backing}?.Value != null ? "******" : null) : null`. Distinguishes accessed-and-null from accessed-and-redacted.
- `PLang.Generators/Emission/Property/Legacy/this.cs:81` — same null-guard tightening, with skip for non-nullable value types to avoid CS0472 (`int != null`). Three-branch dispatch:
  - `IsSensitive && IsValueType && !IsNullable` → unchanged
  - `IsSensitive` → `{SetFlag} ? ({Backing} != null ? "******" : null) : null`
  - else → unchanged
- `PLang.Generators/Emission/Action/this.cs:231` — replaced `return await Run();` with capture-and-recheck:
  ```csharp
  var __runResult = await Run();
  if (__resolutionError != null) return __resolutionError;
  return __runResult;
  ```
  Pre-Run check at the same site is preserved (Legacy path's eager validation reads still need it).

### Tests

- `PLang.Tests/Generator/Matrix/DataWrapped/Handlers.cs` — added `DataWrappedStringUses`, a non-pass-through handler that reads `Body.Value?.Length` and returns `Data(len)`. The existing `DataWrappedString` handler (`Run() => Body`) happens to surface FromError because the FromError-Data IS the result — so it would not have caught the bug. The new handler is the deletion test for the post-Run check.
- `PLang.Tests/Generator/Matrix/DataWrapped/DataWrappedTests.cs` — added `DataWrappedStringUsesCycleTests` with three tests:
  - `DataWrappedStringUses_CyclicVarReference_HandlerSurfacesCycleServiceError` — seeds `a=%b%, b=%a%`, asserts `result.Data.Error.Key == "VariableResolutionCycle"`. Without the post-Run check this would assert `result.Data` is `Ok(0)` (length of null).
  - `DataWrappedStringUses_ExpandingCycle_HandlerSurfacesDepthServiceError` — seeds `a=X-%b%, b=Y-%a%`, asserts `Error.Key == "ResolveDepthExceeded"`.
  - `DataWrappedStringUses_NormalResolution_PostRunCheckIsNoOp` — success-path negative test: success result unaffected by the new check.
- `PLang.Tests/Generator/Matrix/Snapshot/SnapshotTests.cs:101` — renamed `SnapshotOnError_SensitiveProperty_UnaccessedStillMasksPrValue` → `SnapshotOnError_SensitiveProperty_NullPrValue_StaysNull`. Added FinalValue null-guard assertion (`apiKey.FinalValue.IsNull()` when handler accessed `.Value` but resolved to null).
- `PLang.Tests/Generator/GeneratorValidationTests.cs:189` — updated `GeneratedExecuteAsync_CallsRunDirectly` to assert on the new `var __runResult = await Run();` form.

### Hand-off

`Documentation/Runtime2/todos.md` — appended a structured entry for the next branch on migrating handlers off `[VariableName]` and raw-primitive partials. Frames:
- Why it's not on this branch (landing shape, separate design pass needed)
- The design problem (`[VariableName]` semantically wants the literal name, not the resolved value — `Context.Variables.Get(ListName)` and `Variables.Set(ListName, ...)` need the name)
- Three options for the next branch's architect pass; recommendation is `VarRef<T>` as a first-class wrapper
- Scope (~20 handlers in `App/modules/list/`, `App/modules/loop/`, `App/modules/variable/`)
- What deletes once the migration completes (`Legacy/this.cs`, `[VariableName]` attribute, `__Resolve<T>` and friends, the pre-Run `__resolutionError` check)

## Code example — the post-Run check

The diff that closes #1, in generated emission:

**Before** (`App.modules.matrix.datawrapped.DataWrappedStringUses.Action.g.cs`):
```csharp
get { if (__Body_backing == null) {
    __Body_backing = __ResolveData("body").As<string>(Context);
    __Body_set = true;
} return __Body_backing!; }
// ...
if (__resolutionError != null) return __resolutionError;
return await Run();
```

**After**:
```csharp
get { if (__Body_backing == null) {
    __Body_backing = __ResolveData("body").As<string>(Context);
    if (!__Body_backing.Success) __resolutionError = __Body_backing;
    __Body_set = true;
} return __Body_backing!; }
// ...
if (__resolutionError != null) return __resolutionError;

var __runResult = await Run();
if (__resolutionError != null) return __resolutionError;
return __runResult;
```

## Build status

`dotnet run --project PLang.Tests` → 2471 / 2471 green (+3 from v5's 2468).

`plang --test` → 145 / 56 / 4 (down from v5's 152 / 49 / 4). Net 7 tests went from pass→fail; ALL with `"Cannot convert String to this"`. Decided per Ingi: **leave as failed** — these are not v6 regressions in the harmful sense. They are v6 correctly surfacing real type-conversion errors that pre-v6 was silently swallowing. The .pr files for these tests pass the literal string `"this"` (with type tag `actor(user|service|system)`) into handlers that declare `Data<Actor.@this>?` parameters. `TypeConverter` has no string→`Actor.@this` rule, so the conversion produces a FromError-Data. Pre-v6 the FromError lived harmlessly on the backing field; the handlers' `Actor?.Value ?? Context.Actor` fallback masked the real error. v6 surfaces it correctly. The .pr files are wrong (likely a builder issue around the Actor sentinel "this") — fix lives elsewhere, not in this branch.

Affected tests: `Modules/Builder/ValidateValid`, `Modules/Error/Call`, `Modules/Error/RetryOnly`, `Modules/Event/{BeforeStep,Multiple,Wildcard}`, `Modules/Goal/Basic/GoalCall`.

## Open / deferred

- **Auditor nit #4** — `SensitiveAttribute` matched by short name in Discovery. No current namespace collision; auditor said don't fix in isolation.
- **[VariableName] / raw-primitive migration** — handed off via `Documentation/Runtime2/todos.md` for a future architect pass + branch.

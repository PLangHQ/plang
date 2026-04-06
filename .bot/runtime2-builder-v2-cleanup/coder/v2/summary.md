# v2 Summary: Module Cleanup & Data Pattern Consistency

## What this is
Comprehensive cleanup of PLang App modules, enforcing consistent patterns: Data as the universal type, [Provider] attribute for dependency injection, proper return types, and elimination of unnecessary wrapper types.

## What was done

### GoalCall.Parameters → List<Data>
- Changed from `Dictionary<string, object?>?` to `List<Data>?`
- Updated builder prompt (`BuildGoal.llm`) to produce `[{name, value, type}]` format matching action parameters
- GoalMapper bridges v1 format
- **Files**: `GoalCall.cs`, `Engine/this.cs`, `GoalMapper.cs`, `TypeMapping.cs`, `BuildGoal.llm`

### [GoalCallback] Attribute + Streaming Refactor
- New attribute marks GoalCall properties with their injected variable name
- Streaming callbacks create a fresh GoalCall per chunk instead of mutating Variables
- Eliminated `ResolveCallbackVarName` and `varName` parameter threading
- **Files**: `Attributes.cs`, `DefaultHttpProvider.cs`, HTTP action records

### [Provider] Attribute Adoption
- HTTP module (request, download, upload, configure): manual `Providers.Get<IHttpProvider>()` → `[Provider]`
- Identity module (all 8 handlers): manual `Providers.Get<IIdentityProvider>()` → `[Provider]`
- Signing module (sign, verify): `[Provider] ISigningProvider Signer`
- All Run() methods become one-liners

### IdentityVariable → IdentityData : Data
- Renamed `IdentityVariable` to `IdentityData`, extends `Data` directly
- Deleted old `IdentityData.cs` (lazy wrapper) — `%MyIdentity%` uses `DynamicData` with provider lambda
- Provider methods return `IdentityData` directly (not wrapped in `Data.Ok()`)
- Error states carried on IdentityData itself via inherited `Error` property
- `Actor.Identity` is now `IdentityData?` (settable property)

### New Data Infrastructure
- **`DataList<T> : Data, IList<T>`** — typed list with error state, use as list directly
- **`Data.FromError<T>()`** — generic factory for typed error Data
- **`Data.ToError<T>()`** — error-carrying object produces typed result
- `ISettingsStore.GetAll<T>` returns `DataList<T>` natively
- `LoadAllAsync` in identity provider becomes a one-liner

### Signing Pipeline → Provider
- `ISigningProvider` gains `SignAsync`/`VerifyAsync` (full pipeline)
- `Ed25519Provider` implements the complete sign/verify pipeline
- `SignedData` is now pure data — no behavior methods
- Removed `Provider` param from `sign` action (uses default)
- `sign.Data` changed from `object?` to `Data?` with `[IsInitiated]`

### Library → Module Rename
- `modules/library/` → `modules/module/`
- `library.load` → `module.add`
- Added `module.remove` action

### Variable Module Simplification
- `Get` returns Data from Variables directly
- `Set` returns the Data it stored
- `Exists` returns `Data.Ok(bool)`
- Deleted `types.variable` wrapper record

### Step Runner: Condition-Only Child Skipping
- Fixed `RunAsync_NonConditionStep_FalseValue_DoesNotSkip` — `variable.set` returning `Data` with `.Value=false` was triggering child-skipping. Added `IsConditionStep()` check so only `condition` module steps can skip indented children on false result.
- **File**: `PLang/App/Engine/Goals/Goal/Steps/this.cs`

## Code example — before/after pattern

**Before (identity provider):**
```csharp
public async Task<Data> GetAsync(Get action)
{
    var provider = Context.Engine.Providers.Get<IIdentityProvider>();
    if (!provider.Success) return provider;
    return await provider.Value!.GetAsync(this);
}
```

**After:**
```csharp
[Provider]
public partial IIdentityProvider Identity { get; }

public async Task<Data> Run() => await Identity.GetAsync(this);
```

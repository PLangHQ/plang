# v3 Summary — SaveAsync Result Check in GetOrCreateDefaultAsync

## What this is

Fix for a regression found by code analyzer v2: `GetOrCreateDefaultAsync` didn't check the `SaveAsync` result when auto-creating a default identity. The original v1 code checked it, but the consolidation into a shared method dropped the check.

## What was done

**types.cs** (line 88): Added save result check — throws `InvalidOperationException` on failure instead of returning a phantom identity.

```csharp
// Before:
await def.SaveAsync(engine);
return def;

// After:
var saveResult = await def.SaveAsync(engine);
if (!saveResult.Success)
    throw new InvalidOperationException($"Failed to save auto-created default identity: {saveResult.Error?.Message}");
return def;
```

**get.cs**: Added try/catch around `GetOrCreateDefaultAsync` to convert the exception to `Data.FromError(new ServiceError(..., "SaveError", 500))`.

**IdentityData.cs**: No change needed — the throw propagates through `GetAwaiter().GetResult()` naturally, preventing phantom identity from being cached.

## Files modified

- `PLang/Runtime2/modules/identity/types.cs` — throw on save failure
- `PLang/Runtime2/modules/identity/get.cs` — catch and return Data error

## Verification

- `dotnet build PLang/PLang.csproj` — 0 errors
- `dotnet build PLang.Tests/PLang.Tests.csproj` — 0 errors
- All 1647 tests pass

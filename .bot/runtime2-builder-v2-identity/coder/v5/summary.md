# v5 Summary — Fix Auto-Create Overwrite + Test Gaps

## What this is

Fix data loss bug: `GetOrCreateDefaultAsync` could silently overwrite a user-created identity named "default" by creating a new one with the same key. Now promotes existing identities instead.

## What was done

**types.cs** — `GetOrCreateDefaultAsync` now has a 3-step resolution:
1. Find existing default non-archived → return it
2. Find any non-archived identity → promote it to default, save, return it
3. No identities at all → auto-create new "default"

```csharp
// New promotion step (between find-default and auto-create):
var candidate = all.Find(i => !i.IsArchived);
if (candidate != null)
{
    candidate.IsDefault = true;
    var promoteResult = await candidate.SaveAsync(engine);
    if (!promoteResult.Success)
        throw new InvalidOperationException($"Failed to promote identity '{candidate.Name}' to default: {promoteResult.Error?.Message}");
    return candidate;
}
```

**IdentityHandlerTests.cs** — 3 test changes:
- Renamed `Get_NullName_NoDefaultExists_AutoCreates` → `Get_NullName_NoDefaultExists_PromotesExisting` — expects first identity promoted, not new "default" created
- Added `GetOrCreateDefault_ExistingNonDefault_PromotesInsteadOfOverwriting` — proves the exact data loss scenario doesn't happen
- Added `Export_NullName_NoDefault_ReturnsError` — covers the 404 path

## Files modified

- `PLang/App/modules/identity/types.cs`
- `PLang.Tests/App/Modules/identity/IdentityHandlerTests.cs`

## Verification

All 1649 tests pass (2 new).

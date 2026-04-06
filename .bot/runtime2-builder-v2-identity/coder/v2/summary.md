# Coder v2 Summary — Code Analyzer Fixes

## What the reviewer flagged
1. **Bug:** `get.cs` overwrote `%MyIdentity%` on by-name fetch (not just default fetch)
2. Duplicate auto-create logic in `IdentityData.ResolveDefault()` and `Get.Run()`
3. Double `TryGetValue("Created")` in types.cs
4. `IdentityVariable` not sealed
5. Non-atomic rename (remove before save = data loss risk)
6. No test for `Export { Name = null }`
7. Dead JSON round-trip fallback code

## What was changed

### Bug fix: get.cs
- **Before:** All three exit paths called `Identity.Update()`, including by-name fetch
- **After:** Only the default-fetch path calls `Identity.Update()`. By-name returns identity without touching `%MyIdentity%`.
- Added regression test `Get_ByName_DoesNotOverwriteMyIdentity`

### Deduplicate auto-create
- Extracted `IdentityVariable.GetOrCreateDefaultAsync(engine)` as single source of truth
- `Get.Run()` calls it for the null-name path
- `IdentityData.ResolveDefault()` delegates to it (sync-over-async, documented why safe)

### types.cs cleanup
- Sealed the class
- Fixed double `TryGetValue("Created")` — single lookup, branch on type
- Removed dead JSON round-trip fallback (DataSource never returns arbitrary objects)

### Atomic rename
- **Before:** Remove old → Save new (data loss if save fails)
- **After:** Save new → Remove old (old entry untouched if save fails)

### Test gaps filled
- `Export_NullName_ReturnsDefaultPrivateKey` — exercises default fallback path

## Files modified
- `PLang/App/modules/identity/get.cs` — remove Update on by-name, use GetOrCreateDefaultAsync
- `PLang/App/modules/identity/types.cs` — sealed, fix Created, add GetOrCreateDefaultAsync, remove dead code
- `PLang/App/modules/identity/IdentityData.cs` — delegate to GetOrCreateDefaultAsync
- `PLang/App/modules/identity/rename.cs` — save-first-then-remove
- `PLang.Tests/App/Modules/identity/IdentityHandlerTests.cs` — 2 new tests

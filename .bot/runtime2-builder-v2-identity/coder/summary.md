# Identity Module — Coder Progress

## v1 — Full Implementation
Implemented identity module: [Sensitive] attribute infrastructure, IdentityVariable with OBP persistence, IdentityData lazy resolver, 8 CRUD handlers (create/get/getAll/archive/unarchive/rename/setDefault/export), %MyIdentity% DynamicData registration on Actor. All 51 C# tests pass. PLang tests pending builder prompt. See [v1/summary.md](v1/summary.md).

## v2 — Code Analyzer Fixes
Fixed behavioral bug (get.cs overwrote %MyIdentity% on by-name fetch), deduplicated auto-create logic into `GetOrCreateDefaultAsync`, sealed IdentityVariable, fixed double TryGetValue, made rename atomic (save-first), added 2 tests. 1647/1647 pass. See [v2/summary.md](v2/summary.md).

## v3 — SaveAsync Result Check
Fixed regression from v2: `GetOrCreateDefaultAsync` now checks `SaveAsync` result and throws on failure (prevents phantom identity). `Get.Run()` catches and returns `Data.FromError`. All 1647 tests pass. See [v3/summary.md](v3/summary.md).

## v4 — Fix Flaky Test + Weak Assertions
Fixed flaky `Sensitive_IdentityVariable_PrivateKeyExcluded` (base64 `+` JSON escaping), added 2 missing `Error.Key` assertions. All 1647 tests pass. See [v4/summary.md](v4/summary.md).

## v5 — Fix Auto-Create Overwrite
Fixed data loss bug: `GetOrCreateDefaultAsync` now promotes existing non-archived identities to default instead of creating new ones that overwrite. Added 2 tests. All 1649 tests pass. See [v5/summary.md](v5/summary.md).

## v6 — Auditor Fixes
Added try/catch in `IdentityData.ResolveDefault()` for unhandled exception path. Made Export(null) use `GetOrCreateDefaultAsync` for consistency with Get. Added `SensitivePropertyFilter` to `Data.Envelope` options. All 1649 tests pass. See [v6/summary.md](v6/summary.md).

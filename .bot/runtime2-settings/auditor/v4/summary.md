# Auditor v4 — runtime2-settings Review

## What this is

Re-review of Settings infrastructure after coder v3 and v4 fixed auditor findings. Verifying fixes as fresh code and looking for new issues introduced by the changes.

## What was reviewed

- `Engine/Settings/this.cs` — Cast<T> rewrite with Enum.TryParse + narrowed catch filter
- `Engine/Settings/Scope.cs` — new Clone() method
- `Engine/Context/PLangContext.cs` — Clone() now calls SettingsScope?.Clone()
- All 28 Settings tests (6 new since v2)

## Fix verification

### Finding #1 (Clone shares Scope) — Properly fixed
- `Scope.Clone()` iterates the ConcurrentDictionary and copies each KVP into a new Scope
- `PLangContext.Clone()` calls `SettingsScope?.Clone()` — null-safe
- Test `Clone_WritesToClone_DoNotAffectOriginal` verifies both directions
- Test `Clone_CreatesIndependentCopy` at Scope level also verifies both directions
- ConcurrentDictionary's enumerator is snapshot-consistent — safe to iterate during Clone()

### Finding #4 (Bare catch) — Properly fixed, regression caught and resolved
- v3 narrowed the catch but missed `ArgumentException` (Enum.ToObject on string input)
- Tester v3 caught the regression with a concrete scenario
- v4 added `Enum.TryParse(target, s, ignoreCase: true, out var parsed)` before `Enum.ToObject`
- `ArgumentException` added to catch filter as safety net for non-string enum edge cases
- 3 new tests: exact string→enum, case-insensitive, invalid string fallback

## New issues in fixes

### None found

The Cast<T> rewrite is clean:
- `is T` check first (fast path, no conversion)
- Enum branch: TryParse for strings (case-insensitive), ToObject for integer types
- Convert.ChangeType for all other numeric widening
- Targeted catch filter covers all known exception types from these APIs
- Fallback path returns classDefault

One minor observation (not a finding): if TryParse fails on a string, we still call `Enum.ToObject(target, stringValue)` which will throw ArgumentException and be caught. This is an unnecessary throw+catch — the string was already rejected by TryParse. But it's harmless (settings resolution is not a hot path) and the code is simpler for not special-casing it.

## Findings status

| # | Severity | Status | Notes |
|---|----------|--------|-------|
| 1 | major | **closed** | Clone isolation fixed, tested |
| 2 | minor | open | Save/restore complexity — deferred |
| 3 | minor | open | Simulation test — deferred |
| 4 | nit | **closed** | Catch narrowed, regression fixed |

## OBP Assessment

Still clean. No new OBP concerns from the fixes.

## Verdict

**Approved.** Both major and nit findings properly fixed. Remaining minors are acknowledged deferrals (save/restore complexity, simulation test). The Settings infrastructure is solid — 28 tests, clean scope chain resolution, proper clone isolation, robust type conversion.

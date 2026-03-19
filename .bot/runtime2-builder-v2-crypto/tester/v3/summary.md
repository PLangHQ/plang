# Tester v3 — Review of Coder v2 Identity Error Path Tests

## What this is

Review of the 8 identity error path tests added by coder v2 (`IdentityErrorPathTests.cs`). Checks for false-green risks, assertion strength, and missing coverage.

## Test Run Results

- **C# tests**: 1697 total, 1693 passed, 0 failed, 4 skipped (bcrypt)
- All 8 new tests pass

## v2 Findings Status

| v2 Finding | Coder Fix | Status |
|------------|-----------|--------|
| Finding 1: verify.cs null Hash | Null guard + test (coder v1) | **RESOLVED** |
| Finding 2: Identity save-failure chain | 5 tests (GetOrCreateDefault, Get, Export, IdentityData) | **RESOLVED** |
| Finding 3: Identity defensive code | 3 tests (LoadAllAsync, Deserialize) | **RESOLVED** |

## New Findings

### Finding 1 (Major): 5 handler save/remove error paths have zero test coverage

Coder v2 tested the `GetOrCreateDefaultAsync` throw chain (types.cs → handlers), but every handler that directly calls `SaveAsync`/`RemoveAsync` and checks `!result.Success` is untested. These are data-mutation paths — if the early-return was removed, data corruption could follow silently.

| Handler | Line | Error path | Test exists? |
|---------|------|-----------|--------------|
| `create.cs` | 38 | `!saveResult.Success` when clearing existing defaults | **NO** |
| `create.cs` | 53 | `!result.Success` when saving new identity | **NO** |
| `setDefault.cs` | 34 | `!result.Success` when clearing old defaults | **NO** |
| `setDefault.cs` | 39 | `!saveResult.Success` when setting new default | **NO** |
| `rename.cs` | 36 | `!saveResult.Success` when saving with new name | **NO** |
| `rename.cs` | 41 | `!removeResult.Success` when removing old name | **NO** |
| `archive.cs` | 32 | `!result.Success` when saving archived state | **NO** |
| `unarchive.cs` | 27 | `!result.Success` when saving unarchived state | **NO** |

All use the same `FailingSaveDataSource` pattern already in the test file — these are straightforward to add.

### Finding 2 (Minor): Weak assertions on 3 existing tests

These tests pass but could false-green under certain mutations:

**`Get_NullName_SaveFails_ReturnsSaveError`** (line 92) and **`Export_NullName_SaveFails_ReturnsSaveError`** (line 108):
- Check `Error.Key == "SaveError"` and `StatusCode == 500` — good
- Missing: `Error.Message` should contain the original exception text (e.g., `"Failed to save"` or `"Failed to promote"`) to prove the exception message was captured, not just wrapped

**`IdentityData_ResolveDefault_SaveFails_ReturnsNull`** (line 124):
- Only checks `value == null`
- Would be stronger with a comment explaining this IS the contract (catch block returns null), or by verifying that no exception propagated

### Finding 3 (Minor): FailingSaveDataSource.Remove delegates to inner — rename remove-failure untested

`FailingSaveDataSource` only fails on `Set`, but `Remove` delegates to `_inner`. To test `rename.cs` line 41 (`!removeResult.Success`), coder needs either a `FailingRemoveDataSource` or a mode flag on the existing one.

## Recommendations for Coder

1. **Add 8 tests** for Finding 1 — one per error path. Follow the same pattern as existing tests:
   - Create valid identity first (real DataSource)
   - Swap to `FailingSaveDataSource`
   - Run handler, assert `result.Success == false` and `result.Error.Key`
   - For rename remove-failure: add `FailingRemoveDataSource` or extend `FailingSaveDataSource`

2. **Strengthen assertions** in `Get_NullName_SaveFails` and `Export_NullName_SaveFails`:
   - Add `await Assert.That(result.Error.Message).Contains("Failed to")`

3. Test names should follow pattern: `{Handler}_{Scenario}_ReturnsError`

## Verdict: NEEDS-FIXES

Finding 1 is major — 8 untested error paths in data-mutation handlers. The existing tests are correct but incomplete. Same `FailingSaveDataSource` infrastructure makes these easy to add.

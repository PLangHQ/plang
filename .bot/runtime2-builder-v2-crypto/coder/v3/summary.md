# Crypto Module — Coder v3

## What this is

Handler save/remove error path tests — covers all 8 untested data-mutation error paths identified by tester v3 finding 1. Also strengthened existing assertions per finding 2.

## What was done

### File modified
- `PLang.Tests/Runtime2/Modules/identity/IdentityErrorPathTests.cs` — added 8 tests, 1 new DataSource mock, strengthened 2 existing assertions

### Tests added

| Test | Handler | Error path covered |
|------|---------|-------------------|
| `Create_ClearDefaultSaveFails_ReturnsError` | create.cs | Line 38: clearing existing defaults fails |
| `Create_SaveNewIdentityFails_ReturnsError` | create.cs | Line 53: saving new identity fails |
| `SetDefault_ClearOldDefaultSaveFails_ReturnsError` | setDefault.cs | Line 34: clearing old default fails |
| `SetDefault_SaveNewDefaultFails_ReturnsError` | setDefault.cs | Line 39: saving new default fails |
| `Rename_SaveNewNameFails_ReturnsError` | rename.cs | Line 36: saving with new name fails |
| `Rename_RemoveOldNameFails_ReturnsError` | rename.cs | Line 41: removing old entry fails |
| `Archive_SaveFails_ReturnsError` | archive.cs | Line 32: saving archived state fails |
| `Unarchive_SaveFails_ReturnsError` | unarchive.cs | Line 27: saving unarchived state fails |

### Assertions strengthened
- `Get_NullName_SaveFails_ReturnsSaveError` — added `Error.Message.Contains("Failed to save")`
- `Export_NullName_SaveFails_ReturnsSaveError` — added `Error.Message.Contains("Failed to save")`
- `IdentityData_ResolveDefault_SaveFails_ReturnsNull` — added comment explaining null is the contract

### New mock added
- `FailingRemoveDataSource` — delegates all operations except Remove, which returns IOError. Used for rename remove-failure test.

## Status

- All 16 error path tests pass
- Full suite: 1701 pass, 0 fail, 4 skipped (bcrypt)
- All tester v3 findings addressed

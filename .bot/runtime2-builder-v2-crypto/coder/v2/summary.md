# Crypto Module — Coder v2

## What this is

Identity module error path tests — covers untested exception chains identified by tester v2 findings 2 and 3. The identity branch was already closed, so these are added on the crypto branch.

## What was done

### File created
- `PLang.Tests/App/Modules/identity/IdentityErrorPathTests.cs` — 8 tests covering all identified gaps

### Tests added

| Test | Error path covered |
|------|--------------------|
| `GetOrCreateDefault_AutoCreateSaveFails_ThrowsInvalidOperationException` | types.cs line 100-101: auto-create save failure |
| `GetOrCreateDefault_PromoteSaveFails_ThrowsInvalidOperationException` | types.cs line 83-84: promote save failure |
| `Get_NullName_SaveFails_ReturnsSaveError` | get.cs line 33-35: catch InvalidOperationException → SaveError |
| `Export_NullName_SaveFails_ReturnsSaveError` | export.cs line 32-34: catch InvalidOperationException → SaveError |
| `IdentityData_ResolveDefault_SaveFails_ReturnsNull` | IdentityData.cs line 56-59: catch → return null |
| `LoadAllAsync_DataSourceFails_ReturnsEmptyList` | types.cs line 54-55: DataSource.GetAll failure |
| `LoadAsync_UnrecognizedValueType_ReturnsNull` | types.cs line 143: Deserialize returns null |
| `LoadAllAsync_MixedValues_SkipsUnrecognized` | types.cs line 60-61: Deserialize null filtered out |

### Approach
- Created `FailingSaveDataSource` wrapper that delegates reads but returns errors on Set
- Created `FailingGetAllDataSource` that returns errors on all operations
- Used reflection to swap `Actor._dataSource` (private Lazy field) for the failing implementations
- For Deserialize tests, stored raw values (integers, strings) directly in DataSource

## Status

- All 8 new tests pass
- Full suite: 1693 pass, 0 fail, 4 skipped (bcrypt)
- All tester v2 findings resolved

# v3 State

## Status: Complete

## What was done
- Changed `IsFile`/`IsDirectory` from filesystem checks to structural (extension-based) properties
- Changed `ToString()` to return `Relative` instead of `Absolute`
- Made `Exists` use direct `_fs.File.Exists` / `_fs.Directory.Exists` (independent of IsFile/IsDirectory)
- Fixed `Copy`/`Move`/`Delete` to use `_fs.File.Exists()`/`_fs.Directory.Exists()` directly
- Added `Read()`, `List()`, `Save()` methods to Path.cs (behavior belongs to owner - OBP rule 1)
- Made `read.cs`, `list.cs`, `save.cs` handlers into one-line thin delegators
- Updated PathTests: new semantic tests for IsFile/IsDirectory/ToString, added 11 new tests for Read/List/Save
- All 1219 tests passing

## Files modified
- `PLang/App/Engine/Memory/Path.cs` — property semantics + Read/List/Save methods
- `PLang/App/actions/file/read.cs` — thin delegator
- `PLang/App/actions/file/list.cs` — thin delegator
- `PLang/App/actions/file/save.cs` — thin delegator
- `PLang.Tests/App/Modules/Path/PathTests.cs` — updated + new tests

## No changes needed
- `copy.cs`, `move.cs`, `delete.cs`, `exists.cs` — already thin
- `FileHandlerTests.cs` — existing tests pass as-is

## Next steps
- Commit and push

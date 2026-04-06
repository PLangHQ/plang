# v7 Summary — Tester findings: exception tests, overwrite tests, strengthened assertions

## What this is

The tester (v1) found that all exception handling code (added in v6) had zero test coverage — a false-green. Also missing: overwrite conflict tests, Save object serialization test, and PLang .goal tests. Minor issues: weak error assertions, loose list/relative checks.

## What was done

### Tester #1 Critical + #3 Major — Exception path tests + overwrite conflicts

Added 8 tests that exercise the try/catch blocks:
- `Copy_DestExists_OverwriteFalse_ReturnsIOError` — dest file exists, overwrite=false → IOException → caught → "IOError"/500
- `Move_DestExists_OverwriteFalse_ReturnsIOError` — same pattern for move
- `Copy_DestExists_OverwriteTrue_Succeeds` — overwrite=true replaces dest
- `Move_DestExists_OverwriteTrue_Succeeds` — overwrite=true replaces dest
- `Delete_ReadOnlyParent_ReturnsIOError` — chmod parent to r-x, delete fails → caught
- `Save_ReadOnlyDir_ReturnsIOError` — chmod dir to r-x, save fails → caught
- `Read_PermissionDenied_ReturnsIOError` — chmod file to 000, read fails → caught
- `List_PermissionDenied_ReturnsIOError` — chmod dir to 000, list fails → caught

All use `try/finally` to restore permissions for cleanup.

### Tester #4 Major — Save object serialization
- `Save_Object_SerializesToJson` — saves a Dictionary via the else branch (SerializeAsync), reads back, verifies content

### Tester #5 Minor — Stronger error assertions
All 5 error-path tests now verify `error.Key` ("NotFound"/"IOError"/"DirectoryNotEmpty") and `error.StatusCode` (404/500/400).

### Tester #6 Minor — Exact Relative assertion
`Relative_StripsRootDirectory` now asserts exact value `"sub/file.txt"` instead of loose contains.

### Tester #7 Minor — List checks file names
`List_ExistingDirectory_ReturnsFileArray` now verifies file names are "a.txt" and "b.txt", not just count.

### Tester #8 Minor — Copy verifies source exists
`Copy_File_CopiesToDestination` now asserts source file still exists after copy.

### Auditor v2 #1 — ResolveDestination in Move
Applied `ResolveDestination()` to Move for consistency with Copy. Added `Move_FileToExistingDirectory_PutsFileInsideDir` test.

### Auditor v2 #2 — Relative returns "." for root
Changed from `string.Empty` to `"."`. Added `Relative_RootPath_ReturnsDot` test.

### Tester #2 — PLang .goal tests
**Blocked**: Requires LLM builder to generate .pr files. Noted as future work.

## Files modified
- `PLang/App/Engine/Memory/Path.cs` — ResolveDestination in Move, Relative root="."
- `PLang.Tests/App/Modules/Path/PathTests.cs` — 12 new tests, 6 strengthened assertions

## Verification
- 1239/1239 tests pass (+12 new)

# Plan: v7 — Tester findings: exception tests, overwrite tests, Save serialization, PLang tests

## Findings to address

### #1 Critical + #3 Major — Exception path tests + overwrite conflicts (combined)
Copy/Move with `Overwrite=false` when dest exists triggers IOException → exercises try/catch AND tests overwrite behavior. Add:
- Copy_FileExists_OverwriteFalse_ReturnsIOError
- Copy_FileExists_OverwriteTrue_Succeeds
- Move_FileExists_OverwriteFalse_ReturnsIOError
- Move_FileExists_OverwriteTrue_Succeeds

For Delete/Read/Save/List exceptions, use Linux `chmod` to trigger permission-denied:
- Delete_PermissionDenied_ReturnsIOError (chmod parent dir to 555)
- Read_PermissionDenied_ReturnsIOError (chmod file to 000)
- Save_PermissionDenied_ReturnsIOError (chmod dir to 555)
- List_PermissionDenied_ReturnsIOError (chmod dir to 000)

### #4 Major — Save object serialization test
Save a Dictionary to .json via the else branch, read it back, verify content.

### #2 Major — PLang .goal tests
Blocked: requires LLM builder to generate .pr files. Note as future work.

### #5-#8 Minor — Strengthen assertions
- Error tests: verify error code ("NotFound"/404 or "IOError"/500)
- Relative_StripsRootDirectory: assert exact relative path
- List tests: check file names, not just count
- Copy test: verify source still exists

### Auditor v2 observations
- Apply ResolveDestination to Move (consistency with Copy)
- Relative returns "." for root path instead of empty string

## Files to modify
- `PLang/App/Memory/Path.cs` — ResolveDestination in Move, Relative root="."
- `PLang.Tests/App/Modules/Path/PathTests.cs` — new + strengthened tests

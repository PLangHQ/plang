# Plan: v6 — Fix auditor findings (exception handling, Relative bug, Move/Delete edge cases)

## Findings to address

### #1 Critical — Exception handling in all behavior methods
Wrap filesystem ops in try/catch for IOException and UnauthorizedAccessException. Return `Data.FromError(new ServiceError(ex.Message, "IOError", 500))`.

Methods: Copy, Move, Delete, Read, List, Save (6 methods).

### #2 Major — Relative property prefix-matching bug
Add trailing separator guard: check that char at RootDirectory.Length is a separator, or append separator before StartsWith comparison.

### #3 Major — Move.Overwrite for directories
When overwrite=true and destination directory exists, delete destination first before moving.

### #4 Major — Delete non-empty directory
Check if directory has contents when Recursive=false. Return clear error: "Directory is not empty. Use recursive=true."

### #7 Minor — Null guards on constructor
Add `ArgumentNullException.ThrowIfNull()` for rawPath and engine.

### #8 Minor — Copy file-to-existing-directory
If source is a file and destination is an existing directory, append source filename to destination.

### #9 Nit — Test namespace
Change `PLang.Tests.App.Memory` → `PLang.Tests.App.Modules.Path`.

### #10 Nit — Explicit Pattern in List tests
Set `Pattern = "*"` explicitly in all List test action records.

## Skipping
- **#5** (case-sensitive equality) — PLang primarily targets Windows. Document the assumption.
- **#6** (operator ==) — Path is not used in collections/LINQ yet. Low risk. Can add later.

## Files to modify
- `PLang/App/Engine/Memory/Path.cs` — all code fixes
- `PLang.Tests/App/Modules/Path/PathTests.cs` — namespace, Pattern defaults, new tests for edge cases

## Verification
1. `dotnet build` — 0 errors
2. `dotnet run --project PLang.Tests` — all tests pass (new tests for each fix)

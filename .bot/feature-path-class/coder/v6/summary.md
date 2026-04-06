# v6 Summary — Fix auditor findings (exception handling, Relative bug, edge cases)

## What this is

The auditor (v1) reviewed the Path class after 5 coder iterations and found 10 issues: 1 critical (no exception handling), 3 major (Relative prefix bug, Move overwrite ignored for dirs, Delete non-empty throws), 4 minor, 2 nits. This version addresses findings #1-#4, #7-#10.

## What was done

### #1 Critical — Exception handling in all behavior methods
Wrapped filesystem operations in all 6 behavior methods (Copy, Move, Delete, Read, List, Save) with:
```csharp
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
}
```
Locked files, permission errors, disk-full conditions now return PLang-level errors instead of crashing.

### #2 Major — Relative property prefix-matching bug
Added trailing separator guard before `StartsWith` comparison:
```csharp
if (!root.EndsWith(_fs.Path.DirectorySeparatorChar) && !root.EndsWith(_fs.Path.AltDirectorySeparatorChar))
    root += _fs.Path.DirectorySeparatorChar;
```
Also added exact-match check for when the path IS the root directory.

### #3 Major — Move.Overwrite for directories
When `action.Overwrite` is true and destination directory exists, delete it first before moving:
```csharp
if (action.Overwrite && _fs.Directory.Exists(action.Destination.Absolute))
    _fs.Directory.Delete(action.Destination.Absolute, recursive: true);
_fs.Directory.Move(_absolutePath, action.Destination.Absolute);
```

### #4 Major — Delete non-empty directory without Recursive
Check for non-empty directory before calling Delete:
```csharp
if (!action.Recursive && _fs.Directory.GetFileSystemEntries(_absolutePath).Length > 0)
    return Data.FromError(new ServiceError(
        $"Directory is not empty: {Raw}. Use recursive=true to delete contents.", "DirectoryNotEmpty", 400));
```

### #7 Minor — Null guards
Added `ArgumentNullException.ThrowIfNull()` for both constructor parameters.

### #8 Minor — Copy file-to-existing-directory
Added `ResolveDestination()` helper: if source is a file and destination is an existing directory, append source filename.

### #9 Nit — Test namespace
Changed `PLang.Tests.App.Memory` → `PLang.Tests.App.Modules.Path`.

### #10 Nit — Explicit Pattern in List tests
All List test action records now set `Pattern = "*"` explicitly.

## Skipped
- **#5** (case-sensitive equality on Linux) — PLang primarily targets Windows. Noted as known limitation.
- **#6** (operator ==) — Path not yet used in collections/LINQ. Low risk.

## Files modified
- `PLang/App/Engine/Memory/Path.cs` — all code fixes
- `PLang.Tests/App/Modules/Path/PathTests.cs` — namespace, explicit Pattern, 6 new edge-case tests

## Verification
- `dotnet build` — 0 errors
- 1227/1227 tests pass (+6 new: Relative prefix, Move dir overwrite, Delete non-empty, null guards x2, Copy file-to-dir)

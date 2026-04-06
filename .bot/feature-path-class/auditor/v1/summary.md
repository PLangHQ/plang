# Auditor v1 Summary — Review of feature/path-class (coder v5)

## What this is

Code review of the PLangPath class implementation — a rich path wrapper that replaces raw `string` path parameters in all file action handlers. The coder went through 5 iterations (v1-v5), progressively improving OBP compliance based on reviewer feedback. This audit examines the final state (v5) for correctness, safety, and remaining issues.

## What was reviewed

- `PLang/App/Memory/Path.cs` — the core Path class (219 lines)
- All 7 file handlers: `read.cs`, `save.cs`, `copy.cs`, `move.cs`, `delete.cs`, `exists.cs`, `list.cs`
- `PLang.Tests/App/Modules/Path/PathTests.cs` — 40 Path unit tests
- `PLang.Tests/App/Modules/file/FileHandlerTests.cs` — handler integration tests
- Cross-referenced: source generator detection, @file type, ServiceError, Data, PLangFileSystem.RootDirectory

## OBP Assessment

**Strong compliance.** After 4 review rounds:
- All handlers are pure one-line delegators (`Path.Save(this)`, `Source.Copy(this)`, etc.)
- Path owns all behavior (Copy, Move, Delete, Read, List, Save, AsFile)
- Methods accept action records and navigate internally (OBP rule 2)
- Path stores Engine reference, navigates to FileSystem and Serializers (OBP rule 3)
- No decomposed parameters in public API

## Key Findings (10 total)

### Critical (1)
**#1 — No exception handling in behavior methods** (`Path.cs:98`). All 6 filesystem-mutating methods let IOException/UnauthorizedAccessException propagate as unhandled exceptions. Since handlers wrap these in `Task.FromResult()`, the exception is synchronous — not even a faulted Task. A locked file crashes the step instead of producing a PLang-level error.

### Major (3)
**#2 — Relative property prefix-matching bug** (`Path.cs:52`). `StartsWith(_fs.RootDirectory)` without trailing separator guard matches false positives (`/app` matches `/application`). Produces corrupted relative paths. Affects `ToString()` and any PLang string interpolation of paths.

**#3 — Move.Overwrite silently ignored for directories** (`Path.cs:121`). `_fs.Directory.Move()` doesn't accept an overwrite parameter. Users who write `move dir to dest, overwrite` get an IOException when dest exists — the flag has no effect.

**#4 — Delete non-empty directory throws** (`Path.cs:132`). `_fs.Directory.Delete(path, recursive: false)` throws IOException for non-empty directories. Should return a clear Data error instead.

### Minor (4)
**#5** — Equals uses OrdinalIgnoreCase unconditionally (wrong on Linux).
**#6** — `==` operator not overridden (reference vs value equality mismatch).
**#7** — No null guard on constructor parameters.
**#8** — Copy file-to-existing-directory case not handled (user expects file inside dir).

### Nit (2)
**#9** — PathTests namespace doesn't match file location.
**#10** — List tests rely on source generator defaults instead of explicit Pattern.

## Priority Recommendation

Fix in this order:
1. **#1** first — every file operation is affected, and it's the kind of bug users hit immediately in real workloads (permission errors, locked files, full disks)
2. **#2** next — Relative/ToString corruption is subtle and hard to debug when it happens
3. **#3 + #4** together — both are "method doesn't do what the user expects" cases

## Code Example — Finding #1

Current (throws on locked file):
```csharp
public Data Copy(actions.file.Copy action)
{
    if (!Exists) return Data.FromError(...);
    // If _fs.File.Copy throws IOException here, it's unhandled
    _fs.File.Copy(_absolutePath, action.Destination.Absolute, action.Overwrite);
    return Data.Ok(...);
}
```

Suggested pattern:
```csharp
public Data Copy(actions.file.Copy action)
{
    if (!Exists) return Data.FromError(...);
    try
    {
        EnsureDirectory(action.Destination.Directory);
        if (_fs.File.Exists(_absolutePath))
            _fs.File.Copy(_absolutePath, action.Destination.Absolute, action.Overwrite);
        else
            CopyDirectory(...);
        return Data.Ok(...);
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
    }
}
```

## Files Produced

- `.bot/feature-path-class/review-comments.json` — 10 structured findings
- `.bot/feature-path-class/auditor/v1/plan.md` — review plan
- `.bot/feature-path-class/auditor/v1/summary.md` — this file

# Auditor v4 Summary — Fresh Audit with Test Adequacy Lens

## What this is

Full re-audit of the Path class feature (coder v7, 1239 tests) applying the process improvements from v3 self-reflection. This time: verify both code correctness AND test adequacy for every code path.

## What was done

Reviewed all changed files: Path.cs (298 lines), 7 file handlers, PathTests.cs (911 lines), FileHandlerTests.cs, GlobalUsings, TypeMapping. Cross-referenced SerializeAsync implementation to trace exception types. Ran full test suite (1239 pass).

## OBP Assessment

Still strong. All 7 handlers are pure one-line delegators. Path owns all behavior. Action records are navigated, not decomposed. No regression from v7 changes.

## Findings (4 total)

### Major (1)

**#1 — Save's catch filter misses serialization exceptions** (`Path.cs:238`)

The `else` branch of Save calls `_engine.Channels.Serializers.SerializeAsync()`, which delegates to `JsonSerializer.SerializeAsync()`. The catch filter only catches `IOException | UnauthorizedAccessException`, but JSON serialization can throw `JsonException` (circular references, unsupported types) and `NotSupportedException`. These bypass the filter and propagate as unhandled exceptions.

Worse: `_fs.File.Create(_absolutePath)` at line 237 has already created the file before serialization starts. So on failure, a partial/empty file is left on disk AND the step crashes.

The Channel pipeline handles this correctly — `Engine/Channels/this.cs:128-143` wraps `SerializeAsync` in a general `catch (Exception ex)`. Path.Save doesn't.

```csharp
// Current — misses serialization exceptions:
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)

// Suggested — catch both IO and serialization failures:
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    return Data.FromError(new ServiceError(ex.Message, "IOError", 500));
}
catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
{
    return Data.FromError(new ServiceError(ex.Message, "SerializationError", 500));
}
```

Test needed: `Save_NonSerializableObject_ReturnsSerializationError`.

### Minor (1)

**#2 — Move directory overwrite is non-atomic** (`Path.cs:147-150`). If delete-then-move fails mid-way, destination is gone but source is untouched. Industry-standard behavior (same as `mv`), but could be safer with rename-to-temp approach. Not blocking.

### Nit (2)

**#3 — Inconsistent StatusCode assertions.** Copy/Move/Delete NotFound tests check `Error.Key` but not `Error.StatusCode`. Read/List NotFound tests check both. IOError tests check both. Should be consistent.

**#4 — Duplicate test.** `Delete_NonEmptyDirectory_WithoutRecursive_ReturnsError` (line 661) is a weaker version of `Delete_NonEmptyDirectory_WithoutRecursive_ReturnsDirectoryNotEmpty` (line 899).

## Test Adequacy Assessment (new lens)

Applied the v3 checklist: for each code path, "which test hits this line?"

| Code Path | Test | Assertion Quality |
|-----------|------|-------------------|
| Copy file success | `Copy_File_CopiesToDestination` | Strong — checks dest content + source preserved |
| Copy dir ± subfolders | 3 tests | Strong — checks files exist/don't exist |
| Copy not found | `Copy_NotFound_ReturnsError` | Key only, no StatusCode |
| Copy overwrite conflict | `Copy_DestExists_OverwriteFalse_ReturnsIOError` | Strong — Key + StatusCode |
| Copy file-to-dir | `Copy_FileToExistingDirectory_PutsFileInsideDir` | Strong |
| Move file/dir/overwrite | 5 tests | Strong |
| Delete file/dir/recursive | 4 tests | Strong |
| Delete non-empty | 2 tests (1 redundant) | Stronger version checks Key + StatusCode |
| Delete permission denied | `Delete_ReadOnlyParent_ReturnsIOError` | Key only (StatusCode missing) |
| Read success/not-found/denied | 3 tests | Strong |
| List all paths | 5 tests | Strong — checks names, not just count |
| Save string/bytes/object | 3 tests | String exact, bytes exact, object Contains |
| Save permission denied | `Save_ReadOnlyDir_ReturnsIOError` | Key only |
| Save serialization failure | **NO TEST** | **No coverage — finding #1** |
| Relative all paths | 3 tests | Exact values |
| Constructor null guards | 2 tests | Correct exception type |

**Verdict: Test adequacy is strong overall.** The tester v1 fixes addressed the major gaps. The one remaining uncovered path is the serialization failure in Save — which is also uncovered in the production code (finding #1).

## Recommendation

**Fix finding #1 before merge** — it's the same class of bug as the original critical finding (behavior method that can throw). The fix is small (add a second catch clause + one test). Findings #2-#4 are non-blocking.

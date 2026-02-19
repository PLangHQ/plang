# Auditor v4 Review Summary (reviewing coder v7)

4 findings. Fresh audit with test adequacy lens.

## #1 Major — Save catch filter misses serialization exceptions
`Save()`'s catch only handles `IOException | UnauthorizedAccessException`. The `else` branch calls `SerializeAsync()` which can throw `JsonException` or `NotSupportedException` — these bypass the filter and crash. Also leaves a partial file on disk.

## #2 Minor — Move directory overwrite is non-atomic
Delete-then-move can lose data if move fails after delete. Industry-standard behavior (same as `mv`). Not blocking.

## #3 Nit — Inconsistent StatusCode assertions
Copy/Move/Delete NotFound tests check `Error.Key` but not `Error.StatusCode`. Read/List NotFound tests check both.

## #4 Nit — Duplicate test
`Delete_NonEmptyDirectory_WithoutRecursive_ReturnsError` (line 661) is a weaker duplicate of `Delete_NonEmptyDirectory_WithoutRecursive_ReturnsDirectoryNotEmpty` (line 899).

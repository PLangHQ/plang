# v3 Summary — Address v2 review feedback

Addressed 3 categories of review feedback:

1. **Semantic fix**: `IsFile`/`IsDirectory` are now structural (extension-based, no I/O). `ToString()` returns `Relative`. `Exists` stays as live filesystem check.

2. **OBP compliance**: `Read()`, `List()`, `Save()` methods moved to `Path.cs`. Handlers are now one-line delegators.

3. **Error handling**: Errors are created inside Path methods, not in handlers.

All 1219 tests passing. No regressions.

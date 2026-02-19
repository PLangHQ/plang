# v2 Summary: Path behavior methods + thin handler delegators

Fixed OBP violations identified in review comments:
- Path now owns Copy/Move/Delete behavior (OBP rule #1)
- Handlers are thin delegators — operation-specific params (overwrite, includeSubfolders, recursive) live on handlers where LLM maps them
- `!Source.IsFile` bug fixed — now uses `Exists` so directories work correctly
- 14 new tests added (11 Path-level, 3 handler-level)
- All 1210 tests pass

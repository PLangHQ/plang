# Docs v1 Plan — system-goals-architecture

## Context

This branch adds 170 new C# files under `PLang/App/`, renaming `Runtime2` → `App` and implementing the "Everything is Data" architecture. The coder is at v4, auditor passed at v2. No docs bot has run yet.

**User directive:** The project rolled back from v0.3 to v0.2. Documentation folder `Documentation/v0.3/` must be renamed to `Documentation/v0.2/`. Version files must be updated to `0.2.1`.

## Tasks

### 1. Rename `Documentation/v0.3/` → `Documentation/v0.2/`
- `git mv Documentation/v0.3 Documentation/v0.2`
- Update v0.3 references inside `build_process.md` (`.pr format version`, `builderVersion` field) to v0.2
- Grep all docs for stale v0.3 references and fix

### 2. Update version.txt to 0.2.1
- `PLang/version.txt`: `0.1.18.1` → `0.2.1`
- `Publish/version.txt`: `0.1.19.1` → `0.2.1`

### 3. Review XML doc coverage on key new files
- Spot-check the core types: `App/this.cs`, `Data/this.cs`, `Actor/this.cs`, `Goal/this.cs`, `Step/this.cs`, `Action/this.cs`
- Check module handlers: `condition/if.cs`, `loop/foreach.cs`, `goal/call.cs`, `variable/set.cs`
- Check error types: `Errors/IError.cs`, `Errors/Error.cs`
- Flag gaps, fill where meaningful (skip noise like trivial getters)

### 4. Verify architecture docs match implementation
- Read each file in `Documentation/v0.2/` and cross-check against actual C# code
- Flag any stale references to old Runtime2 types or patterns
- Update docs where implementation diverged

### 5. Write reports
- `docs-report.json` at `.bot/system-goals-architecture/docs-report.json`
- `verdict.json` at `.bot/system-goals-architecture/docs/v1/verdict.json`
- `v1/summary.md`
- Update bot root `summary.md`
- Commit, generate patch, push

## Not in scope
- PLang .goal examples (tester's job)
- Builder prompt changes
- Fixing auditor findings (coder's job)

# codeanalyzer v1 — runtime2-callstack — plan

## Scope

Coder/v1 just landed the callstack refactor (commits 01cfc14f .. 4a23ff27). 7 commits,
~15 new/replaced files in `PLang/App/CallStack/`, `PLang/App/Errors/`, `PLang/App/this.cs`,
`PLang/App/modules/error/handle.cs`, `PLang/App/modules/debug/tag.cs`, `PLang/App/Variables/this.cs`,
`PLang/App/Debug/this.cs`. C# tests pass (2580/2580). PLang tests blocked on a pre-existing
builder issue, not related to this code.

## Files to analyze

Core (priority):
- `PLang/App/CallStack/Call/this.cs` — the central new entity (Call)
- `PLang/App/CallStack/this.cs` — rewritten CallStack with AsyncLocal Push/Pop
- `PLang/App/CallStack/CallStackFlags.cs` — flag struct
- `PLang/App/CallStack/Diff.cs` — diff record
- `PLang/App/CallStack/SerializableCallStack.cs` — serialization shape
- `PLang/App/Errors/this.cs` — new Errors namespace root
- `PLang/App/this.cs` — App.Run reshape
- `PLang/App/modules/debug/tag.cs` — new tag action
- `PLang/App/modules/error/handle.cs` — recovery dispatch with Cause threading
- `PLang/App/Variables/this.cs` — new collection-level events
- `PLang/App/Debug/this.cs:115-200` — `--debug={callstack:...}` parsing

## Approach

5-pass review per character spec:
1. OBP compliance (folder layout, @this convention, no AppRoot, no namespace shortcuts)
2. Simplification (dead code, redundant work, premature generalization)
3. Readability
4. Behavioral reasoning (concurrency, exception flow, lifetime)
5. Deletion test (what code path can be removed without breaking a test?)

Output: `result.md` with per-file findings, then `verdict.json`.

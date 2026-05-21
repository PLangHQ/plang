# Coder plan — filesystem-permission v1

Implementing the 5-stage architect plan. Big scope; one stage at a time, commit per stage so each compiles + tests green for bisectability.

## Stage 1 — Permission types (self-contained, no engine touch)

Files:
- `PLang/App/FileSystem/Permission/this.cs` — `@this` Permission record (positional, `AppId, Actor, Path, Verb, Match`) + `Match` enum + `Covers(Permission)` + private `PathMatches` (Exact / Glob / Regex, fail-closed default).
- `PLang/App/FileSystem/Permission/Verb/this.cs` — `@this` container; init-only `Read/Write/Delete` props **default to non-null** (option B confirmed by Ingi). `Covers(@this)` returns true iff for every non-null sub-verb on request, grant has non-null sub-verb that covers it.
- `PLang/App/FileSystem/Permission/Verb/Read.cs` — `Read(bool Recursive = true, bool Metadata = true)` + `Covers(Read)`.
- `PLang/App/FileSystem/Permission/Verb/Write.cs` — `Write(bool Overwrite = true, bool Recursive = true)` + `Covers(Write)`.
- `PLang/App/FileSystem/Permission/Verb/Delete.cs` — `Delete(bool Recursive = true)` + `Covers(Delete)`.

Sub-verb cover semantics: for each bool option, `grant_opt || !request_opt` (grant gives at least what request asks).

Dependencies: `Microsoft.Extensions.FileSystemGlobbing` for Glob. Add to `PLang/PLang.csproj` (currently only transitively present via PLang.Tests).

Tests to flip green: `PLang.Tests/App/FileSystem/PermissionTests/VerbCoversTests.cs` (11) + `PermissionCoversTests.cs` (11).

## Stage 2a — Snapshot-resume engine (biggest)

Read stage-2a-snapshot-resume.md in full. Touches: `App/Types`, `App/Snapshot`, `App/Goals/Goal`, `App/CallStack`, `App/modules/output`, `App/Channels/Stream`, action records. Drops `App.Run`/`App.RunAction`/`AskCallback`/`ErrorCallback`/`cause` param.

## Stage 2b — Path.Authorize

Tiny — `Path.Authorize(verb)` on `PLang/App/FileSystem/Path.cs` plus `PermissionDenied` error.

## Stage 3 — Storage binding

`Actor.@this.Permission` view: in-memory list + sqlite-backed `permission` table. JSON-filter scoping.

## Stage 4 — FS surface rewrite

Mechanical but big — ~50–100 call sites. Path-in, Data-out. Drop `IFileSystem` inheritance. Sub-stage per file family.

## Stage 5 — End-to-end Messages test

PLang `.test.goal` fixture under `Tests/Permission/` (test-designer already wrote the stubs).

## Open / will ask

- Stage 2a is large; will ask for review per deliverable rather than landing the whole thing in one push.
- Stage 4 sub-staging — follow the order in `stage-4-filesystem-surface.md`.

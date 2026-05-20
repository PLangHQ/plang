# Stage 1: Permission Types

**Goal:** Land the pure types — `Permission` record, `Verb/@this` and its three records, `Match` enum — with all `Covers` logic. No filesystem dependency, no engine integration, no storage, no manager state. C# tests pin the coverage matrix.

**Scope:** Types, records, enum, coverage methods. Nothing else.

**Excluded:**
- `Path.Authorize` (stage 2b). Snapshot-resume infrastructure (stage 2a).
- Storage binding (stage 3).
- IPLangFileSystem rewrite (stage 4).
- Anything that touches the engine, actor context, or signing pipeline.

## Deliverables

- `PLang/App/FileSystem/Permission/this.cs` — `@this` IS the `Permission` record (renamed from `FilePermission`). Has `Covers(Permission)` and private `PathMatches(string)`. No separate manager class in this folder — the per-actor manager (`Find`/`Add`/`Revoke`) lives at `App/Actor/Permission/this.cs`, landed by stage 3.
- `PLang/App/FileSystem/Permission/Verb/this.cs` — `@this` with `Read`/`Write`/`Delete` properties and `Covers(@this)`.
- `PLang/App/FileSystem/Permission/Verb/Read.cs` — record + `Covers(Read)`.
- `PLang/App/FileSystem/Permission/Verb/Write.cs` — record + `Covers(Write)`.
- `PLang/App/FileSystem/Permission/Verb/Delete.cs` — record + `Covers(Delete)`.
- `Match` enum (in `this.cs` alongside `Permission`).
- C# tests under `PLang.Tests/App/FileSystem/PermissionTests/` covering:
  - Coverage matrix (each variant: full-grant, full-narrow, every partial mix)
  - Match-mode dispatch (Exact / Glob / Regex)
  - JSON round-trip of a `Permission` record
  - The "same record, two roles" property — `grant.Covers(request)` reads naturally with broad and narrow records

## Dependencies

None. This stage is fully self-contained.

## Design

The full record + verb design lives in [v1/plan/permission-design.md](v1/plan/permission-design.md). Coder reads that for code shapes; this stage file is just the unit of work.

### Three things the coder must get right

1. **Verb variants default to fully-granted records.** `new Verb.@this()` covers every request. Narrowing is explicit.
   - Test: `new Verb.@this().Covers(new Verb.@this())` is true.
   - Test: `new Verb.@this { Write = new Write(Overwrite: false) }.Covers(new Verb.@this())` is false (default request wants Overwrite, narrowed grant doesn't have it).

2. **Same record for grant and request.** `Covers(Permission request)` takes another `Permission` of the same type. The asymmetry is the Match field plus the verb shape — not two parallel types.
   - Test: a grant with `Match.Glob` and pattern `/apps/*/file.txt` covers a request with `Match.Exact` and path `/apps/Email/file.txt` when both have full-allow verbs.
   - Test: same grant does NOT cover a request whose verb narrows the grant (request asks for verb the grant doesn't include).

3. **Match-mode dispatch is closed.** Only Exact, Glob, Regex; the switch's default returns false. No throwing on unknown enum values; degraded behavior to deny is the safe choice.
   - Test: any future enum value (in a test fixture, via reflection or a fake) returns false instead of throwing.

### What stage 1 does NOT do

- Doesn't deal with storage at all — `Permission/@this` is a pure record. Storage lives at `Actor/Permission/@this` (stage 3).
- Doesn't define `Path.Authorize` (stage 2b) or any engine/Snapshot machinery (stage 2a).
- Doesn't touch `IPLangFileSystem` at all — stage 4.
- Doesn't wire signing — stage 3 (where `Actor/Permission/@this` deals with signed Data).

## Acceptance

`dotnet run --project PLang.Tests` passes. New tests under `PermissionTests/` exercise the full coverage matrix and the Match dispatch. No production code outside the Permission folder is touched.

Glob library: default to `Microsoft.Extensions.FileSystemGlobbing` unless an AOT or dependency constraint surfaces. The grant-matching semantics needed are well within that library's capabilities.

## What this stage unblocks

Stages 2a/2b and 3 all depend on `Permission` being a real type they can reference. After stage 1, they can start in parallel — stage 2a lands Snapshot-resume infrastructure, stage 2b builds `Path.Authorize` (depends on 2a), stage 3 builds the storage view. Stage 4 (the FS surface) depends on 2b + 3.

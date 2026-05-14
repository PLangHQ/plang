# Stage 1: Permission Types

**Goal:** Land the pure types — `Permission` record, `Verb/@this` and its three records, `Match` enum — with all `Covers`/`HasAccess` logic. No filesystem dependency, no storage, no manager state. C# tests pin the coverage matrix and JSON round-trip.

**Scope:** Types, records, enum, coverage methods. Nothing else.

**Excluded:** Storage binding (stage 2). `IPLangFileSystem` rewrite (stage 3). Error types (stage 4). Anything that touches an actual filesystem.

**Deliverables:**

- `PLang/App/FileSystem/Permission/this.cs` — contains the `Permission` record with `HasAccess(Path, Verb.@this)` and private `PathMatches(Path)`. The `@this` *class* is stubbed (empty manager) — stage 2 fills it.
- `PLang/App/FileSystem/Permission/Verb/this.cs` — `@this` with `Read`/`Write`/`Delete` properties and `Covers(@this)`.
- `PLang/App/FileSystem/Permission/Verb/Read.cs` — record + `Covers(Read)`.
- `PLang/App/FileSystem/Permission/Verb/Write.cs` — record + `Covers(Write)`.
- `PLang/App/FileSystem/Permission/Verb/Delete.cs` — record + `Covers(Delete)`.
- `Match` enum (in `this.cs` alongside `Permission`).
- C# tests under `PLang.Tests/App/FileSystem/PermissionTests/` covering the coverage matrix (each variant: full-grant, full-narrow, every partial mix), Match-mode dispatch (Exact / Glob / Regex), and JSON round-trip of a `Permission` record.

**Dependencies:** None. This stage is fully self-contained.

## Design

The full design is in [plan/permission-design.md](v1/plan/permission-design.md). Coder reads that for code shapes; this stage file is just the unit of work.

Three things the coder must get right and stage 1 tests must enforce:

1. **Verb variants default to fully-granted records.** `new Verb.@this()` covers every request. Narrowing is explicit. Test: `new Verb.@this().Covers(new Verb.@this())` is true; `new Verb.@this { Write = new Write(Overwrite: false) }.Covers(new Verb.@this())` is false (default request wants Overwrite, narrowed grant doesn't have it).

2. **`HasAccess` takes whole `Path`, not strings.** No `path.Absolute` decomposition at the call site. The record extracts what it needs internally. Test: a permission with `Match.Exact` and `Path = "/a/b"` returns true for `Path` object with `Absolute = "/a/b"` regardless of `Raw` or other Path fields.

3. **Match-mode dispatch is closed.** Only Exact, Glob, Regex; the switch's default returns false. Test: any future enum value (in a test fixture) returns false instead of throwing.

The Glob library choice is open (see `plan/open-questions.md` #4). Coder picks `Microsoft.Extensions.FileSystemGlobbing` unless a constraint surfaces.

## What stage 1 does NOT do

- Doesn't instantiate `Permission/@this` with any storage. The class exists as a shell so stage 2 can fill it.
- Doesn't define `PermissionRequired` — that's stage 4 (its shape depends on what we want the prompt to receive).
- Doesn't touch `IPLangFileSystem` at all.

## Acceptance

`dotnet run --project PLang.Tests` passes. New tests under `PermissionTests/` exercise the full coverage matrix and the Match dispatch. No production code outside the Permission folder is touched.

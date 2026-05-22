# Stage 1: Permission Types

**Goal:** Land the pure types — `Permission` record, `Verb/@this` and its three sub-records, `Match` enum — with all `Covers` logic. No filesystem dependency, no engine integration, no storage. C# tests pin the coverage matrix.

**Scope:** Types, records, enum, coverage methods. Nothing else.

**Out of scope:**
- `Path.Authorize` (stage 2b). Snapshot-resume infrastructure (stage 2a).
- Storage binding (stage 3).
- IPLangFileSystem rewrite (stage 4).
- Anything that touches the engine, actor context, or signing pipeline.

## Deliverables

- `PLang/App/FileSystem/Permission/this.cs` — `@this` IS the `Permission` record. Has `Covers(Permission)` and private `PathMatches(string)`. No separate manager class in this folder; the per-actor manager (`Find`/`Add`/`Revoke`) lives at `App/Actor/Permission/this.cs` (stage 3).
- `PLang/App/FileSystem/Permission/Verb/this.cs` — `@this` with `Read`/`Write`/`Delete` properties and `Covers(@this)`.
- `PLang/App/FileSystem/Permission/Verb/Read.cs` — record + `Covers(Read)`.
- `PLang/App/FileSystem/Permission/Verb/Write.cs` — record + `Covers(Write)`.
- `PLang/App/FileSystem/Permission/Verb/Delete.cs` — record + `Covers(Delete)`.
- `Match` enum colocated with `Permission` in `this.cs`.

## Design

### Permission record

```csharp
public sealed record Permission(
    string AppId,         // App.this.cs:34 — the app the grant belongs to
    string Actor,         // "user" | "system" | "service"
    string Path,          // absolute path or glob pattern
    Verb.@this Verb,      // verb with sub-options
    Match Match);         // Exact / Glob / Regex
{
    public bool Covers(Permission request) => /* ... */;
    private bool PathMatches(string path) => /* ... */;
}
```

### Verb shape

`Verb.@this` is a container with three optional sub-records, default-fully-granted:

```csharp
public sealed record @this(Read? Read = null, Write? Write = null, Delete? Delete = null)
{
    // Default constructor → all three verbs fully granted.
    // Narrowing: new @this { Write = new Write(Overwrite: false) } drops Overwrite.
}
```

Each sub-verb is a record with boolean sub-options, default-true:
- `Read(Recursive=true, Metadata=true)`
- `Write(Overwrite=true, Recursive=true)`
- `Delete(Recursive=true)`

A `Read.Covers(Read request)` returns true iff every option granted in the grant is also requested-or-not-needed. Same shape per verb.

### Match modes

Closed enum:
- `Exact` — string equality on absolute path.
- `Glob` — `Microsoft.Extensions.FileSystemGlobbing` pattern match.
- `Regex` — .NET regex.

Dispatch is closed; unknown enum values → deny (fail-closed, no throw).

## Three things the coder must get right

1. **Verb variants default to fully granted.** `new Verb.@this()` covers every request. Narrowing is explicit record-copy with explicit false.
   - Test: `new Verb.@this().Covers(new Verb.@this())` is true.
   - Test: `new Verb.@this { Write = new Write(Overwrite: false) }.Covers(new Verb.@this())` is false.

2. **Same record for grant and request.** `Covers(Permission request)` takes another `Permission`. Asymmetry encoded in `Match` + verb shape — not two parallel types.
   - Test: a grant with `Match.Glob` and pattern `/apps/*/file.txt` covers a request with `Match.Exact` and path `/apps/Email/file.txt` when verbs match.
   - Test: same grant does NOT cover a request whose verb narrows the grant.

3. **Match-mode dispatch is closed.** Only Exact, Glob, Regex; switch's default returns false. No throwing on unknown values.

## Tests

C# under `PLang.Tests/App/FileSystem/PermissionTests/`:
- Coverage matrix per verb (full-grant, full-narrow, every partial mix).
- Match-mode dispatch (Exact / Glob / Regex + unknown enum → false).
- JSON round-trip of a `Permission` record.
- "Same record, two roles" property — `grant.Covers(request)` reads naturally either way.

## Acceptance

- `dotnet run --project PLang.Tests` passes. New `PermissionTests/` exercises the full coverage matrix.
- No production code outside the `Permission/` folder touched.
- Glob library: default `Microsoft.Extensions.FileSystemGlobbing` unless an AOT or dependency constraint surfaces.

## What this unblocks

Stages 2a, 2b, and 3 all reference `Permission` and `Verb`. After stage 1: 2a runs in parallel (no permission types needed), 2b builds `Path.Authorize` on top of 2a, 3 builds the storage view. Stage 4 depends on 2b + 3.

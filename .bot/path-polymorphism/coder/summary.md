# Coder — path-polymorphism

## Version
v2 (v1 = stages 1–7; v2 = lowercase aliases + Stage 8). Single branch, all work done.

## What this is

PLang's `path` is now scheme-polymorphic. `FilePath` and `HttpPath` (future
`S3Path`/`GitPath`) implement one verb surface (Read/Write/Delete/Stat/List/
Copy/Move). File handlers are one-liners over `path.X()`. The `app.filesystem`
namespace folded under `app.types/path/`. The System.IO.Abstractions wrapper
layer is gone entirely.

Closes codeanalyzer v2 finding #1 on `filesystem-permission` (handler-layer
authorize copy-paste).

## Status — ALL 8 STAGES COMPLETE

| # | Stage | Status |
|---|-------|--------|
| 1 | Namespace move `app.filesystem` → `app.types.path` + `@this` convention | Done |
| 2 | `path` abstract + `FilePath` + scheme registry | Done |
| 3 | Handler one-liners + `IFile`/`DefaultFileProvider` deleted | Done |
| 4 | `[PathScheme]` attribute | Done |
| 5 | `HttpPath` | Done |
| 6 | Per-scheme `Absolute` canonical form | Done |
| 7 | `PathSchemeContractTests` framework + fixtures | Done |
| 8 | System.IO.Abstractions wrapper layer removed (added per Ingi) | Done |

Plus: lowercase `path`/`filepath`/`httppath` global aliases.

**Tests:** C# **2875 / 2875 pass**. PLang `--test` **202 pass / 0 fail / 1 stale**.

The 1 stale (`ContextVars2.test.goal`) — its `%!fileSystem%` assertion was
removed (correct, that context var is gone with the wrapper). Its `.pr` rebuild
is blocked by a **pre-existing** `plang build` breakage (`builder.app` →
`builder.load` action staleness in `os/system/builder/`, untouched on this
branch — Ingi is fixing `plang build` separately). Not a path-polymorphism
defect.

## Key design decisions / deviations from the architect plan

1. **Lowercase aliases, not PascalCase.** Architect specified
   `global using Path = …`. `Path` collides with `System.IO.Path` across the
   codebase. `path` (lowercase) does not — C# is case-sensitive — and matches
   the PLang concept name. Aliases: `path`, `filepath`, `httppath`. The 3 files
   physically inside `namespace app.types` qualify with
   `global::app.types.path.@this` (the bare name is ambiguous with the child
   namespace there — the only carve-out).

2. **Scheme registry factory is `Func<string, Context, Path>`** (architect
   sketched `Func<string, Path>`). Path construction genuinely needs Context
   (goal-dir resolution, App root). `Scheme.From(raw, context)`.

3. **Abstract `[PlangType]` types are indexed** by `app.types.Registry`
   (previously a hard skip) — required for the abstract `path` base to resolve
   to the PLang name `path`.

4. **`GlobMatches` rewritten** glob→regex. The FileSystemGlobbing matcher
   chokes on `://` in URLs; the regex form works for file paths and URLs
   uniformly. File-glob semantics (`*` = one segment) preserved.

5. **Stage 8 was added at Ingi's request.** It was "flag-and-split" deferred in
   v1. Investigation showed the deferral fear (security-sensitive `ValidatePath`)
   was wrong: the root-jail (`FileAccessControl`) was already dead — replaced by
   `Path.Authorize`/`Actor.Permission`. `IPLangFileSystem` was pure
   double-wrapping. So the removal was clean: delete the wrappers, relocate
   `ValidatePath` as a plain normalization static, migrate I/O callers to
   `System.IO`.

## Stage 8 — what moved

- **Deleted:** `IPLangFileSystem`, `PLangFileSystem` + 9 `Default/*` wrappers,
  the `System.IO.Abstractions` NuGet package, `FileAccessControl` + its dead
  Add/Set/Clear API, `App.FileSystem`, the `%!fileSystem%` context variable.
- **`filepath.ValidatePath(raw, app)`** — new static, path-string
  normalization only (relative→absolute anchor + `/system/`→os-folder
  fallback). NOT a gate — `Authorize` is. `filepath.Resolve` calls it;
  bootstrap callers (App.Load/Save, builder — no Goal) call it directly.
- **`filepath.OsAbsolutePath`** — the `os/` folder path.
- **~18 production + ~20 test files** migrated `App.FileSystem.{File,Directory,
  Path}` → `System.IO.*`; `IPLangFileSystem` params → `app.@this`.
- `goals.LoadFromFileAsync` now normalizes its path argument (the wrapper did
  this implicitly on every read; `System.IO.File` does not).
- `test/discover` scopes discovery to the app root with an explicit prefix
  check (the wrapper used to reject out-of-root traversal).
- `Executor` takes a startup-directory string, not an `IPLangFileSystem`.

## Code example — handler shape, before/after

Before (pre-Stage-3):
```csharp
[Code] public partial IFile Files { get; }
public async Task<data.@this> Run()
{
    var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
    if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
    var result = Files.Read(this);
    // … ResolveVariables …
}
```

After:
```csharp
public async Task<data.@this> Run()
{
    var read = await Path.Value!.ReadText();   // Authorize is inside the verb
    if (!read.Success || read.Type?.ClrType.Exit() == true) return read;
    if (ResolveVariables.Value && read.Value is string content)
        return new data.@this(read.Name,
            Context.Variables.Resolve(content, skipInfrastructure: true), read.Type);
    return read;
}
```

## Open items for review / discussion (Ingi flagged)

1. **`PLangPath` test-alias name collision** — pre-existing test alias
   `using PLangPath = app.types.path.@this`. Ingi wants to discuss a rename
   after the stages land. Harmless today (per-file alias, distinct from the
   now-deleted wrapper class). Left as-is per Ingi.
2. **`filepath`/`httppath` alias casing** — Ingi confirmed lowercase `path`;
   the subclass-alias casing was never explicitly finalized. Went lowercase for
   consistency.

## Branch state

All commits pushed to `path-polymorphism` (through `e88eaee0`). Ready for
codeanalyzer review.

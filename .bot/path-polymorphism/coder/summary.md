# Coder — path-polymorphism

## Version
v1 (single continuous session — all 7 stages).

## What this is

PLang's `path` becomes scheme-polymorphic. `FilePath` and `HttpPath` (and future
`S3Path`/`GitPath`/…) each implement the same verb surface (Read/Write/Delete/Stat/
List/Copy/Move). File action handlers degenerate to one-liners over `Path.X()`.
`IFile`/`DefaultFileProvider` are deleted. The `app.filesystem` namespace folds
under `app.types/path/`.

Closes codeanalyzer v2 finding #1 on `filesystem-permission` (handler-layer
authorize copy-paste).

## Status — all 7 stages implemented

| # | Stage | Status |
|---|-------|--------|
| 1 | Namespace move `app.filesystem` → `app.types.path` + `@this` convention | **Done** |
| 2 | `path` abstract + FilePath + Scheme registry | **Done** |
| 3 | Handler one-liners + IFile/DefaultFileProvider deleted | **Done (flag-and-split, see below)** |
| 4 | `[PathScheme]` attribute (marker) | **Done** |
| 5 | HttpPath impl | **Done** |
| 6 | Per-scheme `Absolute` canonical form | **Done** |
| 7 | Contract test framework + fixtures | **Done** |

**Tests:** C# 2875 pass / 1 red; PLang `--test` 203/203 pass.

The single C# red — `HandlerShapeTests.PLangFileSystem_AndWrapperLayer_AbsentFromProductionAssembly`
— is a **deliberate, honest red**: see "Flag-and-split" below.

## Stage detail

### Stage 1 — Namespace move
- `PLang/app/filesystem/**` → `PLang/app/types/path/**` (git-mv, history preserved).
- `class path` → `class @this` (`app.types.path.@this`); partials `this.cs` /
  `this.Operations.cs` / `this.Authorize.cs`.
- `permission/` + `verb/` moved under `path/`.
- `Registry.cs`: abstract types that declare `[PlangType]` are now indexed (the
  abstract `path` base must resolve to the PLang name `path`).
- ~50-reference sweep across PLang/PLang.Tests/PlangConsole/PLang.Generators.

### Stage 2 — abstract path + FilePath + Scheme registry
- `path.@this` abstract; verb surface abstract; `CopyTo`/`MoveTo` virtual with
  cross-scheme defaults (ReadBytes→WriteBytes; CopyTo+Delete).
- `path/file/this.cs` — `FilePath`, holds the file verb impls.
- `path/scheme/this.cs` — per-App registry; `ConcurrentDictionary`,
  case-insensitive, `From(raw, context)`, unknown scheme → `SchemeNotRegistered`.
- `app.types.@this` exposes `Scheme`; App ctor registers `"file"`.
- `Conversion.cs` routes `path` conversion through `Scheme.From`.

### Stage 3 — handler one-liners + IFile death
- 7 file handlers collapsed to delegations over `Path.Value!.X()`. Authorize
  preamble gone — gate fires inside the Path verb (`AuthGate`).
- `IFile` + `DefaultFileProvider` deleted (~280 lines absorbed into FilePath:
  MIME-aware ReadText, options-bearing Delete/Copy/Move/List, Serializers Save).
- `PathTests.cs` (968 lines testing the deleted provider) deleted.

### Stage 4 — `[PathScheme]` attribute
- `AttributeTargets.Class`, `AllowMultiple = true`. Applied to FilePath/HttpPath.
  Marker only — built-ins registered explicitly; future `code.load` consumes it.

### Stage 5 — HttpPath
- `path/http/this.cs` — `HttpPath : Path`, `[PathScheme("http")] [PathScheme("https")]`.
- Verbs map to HTTP methods; `static readonly HttpClient` (process-shared).
- "Let the server respond": non-2xx → `data.@this.Fail` with status preserved;
  network failure → Fail/`NetworkError`.
- Every request signed via the `signing.sign` action (X-Signature header).
- App ctor registers `"http"` / `"https"`.

### Stage 6 — per-scheme Absolute
- `Absolute` now virtual; FilePath unchanged (OS-normalized).
- `HttpPath.Absolute` canonical form: lowercase scheme+host, strip default port,
  normalize path, root→single-slash, sort query keys, strip fragment.
- `permission/this.cs` `GlobMatches` rewritten glob→regex (the FileSystemGlobbing
  matcher chokes on `://`); works for file paths and URLs uniformly.

### Stage 7 — contract test framework
- `HttpTestServer` (HttpListener-based, in-box).
- `PathSchemeContractTests<TFixture>` generic base — 8 contract assertions
  (verb round-trips, permission gate, uniform failure shape).
- `FilePathFixture` + `HttpPathFixture` — both mint out-of-root paths so the
  Permission gate fires uniformly; the suite drives auth via a canned channel.
- `FilePathContractTests` + `HttpPathContractTests` (`[InheritsTests]`).
- `CrossSchemeTests` — file↔http CopyTo/MoveTo via the base default.

## Flag-and-split — deferred work

The architect's Stage 3 also called for deleting the System.IO.Abstractions
wrapper layer (`PLangFileSystem` + `PLangFile`/`PLangDirectoryWrapper`/… under
`path/Default/`). That is **NOT done** — ~14 non-action callers still consume
`App.FileSystem` (`App.Save/Load`, `Builder`, `settings.Sqlite`, `llm/OpenAi`,
`ui/Fluid`, `http/code/Default`, `Executor`, `goals/*`, `test/discover`,
`test/report`, `code/load`, `actor/context`, `PlangConsole/Program`). Many use it
for app-identity concerns (`RootDirectory`, `ValidatePath`, `OsDirectory`) rather
than file I/O. Migrating them is a separate effort.

`HandlerShapeTests.PLangFileSystem_AndWrapperLayer_AbsentFromProductionAssembly`
asserts the wrapper is absent — it fails by design until that migration lands.
The test carries a code comment explaining the deviation; it is a truthful red,
not a stub.

## Decisions / deviations

- **No global `Path` alias.** The architect plan called for
  `global using Path = app.types.path.@this;` — it collides with `System.IO.Path`
  across dozens of files. Reverted to per-file aliases; production code uses
  `global::app.types.path.@this` or `FilePath`/`HttpPath` per-file aliases.
  Documented in `GlobalUsings.cs`.
- **Scheme registry factory is `Func<string, Context, Path>`** (not the
  architect-sketched `Func<string, Path>`). Path construction needs Context
  (goal-dir resolution, App.FileSystem); passing it explicitly beats relying on
  post-construction IContext state. `Scheme.From(raw, context)`.
- **Abstract `[PlangType]` types are now indexed** by `app.types.Registry`
  (previously a hard skip) — required for `path` to resolve to its PLang name.
- **`GlobMatches` rewritten** from FileSystemGlobbing to a glob→regex compile.
  Necessary for URL grants; file-glob semantics (`*` = one segment) preserved.

## Open item for discussion (Ingi flagged)

The test alias `PLangPath` (`using PLangPath = app.types.path.@this;`, pre-existing
in test files, repointed by Stage 1) collides by *name* with the production
wrapper class `app.types.path.Default.PLangPath`. Harmless (different scopes), and
resolves itself when the wrapper layer is deleted — Ingi wants to discuss after
the stages land.

## Code example — handler shape before/after

Before (Stage 3 input):
```csharp
[Code] public partial IFile Files { get; }
public async Task<data.@this> Run()
{
    var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
    if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
    var result = Files.Read(this);
    // ... ResolveVariables ...
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

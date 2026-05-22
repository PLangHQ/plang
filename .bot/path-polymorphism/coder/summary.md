# Coder — path-polymorphism (v1)

## Version
v1.

## What this is

Implements the architect's 7-stage path-polymorphism plan. PLang's `path` becomes
scheme-polymorphic — `FilePath`, `HttpPath`, future `S3Path`/`GitPath` each implement the
same verb surface. File handlers degenerate to one-liners. The whole `app.filesystem`
namespace folds under `app.types.path/`.

Closes codeanalyzer v2 finding #1 on `filesystem-permission` (handler-layer authorize
copy-paste) — once Stage 3 lands.

## What was done — v1

**Complete:** Stages 1, 2 (structural + most tests), 4.

**Deferred:** Stages 3, 5, 6, 7.

### Stage 1 — Namespace move (done)
- All `PLang/app/filesystem/*` moved to `PLang/app/types/path/*`.
- `class path` → `class @this` (`app.filesystem.path` → `app.types.path.@this`).
- Permission + verb folders moved under `path/permission/` and `path/permission/verb/`.
- `IPLangFileSystem` + `Default/*` (System.IO.Abstractions wrappers) moved to `path/`
  (will be deleted in Stage 3).
- `Registry.cs`: skip-abstract narrowed to "abstract && no [PlangType]" so the new
  abstract base remains indexed (otherwise `GetTypeName(typeof(data.@this<Path>))`
  returned `"this"` instead of `"path"`).
- `PathExtension.cs` (`Path.DirectorySeparatorChar` → `System.IO.Path.DirectorySeparatorChar`).
- Test aliases updated: `PLang.Tests/GlobalUsings.cs` re-points `FileSystem`/`PLangFileSystem`.
- ~50 references swept across `PLang/`, `PLang.Tests/`, `PlangConsole/`, `PLang.Generators/`.

### Stage 2 — `path` abstract + FilePath + Scheme registry (structural done)
- `path/this.cs`: `public abstract partial class @this`. Constructor `protected`.
  `_absolutePath` `protected`. Static `Resolve` kept for the type-mapper's
  scalar-PlangType check; delegates to `Scheme.From`.
- `path/this.Operations.cs`: abstract verb declarations (ReadText/WriteText/.../Delete) +
  virtual `CopyTo`/`MoveTo` with cross-scheme defaults (ReadBytes→WriteBytes). `AuthGate`
  helper stays here (`protected`).
- `path/this.Authorize.cs`: helpers (`BuildRequest`, `IsInRoot`, `SignAndStore`) made
  `protected` so FilePath's `BundledTransfer` can call them.
- `path/file/this.cs` (new): `FilePath : Path`, `[PathScheme("file")]`. Constructor with
  the existing absolutePath+context+content+source signature. `Resolve` static factory
  (the FS-specific path-resolution logic — relative-to-goal, ValidatePath, etc.).
- `path/file/this.Operations.cs` (new): FilePath's verb overrides — the existing
  `System.IO.File.X` impls moved from the base verbatim. Same-scheme `MoveTo`/`CopyTo`
  override the base's naive default with `System.IO.File.Move`/`Copy` and the
  bundled-consent prompt.
- `path/scheme/this.cs` (new): per-App registry. `ConcurrentDictionary<string, Func<string, Context, Path>>`,
  case-insensitive scheme matching, `From(raw, context)` parses scheme and dispatches,
  unknown schemes throw `SchemeNotRegistered`.
- `app/types/this.cs`: exposes `Scheme { get; } = new()` next to `Choices`.
- `app/this.cs`: registers `"file"` factory in the App constructor.
- `app/types/Conversion.cs`: new branch — when target type is assignable to
  `path.@this`, route through `context.App.Types.Scheme.From(raw, context)`. Catches
  `SchemeNotRegistered` and returns a clean `data.@this.Fail` shape.
- Construction-site sweep: `new types.path.@this(...)` → `new types.path.file.@this(...)`
  in `file/code/Default.cs`, `GetGoalsTests.cs`, etc.

### Stage 4 — `[PathScheme]` attribute (done)
- `PLang/app/types/path/PathSchemeAttribute.cs`: `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]`,
  one `Scheme` string property + single-string ctor.
- Applied to `FilePath` as `[PathScheme("file")]`. Documentation-only — built-ins are
  still registered explicitly by name at App startup.

### Test bodies written
- `SchemeRegistryTests.cs` — 10 tests.
- `PathAbstractTests.cs` — 6 tests.
- `PathSchemeAttributeTests.cs` — 5 tests.
- Tests pass: 2875 green, 74 red. Reds are TDD placeholders for Stages 3, 5, 6, 7.

## What's still to do

- **Stage 3** — collapse file handlers to `Path.Value!.X()` one-liners, delete
  `IFile`/`DefaultFileProvider`/`[Code]`-partial mechanism on file handlers, migrate
  the ~50 non-action callers (Builder, App.Save, http module, etc.), delete the
  `path/Default/*` System.IO.Abstractions wrapper layer. Highest-risk stage.
- **Stage 5** — `HttpPath` (GET/POST/DELETE/HEAD), `[PathScheme("http")]/[PathScheme("https")]`,
  identity wiring, App-startup registration for `"http"`/`"https"`. "Let the server respond"
  error shaping.
- **Stage 6** — `HttpPath.Absolute` per-scheme canonical form (lowercased host,
  default-port stripped, sorted query, fragment stripped, path normalized).
- **Stage 7** — `PathSchemeContractTests<TFixture>` generic base + FilePath/HttpPath
  fixtures + HttpTestServer (HttpListener-based per test-designer's note).

## Key files modified or added

- Added: `PLang/app/types/path/file/this.cs`, `PLang/app/types/path/file/this.Operations.cs`,
  `PLang/app/types/path/scheme/this.cs`, `PLang/app/types/path/PathSchemeAttribute.cs`.
- Moved: `PLang/app/filesystem/**` → `PLang/app/types/path/**` (git-mv preserves history).
- Modified: `PLang/app/types/path/this.cs` (abstract), `this.Operations.cs` (abstract verbs),
  `this.Authorize.cs` (protected helpers), `PLang/app/types/this.cs` (Scheme accessor),
  `PLang/app/types/Conversion.cs` (Scheme.From routing), `PLang/app/types/Registry.cs`
  (abstract+[PlangType] indexed), `PLang/app/this.cs` (file registration),
  `PLang/app/GlobalUsings.cs` (no global Path alias — collides with System.IO.Path),
  `PLang/app/Utils/PathExtension.cs` (System.IO.Path.DirectorySeparatorChar).
- Test sweep: removed/repointed dozens of per-file `using Path = ...` aliases.

## Code example — handler shape (current, post-Stage-2; pre-Stage-3)

Before (with `using Path = ...` per-file alias):
```csharp
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<global::app.types.path.@this> Path { get; init; }
    public partial data.@this<bool> ResolveVariables { get; init; }
    [Code] public partial IFile Files { get; }

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        var result = Files.Read(this);
        // ... ResolveVariables ...
    }
}
```

After Stage 3 will be (per architect plan):
```csharp
public async Task<data.@this> Run()
{
    var read = await Path.Value!.ReadText();
    // ResolveVariables post-processing only — no Authorize, no Files.
}
```

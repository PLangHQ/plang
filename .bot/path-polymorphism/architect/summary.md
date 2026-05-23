# Path polymorphism — architect summary

PLang's `path` becomes scheme-polymorphic: `FilePath`, `HttpPath`, future `S3Path`/`GitPath`/etc. each implement the same verb surface (Read/Write/Delete/Stat/List/Copy/Move). File action handlers degenerate to one-liners over `Path.X()`. `IFile`/`DefaultFileProvider` die. The whole `app.filesystem` namespace moves under `app.types.path/` to stop the misnomer ("filesystem" can't house http:// and s3://).

Closes codeanalyzer v2 finding #1 on `filesystem-permission` (handler-layer authorize copy-paste), which was deliberately deferred there.

Source of the design: [`Documentation/v0.2/path-polymorphism-plan.md`](../../../Documentation/v0.2/path-polymorphism-plan.md). This summary captures the answers to that doc's open questions plus the staging.

## Merge-state assumptions

This plan assumes the post-runtime2 codebase state (everything is lowercase: `app/`, `app.filesystem`, `app.types`, etc.; `Data` is referenced as `data.@this`; action handlers implement `app.modules.IContext`). Pre-merge references in the source design doc (`App.FileSystem`, `Data<Path>`, `IClass`) translate accordingly.

In the merge state, `app/types/` only contains `choices/` plus the existing registry partials (`this.cs`, `Registry.cs`, `Conversion.cs`). Permission, Verb, and Path are still at `app.filesystem.*`. Moving them under `app.types.path/` is part of this branch's stage 1 work.

## Stages

Stage files were drafted earlier and removed pending sign-off. They'll be re-drafted after this summary settles.

| # | Owns |
|---|------|
| 1 | Namespace move: `app.filesystem/` → `app.types/path/`. Includes `permission/` and `verb/`. Rename sweep. Zero logic change. |
| 2 | `path` becomes abstract; `FilePath` holds today's `path.Operations` impl. `app.types.path.scheme/this.cs` is the per-App registry. PLang `path` type-mapper calls `app.Types.Path.Scheme.From(raw)`. |
| 3 | Handler one-liners; `IFile` + `DefaultFileProvider` + `[Code]`-partial mechanism on file handlers deleted; ~50 non-action callers migrated; surface shape tests flip. |
| 4 | `[PathScheme]` attribute (marker only — defined here, consumed by future `code.load`). |
| 5 | `HttpPath` lands. Identity wired. App startup registers http/https. |
| 6 | `path.Absolute` per-scheme canonical form. `HttpPath.Absolute` = scheme + lowercased host + normalized port + path + sorted query. Authorize prompt unchanged. |
| 7 | `PathSchemeContractTests<T>` generic base. Verb round-trip + Permission gate suite applied to every scheme. |

Stages 1–3 are Phase 1 (closes codeanalyzer v2 #1, file:// only). Stages 4–7 are Phase 2 (polymorphism proven with a second scheme).

## Cross-cutting design decisions

### Namespace — `app.types.path/`

`app.filesystem` is renamed and folded under `app.types/path/`. `app.types/` already exists at `PLang/app/types/` as the PLang-name ↔ CLR-type registry (`this.cs`, `Registry.cs`, `Conversion.cs`, `choices/`). Path becomes a built-in type registered there alongside any future complex types.

Final layout:

```
app/types/
  this.cs                       — existing registry
  Registry.cs                   — existing partial
  Conversion.cs                 — existing
  choices/this.cs               — existing
  path/
    this.cs                     — abstract path (@this convention; see below)
    this.Operations.cs          — partial (thinned; cross-scheme defaults like CopyTo/MoveTo stay here)
    this.Authorize.cs           — Permission gate (scheme-agnostic, stays on the base)
    permission/this.cs          — moved from app.filesystem.permission
    permission/verb/this.cs     — moved from app.filesystem.permission.verb
    permission/verb/Read.cs     — moved
    permission/verb/Write.cs    — moved
    permission/verb/Delete.cs   — moved
    scheme/this.cs              — registry (new)
    file/this.cs                — FilePath  (absorbs DefaultFileProvider body)
    http/this.cs                — HttpPath  (Phase 2)
```

### `path` converts to `@this` convention

**Current state:** `app.filesystem.path` is class `path` in namespace `app.filesystem` — class-named-after-namespace, *not* `@this`-in-folder. Inconsistent with siblings (`permission.@this`, `verb.@this`).

**Target state:** `app.types.path.@this` — `path` is now its own folder containing `this.cs` (the class, named `@this`). Consumers use the global alias `Path` (added to `app/GlobalUsings.cs` under the existing "FileSystem types" stub which is empty today).

After the rename:

```csharp
// PLang/app/types/path/this.cs
namespace app.types.path;

[PlangType("path", Example = "/some/file.json")]
public abstract partial class @this : modules.IContext
{
    public abstract string Raw { get; }
    public abstract string Absolute { get; }
    public abstract string Scheme { get; }
    public abstract Task<data.@this> ReadText();
    // ... rest of verb surface ...
}
```

```csharp
// PLang/app/GlobalUsings.cs — fill in the FileSystem stub
global using Path = app.types.path.@this;
global using FilePath = app.types.path.file.@this;
global using HttpPath = app.types.path.http.@this;   // Phase 2
```

Same FQN doubled-name cost we already accept (`app.types.path.permission.permission.@this` ≈ four "permission"s when fully qualified — most call sites never see it because of the global alias).

### Scheme registry — per-App, mutable, OBP

`app/types/path/scheme/this.cs`:

```csharp
namespace app.types.path.scheme;

public sealed class @this
{
    private readonly ConcurrentDictionary<string, Func<string, Path>> _factories
        = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string scheme, Func<string, Path> factory)
        => _factories[scheme] = factory;

    public Path From(string raw) { /* parse scheme, dispatch */ }
}
```

OBP `@this`, per-App instance (not static). Internal `ConcurrentDictionary` — lock-free reads, atomic writes.

Populated at App start with built-in defaults (no source-gen):

```csharp
Scheme.Register("file",  raw => new FilePath(raw));
Scheme.Register("http",  raw => new HttpPath(raw));    // Phase 2
Scheme.Register("https", raw => new HttpPath(raw));    // Phase 2
```

`code.load "s3.dll"` (out of scope on this branch) plugs into `Register` after reflecting the assembly for `[PathScheme]` markers.

### `Path.From` is not static — type-mapper uses the registry

There's no static `Path.From(string)`. The PLang `path` → CLR `Path` conversion happens in `app.types.Conversion` (already takes context), which calls `context.App.Types.Scheme.From(raw)`. Construction dispatches via the per-App registry. Polymorphism is invisible to action handlers — they receive a `data.@this<Path>` that's already the right subclass.

### `[PathScheme]` is a marker, not a discovery mechanism

`[PathScheme("https")]` is class-level, repeatable (`HttpPath` carries both `http` and `https`). On this branch it's *defined* (stage 4) but only future `code.load` will consume it. Built-ins are registered by explicit name at App startup — no attribute-driven discovery, no generator pass.

### Verb shape is unchanged

`app.types.path.permission.verb/` (post-rename) keeps the same Read/Write/Delete shape with current option records. Verb *names* are universal (scheme-agnostic — Permission grants in human terms). Verb *options* (`Recursive`, `Overwrite`, `Metadata`) are filesystem-shaped; schemes for which they don't apply ignore them. `HttpPath.Read(Recursive=true)` is a no-op — documented as such, no error. Cleaner shape (e.g., moving `Recursive` to Path-matching via wildcards) is a future refactor, not on this branch.

### IFile + DefaultFileProvider + `[Code]`-partial mechanism die

Three-layer call path collapses to two:

```
Before:  Handler → IFile (DefaultFileProvider via [Code] partial) → PLangFileSystem → System.IO.File
After:   Handler → Path.X (FilePath impl)                          → System.IO.File
```

Today every file action handler carries this shape:

```csharp
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<filesystem.path> Path { get; init; }
    public partial data.@this<bool> ResolveVariables { get; init; }

    [Code] public partial IFile Files { get; }       // ← generator-emitted provider injection

    public async Task<data.@this> Run()
    {
        var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
        if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
        var result = Files.Read(this);               // ← legacy provider call
        // ... %var% resolution ...
    }
}
```

Three things go away in stage 3:

1. **`IFile` interface** at `PLang/app/modules/file/code/IFile.cs` — deleted.
2. **`DefaultFileProvider`** at `PLang/app/modules/file/code/Default.cs` (or wherever the impl lives) — deleted.
3. **The `[Code] public partial IFile Files { get; }` line** on every file handler — deleted (along with the `Authorize` preamble, which moves into Path).

The handler shape after:

```csharp
[Action("read")]
public partial class Read : IContext
{
    public partial data.@this<Path> Path { get; init; }
    public partial data.@this<bool> ResolveVariables { get; init; }

    public async Task<data.@this> Run()
    {
        var read = await Path.Value!.ReadText();
        if (!read.Success || read.Type?.ClrType.Exit() == true) return read;
        if (ResolveVariables.Value && read.Value is string content)
            return new data.@this(read.Name, Context.Variables.Resolve(content, skipInfrastructure: true), read.Type);
        return read;
    }
}
```

Authorize moves inside `Path.ReadText()` and friends (on `FilePath`/`HttpPath`) — handlers no longer call it explicitly. That's the codeanalyzer v2 #1 fix: no copy-paste preamble per handler.

The lowest layer (`app/filesystem/Default/PLangFileSystem.cs` and friends) is absorbed into `app.types.path.file.@this` — its concerns (path validation, normalization) move to `path.From` and the `FilePath` verb impls, not maintained as a separate abstraction layer. The System.IO.Abstractions-style wrappers go away with it.

Stage 3 is the gnarly one. The ~50 non-action `IFile` callers (Builder, App.Save, http/code/Default, etc.) migrate in the same stage — no half-migrated intermediate state where both surfaces coexist.

### Single PLang `path` type

The PLang `path` type stays unified. The builder and the LLM never choose between `path`/`url`/`s3-path` — that cognitive load erodes builder accuracy. User writes `read %url%`, builder emits `Path = "%url%"`, scheme dispatch happens at C# construction. Polymorphism is a C#-side concern, invisible above.

### No instance caching at the factory

`Scheme.From(raw)` constructs a fresh Path each call. `FilePath` is cheap. `HttpPath` doesn't keep cross-call state — the HttpClient it talks to is `static readonly` inside the subclass (immutable after init, multi-App-safe). Identity caching would invite lifecycle questions (when does an HttpPath die? per-actor? per-request?) we don't want.

### Permission stays scheme-agnostic, but `Absolute` becomes per-scheme

`Permission` record key is unchanged in shape: `(AppId, Actor, path.Absolute, Verb)`. What changes is `Absolute` — each scheme defines its own canonical-form:

- `FilePath.Absolute` = OS-normalized path (today's behaviour).
- `HttpPath.Absolute` = scheme + lowercased host + normalized port + path + sorted query.

The Authorize prompt reads naturally for both:

```
Allow worker to read /home/user/data.json? (y/n/a)
Allow worker to read https://api.example.com/users.json? (y/n/a)
```

Permission code itself doesn't branch on scheme. It calls `path.Absolute`, gets a string, matches against grants.

### Credentials are scheme-internal, not base-class

`HttpPath` uses PLang's built-in signing identity for requests where identity matters. Developers needing custom config (bearer tokens, mTLS, region-specific S3 creds) reach into `Settings` from inside their own scheme handler. The Path base class doesn't try to abstract credential shapes — deliberately scoped down.

### Contract tests — one base, all schemes

`PathSchemeContractTests<T>` is a generic test base. Each scheme provides a fixture (a way to mint a fresh writable Path); the base asserts:

- `WriteText(x)` → `ReadText() == x`
- `Exists` true after write, false after `Delete`
- `Stat.Length` matches bytes written
- `CopyTo(other).ReadText() == original.ReadText()`
- Unauthorized access fires the Permission gate

No "skip if unsupported" flags. The "let the server respond" rule means an HTTP 405 produces a `data.@this.Fail(405)` — the test asserts on that shape. Drives uniform failure semantics across schemes. FilePath fixture uses a temp dir; HttpPath fixture uses in-process Kestrel.

## What this branch does not do

- Doesn't design or implement `code.load` for runtime scheme loading. That's a separate module-action concern. `Scheme.Register` is the seam it'll plug into.
- Doesn't rework Verb options. `Recursive` stays a no-op on non-FS schemes.
- Doesn't add `S3Path`, `GitPath`, etc. — they live in their own modules per Phase 3 of the source doc.
- Doesn't touch the PLang `path` type's surface to programs. Same `%path%` works.

# Path Polymorphism Plan

**Status (2026-05-23):** Phase 1 + Phase 2 shipped on branch `path-polymorphism`. `Path` is abstract; `FilePath` (`PLang/app/types/path/file/`) and `HttpPath` (`PLang/app/types/path/http/`) are the two live schemes, registered via `[PathScheme("…")]` and discovered by the source generator. File action handlers are one-liners over `Path.Value!.X()`. `IBooleanResolvable` is wired through the condition/assert pipeline so `if %path% exists` (and `if %url% exists`) work without an explicit `check if … exists` step. User-facing docs: `docs/modules/file.md` *Paths can be URLs*, `docs/modules/condition.md` *Path truthiness*. Internal: `Documentation/v0.2/good_to_know.md` "Truthiness — `IBooleanResolvable`…". Phase 3 (S3, Git, …) remains open — drop a new `[PathScheme(...)]` class in and it lights up. The rest of this document is the original plan, preserved for design rationale.

---

Handoff to architect. **Target: a new branch (not `filesystem-permission`).** The `filesystem-permission` branch closes with codeanalyzer v2 finding #1 (handler-layer authorize copy-paste) intentionally deferred to this plan.

## Why now

Codeanalyzer v2 flagged that the file action handlers (`PLang/App/modules/file/{read,save,copy,move,delete,exists,list}.cs`) each re-implement the same two-line authorize preamble:

```csharp
var auth = await Path.Value!.Authorize(new Verb { Read = new ReadVerb() });
if (auth.Type?.ClrType.Exit() == true || !auth.Success) return auth;
return Files.Read(this);
```

`Path.Operations.cs` already has the `AuthGate` helper that absorbs this. The handlers don't use it — they call the legacy `Files.X(this)` (`IFile.Read`-era) surface instead. Codeanalyzer offered two fixes:

- **(a)** Promote `AuthGate` to public, handlers call it directly. Shaves one line per handler, two-floor architecture stays.
- **(b)** Handlers degenerate to one-liners on top of `Path.Operations`, the legacy `IFile.Read/Save/...` surface dies.

The file-comment on `Path.Operations` already advertises (b) ("handlers under `PLang/App/modules/file/*.cs` become thin shells, one-liner each"). But we're going further: **`Path.Operations` is just the `file://` implementation of a polymorphic `Path`.** Different schemes (`http://`, `s3://`, `git://`, ...) implement the same verb surface; handlers don't know which scheme they're talking to.

## Goal

A user writes:

```
- read %url%, write to %content%
```

`%url%` is `https://api.example.com/users.json`. The `file.read` handler is one line:

```csharp
public Task<Data> Run() => Path.Value!.ReadText();
```

No `if (url.StartsWith("http"))` branching. `Path.From("https://...")` returns an `HttpPath`; its `ReadText()` does a GET. Same code path, same `Data` shape, same `Permission` gate.

## Design

### Verb surface (lives on `Path` base)

| Verb | `file://` | `http(s)://` | `s3://` (future) | `git://` (future) |
|------|-----------|--------------|------------------|--------------------|
| `ReadText/ReadBytes` | `File.Read` | GET | `GetObject` | clone+read |
| `Exists` | `File.Exists` | HEAD 2xx | `HeadObject` | rev-parse |
| `Stat` | size+mtime | `Content-Length`+`Last-Modified` | `HeadObject` metadata | commit info |
| `WriteText/WriteBytes/Append/Save` | `File.Write` | POST (let server complain on 405 — same as disk-full) | `PutObject` | commit+push |
| `Delete` | `unlink` | DELETE | `DeleteObject` | revert+commit |
| `List` | dir entries | server-defined (autoindex / not-supported) | `ListObjectsV2` | `ls-tree` |
| `CopyTo(dest)` | base default: `ReadBytes()` → `dest.WriteBytes(...)`; same-scheme overrides may be faster | | | |
| `MoveTo(dest)` | base default: `CopyTo(dest)` + `Delete()` | | | |

`Save` on HTTP **is POST, not "not supported."** Let the server respond. A 405 surfaces as `Data.Fail(405)` the same way a write to a read-only filesystem does — PLang programs handle it via `on error`. Don't refuse what the program asked for; attempt and surface the response.

### Construction: scheme registry

```csharp
Path.From("/home/x.txt")           → FilePath
Path.From("./relative.txt")        → FilePath  (no scheme = file)
Path.From("file:///home/x.txt")    → FilePath
Path.From("https://api.com/users") → HttpPath
Path.From("s3://bucket/key")       → (lookup miss → ServiceError "scheme s3 not registered")
```

Registry is built by the source generator scanning for `[PathScheme("…")]` attributes — same pattern as `[Action]`:

```csharp
[PathScheme("https")]
[PathScheme("http")]
public sealed class HttpPath : Path
{
    public override Task<Data<string>> ReadText()  => /* GET */;
    public override Task<Data>         WriteText(...) => /* POST */;
    public override Task<Data>         Delete()    => /* DELETE */;
    public override Task<Data<StatInfo>> Stat()    => /* HEAD */;
    // CopyTo/MoveTo/Exists/Append/Save: inherit base defaults
}
```

Drop the file in, rebuild, scheme is live. No DI registration, no plugin loader, no `appsettings.json` entry — same ergonomics as adding an action handler.

### Permission stays scheme-agnostic

Permission record key is `(AppId, Actor, Path.Absolute, Verb)`. `Absolute` becomes scheme-defined ("canonical-form-per-scheme") instead of OS-defined. Examples:

- `FilePath.Absolute` = OS-normalized path (today's behaviour).
- `HttpPath.Absolute` = scheme + host (lowercased) + default-port stripped + normalized path + sorted query.

The Authorize prompt reads naturally for any scheme:

```
Allow worker to read https://api.example.com/users.json? (y/n/a)
Allow worker to write s3://my-bucket/output.json? (y/n/a)
```

No per-scheme code in the Permission system itself.

### Credentials are NOT a base-class concern

`HttpPath` uses **PLang identity** (built-in signing) for requests where identity matters — same identity that signs Permission grants. If a developer needs custom config (bearer tokens, mTLS, region-specific S3 creds), they reach into `Settings` from inside their own scheme handler. The base class doesn't try to abstract over credential shapes. This is deliberately scoped down — Ingi's call.

## Phasing

**Phase 1 — Polymorphic Path, file:// only (closes codeanalyzer v2 #1):**

1. `Path` becomes abstract with virtual verb methods.
2. `FilePath : Path` holds today's `Path.Operations.cs` implementation.
3. `Path.From(string)` factory routes to `FilePath` for bare paths and `file://`.
4. File action handlers degenerate to one-liners over `Path.X()`:
   ```csharp
   [Action("read")] public sealed class Read : IClass
   {
       public required Data<Path> Path { get; init; }
       public Task<Data> Run() => Path.Value!.ReadText();
   }
   ```
5. Legacy `IFile.Read/Save/Copy/Move/Delete/Exists/List` deleted. The ~50 non-action callers (Builder, App.Save, http/code/Default, etc.) migrated to `Path.From(...).X()`.
6. New shape test: `FileSystemSurfaceShapeTests` flips assertions that v1/v2 left documenting deferred work.

**Phase 2 — Scheme registry + `HttpPath`:**

1. `[PathScheme("…")]` attribute.
2. Source generator scans for it, emits a static dispatch table.
3. `HttpPath` lands as the second scheme — proves the abstraction.
4. Permission Authorize prompt works for `http(s)://` paths unchanged (the prompt text already reads as URL — see above).
5. Permission tests for `HttpPath`: in-root concept doesn't apply, so every HTTP access prompts on first encounter and persists per `a`.

**Phase 3 — Future schemes (community/internal as needed):**

`S3Path`, `GitPath`, etc. live in their own modules. Drop in, build, done.

## Open architect questions

1. **Where does `Path.From(string)` live in the type tree?** `App.FileSystem.Path` is already the partial class shape. Static factory on the same class? Or new `Path.Factory` type?
2. **Does `[PathScheme]` go on the scheme handler class, or on a `register()` method?** Class-level is simpler but couples one type to one set of schemes (which is fine — `HttpPath` covering both `http` and `https` is the common case).
3. **Caching:** does `Path.From(string)` cache instances per absolute string? `FilePath` is cheap to construct, `HttpPath` might want to share an `HttpClient` per host. Probably "no caching, scheme handler does its own pooling internally."
4. **Tests:** should there be a `PathSchemeContractTests<T>` generic base that every scheme handler runs through (ReadText must round-trip, Stat must populate Length, etc.)? Lower bar for adding schemes.
5. **PLang type system:** today `path` is a single `PlangType`. Does it stay a single type with polymorphic dispatch, or split into `path`, `url`, `s3-path` at the PLang level? Probably stay single — the LLM and the builder shouldn't have to learn N type names.

## What this branch (filesystem-permission) does and doesn't do

**Does:** closes codeanalyzer v2 #2 (the `ValidatePath` Linux case-comparison bug at `PLangFileSystem.cs:227`) by hoisting a `Path.RootComparison` helper used at both gate sites.

**Doesn't:** address v2 #1 (handler-layer authorize copy-paste). That is the Phase 1 above, on a new branch.

**Communicating to codeanalyzer:** the coder v3 summary on `filesystem-permission` calls out this deferral and points here. v2 #1's status flips from "regression" to "tracked on new branch" — same closure path as todos.md items.

## Communication

- This doc is the architect handoff.
- `Documentation/v0.2/todos.md` gets a one-line pointer back to this plan.
- The coder summary on `filesystem-permission` (v3 update) cites this plan as the deferral path for codeanalyzer v2 #1.

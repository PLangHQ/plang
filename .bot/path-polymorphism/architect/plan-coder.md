# Coder plan — path-polymorphism

This branch makes PLang's `path` scheme-polymorphic. Stages 1–3 close codeanalyzer v2 finding #1 from `filesystem-permission` (handler-layer authorize copy-paste) by collapsing the file-handler call path through a polymorphic `path`. Stages 4–7 prove the polymorphism with `HttpPath` and add the contract-test scaffold so future schemes (S3, Git, etc.) drop in cleanly.

## Read first

1. **`summary.md`** — cross-cutting design decisions. 5-minute read.
2. **`Documentation/v0.2/path-polymorphism-plan.md`** — the source design doc. Pre-runtime2 naming there (`App.FileSystem`, `Data<Path>`, `IClass`) translates to the lowercase post-merge state (`app.filesystem`, `data.@this<path>`, `IContext`).
3. **`Documentation/v0.2/architecture.md`** and **`Documentation/v0.2/good_to_know.md`** — auto-loaded into CLAUDE memory, but worth a fresh read.
4. The stage file you're about to touch (re-drafted after summary signoff).

## Where to start

**Stage 1 first.** Pure rename, zero logic change, biggest blast radius (~42+ files reference the namespace). Land it clean before any other work — every subsequent stage references the new namespace. Half a day.

After stage 1:

- **Stage 2** — `path` abstract + Scheme registry. Foundation for everything else. Builds on stage 1's namespace.
- **Stages 3, 4** — parallel possible. Stage 3 collapses handlers + kills IFile (large, mechanical). Stage 4 defines `[PathScheme]` attribute (tiny, one file).
- **Stage 5** — HttpPath. Needs stages 2 + 4.
- **Stage 6** — Permission per-scheme `Absolute`. Needs stage 5.
- **Stage 7** — Contract test framework. Lands last; exercises stages 2, 5, 6.

## Stage-by-stage

| # | Owns | Depends on | Approx size |
|---|------|------------|-------------|
| 1 | `app.filesystem/` → `app.types/path/` rename sweep (includes `permission/`, `verb/`). Converts `path` from class-named-after-namespace to `@this` convention | — | Large by *count*, trivial per-file |
| 2 | `path` abstract, `FilePath`, `scheme/this.cs` registry, App-start wiring, PLang type-mapper update | Stage 1 | Medium |
| 3 | Handler one-liners; `IFile`+`DefaultFileProvider`+`[Code]`-partial mechanism deleted; ~50 non-action callers migrated; surface shape tests flipped | Stage 2 | Large by volume, mechanical |
| 4 | `[PathScheme]` attribute (marker only) | Stage 1 | Tiny |
| 5 | `HttpPath` impl; identity wired; http/https registered at startup | Stages 2, 4 | Medium |
| 6 | `path.Absolute` per-scheme; FilePath unchanged, HttpPath canonical-form | Stage 5 | Small-medium |
| 7 | `PathSchemeContractTests<T>` generic base; FilePath + HttpPath fixtures | Stages 5, 6 | Medium |

## Conventions to know

- **Casing:** post-runtime2, folders and namespaces are lowercase (`app/`, `app.types`, `app.filesystem`). Global aliases (e.g. `Path`, `FilePath`) keep call-site identifiers PascalCase.
- **OBP `@this` convention:** singular folder, `@this` is the type. `path/this.cs` IS the abstract `path`. `path/file/this.cs` IS FilePath. `path/scheme/this.cs` IS the scheme registry. See `CLAUDE.md` "OBP Shape Smells."
- **Stage 1 converts `path` to `@this` convention.** Today: `app.filesystem.path` is class `path` at namespace `app.filesystem`. After: `app.types.path.@this`. Partial files use `this.Operations.cs`, `this.Authorize.cs` form (replacing `path.Operations.cs`, `path.Authorize.cs`).
- **Doubled-name FQN:** accepted cost — fully qualified `app.types.path.permission.permission.@this`. Global aliases keep call-site code unaffected.
- **`[PathScheme]` is a marker, not discovered at startup.** Built-ins are explicitly registered by name. Attribute is for future `code.load` only — define it, don't consume it yet.
- **Single PLang `path` type.** Don't split into `url`/`s3-path` PLang-side. Polymorphism is C#-only.
- **No `Console.*` writes in production C#.** Channels are the output path. See `CLAUDE.md` "Console.* Is Banned in Production C#."
- **No static mutable state for the Scheme registry.** It's a per-App OBP `@this`. `ConcurrentDictionary` for the internal map.
- **HttpClient pooling:** `static readonly` inside `HttpPath` is fine (immutable after init, multi-App-safe). No per-instance caching at the factory level.
- **`Data` is referenced as `data.@this`** — there is no global `Data` alias visible in handler code today. Use `data.@this`, `data.@this<T>`, `data.@this.Fail(...)` consistently.
- **Action handlers implement `IContext`** (from `app.modules`), not `IClass`. Lazy params use `public partial data.@this<T> Property { get; init; }` — the generator emits the body.

## Build / test

Always rebuild from clean before claiming a `plang --test` result — stale `PlangConsole/bin` binaries produce phantom failures. See `CLAUDE.md` "Stale-binary trap."

```bash
# C# tests (recompiles in place)
dotnet run --project PLang.Tests

# Clean PLang test run
rm -rf PlangConsole/bin PlangConsole/obj PLang/bin PLang/obj \
       PLang.Tests/bin PLang.Tests/obj \
       PLang.Generators/bin PLang.Generators/obj
dotnet build PlangConsole
cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test
```

## Migration paths to watch

- **Stage 1** touches every `using app.filesystem;` and every `app.filesystem.X` reference. Run a global rename, then build; if compilation passes, the rename is mechanically done. No behavioral change to verify. The `path` → `@this` conversion is a separate sub-step inside stage 1 (rename `path.cs` → `this.cs`, rewrite `class path` → `class @this`, update partials, add global alias).
- **Stage 3** deletes `IFile`, `DefaultFileProvider`, and the `[Code] public partial IFile Files` line on every file handler. Every non-action caller must migrate to `app.Types.Path.Scheme.From(raw).X()` (or accept a `Path` directly if it already had one). The migration is mechanical but volume-heavy. Don't introduce a temporary shim — the goal is no half-migrated state.
- **Stage 5** introduces HTTP I/O inside Path verbs. Use the existing HTTP client surface the codebase already uses (don't add a new dependency). Identity comes from PLang's built-in signing; reuse the existing identity API.
- **Stage 6** changes Permission keying for HttpPath only. FilePath grants in existing sqlite stores stay valid (Absolute formula unchanged). HttpPath has no prior grants. Migration cost: zero.

## Out of scope on this branch

- `code.load "s3.dll"` and runtime scheme registration via PLang — separate module-action design.
- Reshaping Verb option records (`Recursive` → Path-matching wildcards). Out-of-scope structural change. `Recursive` stays a no-op on non-FS schemes for now.
- `S3Path`, `GitPath`, any third scheme. Phase 3 of the source doc.
- Programs needing per-host bearer tokens, mTLS, etc. — they reach into Settings from inside their own scheme handler. The base class doesn't abstract over credentials.

# Test designer plan — path-polymorphism

This branch makes `path` scheme-polymorphic. The test surface is dominated by the *contract* tests (Stage 7) — the same suite runs against every scheme, so getting that base right pays out for every future scheme. Stage 3 has a real volume of mechanical assertions; stage 6 has subtle canonical-form edge cases.

## Read first

1. **`summary.md`** — cross-cutting design decisions. 5-minute read.
2. **`Documentation/v0.2/path-polymorphism-plan.md`** — the source design doc. (Pre-runtime2 casing — translate to lowercase.)
3. Each stage's "Tests" section as you size that stage's work.

## Conventions to know

- **Casing:** `app.*` namespaces lowercase. Global aliases (`Path`, `FilePath`, `HttpPath`) keep call-site identifiers PascalCase.
- **`Data` is `data.@this`** — there is no global `Data` alias today. Test code uses `data.@this`, `data.@this<T>`, `data.@this.Fail(...)`.
- **Handler shape:** action handlers implement `app.modules.IContext`; lazy params are `public partial data.@this<T> Prop { get; init; }`.
- **One concern per test file** when the file grows past ~6 tests.
- **C# tests recompile in place** via `dotnet run --project PLang.Tests`. No stale-binary trap.
- **PLang `--test` tests** need a clean rebuild before claiming a result — see `CLAUDE.md` "Stale-binary trap."
- **Surface tests** (no production reference to X) — use reflection, not text search, so they don't false-positive on a comment that mentions the dead symbol.

## Where to start

**Stage 2's tests** can be designed as soon as stage 1 (namespace rename + `@this` conversion) lands. Pure-types + factory dispatch coverage, no mocks needed beyond a `TestScheme` registered into the registry.

Stages 3 and 4 tests can be designed in parallel with stage 2:
- Stage 3 is mostly "did the migration finish?" surface assertions — reflection-based assertions that no production reference to dead symbols (`IFile`, `DefaultFileProvider`, old namespace) remains.
- Stage 4 is one type — one tiny test that the attribute carries through reflection correctly.

**Stage 7 is the centerpiece.** Design the contract base before stage 5 (HttpPath) lands — that way HttpPath ships with the contract suite already running against it on day one. Stage 5's own tests cover HttpPath-specific behavior the contract base can't (auth header, 405 mapping, redirect handling).

## Stage-by-stage test plan

| Stage | Test kind | Where | Notes |
|-------|-----------|-------|-------|
| 1 | Build-only verification | n/a | Compilation passes = stage done. Plus one survey assertion that `app.filesystem` namespace contains zero types. |
| 2 | C# unit | `PLang.Tests/app/types/PathTests/` | `path` abstract dispatch, FilePath round-trip, Scheme registry Register/From, type-mapper integration. |
| 3 | C# survey + plang `--test` | `PLang.Tests/app/types/PathTests/`, `Tests/app/modules/file/` (kept green) | Assert no `IFile`/`DefaultFileProvider`/`[Code] partial IFile` references remain. Existing file-handler test files keep passing unchanged. |
| 4 | C# unit | `PLang.Tests/app/types/PathTests/PathSchemeAttributeTests.cs` | Reflection-finds-attribute, multiple instances on same class. |
| 5 | C# unit + integration | `PLang.Tests/app/types/PathTests/HttpPathTests/` | In-process Kestrel; GET/POST/DELETE/HEAD round-trips; 405 → `data.@this.Fail` shape. |
| 6 | C# unit | `PLang.Tests/app/types/PathTests/PermissionAbsoluteTests/` | Canonical-form: host casing, default-port stripping, query sorting, path normalization. |
| 7 | Generic test base | `PLang.Tests/app/types/PathTests/PathSchemeContractTests.cs` | Generic `<TFixture>` runner. FilePath + HttpPath fixtures both run the same suite. |

(Folder casing in `PLang.Tests/` follows the App-side convention — lowercase `app/types/` mirrors `PLang/app/types/`. If existing test folders are mixed case, follow whatever the surrounding `PLang.Tests/` tree uses — consistency with neighbours wins.)

## Test surfaces per stage

### Stage 1 — namespace move + `@this` conversion

No new behavioral tests. Pass conditions:

- Existing test suite passes unchanged.
- Survey: `app.filesystem` namespace contains zero loaded types in the App assembly.
- Survey: `app.types.path.@this` is reachable via the global `Path` alias.

### Stage 2 — `path` abstract + Scheme registry

C# unit, minimal mocks (a `TestScheme` registered into the registry):

- **`Scheme.Register`** — registering a new scheme, then `From("test://x")` returns the right subclass; double-registering replaces.
- **`Scheme.From` bare paths** — `/home/x.txt`, `./relative.txt`, `C:\Users\x` all route to FilePath without a scheme prefix.
- **`Scheme.From` schemed paths** — `file:///home/x.txt` → FilePath, `https://api.com/x` → HttpPath (once stage 5 lands).
- **`Scheme.From` unknown scheme** — returns a service-error `data.@this`, doesn't throw. `"s3://bucket/key"` with s3 unregistered fails cleanly.
- **PLang type-mapper integration** — a step parameter typed `path` with value `"%url%"` resolves into the correct Path subclass at parameter resolution time.
- **FilePath verb round-trip** — `WriteText(x) → ReadText() == x`. (This is essentially the existing PLangFileSystem test coverage, migrated to call `Path.WriteText`/`Path.ReadText` directly.)
- **Multi-App isolation** — two App instances with different scheme registrations don't see each other's registrations.

### Stage 3 — handler one-liners + IFile death

Surface tests (no logic to re-test, just migration completeness):

- **No production reference to `IFile`** — reflection-based type search in the App assembly returns zero hits outside test code.
- **No production reference to `DefaultFileProvider`** — same.
- **No production reference to `PLangFileSystem`, `PLangFile`, etc.** — the System.IO.Abstractions-style wrappers in `app/filesystem/Default/` are gone.
- **No `using app.filesystem`** anywhere in the codebase (covered by stage 1's sweep but worth re-asserting after stage 3 in case the migration brought references back).
- **No `[Code]` attribute with `IFile`** as the partial property type — the source-generator-driven provider injection is gone for file handlers.
- **File-handler `.pr` tests stay green** — `Tests/app/modules/file/` runs unchanged. Behavior of the file module is unchanged from the PLang program perspective.
- **Handler shape is one-line** — `read.cs`, `save.cs`, etc. contain only a one-line `Run` body over `Path.Value!.X()` (possibly plus the `ResolveVariables` post-step for `read`). Reviewable by snapshot test.

### Stage 4 — `[PathScheme]` attribute

One small file. Assert:

- Attribute is `AttributeUsage(AttributeTargets.Class, AllowMultiple = true)`.
- Reflecting a class with `[PathScheme("a")] [PathScheme("b")]` yields both schemes.
- Attribute has the right shape for future reflection-based registration (one `Scheme` string property).

### Stage 5 — HttpPath

In-process Kestrel test server. Cover:

- **GET happy path** — `new HttpPath("http://localhost:NNN/x").ReadText()` returns the body. Status 200, content-type aware.
- **GET error** — 404 → `data.@this.Fail` with status in the error.
- **POST happy path** — `WriteText("body")` posts the body, 200 → `data.@this.Ok`.
- **POST refused** — server returns 405 → `data.@this.Fail(405)` (the "let the server respond" rule).
- **DELETE** — DELETE verb, 204 → `data.@this.Ok`.
- **HEAD/Stat** — Content-Length and Last-Modified populated.
- **Identity** — request carries PLang signing identity headers; absence on a path that requires identity → server returns 401, captured as `data.@this.Fail`.
- **No identity caching across calls** — calling twice produces two independent requests (no per-instance state).
- **HttpClient pooling** — `static readonly` HttpClient is not recreated per `HttpPath` instance (assert by instance count or by behavior under load).

### Stage 6 — Permission per-scheme `Absolute`

Pure canonical-form unit tests:

- **FilePath** — `Absolute` unchanged from today. Existing tests stay green.
- **HttpPath canonical-form rules** — design and pin each:
  - `HTTP://Example.COM/foo` → `http://example.com/foo` (lowercased scheme + host)
  - `https://example.com:443/foo` → `https://example.com/foo` (default port stripped)
  - `http://example.com:80/foo` → `http://example.com/foo` (default port stripped)
  - `https://example.com/a/../b` → `https://example.com/b` (path normalized)
  - `https://example.com/?b=2&a=1` → `https://example.com/?a=1&b=2` (query sorted)
  - `https://example.com/` and `https://example.com` → same Absolute (trailing-slash canonicalization — pin which way)
  - Fragments stripped (or kept — pin and document)
- **Permission match across schemes** — a FilePath grant doesn't match an HttpPath request (different schemes, different Absolute prefixes).

### Stage 7 — `PathSchemeContractTests<T>`

The contract base. Design carefully — every future scheme will run through this.

Required fixture surface for a scheme:

```csharp
public interface IPathSchemeFixture
{
    Task<Path> CreateFresh();          // mint a writable Path, scheme-specific
    Task Cleanup(Path p);              // tear down
    bool CanPerform(VerbName v);       // for scoping which tests run; NOT for skipping assertions
    string Scheme { get; }
}
```

(`CanPerform` is not for skipping — it's for asserting "scheme says no" returns `data.@this.Fail`, not throws.)

Contract assertions:

- **`WriteText(x).Then(ReadText()) == x`** — round-trip through bytes.
- **`Exists` lifecycle** — false → write → true → delete → false.
- **`Stat.Length` matches written bytes** — exact equality.
- **`CopyTo` round-trip** — `src.CopyTo(dst).Then(dst.ReadText()) == src.ReadText()`.
- **`CopyTo` cross-scheme** — `FilePath.CopyTo(HttpPath)` uses the base default (ReadBytes + WriteBytes); assert the bytes equal.
- **`MoveTo` is CopyTo + Delete** — base default; source no longer Exists.
- **Permission gate fires on unauthorized Read** — without a grant, `ReadText()` returns `data.@this.Fail` with PermissionDenied; never reaches the underlying I/O.
- **Permission gate fires on unauthorized Write** — same shape.
- **Failure shape uniform across schemes** — the `data.@this.Fail` returned by an unauth read has the same `Error.Type` across schemes.

FilePath fixture: temp dir. HttpPath fixture: in-process Kestrel.

## Out of scope on this branch

- No tests for `code.load`-driven scheme registration — that surface lands separately.
- No tests for `S3Path`, `GitPath`, etc. — they ship with their own modules.
- No reshaping of Verb option records — `Recursive` is just ignored on HttpPath, not removed.

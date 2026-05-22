# Test designer plan — path-polymorphism (v1)

## Task

Translate the architect's 7-stage design (`.bot/path-polymorphism/architect/`) into test
suites — C# TUnit tests and PLang test goals — that define the behavioral contract for the
coder. Tests are the spec: signatures + intent comments only, bodies are `Assert.Fail("Not
implemented")` (C#) / `- throw "not implemented"` (PLang).

## Design summary (what the branch does)

`path` becomes scheme-polymorphic. The `app.filesystem` namespace moves under
`app.types/path/`. `path` turns abstract; `FilePath` holds today's impl; `HttpPath` is a
second scheme. A per-App `Scheme` registry dispatches `raw → Path` subclass. File action
handlers degenerate to one-liners over `Path.Value!.X()`; `IFile`/`DefaultFileProvider` die.
Authorize moves inside each scheme's verb impl. A generic `PathSchemeContractTests<TFixture>`
runs the same verb/permission suite against every scheme.

## Conventions established (from codebase survey)

- **Test home:** `PLang.Tests/App/Types/Path/` — PascalCase folder per CLAUDE.md ("Test
  folder names under `PLang.Tests/App/` stay PascalCase"). Mirrors `PLang/app/types/path/`.
  The existing `PLang.Tests/App/FileSystem/` tree is the *current* home of path/permission
  tests; migrating those is the coder's stage-1/3 job, not mine. I write new files only.
- **`Data` IS aliased in PLang.Tests** (`global using Data = global::app.data.@this;`) —
  unlike the PLang project. Test code may use `Data`; I stay explicit with `global::app.data.@this`
  where the architect sketches do.
- **TUnit fluent:** `await Assert.That(x).IsEqualTo(y)`, `.IsNotNull()`, `.IsTrue()`,
  `.IsTypeOf<T>()`. `[Test] public async Task`.
- **App construction:** `new global::app.@this(tempDir)` or `(tempDir, fileSystem: fs)`.
  Context = `app.User.Context`. Pattern from `FileHandlerTests`/`DefaultHttpProviderTests`.
- **Canned channel stub:** `CannedAnswerChannel` pattern from `PathAuthorizeTests` answers
  Authorize prompts with `y`/`n`/`a`.
- **HttpPath test server:** no Kestrel/TestServer in PLang.Tests today, and the csproj has
  no ASP.NET reference. `System.Net.HttpListener` (in-box, no package) is the lighter
  choice for the in-process server. Flagged as a coder/csproj decision — see Open Questions.

## Batch breakdown

| Batch | Stage(s) | Area | ~Tests | Files |
|-------|----------|------|--------|-------|
| 1 | 1, 4 | Namespace-move surveys + `[PathScheme]` attribute | 9 | `NamespaceMoveTests.cs`, `PathSchemeAttributeTests.cs` |
| 2 | 2 | Scheme registry + abstract `path` shape | 11 | `SchemeRegistryTests.cs`, `PathAbstractTests.cs` |
| 3 | 2 | FilePath verb round-trip + PLang type-mapper dispatch | 9 | `FilePathVerbTests.cs`, `PathTypeMapperTests.cs` |
| 4 | 3 | Handler one-liners + `IFile` death surveys + PLang file behavior | 9 | `HandlerShapeTests.cs`, `Tests/.../PathScheme.test.goal` |
| 5 | 5 | HttpPath behavior (in-process server) | 11 | `Http/HttpPathTests.cs`, `Http/HttpTestServer.cs` |
| 6 | 6 | Permission per-scheme `Absolute` canonical-form | 9 | `AbsoluteCanonicalFormTests.cs` |
| 7 | 7 | `PathSchemeContractTests<T>` generic base + fixtures | 10 | `Contract/IPathSchemeFixture.cs`, `Contract/PathSchemeContractTests.cs`, `Contract/{FilePath,HttpPath}Fixture.cs`, `Contract/*ContractTests.cs`, `Contract/CrossSchemeTests.cs` |

Total ≈ 68 C# tests + 1 PLang test goal file.

## Workflow

Per the test-designer character: present the plan, then propose tests in batches of ~10,
get approval per batch, then write the files. Stage 1's tests can be designed immediately;
later stages reference types that don't exist yet — that's expected, the tests *are* the
forward contract and will not compile until the coder lands each stage.

## Open questions (non-blocking — noted for coder/architect)

1. **HttpPath test server dependency.** Architect says "in-process Kestrel." That needs
   `<FrameworkReference Include="Microsoft.AspNetCore.App"/>` in `PLang.Tests.csproj`.
   `System.Net.HttpListener` avoids the dependency entirely. Recommending `HttpListener`
   for the fixture; coder confirms the csproj edit. Does not block test design.
2. **Fixture Context wiring.** `FilePath`'s stage-2 ctor is `public @this(string raw)` — no
   context. Authorize needs `Context.Actor`. The contract fixture's `CreateFresh()` must
   wire Context onto the minted Path (via the `IContext` setter) before returning it. The
   fixture interface returns `Task<Path>` already context-wired — pinned in batch 7.
3. **`Append`/`Mkdir`/`Save` in the abstract surface.** Today's `path.Operations.cs` has
   `Append`, `Mkdir`, `Stat`, `Save`-equivalent. The architect's abstract sketch lists
   `Append`/`Save` but not `Mkdir`. Tests assert the verb set the architect named; if the
   coder keeps `Mkdir`, that is additive and fine.

## Status

Complete. User approved all batches up front — all 14 files written in one pass. 94 tests,
all red, `PLang.Tests` builds 0 errors. Open question 1 resolved: `HttpTestServer.cs` is
shaped for `System.Net.HttpListener` (no new package). The "won't compile" worry from the
batch table was avoided — pure `Assert.Fail` stubs reference no production types, and the
Contract/ files alias the current `app.filesystem.path`. See `result.md` for the full
write-up.

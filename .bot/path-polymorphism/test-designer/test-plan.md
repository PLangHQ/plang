# Test plan — path-polymorphism (approved, all batches)

The user approved all batches up front ("do everything, all batches"). All 14 test files
were written in one pass. This file records the final batch breakdown.

## Test home

`PLang.Tests/App/Types/PathTests/` — PascalCase per CLAUDE.md ("Test folder names under
`PLang.Tests/App/` stay PascalCase"). The `PathTests` suffix avoids a namespace segment
named `Path` shadowing the per-file `Path` type alias. Mirrors `PLang/app/types/path/`.

## Batches

| Batch | Stage | File(s) | Tests |
|-------|-------|---------|-------|
| 1 | 1 | `NamespaceMoveTests.cs` | 7 |
| 1 | 4 | `PathSchemeAttributeTests.cs` | 5 |
| 2 | 2 | `SchemeRegistryTests.cs` | 10 |
| 2 | 2 | `PathAbstractTests.cs` | 6 |
| 3 | 2 | `FilePathVerbTests.cs` | 10 |
| 3 | 2 | `PathTypeMapperTests.cs` | 5 |
| 4 | 3 | `HandlerShapeTests.cs` | 9 |
| 5 | 5 | `Http/HttpTestServer.cs` (skeleton) + `Http/HttpPathTests.cs` | 12 |
| 6 | 6 | `AbsoluteCanonicalFormTests.cs` | 11 |
| 7 | 7 | `Contract/VerbName.cs`, `IPathSchemeFixture.cs`, `PathSchemeContractTests.cs`, `FilePathFixture.cs`, `HttpPathFixture.cs`, `FilePathContractTests.cs`, `HttpPathContractTests.cs`, `CrossSchemeTests.cs` | 8 base × 2 concrete + 3 cross = 19 |

**Total discovered: 94 tests.** All red (`Assert.Fail("Not implemented")`). `PLang.Tests`
builds with 0 errors.

## Design decisions

- **Survey tests use string-based reflection** (`Assembly.GetType("...")`, namespace
  scans) — never a compile-time `typeof` of a moved/deleted symbol, never a grep. A stale
  comment mentioning `IFile` must not false-positive.
- **All test bodies are `Assert.Fail("Not implemented")`** with no forward `using`
  directives → every behavioral test file compiles today and fails at runtime (proper TDD
  red). The coder turns each red→green per stage.
- **Contract/ files alias the path type to the current `app.filesystem.path`** (not the
  not-yet-existent `app.types.path.@this`) so the project compiles before stage 1. Stage
  1's `app.filesystem` → `app.types.path` rename sweep repoints them automatically — the
  exact treatment the existing `PathAuthorizeTests` / `FileHandlerTests` aliases get.
- **`PathSchemeContractTests<TFixture>` is an abstract generic base**; the two concrete
  subclasses carry `[InheritsTests]` so TUnit discovers the inherited contract tests.
- **`HttpTestServer.cs` + the two fixtures are skeletons** (`throw NotImplementedException`)
  with full doc-comment contracts — test infrastructure the coder implements in stages 5/7.

## Coder hand-off notes

1. **HttpPath test server:** use `System.Net.HttpListener` (in-box) — no
   `Microsoft.AspNetCore.App` framework reference needed. `HttpTestServer.cs` is shaped
   for it.
2. **Fixture Context wiring:** `CreateFresh()` must return a Context-wired Path —
   `FilePath`/`HttpPath` ctors are `(string raw)` only and Authorize needs `Context.Actor`.
3. **PLang-level regression:** `Tests/Modules/File/File.test.goal` is the existing
   behavioural guard for stage 3 — keep it green; no new `.goal` file was added.

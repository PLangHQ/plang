# Test designer summary — path-polymorphism

**Version:** v1

## What this is

The `path-polymorphism` branch makes PLang's `path` scheme-polymorphic: `FilePath`,
`HttpPath`, and future `S3Path`/`GitPath` each implement one verb surface
(Read/Write/Delete/Stat/List/Copy/Move). The `app.filesystem` namespace moves under
`app.types/path/`; `path` turns abstract; a per-App `Scheme` registry dispatches
`raw → Path` subclass; file handlers degenerate to one-liners and `IFile`/
`DefaultFileProvider` die. Phase 1 (stages 1-3) closes codeanalyzer v2 finding #1 from
`filesystem-permission` (handler-layer Authorize copy-paste). Phase 2 (stages 4-7) proves
polymorphism with `HttpPath` and adds a contract-test scaffold for future schemes.

The test designer's job: turn the architect's 7-stage design into test suites that are the
behavioral contract the coder implements against.

## What was done

14 C# TUnit test files created under `PLang.Tests/App/Types/PathTests/` — **94 tests**,
all in TDD red state (`Assert.Fail("Not implemented")`), build clean (0 errors), 94/94
failing as expected. The spec for each test lives in its XML doc comment.

Layout (one file per stage area; see `test-plan.md` for the full table):

- `NamespaceMoveTests.cs`, `PathSchemeAttributeTests.cs` — stages 1, 4
- `SchemeRegistryTests.cs`, `PathAbstractTests.cs`, `FilePathVerbTests.cs`,
  `PathTypeMapperTests.cs` — stage 2
- `HandlerShapeTests.cs` — stage 3
- `Http/HttpTestServer.cs` (skeleton) + `Http/HttpPathTests.cs` — stage 5
- `AbsoluteCanonicalFormTests.cs` — stage 6
- `Contract/` (8 files) — stage 7: the generic `PathSchemeContractTests<TFixture>` base,
  `IPathSchemeFixture` interface, `VerbName` enum, FilePath/Http fixture skeletons, two
  `[InheritsTests]` concrete subclasses, cross-scheme tests.

Key decisions:

- **Survey/absence tests use string-based reflection** — never a `typeof` of a
  moved/deleted symbol, never a grep (a stale comment must not false-positive).
- **Pure `Assert.Fail` bodies, no forward `using`s** → behavioral test files compile
  today and fail at runtime. The C# suite is never broken while the coder works.
- **Contract/ files alias the *current* `app.filesystem.path`** so the project compiles
  before stage 1; stage 1's rename sweep repoints them automatically.
- **`[InheritsTests]`** on the concrete contract subclasses so TUnit discovers the
  inherited contract tests (verified: 8 contract tests run twice).
- **`HttpListener`, not Kestrel**, for the test server — no new csproj dependency.

Nothing in progress. Next: run the **coder** to implement the 7 stages and turn the 94
tests green.

## Code example

The pattern of every test — `Assert.Fail` body, spec in the doc comment:

```csharp
/// <summary>Intent: POST to a GET-only endpoint returns a 405 — shaped as
/// <c>data.@this.Fail</c> with <c>Error.Type = "MethodNotAllowed"</c>, status 405. The
/// canonical "let the server respond" case: the server's refusal is a return value,
/// not a thrown exception.</summary>
[Test] public async Task Post_405_ReturnsFail_405_MethodNotAllowed()
{
    Assert.Fail("Not implemented");
}
```

The contract suite — one generic base, run against every scheme via a one-line subclass:

```csharp
[InheritsTests]
public sealed class HttpPathContractTests : PathSchemeContractTests<HttpPathFixture> { }
```

## For the coder

- Implement the three skeletons: `HttpTestServer`, `FilePathFixture`, `HttpPathFixture`
  (signatures + doc-comment contracts are in place; bodies `throw NotImplementedException`).
- `CreateFresh()` must return a Context-wired Path — the scheme ctors are `(string raw)`
  only and Authorize needs `Context.Actor`.
- `Tests/Modules/File/File.test.goal` is the existing PLang-level regression net for
  stage 3 — keep it green; no new `.goal` file was added.

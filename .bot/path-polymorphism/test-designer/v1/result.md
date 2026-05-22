# Test designer result — path-polymorphism v1

## Outcome

14 C# test files created under `PLang.Tests/App/Types/PathTests/`. **94 tests**, all
discovered by TUnit and failing with `Not implemented` — proper TDD red state.
`dotnet build PLang.Tests` passes with **0 errors**. No existing test affected.

```
total: 94   failed: 94   succeeded: 0   skipped: 0
```

## Files

```
PLang.Tests/App/Types/PathTests/
  NamespaceMoveTests.cs          (7)  stage 1 — namespace move surveys
  PathSchemeAttributeTests.cs    (5)  stage 4 — [PathScheme] marker attribute
  SchemeRegistryTests.cs        (10)  stage 2 — Register/From, dispatch, multi-App isolation
  PathAbstractTests.cs           (6)  stage 2 — abstract path hierarchy shape
  FilePathVerbTests.cs          (10)  stage 2 — FilePath verb round-trips
  PathTypeMapperTests.cs         (5)  stage 2 — PLang path type-mapper → registry dispatch
  HandlerShapeTests.cs           (9)  stage 3 — IFile death + handler one-liner surveys
  AbsoluteCanonicalFormTests.cs (11)  stage 6 — per-scheme Absolute canonical-form
  Http/
    HttpTestServer.cs            (-)  skeleton — in-process server infra (coder fills)
    HttpPathTests.cs            (12)  stage 5 — HttpPath GET/POST/DELETE/HEAD, errors, identity
  Contract/
    VerbName.cs                  (-)  test-only enum for IPathSchemeFixture.CanPerform
    IPathSchemeFixture.cs        (-)  the per-scheme fixture contract
    PathSchemeContractTests.cs   (8)  generic base — verb + permission contract
    FilePathFixture.cs           (-)  skeleton — file-scheme fixture (coder fills)
    HttpPathFixture.cs           (-)  skeleton — http-scheme fixture (coder fills)
    FilePathContractTests.cs     (8)  [InheritsTests] — runs the contract on file
    HttpPathContractTests.cs     (8)  [InheritsTests] — runs the contract on http
    CrossSchemeTests.cs          (3)  cross-scheme CopyTo / MoveTo
```

The contract base's 8 tests run once per concrete subclass → 16; with CrossScheme's 3 the
Contract folder is 19. Grand total 94.

## How the tests encode the spec

Every test method body is `Assert.Fail("Not implemented")`. The **spec lives in the XML
doc comment** above each method — it states the exact behavior, inputs, and expected
`data.@this` shape. The coder reads the comment + method name and implements both the
production code and the test body.

The contract artifacts that are *not* `Assert.Fail` stubs:

- `IPathSchemeFixture` — written in full (it is a contract, not a test).
- `VerbName` — a small test-only enum, written in full.
- `HttpTestServer`, `FilePathFixture`, `HttpPathFixture` — **skeletons**: real method
  signatures, bodies `throw new NotImplementedException()`, full doc-comment contracts.
  These are test infrastructure the coder implements in stages 5 and 7.

## Key decisions and why

1. **String-based reflection for all survey/absence tests.** Stage 1 ("namespace
   `app.filesystem` is empty") and stage 3 ("no `IFile`/`DefaultFileProvider`/
   `PLangFileSystem`") cannot use `typeof(global::app.filesystem.path)` — that stops
   compiling once the type is gone. They use `Assembly.GetType("app.filesystem.path")`
   and namespace scans. Also avoids the grep false-positive a comment would trigger.

2. **Pure `Assert.Fail` bodies + no forward `using`s ⇒ the project compiles today.**
   A behavioral test file that only contains `Assert.Fail("Not implemented")` references
   no production type, so it compiles before any stage lands and fails at runtime —
   correct TDD red. This avoids breaking the whole C# suite while the coder works.

3. **Contract/ files alias `app.filesystem.path`, not `app.types.path.@this`.** The
   fixture interface genuinely needs the path *type* in its signatures. Pointing the
   alias at the current type keeps the project green now; stage 1's rename sweep
   (`git grep app.filesystem` → 0 is its done-check) repoints them — the same treatment
   the existing `PathAuthorizeTests`/`FileHandlerTests` path aliases receive.

4. **`[InheritsTests]` on the concrete contract subclasses.** TUnit's analyzer
   (`TUnit0030`) flagged that inherited `[Test]` methods on a generic base are not
   discovered without it. Verified: with the attribute, the 8 contract tests run twice
   (FilePath + Http).

5. **`HttpListener` over Kestrel for the test server.** The architect said "in-process
   Kestrel"; that needs a `Microsoft.AspNetCore.App` framework reference the test csproj
   lacks. `System.Net.HttpListener` is in-box and sufficient. `HttpTestServer.cs` is
   shaped for it. Recorded as a coder decision — no csproj edit was made.

## Coverage against the architect's stage test plans

Every "Key surface" bullet in `plan-test-designer.md` maps to at least one test:

- Stage 1 — namespace empty, `Path` alias reachable, `@this` convention: ✓ (7)
- Stage 2 — Register/From, bare paths, unknown scheme → Fail, FilePath round-trip,
  type-mapper, multi-App isolation: ✓ (31 across 4 files)
- Stage 3 — no `IFile`/`DefaultFileProvider`/`PLangFileSystem`, no `[Code]` partial,
  handler one-liner shape, gate still fires, PLang behavior unchanged: ✓ (9)
- Stage 4 — AttributeUsage, AllowMultiple, reflection finds both, ctor contract: ✓ (5)
- Stage 5 — GET/POST/DELETE/HEAD, 404, 405→Fail(405), identity headers, NetworkError,
  no per-instance state, HttpClient pooling: ✓ (12)
- Stage 6 — FilePath unchanged + six HttpPath canonical-form rules + cross-scheme
  permission non-match + glob grant: ✓ (11)
- Stage 7 — fixture interface, generic base (round-trip, Exists lifecycle, Stat length,
  CopyTo same-scheme, MoveTo, unauthorized read/write gate, uniform failure shape),
  both fixtures, cross-scheme CopyTo: ✓ (19)

## Open items for the coder (non-blocking)

- Implement `HttpTestServer`, `FilePathFixture`, `HttpPathFixture` (skeletons today).
- Confirm no `Microsoft.AspNetCore.App` reference is needed (use `HttpListener`).
- `CreateFresh()` must return a Context-wired Path (ctor is `(string raw)` only).
- `Mkdir` exists on today's `path.Operations.cs` but is not in the architect's abstract
  surface sketch. Tests assert only the architect-named verb set; keeping `Mkdir` is
  additive and fine.

## Next step

Run the **coder** to implement the 7 stages and turn the 94 tests green.

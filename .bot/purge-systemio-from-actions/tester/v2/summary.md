# tester — purge-systemio-from-actions/v2

## Run state

Clean rebuild (`rm -rf */bin */obj` + `dotnet build PlangConsole`):
**0 errors, zero PLNG002 warnings**. Coder's analyzer claim verified.

- **C# suite**: 3025 total / 3025 pass / 0 fail / 0 skip (`dotnet run --project PLang.Tests`, ~16s).
- **PLang suite**: 206 total / 206 pass / 0 fail / 0 timeout / 0 stale (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`, ~20s end-to-end of the relevant portion).
- **Coverage (cobertura, file at `coverage.cobertura.xml`)**: overall **76.9% line / 39.6% branch**.
  - Per-package: `PLangLibrary` 55.2% line / 36.9% branch; `PLang.Tests` 94.9% / 53.6%; `PLang.Generators` 84.2% / 66.0%.
  - Path verb surface (`types/path/**`, the new infrastructure): nearly all >80% line; Execute verb 100%; JsonConverter 95%; Authorize 96%.
  - Cold spots on changed files — see findings.

## Process note (not a coder bug, but worth flagging)

`baseline-tests.md` is missing from both `coder/v1/` and `coder/v2/`. Per the
tester workflow I'm meant to diff against that file to separate regressions
from pre-existing failures. Since the suite is currently fully green, the
absence does not block this review, but the next tester pass on a noisy
suite would be flying blind. Recommend coder establishes the file on the
next iteration's branch baseline.

## Headline security claim — verified

The brief's headline (`plang --test=/etc` must surface as denial, not a
silent empty list) is verified at the *handler* layer by
`DiscoverDenialPathTests.Discover_WithTestPathOutsideRoot_DenialNotSilentEmpty`:
the test constructs a `test.discover` action with `Path` resolved to
`//etc` and calls `Run()` — it observes `Success = false`. Mutation-deleting
the AuthGate from `test/discover.cs` would flip this red.

`ExecuteVerbTests` similarly hits the right layer for D8 (`module/add`,
`code/load`) — it asserts:
- the prompt copy literally contains `"execute"` (distinguishing from Read);
- `Permission.Find` returns null when only Read is granted, even if Execute
  is requested (taxonomy correctness, not just verb existence);
- an out-of-root `LoadAssemblyAsync` with "n" returns `Success = false`.

These are honest, intent-checking tests. The verdict below applies *only*
to the other denial test files.

## Key findings — false greens at the handler layer

`HttpStaticFileDenialTests`, `FluidIncludeDenialTests`,
`OpenAiImageDenialTests`, and `DebugTraceWriteTests` all claim in their
XML doc-comments to verify that a particular **handler** (`http upload`,
`Fluid include`, `OpenAi.ResolveImage`, `debug/this.cs` trace write)
routes filesystem access through `path.*` verbs. In practice they only
exercise the verbs directly — they never instantiate the handler.

Apply the deletion test: if `OpenAi.ResolveImage` reverted its `path.ReadAsDataUri`
call back to `System.IO.File.ReadAllBytes`, **`OpenAiImageDenialTests` would
still pass** — because the test exclusively calls `p.ReadAsDataUri()` itself.
Same for `ui/code/Fluid.cs` (test calls `p.ReadText`, never `PlangFileProvider`)
and `http/code/Default.cs` (test calls `p.ReadBytes`, never `CreateFileContent`).
Coverage confirms: `modules/ui/code/Fluid.cs` 42.6% line and
`modules/debug/this.cs` 35.2% line — the handlers' new branches are largely
unexercised. The path verbs are already well-covered elsewhere; these tests
add no incremental safety.

Today the migration is mechanically enforced by PLNG002 at error severity,
so a regression to `System.IO` won't compile. The risk is non-PLNG002
regressions: a handler that *still* uses path verbs but introduces a path
that pre-dates the AuthGate check, or rebuilds raw strings between two
verbs, would slip past these tests. The PLNG002 gate is the real seatbelt;
these denial tests are theatre.

Two related issues in the same vein:

- `AppGoalsThroughPathVerbsTests.AppLoad_OnCorruptAppPr_ReturnsFailureNotCrash`
  writes garbage JSON to `app.pr`, awaits `app.Load()`, and **never
  asserts anything**. If `Load` silently swallows the parse error and
  reports success, the test passes; if it propagates a useful error key,
  the test passes; if it returns void and the bug is "we corrupted state
  in memory but didn't tell the caller", the test passes. The name promises
  "ReturnsFailure" — the body checks neither failure nor non-crash invariants.
- Multiple tests use the test-name pattern `Foo_UsesPathListNotDirectoryGetFiles`
  (`AppGoalsThroughPathVerbsTests`, `DebugTraceWriteTests`,
  `AbsoluteDisciplineTests.PathInternals_ReachForAbsolute_IsAllowed_NoDiagnostic`).
  None of them actually verify which underlying API was reached. The test
  name asserts an implementation property the test body has no way to
  observe. Either rename to match the body or add a probe (e.g. a
  `PlangFileSystem`-style sentinel that fails on `Directory.GetFiles`).

## Minor findings

- **Weak `result.Success).IsFalse()` assertions** in `OpenAiImageDenialTests`,
  `HttpStaticFileDenialTests`, `FluidIncludeDenialTests`,
  `ExecuteVerbTests.LoadAssemblyAsync_OutOfRoot_DeniedAnswer_DoesNotLoadAssembly`,
  and `SqliteAuthorizeDenialTests`. Per the character guide: also check
  `Error.Key` / `Error.StatusCode`. A read that failed for a non-permission
  reason (e.g. file-not-found) would still pass these. (Sqlite is partially
  excused — it additionally asserts `File.Exists(outOfRoot)).IsFalse()` for
  the post-condition.)
- **`DiscoverDenialPathTests.Discover_WithDotDotTraversal_DeniedByAuthGate`**
  wraps its assertion in `if (result.Success)` — meaning when denial flips
  success to false, the test runs *zero* assertions. Restructure so each
  branch has at least one assertion.
- **PLang-side denial coverage dropped to zero.** Eight stubbed
  `Tests/Permission/*OutsideRoot/Start.test.goal` files were deleted in
  `003c5267e` ("test fixes"). They had been `- throw "not implemented"`
  placeholders, so the deletion isn't wrong, but no PLang denial tests
  replaced them. The brief's "test/discover denial" angle is best
  exercised end-to-end via `plang --test=/etc` against a real PLang test
  goal; without that, the only protection is the C# `Discover_With…` tests
  above, which do work, but skip the CLI surface.
- **Pre-test stderr noise**: line 1 of `plang --test` output is
  `builder.validate: Failed to deserialize List`1 to this: ...`. The test
  it belongs to ('_fixtures_pass/trivial.fixture.goal'? — sequence
  unclear) still passes. If this is intentional (negative-path probe of
  the validator), worth a comment in the fixture; if not, a real bug is
  hiding behind the green.
- **Cold spots on changed files**:
  - `modules/debug/this.cs` 35.2% line / 20.2% branch — `ResolveLlmFilePath`
    + `WriteLlmTrace` not exercised end-to-end through the action handler.
  - `modules/ui/code/Fluid.cs` 42.6% line / 27.6% branch — `PlangFileInfo` /
    `PlangFileProvider` glue underexercised; only the inner verbs are hit.
  - `modules/builder/code/Default.cs` 35.5% line — pre-existing, not from
    this branch, but noting it as a backdrop.

## What's solid

- The path-verb infrastructure has serious coverage (`Execute` verb 100%,
  `Read` 100%, `Write` 100%, `permission/this.cs` 81%, `JsonConverter` 95%,
  `Authorize` 96%). The new `LoadAssemblyAsync` / `ReadAsDataUri` /
  `ReadAsBase64` surfaces and the Execute verb propagation are well-tested.
- `ExecuteVerbTests` and `DiscoverDenialPathTests` (test 1) are
  template-quality tests — they hit the right layer, assert intent, and
  would fail under mutation.
- `InRootSilentFastPathTests` is honest: an `AskCountingChannel` + four
  in-root verb calls asserting `AskCount == 0` is exactly the regression
  guard described. This protects the migration's "no new prompts" promise.
- The 206-test PLang suite is fully green and the headline `test.discover`
  + `Goal` Path-typing work flows end-to-end through actual plang execution.

## Verdict

`needs-fixes` — major findings are real (handler-layer tests don't test the
handler), but the suite is green and the PLNG002 error-severity gate
provides a stronger guarantee than any of these denial tests do. Coder may
choose to either harden the misleading tests (preferred: actually invoke
the handlers) or rename/scope them to admit they only exercise the verbs.
The headline brief is delivered.

If the coder prefers to address findings in a follow-up branch, that's
also defensible — these are quality issues in *new* tests, not regressions
in the production code. Either way, the production migration is sound.

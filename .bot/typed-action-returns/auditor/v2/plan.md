# Auditor v2 — plan

## Coder v2 fix
Commit `8576f2dc6` — 3 files, +50/-1.

- `PLang/app/modules/http/HttpBuildHelpers.cs`: added the registered-types gate
  (`if (app?.Types.Get(typeName) == null) return Ok();`) immediately before the
  final stamp. Comment explicitly references the mirror in `file/read.cs`.
- `PLang.Tests/App/TypedReturnsTests/Stage4_BuildMethodImplsTests.cs`:
  `HttpRequest_Build_LiteralUrlWithUnregisteredExtension_ReturnsBareOk`
  exercises `https://x/report.pdf` and asserts `Value IsNull`.

## What I'm verifying
1. The fix matches the suggestion exactly (it does).
2. The new test is honest — mutation-validate by removing the gate and
   confirming the test flips red.
3. Stage4 suite still green.
4. No drift in any of the seams I cleared in v1.

## Result
- Mutation test: gate removed → `HttpRequest_Build_LiteralUrlWithUnregisteredExtension_ReturnsBareOk`
  fails with "Expected to be null but found pdf". Gate restored → green.
- Stage4 suite: 12/12 green.
- No other changes in the diff; nothing else to re-audit.

Verdict: **PASS**.

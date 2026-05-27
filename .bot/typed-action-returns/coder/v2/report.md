# Coder v2 — auditor F1 fix

## Auditor finding addressed

**F1 (Major)** — `HttpBuildHelpers.InferTypeFromUrl` did not gate inferred
extension names against the PLang Types registry, so literal URLs ending in
`.pdf` / `.html` / `.png` / `.docx` (MIME-known but unregistered) stamped a
type name that `variable.set` then rejected at runtime with
"Unknown type 'X'".

## Fix

`PLang/app/modules/http/HttpBuildHelpers.cs` — mirror the
`Context.App.Types.Get(typeName) == null` gate already present in
`PLang/app/modules/file/read.cs:60-65`. Three-line change before the final
`Ok(typeName)`.

## Test

`PLang.Tests/App/TypedReturnsTests/Stage4_BuildMethodImplsTests.cs` — added
`HttpRequest_Build_LiteralUrlWithUnregisteredExtension_ReturnsBareOk`
asserting `https://x/report.pdf` yields a bare `Ok()` (Value null), not
`Ok("pdf")`.

## Mutation verification

Removed the new gate line; the new test flipped red (`Expected null but
found pdf`). Restored the line; all 12 Stage4 tests green.

## Verdict

F1 closed.

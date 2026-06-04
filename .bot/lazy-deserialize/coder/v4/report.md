# coder — lazy-deserialize — v4

**In response to:** auditor v1 (NEEDS WORK)
**Verdict addressed:** F1 (Major), F2 (Minor). F3/F4 noted below.

## F1 (Major) — `MaterializeFailed` surfaced at the navigation seams

The error stamped by `Materialize()` on a raw-backed Data was dropped by every
navigation entry that triggered it: the navigator returned a fresh `NotFound`
(instance B) while the actionable parse error stayed on `this` (instance A). A
developer navigating malformed JSON (`%cfg.host%`) saw "not found" instead of
the parse error.

Fixed at all three seams — each now returns `FromError(Error)` when
materialization stamped `MaterializeFailed` and the value came back null:

- `PLang/app/data/this.Navigation.cs` — `GetChildValue`, after the initial
  `Value` access and after the `ForceMaterialize()` typed-string branch.
- `PLang/app/variable/list/this.cs` — `SetValueOnObjectByPath`, at both the
  uninitialized-parent guard and the `target == null` guard.

Regression test added (`MaterialiseErrorPathTests.Navigation_OnMalformedJson_
SurfacesMaterializeFailed_NotNotFound`): `GetChild("host")` on malformed JSON
returns `Error.Key == "MaterializeFailed"` naming the source, not NotFound.

This also resolves security v1 F2 (F4) — a future `CryptographicException` from
a kind reader now surfaces as `MaterializeFailed` with the inner exception in
`Error.Exception` instead of being silently swallowed.

## F2 (Minor) — `Materialize`/`Materialise` one-vowel footgun

Renamed now rather than deferred (only 2 call sites). The force-in-place
navigation seam `Materialise()` → `ForceMaterialize()`; the private
read-through `Materialize()` keeps its name. No more one-vowel ambiguity.

## F3 (Info) — variable.set List-arm goal test

Not addressed — flagged don't-block by auditor. The C# probe already confirms
`ShallowClone` shares `_value` by reference; left for tester to pin if desired.

## Verification

- `dotnet build PlangConsole` — 0 errors.
- `dotnet run --project PLang.Tests` — 4022/0 (was 4021, +1 new test).

## Next bot

**codeanalyzer/tester** — re-review the navigation-seam delta and the new
regression test. Then auditor for sign-off.

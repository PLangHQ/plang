# auditor — lazy-deserialize — v2

**Verdict:** PASS
**HEAD audited:** `602c4f8ff`
**In response to:** coder v4 addressing auditor v1 F1 + F2.

## What changed since v1

Coder v4 (`602c4f8ff`) touched 5 files, +88/-5:

- `PLang/app/data/this.cs` — `Materialise()` → `ForceMaterialize()` (one-vowel footgun removed).
- `PLang/app/data/this.Navigation.cs` — two surfacings at `GetChildValue`: post-`Value`
  access (line 250-256) and post-`ForceMaterialize` typed-string branch (line 277-280).
- `PLang/app/variable/list/this.cs` — two surfacings at `SetValueOnObjectByPath`:
  the uninitialized-parent guard (line 275-281) and the `target == null` guard
  (line 309-313).
- `PLang.Tests/App/LazyDeserialize/LazyDataTests/MaterialiseErrorPathTests.cs` — new
  regression test `Navigation_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound`
  + the file's other three tests pin the read-time→touch-time contract.
- `LazyMaterialisationTests.cs` — one-line rename follow-through.

## F1 (Major) — RESOLVED

The fix is the minimal surface shape: after triggering Materialize, check
`Error?.Key == "MaterializeFailed"` and return `FromError(Error)` instead of
falling through to NotFound. Applied at every seam I named in v1:

| Seam (v1 file:line) | Fix landed |
|---|---|
| `this.Navigation.cs:248` (post-Value, pre-navigator) | ✓ lines 250-256 |
| `this.Navigation.cs:270` (post-ForceMaterialize typed-string) | ✓ lines 277-280 |
| `variable/list/this.cs:274` (uninitialized parent) | ✓ lines 275-281 |
| `variable/list/this.cs:303` (target == null) | ✓ lines 309-313 |

The regression test asserts the contract directly:

```csharp
var d = data.FromRaw("{ this is not valid json", type.Create("object","json",...), ctx, "cfg");
var child = d.GetChild("host");
await Assert.That(child.Error!.Key).IsEqualTo("MaterializeFailed");
await Assert.That(child.Error!.Message.Contains("cfg")).IsTrue();
```

The companion tests (`MalformedJson_ErrorsAtFirstTouch_NotAtRead`,
`Materialise_Failure_SurfacedAs_DataError_NotThrown_ToCourier`) pin the read-time→
touch-time deferral and the OBP rule #9 "courier doesn't see throws" — solid trio.

**Coverage asymmetry, flag-don't-block:** the test covers the read path
(`GetChildValue`). The two set-path fixes (variable/list) are not directly tested. The
fix shape is identical and the contract is symmetric, but a future change to the
set-path could regress without test signal. Mirror test recommended (`SetChild`
on malformed JSON parent), not required for merge.

**Auditor v1 asked for a goal test, coder delivered a C# unit test.** Accepting:
the unit test is faster and targets the exact contract; the goal-level wire-up
between `file.read … as json` → `set %x.host%` is generic plumbing already exercised
by other LazyDeserialize goal tests.

## F2 (Minor) — RESOLVED

The `Materialise`/`Materialize` one-vowel ambiguity is gone. Discriminated by role:

- `Materialize()` (private, read-through that catches and stamps the error)
- `ForceMaterialize()` (internal, force-in-place navigation seam)

Verified no stale `.Materialise(` references in `PLang/` or `PLang.Tests/` C# files.
Method-name occurrences of "Materialise" that remain are TUnit test method names
(`MaterialisesViaReader_…`, etc.) — those are spelling cosmetics, not callsites.

## F4 (Info from v1, tied to security v1 F2) — RESOLVED transitively

A future `CryptographicException` from a kind reader is now surfaced as a
`MaterializeFailed` Data.Error with the inner exception in `Error.Exception`
instead of being silently masked at the navigation seam. The catch-all in
`Materialize()` still excludes `NRE/OOM/SOE` only — but the post-fix surfacing
path means callers see what was caught.

## F3 (Info) — OPEN, unchanged

The `variable.set` List-arm regression test (`set %bundle% = [%signed%]`) tester
flagged is still not added. Coder explicitly deferred — probe-confirmed-benign. Tester
to pin if they want it. Doesn't block merge.

## Verification

- C# suite: `dotnet run --project PLang.Tests` → **4022/0** (was 4021/0 + 1 new test).
- Build: 0 errors on `PlangConsole`.
- Goal suite not re-run (the change is internal navigation-error surface; no
  goal-side semantics changed). Trusting codeanalyzer v2 / tester v3 baseline of
  273/273 on the unchanged-by-coder-v4 portion of HEAD.

## Standing carry-forward (not blockers)

- **Symmetric set-path test.** Mirror `Navigation_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound`
  for `SetValueOnObjectByPath`. One C# test, ≈10 lines.
- **F3 (tester).** `variable.set` List arm goal regression. Symmetry insurance.

## Next bot

**none — clear to merge.** All three upstream verdicts hold; auditor F1 is resolved
with a contract-pinning regression test; F2 is genuinely fixed (rename, not deferred);
F3/F4 are flag-don't-block residuals.

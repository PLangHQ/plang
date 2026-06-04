# tester v13 — result (re-test merged type-kind-strict + lazy-deserialize)

## Verdict: PASS

The branch I FAILed at v8 is now honestly green. My v8 FAIL was **not** a logic
defect — it was reproducibility: the PLang suite was not deterministically green
from a clean binary because 688/703 committed `.pr` were stale vs the branch's own
stage-4 `variable.set.Type` entity change, so the runtime ran them wrong or
LLM-rebuilt them non-deterministically (cache gitignored, cold on fresh clone).
The reported "262/262" was a warm-cache artifact. That blocker is now dead.

## Reproducibility gate (the v8 FAIL reason) — RESOLVED

Clean binary (`rm -rf bin/obj` across all projects, `dotnet build PlangConsole`),
then `cd Tests && plang --test` twice:

| run | exit | result | git status after (non-tester) |
|-----|------|--------|-------------------------------|
| 1 | 0 | `273 total, 273 pass, 0 fail, 0 timeout, 0 stale, 0 skipped` | clean (0 dirty) |
| 2 | 0 | `273 total, 273 pass, 0 fail, 0 timeout, 0 stale, 0 skipped` | clean (0 dirty) |

Identical totals, exit 0 both times, **zero `.pr` rewritten by the run**. The
lazy-deserialize merge regenerated and committed the `Tests/` tree against the
stage-4 entity shape — exactly the fix I asked for in v8.

## Suite totals (clean rebuild)

- **C# (TUnit): 4025 / 4025 / 0 fail / 0 skip.** Matches codeanalyzer v3.
- **PLang: 273 / 273 / 0 / 0-stale**, deterministic ×2 (above).

## coder v13 work — test-only, both files honest

coder v13 = two new C# test files (no production source touched; verified via the
report and `git diff` of the range).

1. `MaterialiseErrorPathTests.cs` (+2 set-path tests). Asserts
   `Error.Key == "MaterializeFailed"` — the specific key, distinct from a generic
   `NotFound` — and that the message names the source variable, and that reading
   `.Value` returns null rather than throwing out of the courier (OBP rule #9).
   These are intent assertions, not `Data.Ok()` rubber-stamps.

2. `SignedDataSurvivesVariableSetListTests.cs`. Signs a Data, binds a list literal
   through the **real** `variable.set` handler, reads `%bundle[0]%` back, asserts
   the element's `Signature` is non-null **and** `signing.verify` returns `true`.
   Verifies the by-reference ShallowClone preserves the `[JsonIgnore]` signature —
   real behavior, not a mock.

### Independent mutation verification (tester, not just trusting the coder)

I neutralized the `MaterializeFailed` branch of the second set-path guard
(`PLang/app/variable/list/this.cs:311-312`, made it fall through to `NotFound`),
rebuilt, and ran the suite:

```
failed SetPath_NestedOnMalformedJson_SurfacesMaterializeFailed_NotNotFound
failed SetPath_OnMalformedJson_SurfacesMaterializeFailed_NotNotFound
  total: 4025  failed: 2  succeeded: 4023
```

Exactly the two new set-path tests flipped red — nothing else. Reverted; `git diff`
of source empty, `git status` clean of all non-`.bot` paths. The tests genuinely
catch the regression they claim to guard.

## Strict×lazy seam — coverage survived the merge

The integration seam codeanalyzer v3 traced (strict-kind enforcement across lazy
materialization) is still covered:
- read-lift / already-loaded path: `IntegrationCutsTests/Cut2_StrictMismatchFailsAtRightLayer.cs`
  (`ReadLiftImagePngAsImageGifStrict_FailsAtSet`)
- lazy path-backed path: `ReferenceFundamentalTests/LazyPathHandleTests.cs`
  (`BytesAsync_StrictKindMismatch_ThrowsAtLoad_NotAtConstruction`)

## Findings

- **F1 (minor, carry-forward of v8 F2, non-blocking).** No end-to-end PLang goal
  walks set → store → retrieve → load → throw for a lazy strict image.
  `SetAsImageGifStrictMismatch.test.goal` only asserts `Type.Name`/`Kind`; it never
  forces a byte load, so the deferred throw never fires at the goal layer. The
  contract IS pinned in C# (`LazyPathHandleTests` for the throw, `Cut2` for the
  set-seam imprint). Now that fixtures are stable this could be added as polish;
  C# coverage makes it low-risk. Not a gate.

## What I did NOT re-litigate

The full strict-kind / Data.Kind fold / value-construction-onto-types history
(coder v1–v10) was validated by codeanalyzer v1–v2 and my own v8 code-read; the
merge with lazy-deserialize was re-reviewed clean by codeanalyzer v3, auditor v2,
security v1, docs v1. I focused on (a) the specific blocker I raised at v8 and
(b) the new v13 test quality — the highest-risk, least-reviewed surface.

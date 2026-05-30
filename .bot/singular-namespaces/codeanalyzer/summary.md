# Code Analyzer — summary (singular-namespaces)

**Version:** v4 (review of coder v2's "fundamental changes" responding to tester v1)

## What this is
Review of the `singular-namespaces` refactor's value-system reshape. Branch history:
- **v1 (FAIL):** type-entity door asymmetry (`app.Type[name]` contextless half-entity) + dead enumerators.
- **v2 (FAIL):** door fix was non-deterministic (first-wins cache over unordered types).
- **v3 (PASS):** deterministic collision resolution by richness rank.
- **tester v1 (FAIL):** green-but-dishonest — Stage 2 nullability tests inverted "Per Ingi", Stage 4 golden a tautology, +5.
- **coder v2:** addressed all 7 tester findings + flipped `Data.Context`/`Data.Type` non-null, added `type.Null` sentinel, made `Promote()` throw on unstamped fold reads, added producer stamping.
- **v4 (this — PASS):** review of those fundamentals.

## v4 result: **PASS** (4 minor/latent notes, no blockers)

### The interaction that mattered — verified safe
My v3 PASS said cache build was safe *because `Promote()` short-circuits on null Context*.
Coder v2 made `Promote()` **throw** in that case. Safety now rides on `_foldLoaded` instead:
every catalog entry uses the 2-arg ctor (`new type(name, clr)`) which sets `_foldLoaded=true`,
so `Rank()`→`Fields`→`Promote()` early-returns at line 152 before the Context check. The coder
added that `_foldLoaded=true` line as part of this change — without it, cache build would crash.
Confirmed green via clean rebuild + golden/nullability/data subsets.

### Green is real (ran it myself)
Build 0 errors; `BuilderSchema*` 2/2 (F2 golden is a real SHA256 compare, not a tautology);
`NullabilityTests` 7/7 (F1 rewrites assert the architect's spec direction, not the inverted
"Per Ingi"); `DataTests` 310/310.

### Findings (full: `v4/report.md`)
- **F1 (recommend fix):** `type.IsNull => Value == "null"` is string-magic. `Null` is a
  singleton always returned by the getter, so `ReferenceEquals(this, Null)` is exact and free —
  kills the `new type("null")` / user `type=null` collision footgun. One line. Latent today.
- **F2 (minor):** test `DataType_OnUnstampedData_ThrowsHard_NoSilentFallback` asserts `.IsNull()` —
  nothing throws. Name overpromises (same shape tester flagged). Rename. Assertion is honest.
- **F3 (latent):** `Data.As(string typeName)` dropped its `?? GetPrimitiveOrMime` fallback,
  contradicting the ValidateBuild/Sqlite reasoning that kept it. No production caller today.
- **F4 (minor):** `Scheme` getter `Context.App.Type.Scheme` lost null-safety (was `?.`) — bare
  NRE on an unstamped path entity instead of Promote's helpful producer-bug message.

### Genuinely good
Producer stamping (Permission, Sqlite, set.cs route through the entity's own resolver) is
root-cause-at-the-producer, not consumer patching. Promote throw is fail-loud-at-source. F1
nullability tests honestly flipped to match the architect's spec.

## Next (PASS → tester)
`run.ps1 tester singular-namespaces "Review the code on branch singular-namespaces" -b singular-namespaces`

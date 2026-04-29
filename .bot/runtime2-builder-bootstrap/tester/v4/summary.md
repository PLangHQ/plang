# tester v4 — verifying coder's response to v3

## What this is

Round 4 of test-quality review on `runtime2-builder-bootstrap`. v3 (commit `1c3fbcd5`) issued `needs-fixes` flagging two missing-coverage gaps (V3-1 helper, V3-2 math renders) plus carryover F4. Coder pushed `6fd35065` "Address V3-1, V3-2: unit-test the F2 helper and F3 example renders" — 2 new C# test files + 1 visibility flip — explicitly leaving F4 as separate-branch work. v4 verifies the closures via mutation testing, confirms F2/F3 integration signals stay green, tracks F4 as carryover, and decides whether to hand to security.

## What was done

Three-pass review.

**Pass 1 — re-run all suites.**
- C# (TUnit): **2309 / 2309 / 0** (was 2288 in v3 — +21 = +14 helper + +7 math). Fully green.
- PLang `/Tests/`: **142 pass / 24 fail / 4 stale (170 total)** — bit-identical to v3. F2 (BuilderValidateValid) still PASS in 25.8ms. F3 (Loop) still PASS in 2.5ms. F4 cluster (23 reds + 1 stale) untouched.

**Pass 2 — coverage on the changed surface.**
- `IsCatalogDescription` lines 659-665 went from **0% in v3 → 100% in v4**. The match-true path is no longer exclusive to `BuilderValidateValid.test.goal`.
- `ExampleRenderer.Render()` body (lines 24-62) — all 33 method-body lines hit by C# tests.
- All 5 math `ExamplesForLlm()` bodies (`add/subtract/multiply/divide/power`, lines 10-22) hit.

**Pass 3 — mutation testing on the new tests (the actual false-green hunt).**

I broke each closure on purpose to verify the new tests bite:

| Mutation | Test that bit |
|---|---|
| Drop `variable.set` from `Add.ExamplesForLlm()` natural form | `Add_NaturalForm_RendersAddThenSet` ("Expected to contain `variable.set`") + `AllArithmeticActions_HaveTwoExamples_NaturalAndRhs` ("Chain.Length expected 2 but found 1") |
| Swap chain order (variable.set first, math.add second) | `AllArithmeticActions_HaveTwoExamples_NaturalAndRhs` ("Chain[0].Module expected `math` but found `variable`") |
| Change `A=5` → `A=99` | `Add_NaturalForm_RendersAddThenSet` ("Expected to contain `A([object] 5)`") |
| Flip helper line 664 `return true` → `return false` | `Nullable_Suffix_Matches` + `Generic_TypeName_Nullable_Matches` ("Expected to be true but found False") |

→ 4/4 mutations caught. Both files restored byte-identical to origin and rerun confirms 2309/2309 green.

## Caveats / minor follow-ups (not blocking)

- **M1 — chain order isn't pinned at the render level for 4 out of 5 ops.** Mutation 2 demonstrated that swapping math.add and variable.set is caught only by `AllArithmeticActions_HaveTwoExamples_NaturalAndRhs` (spec-level), not by the per-op render tests (Contains-only). Acceptable — the cross-action test pins it for all 5 ops — but tightening Subtract/Multiply/Divide/Power RHS tests to `Contains("variable.set")` would close the remaining gap.
- **M2 — `[object]` type tag is brittle to a `Data.@this` refactor.** Intentional pin on the current type-mapping contract; flagged so future refactors know which test to update.
- **F4 (23 reds) — carried over.** Coder explicitly scoped this commit to V3-1 + V3-2. F4 is bigger separate-branch work.

## Code example — the strongest closure (V3-1)

Mutation: flip line 664 of `IsCatalogDescription`:

```csharp
if (rest[0] == '?') rest = rest[1..];
if (rest.Length == 0) return false; // MUTATION: was return true
return rest.StartsWith(" = ");
```

Test output:

```
failed Nullable_Suffix_Matches (10ms)
  AssertionException: Expected to be true but found False
  at Assert.That(DefaultBuilderProvider.IsCatalogDescription("int?", "int")).IsTrue()

failed Generic_TypeName_Nullable_Matches (10ms)
  AssertionException: Expected to be true but found False
  at Assert.That(DefaultBuilderProvider.IsCatalogDescription("list<int>?", "list<int>")).IsTrue()
```

A regression that broke the nullable-suffix branch surfaces immediately at the C# level instead of waiting for `BuilderValidateValid.test.goal` to be rebuilt.

## Verdict

**`approved`.**

V3-1 and V3-2 are closed cleanly with strong unit tests that survive the deletion test. F2/F3 integration signals stay green. C# suite went 2288 → 2309 (+21 honest tests). F4 remains open but was never in v4 scope — recommend hand to the **security** analyst next, with F4 documented as an open carryover.

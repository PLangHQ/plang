# tester v3 — verifying coder's response to v2

## What this is

Round 3 of test-quality review on `runtime2-builder-bootstrap`. v2 (commit `bbf982d4`) flagged 12 v1 findings closed (10 cleanly + 2 carryover) and 3 untouched: F2 (BuilderValidateValid `int = 1` cluster), F3 (Loop arithmetic string-concat), F4 (Signing/Identity/UI/Event/etc. cluster, ~25 reds). Coder pushed 2 commits (`bbf982d4..5c917ac5`) addressing F2 + F3, explicitly scoping out F4. v3 verifies the closures, hunts false greens in the new code, and tracks F4 as carryover.

## What was done

Three-pass review.

**Pass 1 — closure verification with deletion test.**

- **F2 closed at integration level.** New `IsCatalogDescription` helper at `DefaultBuilderProvider.cs:653–664` recognizes the 4 catalog-description shapes (`"X"`, `"X?"`, `"X = default"`, `"%var% X"`), anchored on `typeName` from the schema slot. Two skip-guards added at `NormalizeParameterTypes:616` and the `goal.call` sanity guard at line 263. `BuilderValidateValid.test.goal` goes from ~80 conversion errors to PASS in 58ms. Logic manually checked for false-positive risk — the typeName anchor prevents real LLM-emitted values from being misclassified.
- **F3 closed at integration level.** `ExamplesForLlm()` static method added to 5 math actions (`add`, `subtract`, `multiply`, `divide`, `power`), each declaring 2 example chains (natural form + RHS form). Picked up by `App.Modules.@this.Describe()` line 248 via reflection. `Loop.test.goal` goes from `"0 + 1 + 1 + 1"` (string concat) to PASS producing `3`. Verified the rebuilt `countitem.pr` — `set %count% = %count% + 1` now compiles to a `math.add` + `variable.set` chain matching the example payload.

**Pass 2 — re-run all suites.**

- C# (TUnit): **2288 / 2288 / 0** (unchanged from v2). Fully green.
- PLang `/Tests/`: **142 pass / 24 fail / 4 stale (170 total)**. Net change vs v2 (132/25/4): +10 pass, −1 fail, +9 total. The +9 total is the lowercase rename: 8 lowercase passes + 1 lowercase fail (`ForeachCallsGoalPerItem`) joined `Tests/`. F2 and F3 each contributed −1 fail / +1 pass.
- PLang `/tests/`: rename completed; only an empty `tests/modifiers/testdata/` left. No tests.

**Pass 3 — fresh-eyes false-green hunt.**

The closures rely entirely on integration tests. Two missing-coverage findings:

1. **`IsCatalogDescription` has zero C# unit tests.** Coverage report on lines 653–664 shows the **match-true path (lines 659–663) is never executed by any C# test** — every C# call falls through the `!v.StartsWith(typeName) → return false` early-out. Only `BuilderValidateValid.test.goal` exercises the positive match path. A regression that flipped the helper to always-return-false would leave the C# suite green and surface only on the next BuilderValidateValid rebuild.

2. **`math.add.ExamplesForLlm()` rendered output has no C# assertion.** `ExampleRenderer.Render()` is at 85.2% line coverage, but no test asserts that the math.add example renders into the `math.add` + `variable.set` chain the LLM consumes. Loop.test.goal is the only signal. A renderer regression would surface on the next Loop rebuild.

Both are minor — the integration tests are honest signals. But they should be unit-tested for early failure detection.

## Caveats

- **F4 is unaddressed.** 23 PLang reds remain across Signing (9), Identity (2), UI (2), Event (3), Goal-call (1), ContextVars (1), Crypto (1), Test/Discover (1), ErrorTypes (1), App/SetupGoal (1), ConditionCompound (1), ForeachDictionary (1) + ForeachCallsGoalPerItem (lowercase-relocated). Coder explicitly scoped this commit to F2+F3.
- **F5 locale-format** — still no non-Invariant culture test (carryover from v1).
- **promoteGroups still unreachable** — 0% coverage, no goal references it (carryover).
- **F8 ListOfStringToListOfString_PassesThrough mislabeled** — cosmetic carryover from v2.

## Code example — the strongest closure (F2)

```csharp
// DefaultBuilderProvider.cs:653-664 — the helper
private static bool IsCatalogDescription(string value, string typeName)
{
    if (string.IsNullOrEmpty(typeName)) return false;
    var v = value.AsSpan().Trim();
    if (v.StartsWith("%var% ")) v = v[6..];
    if (!v.StartsWith(typeName)) return false;
    var rest = v[typeName.Length..];
    if (rest.Length == 0) return true;
    if (rest[0] == '?') rest = rest[1..];
    if (rest.Length == 0) return true;
    return rest.StartsWith(" = ");
}

// Skip-guard in NormalizeParameterTypes (line 616)
if (p.Value is string desc && IsCatalogDescription(desc, p.Type.Value)) continue;
```

The typeName anchor is the elegant part: a real LLM-emitted value (`"hello"` for a string-typed slot) won't start with `"string"`, so it can't accidentally be classified as a description. The guard is sound. **The gap is at the test level**: the helper has 4 distinct match shapes and zero C# unit assertions on any of them.

## Verdict

**`needs-fixes`.** The closures are real and the design is correct, but:

1. F4 (23 reds) is not addressed.
2. The closures rest on integration tests only — adding ~10 lines of unit tests (4 IsCatalogDescription cases + 5 ExamplesForLlm renders) would catch future regressions before they reach the LLM-integration tests.

Recommend back to coder for F4 first. If F4 is genuinely out-of-scope (separate branch), bump the verdict to `approved-with-followups` and hand to security with the V3-1/V3-2 unit-test gaps documented.

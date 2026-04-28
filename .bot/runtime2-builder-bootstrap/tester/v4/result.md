# Tester v4 — verifying coder's V3-1 / V3-2 unit tests

**Tested:** commit `6fd35065` "Address V3-1, V3-2: unit-test the F2 helper and F3 example renders" (2 new test files, 1 visibility flip, +261/−1).

## Test runs

| Suite | v3 | v4 | Δ |
|---|---|---|---|
| C# (TUnit) | 2288 / 2288 / 0 | **2309 / 2309 / 0** | +21 (+14 helper + +7 math) |
| PLang `/Tests/` | 142 pass / 24 fail / 4 stale (170 total) | **142 / 24 / 4 / 170** | identical |

F2 (BuilderValidateValid) still PASS in 25.8ms. F3 (Loop) still PASS in 2.5ms. F4 cluster (23 reds) untouched per coder's explicit scoping.

## Closure verification

### V3-1 — `IsCatalogDescription` unit tests (was missing-coverage minor)

**Status: CLOSED.**

Coverage on `DefaultBuilderProvider.cs:655-666`:

| Line | v3 hits | v4 hits |
|---|---|---|
| 657 `if (string.IsNullOrEmpty(typeName)) return false;` | 1 | 1 |
| 658 `var v = value.AsSpan().Trim();` | 1 | 1 |
| 659 `if (v.StartsWith("%var% ")) v = v[6..];` | **0** | **1** |
| 660 `if (!v.StartsWith(typeName)) return false;` | 1 | 1 |
| 661 `var rest = v[typeName.Length..];` | **0** | **1** |
| 662 `if (rest.Length == 0) return true;` | **0** | **1** |
| 663 `if (rest[0] == '?') rest = rest[1..];` | **0** | **1** |
| 664 `if (rest.Length == 0) return true;` | **0** | **1** |
| 665 `return rest.StartsWith(" = ");` | **0** | **1** |

Match-true path now exercised by 14 unit tests in `PLang.Tests/App/Modules/builder/IsCatalogDescriptionTests.cs`. Visibility flipped `private` → `internal` to allow direct calls — `InternalsVisibleTo("PLang.Tests")` already in `PLang/PLang.csproj:44`, no new wiring needed.

**Mutation test on the helper.** I flipped line 664 (`if (rest.Length == 0) return true;` → `return false;`) and reran the suite:

```
failed Nullable_Suffix_Matches (10ms)
  Expected to be true but found False
  at Assert.That(DefaultBuilderProvider.IsCatalogDescription("int?", "int")).IsTrue()
failed Generic_TypeName_Nullable_Matches (10ms)
  Expected to be true but found False
  at Assert.That(DefaultBuilderProvider.IsCatalogDescription("list<int>?", "list<int>")).IsTrue()
```

→ Mutation caught. Two tests bite. Helper after restoration is byte-identical to origin.

**Per-line deletion analysis** (mapped each helper line to a test that would fail under deletion):

| Helper line | Test that bites |
|---|---|
| `IsNullOrEmpty(typeName)` early-out | `Empty_TypeName_DoesNotMatch` |
| `%var%` prefix strip | `Var_Prefix_Matches`, `Var_Prefix_With_Default_Matches` |
| `StartsWith(typeName)` early-out | `LiteralValue_DoesNotMatch_StringSchema`, `TypeName_Mismatch_DoesNotMatch` |
| First `rest.Length == 0` | `Bare_TypeName_Matches`, `Generic_TypeName_Matches`, `Surrounding_Whitespace_Trimmed` |
| `rest[0] == '?'` consume | `Nullable_Suffix_Matches`, `Nullable_With_Default_Matches`, `Generic_TypeName_Nullable_Matches` |
| Second `rest.Length == 0` | `Nullable_Suffix_Matches`, `Generic_TypeName_Nullable_Matches` |
| `StartsWith(" = ")` final | `TypeName_With_Default_Matches`, `Trailing_Junk_After_TypeName_DoesNotMatch` |

Every line has at least one test that would fail under deletion. **No false greens detected.**

The negative cases anchor on `typeName` correctly:
- `Trailing_Junk_After_TypeName_DoesNotMatch`: `"intish"` against `"int"` — would slip a naive prefix-match, the grammar branch (rest must be empty, `?`, or ` = `) catches it.
- `LiteralValue_DoesNotMatch_StringSchema`: `"hello"` against `"string"` — the LLM-emitted real value, doesn't start with `"string"`, returns false.
- `Number_Value_DoesNotMatch_IntSchema`: `"5"` against `"int"` — same reasoning.

### V3-2 — `math.<op>.ExamplesForLlm()` unit tests (was missing-coverage minor)

**Status: CLOSED.**

Coverage on `ExampleRenderer.Render()` (`ExampleRenderer.cs:24-62`): all 33 method-body lines hit. All 5 math `ExamplesForLlm()` bodies (`add/subtract/multiply/divide/power`, lines 10-22) hit by `MathExamplesForLlmTests`.

The setup spins a real `PLangEngine("/test")` with `Build.IsEnabled = true`; `Render()` is driven through real `_app.Modules.GetActionType` reflection — no stub.

**Mutation test 1 — drop `variable.set` from the natural-form chain.** Edited `add.cs:14-15` to remove the second action.

```
failed Add_NaturalForm_RendersAddThenSet (26ms)
  Expected to contain "variable.set"
  but found "math.add A([object] 5), B([object] 3)"
failed AllArithmeticActions_HaveTwoExamples_NaturalAndRhs (31ms)
  Expected to be 2 but found 1
  at Assert.That(spec.Chain.Length).IsEqualTo(2)
```

→ Mutation caught. Both per-op and cross-action tests bite.

**Mutation test 2 — swap chain order** (variable.set first, math.add second).

```
failed AllArithmeticActions_HaveTwoExamples_NaturalAndRhs (28ms)
  Expected to be equal to "math" but found "variable"
  at Assert.That(spec.Chain[0].Module).IsEqualTo("math")
```

→ Caught — but only by the cross-action test, not by `Add_NaturalForm_RendersAddThenSet`. The Contains-only assertions in the per-op render tests cannot detect chain inversion. The cross-action test does the heavy lifting at the spec level (and pins it for all 5 ops). **Acceptable** because the spec-level pin covers chain integrity for all five ops; the render test focuses on rendered-output fidelity.

**Mutation test 3 — change literal value** (`A=5` → `A=99`).

```
failed Add_NaturalForm_RendersAddThenSet (5ms)
  Expected to contain "A([object] 5)"
  but found "math.add A([object] 99), B([object] 3) | variable.set Name([string] %sum%), Value([object] %__data__%)"
```

→ Caught. The exact-string assertion catches literal drift.

The mutation also revealed the rendered-output structure: `math.add A([object] 5), B([object] 3) | variable.set Name([string] %sum%), Value([object] %__data__%)`. Confirms the `[object]` type tag for `Data.@this` (non-generic) parameters and the `[string]` tag for `Name` (typed `Data.@this<string>` after `UnwrapDataAndNullable`).

## Findings (minor, not blocking)

### M1 — Per-op render assertions don't pin chain order (already mitigated)

The four `*_BothForms_RenderChain` tests (Subtract/Multiply/Divide/Power) and `Add_NaturalForm_RendersAddThenSet` use `Contains(...)` assertions only. A regression that swapped the chain order in the rendered string would not be caught at the per-op level.

**Mitigation already in place:** `AllArithmeticActions_HaveTwoExamples_NaturalAndRhs` directly asserts `spec.Chain[0].Module == "math"` and `spec.Chain[1].Module == "variable"` for all 5 ops. Mutation test 2 confirmed this catches a swap.

**Minor follow-up:** the per-op render tests for Subtract/Multiply/Divide/Power don't assert that `variable.set` appears in the rendered RHS-form output (only the Add RHS test does). Render-time regressions specific to non-Add ops would lean on `AllArithmeticActions` alone. Not blocking — the cross-action spec pin covers it — but tightening to `Contains("variable.set")` everywhere would be cheap.

### M2 — `[object]` type tag is brittle to a Data refactor (acceptable)

The Add_NaturalForm test asserts `Contains("A([object] 5)")`. If a refactor changed `Add.A` from `Data.@this` (non-generic) to `Data.@this<int>`, the rendered tag would change to `[int]` and the test would fail. This is **intended** — it pins the current type-mapping contract — but should be flagged so future refactors know which test to update.

### F4 — carried over from v1, untouched (out-of-scope per coder)

23 PLang reds across Signing (9), Identity (2), UI (2), Event (3), Goal-call (1), ContextVars (1), Crypto (1), Test/Discover (1), ErrorTypes (1), App/SetupGoal (1), ConditionCompound (1), ForeachDictionary (1) + 1 stale (`SigningNonceReplay`). Identical to v3. Coder explicitly scoped this commit to V3-1 + V3-2. Tracked, not blocking.

### Other v1 carryovers (unchanged)

- F5 locale-format (no non-Invariant culture test) — unchanged
- F6 `promoteGroups` 0% coverage (no goal references it) — unchanged, build-time-only by design
- F8 `ListOfStringToListOfString_PassesThrough` cosmetic mislabel — unchanged

## Confidence

The two new test files are honest. Mutation testing across 4 representative regressions (3 math, 1 helper) — all caught. Coverage went from 0/9 lines on the helper match-true path to 9/9. Unit-level safety net is now in place; integration tests (`BuilderValidateValid`, `Loop`) remain as the second signal.

**Verdict: approved.** V3-1 and V3-2 are closed cleanly. F4 remains open but is explicit-scope-out work — the appropriate next step is to hand to the security analyst, not back to the coder.

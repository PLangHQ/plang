# Tester v3 — closure verification + fresh-eyes

**Tested:** commits `bbf982d4..5c917ac5` (2 commits, 8 source files, +197/−146 — most of the diff is a `tests/` → `Tests/` directory rename + Loop `.pr` rebuilds).

## Test runs

| Suite | v2 | v3 | Δ |
|---|---|---|---|
| C# (TUnit) | 2288 / 2288 / 0 | **2288 / 2288 / 0** | no change |
| PLang `/Tests/` | 132 pass / 25 fail / 4 stale (161 total) | **142 pass / 24 fail / 4 stale (170 total)** | +10 pass, −1 fail, +9 total¹ |
| PLang `/tests/` | 8 / 1 fail / 9 total | **0** (rename completed; only an empty `tests/modifiers/testdata/` left) | n/a |

¹ +9 total comes from the rename — 9 lowercase tests joined `Tests/`. Of those, 8 passed in v2 (`MockLlmSmoke`, `ValidateActionsOnly`, 6 modifiers tests). One was failing (`ForeachCallsGoalPerItem`) and is still red in v3 — **not a new regression**, just relocated.

Reconciling: v2 (Tests/+lowercase) had 26 fails. v3 has 24. Net −2 = F2 closed (−1) + F3 closed (−1).

## Closure verification

### F2 — BuilderValidateValid `int = 1` cluster (was MAJOR)

**Status: CLOSED at integration level. Unit test missing.**

Production code: `IsCatalogDescription(string value, string typeName)` at `DefaultBuilderProvider.cs:653–664`. Anchored on `typeName` from the schema — the LLM cannot trip the guard with a real value because `"some_real_value"` will not start with `"int"`. Logic verified manually:

```csharp
// value="int = 1", typeName="int"  →  match (rest=" = 1")          → SKIP ✓
// value="int?",   typeName="int"   →  match (rest="?", then empty) → SKIP ✓
// value="int",    typeName="int"   →  match (rest="")              → SKIP ✓
// value="%var% string", typeName="string"  →  match (after strip)  → SKIP ✓
// value="hello",  typeName="string"  →  no match (early return)    → CONVERT ✓
// value="int = 5", typeName="string" →  no match (StartsWith fail) → CONVERT ✓ (LLM emits string-typed real value)
```

Two skip-guards added at the call sites:
- `NormalizeParameterTypes` line 616 — eliminates the ~80 `Cannot convert 'int = 1' (String) to Int32` errors.
- Goal-call sanity guard at line 263 — eliminates the dotted-name false-positives like `goal.call.Name 'goal.call' looks like a type name`.

**Integration test (`Tests/Modules/Builder/ValidateValid/BuilderValidateValid.test.goal`):** PASS in 58ms. (Was failing with ~80 conversion errors in v2.)

**False-green caveat — unit-level coverage is missing.**

`IsCatalogDescription` is a private 12-line helper with **4 distinct match shapes**. C# coverage report on lines 653–664:

| Line | Code | Hits |
|---|---|---|
| 658 | `if (!v.StartsWith(typeName)) return false;` | 1 |
| 659 | `var rest = v[typeName.Length..];` | **0** |
| 660 | `if (rest.Length == 0) return true;` | **0** |
| 661 | `if (rest[0] == '?') rest = rest[1..];` | **0** |
| 662 | `if (rest.Length == 0) return true;` | **0** |
| 663 | `return rest.StartsWith(" = ");` | **0** |

Every C# test that calls this helper falls through the `!v.StartsWith(typeName) → return false` early-out. **The match-true path is exercised exclusively by the `BuilderValidateValid.test.goal` integration test.** A regression that flipped the helper to always-return-false would still leave C# green (because no C# test asserts a positive match) and only surface as `BuilderValidateValid` going red on the next test run.

**Recommendation (minor):** add 4 unit tests in a new `DefaultBuilderProviderTests.cs` (or extend the existing one) — one per shape (`"int"`, `"int?"`, `"int = 1"`, `"%var% int"`). The helper is a static private method but can be made internal-static or surfaced via a thin internal wrapper.

### F3 — Loop arithmetic `"0 + 1 + 1 + 1"` (was MAJOR)

**Status: CLOSED at integration level. Unit test missing.**

Production code: `ExamplesForLlm()` static method on `math.add`, `math.subtract`, `math.multiply`, `math.divide`, `math.power`. Each declares 2 `ExampleSpec` entries — a "natural form" example and an "RHS form" example matching `set %x% = %x% + 1`. Picked up by `App.Modules.@this.Describe()` at line 248 via reflection (`GetMethod("ExamplesForLlm", BindingFlags.Public | BindingFlags.Static, ...)`) and rendered through `ExampleRenderer.Render()`.

**Integration test (`Tests/Modules/Loop/Loop.test.goal`):** PASS in 21ms (was producing `"0 + 1 + 1 + 1"` in v2).

**.pr inspection** — verified `Tests/Modules/Loop/.build/countitem.pr` after rebuild:

```json
"text": "set %count% = %count% + 1",
"actions": [
  { "module": "math", "action": "add",
    "parameters": [
      { "name": "A", "value": "%count%", "type": "object" },
      { "name": "B", "value": 1, "type": "object" }
    ]
  },
  { "module": "variable", "action": "set",
    "parameters": [
      { "name": "Name", "value": "%count%", "type": "string" },
      { "name": "Value", "value": "%__data__%", "type": "object" }
    ]
  }
]
```

The chain matches the example payload exactly. **F3 is closed in practice.**

**False-green caveats:**

1. **No C# test asserts the rendered output of `math.add.ExamplesForLlm`.** `ExampleRenderer.Render(spec, modules)` is exercised at 85.2% line coverage but no test asserts that the math.add → variable.set chain renders into the formal-language string the LLM consumes. If a future refactor of `ExampleRenderer` breaks the `%__data__%` chaining or drops the second action, the math examples silently regress and the LLM goes back to mapping `set %x% = %x% + 1` as a single literal `variable.set`. The test surface for that regression is `Loop.test.goal` going red on the next rebuild — and the rebuild is non-deterministic.

2. **The closure is LLM-dependent.** Even with the example, the LLM might still occasionally pick the wrong mapping (the builder is non-deterministic). One green run on Loop.test.goal is one data point. Mitigation: the example is structurally identical to the failing input, so the LLM has every reason to mirror it. Acceptable.

**Recommendation (minor):** add a `MathExamplesTest.cs` that verifies `math.add.ExamplesForLlm()[1]` (the RHS form) renders correctly through `ExampleRenderer.Render()` and contains both `math.add` and `variable.set` action chains with the right parameter names. Same for the other 4 math actions. Catches future renderer regressions without depending on a live LLM.

### F4 — Signing/Identity/UI/Event/etc. cluster (was MAJOR)

**Status: UNTOUCHED. 23 of 24 PLang reds remain.** (24 −1 for ForeachCallsGoalPerItem which was lowercase-relocated.)

The coder explicitly said in the commit message "F2 and F3" — F4 not addressed. The full v3 fail list:

| Test | Error | Cluster |
|---|---|---|
| App/SetupGoal/Start | Expected: True, Actual: (null) | App |
| Modules/Error/Types/ErrorTypes | Expected: "TestKey", Actual: (null) | Error |
| Modules/Signing/DotNavigation | algorithm should be ed25519 → null | Signing |
| Modules/Signing/EmptyData | Contract mismatch | Signing |
| Modules/Signing/ProviderSwap | default provider should be ed25519 → null | Signing |
| Modules/Signing/Roundtrip | Contract mismatch | Signing |
| Modules/Signing/NoIdentity | sign without identity should fail → null | Signing |
| Modules/Signing/TamperedData | File not found: .build/sign.pr | Signing |
| Modules/Signing/WithHeaders | Contract mismatch | Signing |
| Modules/Signing/TimedOut | Action 'timeout.after.after' not found | Signing (action-routing) |
| Modules/Event/Remove | Expected: 1, Actual: 0 | Event |
| Modules/Event/Priority | Expected: "high,low", Actual: "" | Event |
| Modules/Event/Override | File not found: nonexistent.json | Event |
| Modules/Goal/Relative | File not found: .build/sub/subgoal.pr | Goal-call |
| Modules/Identity/Unarchive | Expected: (null), Actual: "%__data__" | Identity |
| Modules/Identity/ArchiveDefault | Expected non-null, Actual: (null) | Identity |
| Modules/Test/Discover/Stale | Expected: True, Actual: False | Test |
| Modules/Crypto/HashBcryptVerify | Algorithm 'bcrypt' is not supported | Crypto |
| Modules/Ui/RenderCallGoal | Expected: "Error", Actual: "Result: {}" | UI |
| Modules/Ui/RenderWithParams | Expected: "Title: ..., Author: ", Actual: "Author: Ingi" | UI |
| Modules/Variable/ContextVars/Basic | engine name should be set → null | ContextVars |
| Modules/Loop/Foreach/Dictionary | Expected: 3, Actual: 1 | Loop-foreach |
| Modules/Condition/Compound/Mixed | Expected: "yes", Actual: (null) | Condition |
| Builder/ForeachCallsGoalPerItem | Expected: 3, Actual: 2 | Loop-foreach (lowercase-relocated) |

These are **real production runtime regressions** introduced by the v2 builder squash 50351d8b. Decisive for the verdict.

## Carryover findings (still open from prior versions)

### F5 — locale-format coverage (carryover from v1/v2)

The format-side `InvariantCulture` fix is correct at all 3 sites (`ExampleRenderer:108`, `FluidProvider:143`, `DefaultBuilderProvider:439`), but no test sets `Thread.CurrentCulture = it-IT` and asserts the round-trip produces `"3.14"` not `"3,14"`. Coder did not address.

### PromoteGroups still unreachable

`promoteGroups.cs` referenced in zero `.goal` files anywhere (`grep -rn "promoteGroups" Tests/ tests/ os/` → no hits). Still 0% coverage. Carryover from v2 — no change.

### enrichResponse 0% (intentional-by-design)

Only referenced in `os/system/builder/BuildGoal.goal:35` (build pipeline), not exercised by `--test`. XML-doc-declared "build-time only." Carryover from v2 — same status. Honest.

### F8 mislabeled test (carryover from v2)

`TypeMappingTests.ListOfStringToListOfString_PassesThrough` comment claims it tests the auto-wrap, but the input is consumed by the list-conversion branch at `TypeConverter.cs:126` — line 156–168 (auto-wrap) is never reached. Cosmetic. Carryover.

## Coverage summary (changed/relevant files)

| File | v2 | v3 | Note |
|---|---|---|---|
| `modules/builder/providers/DefaultBuilderProvider.cs` | 60.8% | **60.8%** | +33 lines (`IsCatalogDescription` + 2 guards). Helper match-true path uncovered by C# tests. |
| `modules/math/add.cs` | — | **100%** | Pre-existing `MathTests.cs` covers `Run()`. `ExamplesForLlm()` body is invoked via reflection during catalog build. |
| `modules/math/subtract.cs` | — | **100%** | same |
| `modules/math/multiply.cs` | — | **100%** | same |
| `modules/math/divide.cs` | — | **100%** (incl. div-by-zero branch) | same |
| `modules/math/power.cs` | — | **100%** | same |
| `Catalog/ExampleHelpers.cs` | — | **76.9%** | `dot <= 0 || dot == length-1` throw path uncovered |
| `Catalog/ExampleRenderer.cs` | 85.2% | **85.2%** | unchanged |
| `modules/file/read.cs` | 100% | **100%** | F7 closure preserved |
| `modules/builder/validateResponse.cs` | 98.3% | **98.3%** | unchanged |
| `Utils/TypeMapping.cs` | 98.8% | **98.8%** | unchanged |
| `Utils/TypeConverter.cs` | 54.9% | **54.9%** | unchanged |
| `modules/builder/promoteGroups.cs` | 0% | **0%** | still unreachable |
| `modules/builder/enrichResponse.cs` | 0% | **0%** | build-time-only by design |

## Findings ledger for v3

| ID | Severity | Type | What |
|---|---|---|---|
| V3-1 | minor | missing-coverage | `IsCatalogDescription` 4 match shapes have no C# unit tests; only the integration test exercises the match-true path |
| V3-2 | minor | missing-coverage | `math.add.ExamplesForLlm()` rendered output has no direct C# assertion; renderer regression would only surface via Loop.test.goal on next rebuild |
| V3-3 | major-carryover | runtime-regression | F4 cluster (23 PLang reds across Signing, Identity, UI, Event, Goal-call, ContextVars, Crypto, Test/Discover, ErrorTypes, App/SetupGoal, ConditionCompound, ForeachDictionary) — explicitly not addressed |
| V3-4 | minor-carryover | missing-coverage | F5 locale-format — no non-Invariant culture test |
| V3-5 | minor-carryover | missing-coverage | promoteGroups still unreachable from any goal |
| V3-6 | minor-carryover | cosmetic | F8 mislabeled `ListOfStringToListOfString_PassesThrough` test |

## Verdict

**`needs-fixes` (mild-medium).**

F2 and F3 are closed in practice — both go from red to green. The fixes are correct in design (catalog-anchor, examples-not-evaluator) and the integration tests prove they work end-to-end.

But:
- **F2 + F3 closures rest entirely on integration tests.** No C# unit test exercises the IsCatalogDescription match-true path or the math.add ExamplesForLlm rendered output. A regression in either component would slip through the C# suite (still 100% green) and surface only when someone happens to rebuild `BuilderValidateValid.test.goal` or `Loop.test.goal`. This is not a critical gap — but it's the kind of "test surface that would have caught the BuildingGuard regression in v1" insight worth preserving.
- **F4 is unaddressed.** 23 PLang reds remain. Coder explicitly chose to scope this commit to F2+F3.

**Recommendation:** Send back to coder for F4. While there, optionally close V3-1 and V3-2 with two small unit-test files. Then re-run tester before security/auditor.

If F4 is deemed out-of-scope for this branch (i.e., to be fixed on a separate branch later), the verdict could be downgraded to `approved-with-followups`. But the coder's commit message did not declare this scope decision — verdict stays `needs-fixes` until that's clarified.

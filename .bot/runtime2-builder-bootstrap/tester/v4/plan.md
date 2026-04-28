# tester v4 — verifying coder's response to v3

## Context

Coder pushed `6fd35065` "Address V3-1, V3-2: unit-test the F2 helper and F3 example renders" responding to my v3 `needs-fixes`.

Diff stats:
- `PLang/App/modules/builder/providers/DefaultBuilderProvider.cs` — 4 lines (visibility change `private` → `internal` + comment)
- `PLang.Tests/App/Modules/builder/IsCatalogDescriptionTests.cs` — new file, 84 lines, 14 test cases
- `PLang.Tests/App/Modules/math/MathExamplesForLlmTests.cs` — new file, 174 lines, 7 test cases

Claim: 2288 → 2309 (+21), all green.

F4 cluster (23 PLang reds) remains untouched per coder's explicit scoping decision.

## Goal of v4

Prove the new C# unit tests are honest. They were written by the coder specifically to close coverage findings, which makes them the highest-risk new tests on the branch — quick-written tests for known-passing code can be loose enough to pass even when the production code is broken. My job is to make sure they bite.

## Work plan

### 1. Sanity check the build and run all C# tests
- `dotnet build PLang.sln`
- `dotnet run --project PLang.Tests` (TUnit, .NET 10)
- Confirm 2309 / 2309 / 0 (or note any drift). Specifically confirm the 21 new tests are present and passing.

### 2. Re-run PLang `/Tests/` suite
- `plang p build` (from repo root) — verify F2 + F3 closures still hold
- `plang p --test`
- Expect ~142 pass / 24 fail / 4 stale (170 total) — same as v3
- Read `BuilderValidateValid.test.goal` and `Loop.test.goal` `.pr` files to confirm the integration signals haven't shifted

### 3. Coverage on the changed surface
- Run `dotnet run --project PLang.Tests -- --coverage` (or whatever flag the runner accepts; per memory `feedback_test_commands.md`)
- Confirm `IsCatalogDescription` lines 659-663 now hit by C# tests
- Confirm `math.add/subtract/multiply/divide/power.ExamplesForLlm()` and `ExampleRenderer.Render()` paths covered

### 4. Hunt false greens in the new unit tests — primary v4 work

#### 4a. `IsCatalogDescriptionTests` (14 cases)
Apply the **deletion test** at the helper level:
- Mutate `IsCatalogDescription` body line-by-line. Does each line have at least one test that fails when removed/inverted?
  - `if (string.IsNullOrEmpty(typeName)) return false;` — `Empty_TypeName_DoesNotMatch` covers
  - `if (v.StartsWith("%var% ")) v = v[6..];` — `Var_Prefix_Matches` and `Var_Prefix_With_Default_Matches` cover
  - `if (!v.StartsWith(typeName)) return false;` — `LiteralValue_DoesNotMatch_StringSchema`, `TypeName_Mismatch_DoesNotMatch` cover
  - `if (rest.Length == 0) return true;` (after first check) — `Bare_TypeName_Matches`, `Generic_TypeName_Matches`, `Surrounding_Whitespace_Trimmed`
  - `if (rest[0] == '?') rest = rest[1..];` — `Nullable_Suffix_Matches`, `Nullable_With_Default_Matches`, `Generic_TypeName_Nullable_Matches`
  - second `if (rest.Length == 0) return true;` — `Nullable_Suffix_Matches`
  - `return rest.StartsWith(" = ");` — `TypeName_With_Default_Matches`, `Trailing_Junk_After_TypeName_DoesNotMatch`
- Look for any line whose deletion would still leave 14/14 green — that's a finding.
- Cross-check the negative cases really anchor on `typeName`. The strongest negative is `Trailing_Junk_After_TypeName_DoesNotMatch` (`"intish"` against `"int"`) — verify it's actually testing the trailing-junk grammar branch.

#### 4b. `MathExamplesForLlmTests` (7 tests)
- The Setup spins a `PLangEngine("/test")` and sets `Build.IsEnabled = true`. Verify this isn't a stub — it must drive `ExampleRenderer.Render()` into real catalog lookup so the rendered string genuinely reflects production behavior. If the engine stubs out the catalog at this path, the test is meaningless.
- Assertions are `.Contains(...)` not equality. That's loose by design (avoids brittleness on whitespace/formatting), but check whether it's *too* loose:
  - Does any test pass if `math.add` and `variable.set` are emitted in **wrong order**? The `AllArithmeticActions_HaveTwoExamples_NaturalAndRhs` cross-test pins `Chain[0]=math, Chain[1]=variable.set` directly on the spec — that's good. But the rendered-output tests only check for `Contains("math.add") && Contains("variable.set")` — order isn't pinned at the render level.
  - Could a buggy renderer that drops the `variable.set` step still pass the natural-form tests? Check whether `Contains("variable.set")` would still appear in some other unrelated rendered text.
  - The `Render()` method is shared across all 5 ops — if it's broken for one, it's broken for all. Are 5 separate ops being tested, or just 5 wrappers around the same code?
- Run a deletion test on a representative `ExamplesForLlm()` body — e.g. flip `Add.ExamplesForLlm()` to return only the natural example (drop the RHS one). Does `Add_RhsForm_RendersAddThenSet_WithVarOperand` and `AllArithmeticActions_HaveTwoExamples_NaturalAndRhs` fail? They should.
- Run the cross-action test against a hypothetical regression that swaps `Chain[0]` and `Chain[1]`. Does `Chain[0].Module=="math"` catch it? Yes — that's the strongest assertion in the file.

#### 4c. Visibility change
- `IsCatalogDescription` flipped `private static` → `internal static`. Verify `InternalsVisibleTo` is genuinely already in place for `PLang.Tests` (the coder's commit message claims it is). If not, the build would fail — but I should confirm the AssemblyInfo or csproj.

### 5. Track carryover findings
- F4 (23 reds) — re-count, list any newly-failing tests
- F5/F6/F8 — note status, no new work expected

### 6. Compile coverage diff
- Compare with v3's `coverage.json`. The two helper paths should now show non-zero hits.
- Save fresh `coverage.json` to `v4/`.

### 7. Verdict
- If new tests pass deletion test + integration signals still green + F4 status unchanged → `approved-with-followups` (V3-1/V3-2 closed, F4 still on the board)
- If any new test is a false green or fails deletion test → `needs-fixes` with specific finding
- If C# suite drops below 2309 or F2/F3 closures regress → `needs-fixes`

## Files to write
- `v4/plan.md` (this file)
- `v4/coverage.json`
- `v4/plang_tests_results.json`
- `v4/result.md` — detailed findings
- `v4/summary.md` — formal v4 summary
- `v4/verdict.json` — pass/fail
- `.bot/runtime2-builder-bootstrap/test-report.json` — overwrite with v4 results (this is the shared file)

## Not in scope
- F4 cluster — coder explicitly scoped out, tracked as carryover only
- Reviewing production code beyond the closure surfaces (per `feedback_tester_role.md` — tester role is test quality, not production code review)
- New module/feature additions — none in this commit

## Awaiting approval before starting work.

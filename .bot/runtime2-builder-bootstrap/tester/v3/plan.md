# tester v3 — verifying coder's response to v2

## Tested commits

`bbf982d4..5c917ac5` (2 commits, 78 files, +197 / −146 — but only **8 source files** are real code changes; the rest is a `tests/` → `Tests/` directory rename, doc fixes, deleted v7-demo, and Loop `.pr` rebuilds).

## Coder's claim

- **F2 closed** — `IsCatalogDescription` helper in `DefaultBuilderProvider` skips coercion of catalog metadata strings ("int = 1", "%var% string", "list<int>?") in two places: `NormalizeParameterTypes` (line 614) and the `goal.call` sanity guard (line 258). Eliminates the ~80 conversion errors in `BuilderValidateValid`.
- **F3 closed** — added `ExamplesForLlm()` to `math.add/subtract/multiply/divide/power` so the LLM maps `set %count% = %count% + 1` to a `math.add` + `variable.set` chain instead of a single `variable.set` with literal RHS string. No runtime evaluator added; the catalog teaches the translation.
- **F4** — explicitly NOT addressed in this commit message. Signing cluster still expected red.

## What I'll do

### Pass 1 — closure verification with deletion test

For each of F2/F3, trace the production code change to confirm the new behavior is real, not a paper closure.

- **F2**: Read `IsCatalogDescription` carefully. What strings does it accept? Could a legitimate LLM-emitted value be misclassified as a description and silently skipped? (e.g. user types `set %x% = "int = 1"` literally?) Run `BuilderValidateValid.test.goal` to confirm the int=1 cluster is gone.
- **F3**: Read `Loop.test.goal` and rebuild — confirm the `set %count% = %count% + 1` step now maps to `math.add` + `variable.set`. Run the test, confirm it produces `3` not `"0+1+1+1"`. Spot-check the rebuilt `.pr` file matches step intent. The `ExamplesForLlm` change is build-time only and can't be unit-tested with deterministic LLM behavior — verdict will rely on whether the actual goal test is now green.

### Pass 2 — re-run all suites

- C# (TUnit) — `dotnet run --project PLang.Tests`
- PLang `/Tests/` — `plang p build` then `plang p --test`
- PLang `/tests/` — same but in lowercase dir... wait, lowercase is now empty (commit 5c917ac5 deleted v7-demo, the only thing left). Confirm nothing escaped the rename.

### Pass 3 — fresh-eyes on the new code

- **`IsCatalogDescription` false-positive risk**. The helper anchors on `typeName` from the schema slot (good), but the typeName comes from the LLM output. If the LLM swaps `Type.Value` from `"int"` to literally `"hello"`, would the guard trip and silently skip a real coercion error? Check the call sites — does the guard run before or after the type is validated?
- **C# test for `IsCatalogDescription`**. Is there one? It's a private helper with non-trivial logic; even if the .goal smoke test is green, a unit test would catch regressions earlier.
- **ExamplesForLlm — is it tested?** These methods are picked up by the catalog via reflection. If the catalog rendering is broken or the example payload is malformed, only an integration test would notice. Look for ExampleRenderer tests that round-trip through `ExamplesForLlm`.

### Pass 4 — coverage spot-check

| File | Expected before | Goal |
|---|---|---|
| `DefaultBuilderProvider.cs` | 60.8% (v2) | Both new skip-guard branches exercised |
| `math/add.cs` | (was untested-as-action?) | Add unit tests cover Run; verify no test hits ExamplesForLlm |
| `math/{subtract,multiply,divide,power}.cs` | same | same |

### Pass 5 — carryover ledger

Track findings still open:
- **F4 Signing cluster** — coder explicitly skipped. If still red, this is the deciding finding for the v3 verdict.
- **F5 locale-format coverage gap** — non-Invariant culture test still missing.
- **PromoteGroups unreachable** — still no goal references it.
- **F8 ListOfStringToListOfString_PassesThrough mislabeled** — cosmetic, deferred.

## Verdict logic

- If F2 + F3 both close cleanly AND F4 is the only remaining major → `needs-fixes` mild, send back for F4.
- If F2 + F3 close but the new `IsCatalogDescription` introduces a false-positive risk → `needs-fixes`, escalate.
- If F2 + F3 + F4 all close → `approved`, hand to security.

## Risks

- Math.test.goal already exists and tests these 5 actions through the runtime; if it was previously green, the coder's `ExamplesForLlm` changes are LLM-only metadata and won't affect runtime behavior.
- The .pr rebuild in `Tests/Modules/Loop/.build/` is non-deterministic builder output — must read it and verify module/action/parameters match the step intent (this is the most likely failure mode for F3 closure).
- `IsCatalogDescription` matches catalog descriptions but the typeName is LLM-controlled. A misbehaving LLM that emits `Type="String"` and `Value="String"` (a real string) would have its real value silently skipped. Check if this is a real risk.

## Output

- `v3/plan.md` (this file)
- `v3/v2_review_summary.md` — what the coder did in response to v2
- `v3/coverage.json`
- `v3/result.md` — per-finding verification
- `v3/summary.md`
- `v3/verdict.json`
- `v3/changes.patch`
- `.bot/runtime2-builder-bootstrap/test-report.json`
- Update `tester/summary.md` with v3 line

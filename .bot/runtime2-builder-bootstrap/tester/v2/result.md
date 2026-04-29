# Tester v2 — Closure Verification + Fresh-Eyes

**Tested:** commits `d8eb2958..bbf982d4` (5 commits, 31 files, +248/-180).

## Test runs

| Suite | v1 | v2 | Δ |
|---|---|---|---|
| C# (TUnit) | 2289 total / 2281 pass / 8 fail | **2288 / 2288 / 0** | -8 fail (BuildingGuardTests deleted), +7 new tests |
| PLang `/Tests/` | 161 / 132 / 25 fail / 4 stale | **161 / 132 / 25 / 4** | unchanged |
| PLang `/tests/` | 9 / 8 / 1 fail | **9 / 8 / 1** | unchanged |

C# suite is now 100% green. PLang test totals unchanged — the coder did not touch the F2/F3/F4 PLang failure clusters.

## Closure verification (10 of 12)

### F1 — BuildingGuardTests deletion (was CRITICAL)
**Status: closed by design call.**

Verified the commit's claim that other layers still enforce `Build.IsEnabled`:

- `PLang/App/Variables/this.cs:480` — variables resolution short-circuit ✓
- `PLang/App/modules/file/providers/DefaultFileProvider.cs:21,56` — `.pr` write guard ✓
- `PLang/App/Actor/this.cs:120` — Actor setup gating ✓
- `PLang/App/this.cs:397` — App shutdown ✓

The per-action guard was redundant. The C# tests removed (8 reds) would have caught the original deletion only because the test file survived the squash that deleted the production code; they were not asserting a unique behavior.

### F5 — Locale format-side InvariantCulture
**Status: production fix verified, coverage still missing.**

All three sites correctly pass `CultureInfo.InvariantCulture`:
- `Catalog/ExampleRenderer.cs:108`
- `modules/ui/providers/FluidProvider.cs:143`
- `modules/builder/providers/DefaultBuilderProvider.cs:439` (FormatValue)

But: no test sets `Thread.CurrentCulture` to `it-IT`/`de-DE` and asserts the output is `"3.14"` not `"3,14"`. `Phase0Proof.Phase05_CultureInfo_DefaultsToInvariant` only verifies `engine.Culture == InvariantCulture`; it does not exercise the format path. A regression that flipped any of those `Convert.ToString(conv, InvariantCulture)` back to `conv.ToString()` would slip through silently.

This is **missing-coverage minor**, not a blocker — the fix is correct and the carryover risk is low.

### F6 — promoteGroups + enrichResponse 0% coverage
**Status: closed by XML-doc design declaration.**

XML doc claims "build-time only, exercised by bootstrap cycle." Verified:
- `promoteGroups`: NOT referenced in any goal file under `Tests/`, `tests/`, or `os/` (still unreachable, even from `os/system/builder/`).
- `enrichResponse`: referenced only in `os/system/builder/BuildGoal.goal:35` (the build pipeline, not `--test`).

The "`--test` never exercises these" claim is honest. The promoteGroups carryover from codeanalyzer v4 (unreachable from any goal anywhere) still stands, but coder has explicitly accepted the coverage gap.

### F7 — Gap 2 file.read ResolveVariables
**Status: closed with strong tests. Coverage 62.5% → 100%.**

Three new tests in `FileHandlerTests.cs:100-157`:

- `Read_ResolveVariablesTrue_ResolvesVariableInContent` — exercises `Variables.Resolve(content, skipInfrastructure: true)` happy path. ✓
- `Read_ResolveVariablesFalse_LeavesVariableLiteral` — covers the default-false branch. ✓
- `Read_ResolveVariablesTrue_BlocksInfrastructureVariables` — exercises the security guard. **Deletion-test verified**: if `skipInfrastructure: true` is removed from `read.cs:30`, `Variables.Get("!app.Id")` resolves to the actual App.Id GUID (the `!app` infrastructure DynamicData is registered at `Actor/Context/this.cs:172`), so the test fails. The guard is real, not a tautology.

### F8 — Gap 3 single→list auto-wrap
**Status: closed with strong tests + 1 caveat. Coverage 50.4% → 54.9%.**

Four new tests in `TypeMappingTests.cs:711-765`. Three exercise `TypeConverter.cs:156-168` correctly:
- `StringToListOfString_WrapsAsSingleElementList` → hits `listElementType.IsAssignableFrom(sourceType)` at line 156 ✓
- `IntToListOfInt_WrapsAsSingleElementList` → same branch ✓
- `StringToListOfInt_ConvertsThenWrapsAsList` → hits convert-then-wrap fallback at line 162 ✓

**Caveat:** `ListOfStringToListOfString_PassesThrough` is mislabeled. The comment claims it's a "sanity guard that auto-wrap doesn't engage on already-list inputs," but the input `List<string>` is consumed by the list-conversion branch at `TypeConverter.cs:126` (`value is IList sourceList`) — line 156 is never reached. Deleting the auto-wrap branch (lines 156-168) would NOT fail this test. It still validates the list-conversion branch (a different real path), so it's not a false green — but it doesn't verify what its comment claims. Minor cosmetic.

### F9 — ErrorHandle retry weak assertion
**Status: closed with strong stateful-lambda pattern.**

`ErrorHandleTests.cs:188-253` — three retry-exhaustion tests now use `int callCount = 0` + lambda + `await Assert.That(callCount).IsEqualTo(N)` where N is `1 + retryCount`. Deletion-test passes: replacing the retry loop body with `for (int i = 0; i < 0; i++)` would yield `callCount == 1`, failing the `IsEqualTo(3)` assertion. ✓

### F10 — IfErrorOrchestration error identity
**Status: closed with explicit pin.**

`IfErrorOrchestrationTests.cs:76-77` pins `Error.StatusCode == 404` and `Error.Key == "NotFound"`. Production source: `GoalError.NotFound()` at `PLang/App/Errors/GoalError.cs:29` returns `(message, "NotFound", 404)`. The test no longer accepts any error — only the goal-not-found error from the `goal.call` to `DoesNotExist`. ✓

### F11 — ValidateResponse NullInputs message pin
**Status: closed with exact production-string pin.**

`ValidateResponseTests.cs:131-132` asserts `Error.Message.Contains("StepResults.Value is null")` and `"Goal.Value is null"`. Production strings at `validateResponse.cs:45,55` produce these exact substrings. The four ValidationError variants (null inputs, step-count, gap, Keep-without-prior) now disambiguate by message. ✓

### F12 — List/Foreach Success.IsFalse follow-ups
**Status: closed.**

- `ListTests.Get_OutOfRange_Fails` (line 124-125) — pins `Error.Key == "ValidationError"` and message contains `"out of range"`. Production source at `list/get.cs:18` constructs `ValidationError($"Index {Index.Value} out of range for '{ListName}'")`. ✓
- `ForeachErrorPropagationTests` (multiple) — pins `Error.StatusCode == 404` and `item == "a"` (loop stopped on first iteration). ✓

## Untouched findings (carryover)

### F2 — BuilderValidateValid `int = 1` cluster (MAJOR, untouched)
**Still red.** `Modules/Builder/ValidateValid/BuilderValidateValid.test.goal` reports ~80 conversion errors of the form `Cannot convert 'int = 1' (String) to Int32`. The @known annotation strings are still flowing through `Convert.ChangeType` instead of being unwrapped. Affects every action's parameter coercion. Sample: `goal.return.Depth`, `variable.set.AsDefault`, `list.add.AtIndex`, `mock.intercept.Parameters: Cannot convert String to Dictionary'2`, `error.handle.Actions: Cannot convert String to this`.

This is a real production runtime bug, not a build-time-only concern. Coder did not address.

### F3 — Loop test string-concat (MAJOR, untouched)
**Still red.** `Modules/Loop/Loop.test.goal` expects `3` and gets `"0 + 1 + 1 + 1"`. Arithmetic add is treated as string concatenation. Coder did not address.

### F4 — Signing cluster (MAJOR, untouched)
**Still red.** 9 signing tests fail with mixed errors: `Contract mismatch`, `File not found: .build/sign.pr`, `Action timeout.after.after not found` (extra `.after` segment — module/action routing bug). Coder did not address.

## Carryover notes

- **PromoteGroups still unreachable.** The XML doc claims "build-time only, exercised by bootstrap cycle." But `promoteGroups` is referenced in zero `.goal` files anywhere, including `os/system/builder/`. So even the bootstrap cycle doesn't invoke it. The "honest signal is the next bootstrap" claim is not supported. Either delete the action or call it from somewhere.
- **Locale-format coverage carryover from v1 #5.** The fix at three sites is correct, but no test exercises the round-trip with non-Invariant culture.

## Coverage summary (changed files only)

| File | v1 | v2 | Note |
|---|---|---|---|
| `modules/file/read.cs` | 62.5% | **100.0%** | Gap 2 tests landed |
| `Utils/TypeConverter.cs` | 50.4% | **54.9%** | Auto-wrap branch hit; many other paths still uncovered |
| `Utils/TypeMapping.cs` | — | 98.8% | clean |
| `modules/builder/promoteGroups.cs` | 0% | **0%** | intentional-by-design |
| `modules/builder/enrichResponse.cs` | 0% | **0%** | intentional-by-design |
| `modules/builder/validateResponse.cs` | — | 98.3% | strong |
| `modules/builder/providers/DefaultBuilderProvider.cs` | 63.6% | 60.8% | slight drop (FormatValue InvariantCulture branch added but not exercised by a non-Invariant test) |
| `modules/ui/providers/FluidProvider.cs` | — | 73.4% | the FormatFormalValue path with conv.ToString → InvariantCulture is mid-coverage |
| `Catalog/ExampleRenderer.cs` | — | 85.2% | similar |
| `modules/error/handle.cs` | — | 99.2% | strong (F9 fix) |
| `modules/condition/if.cs` | — | 92.0% | strong (F10 fix) |
| `modules/list/get.cs` | — | 100.0% | strong (F12 fix) |
| `modules/loop/foreach.cs` | — | 97.5% | strong (F12 fix) |

## Verdict

**needs-fixes** — but mild. The 10 closures are solid (with 2 small caveats). The C# suite is 100% green. F2/F3/F4 (25 PLang reds + 1 in lowercase suite) remain real production failures the coder did not address — these are not build-time-only.

If F2/F3/F4 are deemed out-of-scope for this branch (they pre-date the bootstrap work), then verdict could be approved-with-followups. But they weren't pre-existing — they came in with the v2 builder squash 50351d8b. Recommend back to coder for those three clusters before security/auditor.

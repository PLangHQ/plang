# codeanalyzer v2 — review of coder's response to v1 findings

Reviewed coder's v2 (commits `01aa150c..4018f26b`) against v1's 38 findings. Coder addressed 12 findings explicitly (1, 2, 3, 6, 9, 11, 12, 19, 21, 27, 28, 33), deferred 25 with rationale, and silently missed 1 (Finding 7).

**TL;DR:** Production fixes are sound. **Tests claim to close v1 gaps but two of the headline new tests (`IncrementalCacheTests`, `NoDeadEmissionTests`) cannot catch the bugs they were named after.** Verdict: **NEEDS WORK** — not for production code, but because Ingi's specific v1 concern about test gaps is not actually resolved.

---

## Verified correct (v1 findings closed)

### Production code

**Finding 27 — cycle detection in `Data.AsT_Impl` (`PLang/App/Data/this.cs:390-437`).** Thread-static `_resolvingValues` HashSet, `isCycleRoot` flag captured before allocation, `try/finally` cleanup, key on raw `%`-containing string with `StringComparer.Ordinal`. Direct cycles (`%a%↔%b%`, `%x% = %x%`) and chained cycles correctly detected and short-circuited via `ConvertAndWrap<T>(strVal, ctx)` returning the raw string. **Live `plang test` no longer crashes** (was 100% StackOverflow).

**Finding 1 — `ActionClassInfo` is now a record with `EquatableArray<T>` collections (`PLang.Generators/Discovery/this.cs:282-296`, `PLang.Generators/EquatableArray.cs`).** Record conversion correct; EquatableArray's `Equals` walks via `SequenceEqual`, `GetHashCode` walks elements with stable `unchecked` hash combine. Default-instance handling (`_array == null`) explicit in `Equals`/`GetHashCode`. `IIncrementalGenerator` value-equality contract is now structurally delivered.

**Finding 11 — `__variables` field deleted.** Verified absent from `EmitResolutionState` (`PLang.Generators/Emission/Action/this.cs:84-92`) and from a sampled generated `App.modules.matrix.plain.BoolPlain.Action.g.cs`.

**Finding 12 — `__paramData` + `ParamData()` accessor deleted.** Same verification — gone from emission and from generated output.

**Finding 33 — App.Run OCE catch documented (`PLang/App/this.cs:411-414`).** Comment clearly explains the deliberate swallow, the dependency on `timeout.after`, and the asymmetry with `Step.RunAsync`.

**Finding 28 — non-generic collection contract documented (`PLang/App/Data/this.cs:508-512`).** Comment placed directly above `SubstitutePrimitive`. Notes the `IList<object?>` / `IDictionary<string, object?>` shape requirement and that JSON ingestion normalizes upstream.

**Finding 2 — dead `OriginalDefinition.Name == "@this"` disjunct dropped (`Discovery/this.cs:134-136`).**

**Finding 3 — triple `INamedTypeSymbol` cast extracted (`Discovery/this.cs:191-195`).**

**Finding 6 — `RawScalarPropertyDescriptor` is now `internal` (`Discovery/this.cs:44`).**

**Finding 9 — class renamed `LazyParamsGenerator` → `@this` (`PLang.Generators/this.cs:14`).** Generated folder is now `PLang.Generators.this`. Three test paths updated: `SnapshotParamsTests.cs:31`, `GeneratorValidationTests.cs:33`, `NoDeadEmissionTests.cs:28`.

**Finding 21 — double `({InnerType})` cast for enum default removed (`Discovery/this.cs:226-227`).** Comment explains why: emission already wraps in `({InnerType})…`, so a separate cast produced `({T})({T})value`.

**Finding 19 — raw string literals adopted in `Emission/Action/this.cs`.** The `$$"""…"""` form is genuinely more readable. Interpolation via `{{name}}`, literal braces via `{`/`}`. Closing `"""` column dedents consistently. The summary admits 5 trivial blank-line drift in a sampled handler — consistent with a generally-faithful rewrite.

### Build / test runs

- `dotnet build PLang.Tests/PLang.Tests.csproj` clean (warnings only).
- All 9 `IncrementalCacheTests` pass.
- `NoDeadEmissionTests` passes (1 test).
- 6 new `DataAsTResolutionTests` (4 cycle + 2 non-generic) pass.
- 1 new `AppRunScaffoldingTests.AppRun_HandlerThrowsOCE_TranslatesToServiceError_DoesNotPropagate` passes.
- Generated output verified to no longer contain `__variables`, `__paramData`, or `ParamData(`.

---

## NEW FINDINGS — v2-introduced or v1-mishandled

### MAJOR

**39. `IncrementalCacheTests` does not drive Roslyn at all (`PLang.Tests/Generator/IncrementalCacheTests.cs`).**

What was promised in the plan:

> "Drive the generator through Roslyn's `CSharpGeneratorDriver.RunGenerators` against a small inline source. Run twice. Assert the second `RunResult.Results[0].TrackedSteps[<stepName>].Outputs[0].Reason == IncrementalStepRunReason.Cached`. **This is the test that would have caught the bug.**"

What was delivered: 9 unit-equality tests on `ActionClassInfo` and `EquatableArray<T>` constructed in-test. Record `Equals` is C# compiler-guaranteed. `EquatableArray<T>.Equals`/`GetHashCode` is worth testing in isolation, but the IIncrementalGenerator cache contract — whether the pipeline actually USES that equality to skip re-emission — is **not exercised**. None of these tests would fail if a future change broke caching at the pipeline level (e.g. interposing a non-equatable carrier between stages).

This is exactly the gap Ingi flagged in v1. The carrier-level equality regression of v1 (Finding 1, `sealed class`) would be caught — but the broader claim of "test that would have caught the bug at the pipeline level" is not satisfied. Recommend supplementing with one true `CSharpGeneratorDriver` cache-hit assertion using `TrackedSteps`/`IncrementalStepRunReason.Cached`.

**40. `NoDeadEmissionTests` is structurally incapable of catching `__variables` or `__paramData` regressions (`PLang.Tests/Generator/NoDeadEmissionTests.cs:39-81`).**

Empirically verified by simulation. The test computes `reads = allOccurrences − assignments` and flags `reads <= 0`. For the v1 `__variables` regression (declared + 1 assignment, no reads):

| field | occurrences | assignments | reads | flagged? |
|---|---|---|---|---|
| `__variables` (v1 regression) | 2 (decl + LHS) | 1 | **1** | **No** |
| `__paramData` (v1 regression) | 4 (decl + 2 LHS + 1 internal accessor body) | 2 | **2** | **No** |

The author flagged this in their own comment (lines 70-72): *"The declaration itself counts as a 'use' by the regex above (just the bare name) but isn't a read — adjust by subtracting 1 if the line has no `=`. Simpler heuristic: a field is dead if reads <= 0."* The "simpler heuristic" doesn't work; the author chose it anyway.

The test passes today because the post-deletion code has no dead fields — not because the test detects them. A future regression of `__variables` would silently re-land. The headline claim ("regex over every `*.Action.g.cs` asserting every private field has at least one read; would have flagged `__variables` and `__paramData` automatically") is wrong on the second clause.

Two fixes needed:
1. Tighten `reads` computation: `reads = occurrences − assignments − 1 (declaration without inline init)` or count non-LHS occurrences explicitly. This makes the test catch `__variables`-class bugs.
2. Cross-file analysis for `__paramData`-class bugs (the accessor reads the field within the file, but nothing across the repo calls the accessor). The test as designed cannot do this — would need a separate test scanning `PLang/`, `PLang.Tests/`, `os/` for accessor callers.

Without these fixes, the test gives false confidence — exactly the v1 problem Ingi identified.

### MINOR

**41. Cycle protector keys on raw input string — expanding cycles still recurse infinitely (`Data/this.cs:408-414`).**

Construct: `%a% = "X-%b%"`, `%b% = "Y-%a%"`. Each recursion produces a unique, longer string (`"X-%b%"` → `"X-Y-%a%"` → `"X-Y-X-%b%"` → …). `_resolvingValues.Add(strVal)` always returns true; cycle never detected. Stack-overflows once depth gets sufficient.

Mitigations: (a) also key on the variable name being resolved (full-match path has it directly; partial-match could collect names from a regex); (b) add a depth bound (`MAX_DEPTH = 32`); (c) bound the resolved string length and short-circuit.

Risk in practice: low (rare pattern). But the cycle-protection contract ("no stack overflow on user data") is incomplete, and the existing tests don't exercise the expanding case — `AsT_CyclicVarReference` uses `%a%↔%b%` direct equality, which is the easy case.

**42. OCE asymmetry test only pins one direction (`AppRunScaffoldingTests.cs:155-168`).**

Plan promised paired tests:
> - `AppRun_HandlerThrowsOCE_TranslatesToServiceError` — handler that throws OCE → result is non-Success ServiceError ✓ delivered
> - `StepRunAsync_HandlerThrowsOCE_LetsItPropagate` — sanity-check the asymmetry holds ✗ **missing**

The comment in `App.Run` (line 414) asserts "Step.RunAsync's catch DOES exclude OCE — that asymmetry is intentional", but no test pins that direction. A future "consistency fix" to `Step.RunAsync` (also swallow OCE) silently breaks `timeout.after` and no test catches it. Coder's summary doesn't acknowledge dropping the Step side.

**43. Cycle tests assert only `IsNotNull` — value contract not pinned (`DataAsTResolutionTests.cs:224-259`).**

Three of four cycle tests assert just `await Assert.That(result).IsNotNull();`. Catastrophic regression (StackOverflow) is detected via test-runner crash, but a "fix" that returns wrong value (empty string instead of cycle string, or `null` instead of `Data<T>`) wouldn't fail any test. Should assert the cycle-broken value is the original string, e.g. `await Assert.That(result.Value).IsEqualTo("%a%");` for the direct cycle case.

`AsT_DeepChain_5Levels_ResolvesCorrectly` does this correctly — model the cycle tests on it.

### NIT

**44. `NoDeadEmissionTests` regex restricts to `__`-prefixed fields only (`NoDeadEmissionTests.cs:48`).**

Pattern `(__\w+)` matches only fields starting with `__`. All current generated fields use the prefix, but the convention isn't pinned by any test. Either generalize the regex or add an assertion that no non-`__`-prefixed private field is emitted.

**45. Finding 7 silently dropped — diagnostic location is still synthesized as 1-character span (`PLang.Generators/this.cs:34-36`).**

Coder's "not taken" list (plan + summary) does not include Finding 7. Discovery now stores `loc?.GetLineSpan().StartLinePosition.Line/.Character` (the upstream Location is available), but `DiagnosticInfo` still flattens to `(FilePath, Line, Character)`, and the orchestrator constructs `Location.Create(d.FilePath, ..., new LinePositionSpan(start, new LinePosition(d.Line, d.Character + 1)))` — a 1-column span. IDE squiggle on a property-name diagnostic still points at one column instead of underlining the identifier.

Two fixes possible: (a) include identifier length in `DiagnosticInfo` (`int Length`); (b) widen `DiagnosticInfo` to carry `int EndLine`/`int EndCharacter` from `loc.GetLineSpan().EndLinePosition`.

---

## Reaffirmed v1 findings (deferred by coder, acknowledged)

Coder logged 22 findings as "not taken" with explicit rationale (transitional, pre-existing, future cleanup, or readability-only). I accept these:
- 4, 5 — Discovery's parallel classifiers + 70-line `BuildProperty` cascade. Refactor opportunity.
- 13, 15, 16, 17, 18 — transitional dead code that Phase 5 cleanup will sweep.
- 14, 23 — drop `__app`, simplify Provider lazy-fallback. Cross-cutting; own PR.
- 22 — split four-branch Data getter into per-shape methods. Readability.
- 24 — `SubstitutePrimitive` couples Data to Action. Pre-existing; marker interface refactor.
- 25, 26 — typed-fast-path duplication, hand-rolled `ToBoolean`. Pre-existing.
- 29 — `As<T>` ignores `_type.Convert`. Pre-existing, no current handler hits.
- 30, 31, 32, 34, 35, 36, 37, 38 — pre-existing or pure readability.
- 20 — coder reasoned the four-getter-shape inconsistency is cosmetic, not behavioral. I accept that for now; matrix tests cover all four shapes through the standard pipeline.

---

## Summary of v2 findings

| # | Severity | Topic | Status |
|---|----------|-------|--------|
| 39 | MAJOR | `IncrementalCacheTests` doesn't drive Roslyn | NEW |
| 40 | MAJOR | `NoDeadEmissionTests` cannot catch its named regressions | NEW |
| 41 | MINOR | Cycle protector misses expanding-string cycles | NEW |
| 42 | MINOR | OCE asymmetry pinned only on App.Run side | NEW (half-fix) |
| 43 | MINOR | Cycle tests don't assert value | NEW |
| 44 | NIT | `NoDeadEmissionTests` regex limited to `__`-prefixed | NEW |
| 45 | NIT | Finding 7 (synthetic 1-char span) silently dropped | v1 missed |

**Counts: 2 MAJOR, 3 MINOR, 2 NIT = 7 v2-specific findings.**

---

## Verdict: NEEDS WORK

**Production code:** PASS. The 12 production-code fixes from v1 (1, 2, 3, 6, 9, 11, 12, 21, 27, 28, 33, plus the Phase E raw-string refactor) are all correctly applied. `plang test` is no longer crashing. Generator's incremental cache contract is now structurally delivered. Generated output is leaner.

**Test set:** FAIL on the specific concern Ingi raised in v1. Two of the three new test files (`IncrementalCacheTests`, `NoDeadEmissionTests`) are present-but-toothless: they pass for the post-fix code but cannot catch the regressions they were named after. The test gap that Ingi pointed out — "unit tests should have caught some of those" — is not closed; it has been re-papered.

### Recommended next steps for coder (v3)

1. **Replace or supplement `IncrementalCacheTests`** with a real `CSharpGeneratorDriver` cache-hit test using `TrackedSteps` and `IncrementalStepRunReason.Cached`. The 9 existing equality tests can stay or be reduced; the gap is the missing pipeline-driven test.
2. **Fix `NoDeadEmissionTests` heuristic** to actually detect declaration+assignment-no-read fields. Add a separate cross-file scan for unused accessors (catches `__paramData`-class bugs).
3. **Add `StepRunAsync_HandlerThrowsOCE_LetsItPropagate`** to pin the asymmetry's other direction.
4. **Tighten cycle test assertions** to pin specific return values (e.g., the cycle-broken raw string).
5. **Optional: harden cycle detection** for expanding cycles via depth bound or variable-name keying.
6. **Address Finding 7** (carry the full location span through `DiagnosticInfo` so IDE squiggles underline the identifier).

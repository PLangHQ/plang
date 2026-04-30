# Coder v2 plan — fix codeanalyzer findings + close test gaps

## Context

- Codeanalyzer review flagged 38 findings (10 MAJOR / 19 MINOR / 9 NIT) — see `v1_review_summary.md`.
- `plang test` is currently **crashing with StackOverflowException** in `AsT_Impl` (Finding 27 in real life, not theory).
- Ingi is right: the test set has real gaps. This plan addresses both the production fixes AND plugs the test gaps so this class of regression is caught next time.

## Goal

Land the codeanalyzer's top-3 mechanical fixes + the load-bearing behavioral fixes (cycle detection, OCE comment) + new tests that pin every contract the v1 set missed. Leave transitional cleanup (legacy helpers, dual validation blocks) for a future pass after Phase 5 lands.

---

## Phase A — Critical fix (unblocks `plang test`)

### A1. Cycle detection in `Data.AsT_Impl` (Finding 27)

**File:** `PLang/App/Data/this.cs` (~line 412)

**Problem.** When a variable's value is itself a `%var%` reference, `AsT_Impl` recurses on `Variables.Get(name).Value` without any visited-set. `%a%↔%b%` cycles → `StackOverflowException`. `Variables.Resolve` already has cycle protection via thread-static `_resolvingVars`, but the full-match path in `As<T>` bypasses `Resolve` to call `Variables.Get` directly.

**Fix.** Mirror `Variables._resolvingVars`:
- Add `[ThreadStatic] private static HashSet<string>? _resolvingTypedVars;` to `Data.@this`.
- In the full-match branch, before recursing, add the variable name to the set and remove it after.
- If the name is already in the set, return `Data.NotFound` (or `null`-valued `Data<T>`).
- Use `try/finally` to guarantee removal on exception paths.

**Test.** `DataAsTResolutionTests`:
- `AsT_CyclicVarReference_DoesNotStackOverflow` — `Variables.Set("a", "%b%"); Variables.Set("b", "%a%"); data.As<string>` returns gracefully (asserting either `null` Value or non-Success), not stack overflow.
- `AsT_SelfReferencingVar_DoesNotStackOverflow` — `Variables.Set("x", "%x%")` → same.
- `AsT_DeepChain_5Levels_ResolvesCorrectly` — non-cyclic chain of 5 indirections still resolves.

### A2. Re-run `plang test` after A1

Confirm the test process completes. Capture the success/fail count Ingi mentioned (~100 tests).

---

## Phase B — Codeanalyzer top-3 mechanical fixes

### B1. `ActionClassInfo` → record + value-equal collections (Finding 1)

**File:** `PLang.Generators/Discovery/this.cs` (~line 281)

**Fix.**
- Convert `ActionClassInfo` from `sealed class` to `public sealed record ActionClassInfo(...)`.
- Replace each `List<T>` field with `EquatableArray<T>` (small struct wrapper around `T[]` that implements `IEquatable<EquatableArray<T>>` via `SequenceEqual`). EquatableArray is the standard incremental-generator pattern.
- Add `IsExternalInit.cs`-style polyfill if needed for record support on netstandard2.0 (already exists).
- Verify all property records (`PropertyBase` and subclasses) are records — they already are.
- Verify `RawScalarValidation` and `DiagnosticInfo` are records — they already are.

**File:** new `PLang.Generators/EquatableArray.cs`

**Test.** New `PLang.Tests/Generator/IncrementalCacheTests.cs`:
- Drive the generator through Roslyn's `CSharpGeneratorDriver.RunGenerators` against a small inline source.
- Run twice. Assert the second `RunResult.Results[0].TrackedSteps[<stepName>].Outputs[0].Reason == IncrementalStepRunReason.Cached`.
- This is the test that would have caught the bug.

### B2. Delete `__variables` field (Finding 11)

**File:** `PLang.Generators/Emission/Action/this.cs` (lines 79, 122)

**Fix.** Remove the field declaration AND the `__variables = context.Variables;` assignment line.

**Test.** New `PLang.Tests/Generator/NoDeadEmissionTests.cs`:
- For every `*.Action.g.cs` in the obj/Debug/net10.0/generated/ tree, parse the source.
- For every private field declared in the partial class, assert at least one read elsewhere in the same file. Read = identifier appears not on the LHS of `=`.
- This single test catches `__variables` AND `__paramData` AND any future dead emission.

### B3. Delete `__paramData` + `ParamData()` accessor (Finding 12)

**File:** `PLang.Generators/Emission/Action/this.cs` (lines 91-97, 230)

**Fix.**
- Remove the `__paramData` field, the `ParamData()` accessor method, and the `__paramData[name] = ...` assignment inside `__Resolve<T>`.
- Verify no caller across `PLang/`, `PLang.Tests/`, `os/` uses `ParamData(...)` — already confirmed by codeanalyzer, double-check before deleting.

**Test.** Same `NoDeadEmissionTests` from B2 covers this.

---

## Phase C — Behavioral concern documentation + tests

### C1. Document App.Run OCE catch (Finding 33)

**File:** `PLang/App/this.cs` (~line 411)

**Fix.** Add a one-paragraph comment on the catch clause:
> // Deliberately catches OperationCanceledException — `timeout.after` depends on this:
> // the inner action's generated ExecuteAsync swallows OCE into a ServiceError result, so
> // timeout.after detects the timeout via CTS state + failed result, not via OCE bubbling up.
> // Step.RunAsync's catch DOES exclude OCE — that asymmetry is intentional.

**Test.** New `PLang.Tests/App/AppRunOceTests.cs`:
- `AppRun_HandlerThrowsOCE_TranslatesToServiceError` — handler that throws `OperationCanceledException` → result is non-Success ServiceError (not a re-thrown OCE).
- `StepRunAsync_HandlerThrowsOCE_LetsItPropagate` — sanity-check the asymmetry holds.

### C2. Non-generic collection contract documentation (Finding 28)

**File:** `PLang/App/Data/this.cs` (`SubstitutePrimitive`, ~line 500)

**Fix.** Add one comment line documenting the shape contract: `WalkList`/`WalkDict` only match the typed-generic shapes; non-generic `IList`/`IDictionary` is treated as a primitive and passes through. All JSON ingestion is normalized through `UnwrapJsonElement` to typed forms before reaching the walker.

**Test.** New entry in `DataAsTResolutionTests`:
- `AsT_NonGenericList_PassesThroughWithoutSubstitution` — feed an `ArrayList` containing `"%x%"`, assert pass-through (current behavior pinned).
- `AsT_NonGenericDict_PassesThroughWithoutSubstitution` — feed a `Hashtable`, assert pass-through.

### C3. Direct-init composition test for Data getter shapes (Finding 20)

**File:** `PLang.Tests/Generator/Matrix/Plain/PlainTests.cs` (or new file)

**Fix.** No production change — just a test that pins the four getter shapes (plain Data / nullable / default / typed) when constructed via direct init (`new Handler { PropName_backing = ..., PropName_set = true }`). The existing matrix tests go through `MatrixRunner.RunAsync` which assigns properties via the standard pipeline; the direct-init path is the C#-composition use case that exposed Finding 20's `SetFlag` vs `Backing == null` divergence.

---

## Phase E — Raw string literals for emission (Finding 19, requested by Ingi)

**File:** `PLang.Generators/Emission/Action/this.cs` (the `sb.AppendLine` cascades)

**Goal.** Replace `sb.AppendLine("    private global::App...")` walls with C# 11+ raw string literals (`"""..."""`) using `{}` interpolation. The emitted shape becomes visible top-to-bottom instead of buried in `\"` and `+` concatenation.

**Approach.**
- Convert each emit method one at a time (EmitMarkers, EmitResolutionState, EmitDataAndErrorHelpers, EmitExecuteAsync, EmitLegacyHelpers, EmitSnapshotInternal, etc.).
- Use `$"""..."""` for interpolation and `{{` `}}` to escape literal braces.
- After each method conversion: rebuild PLang.csproj, diff one generated `.g.cs` to confirm zero output drift.
- Don't touch the leaf `Property/*/this.cs` emission methods in this phase unless they're trivially convertible — focus is on `Emission/Action/this.cs`.

**Test.** No new test — equality with the v1-emitted output is the contract. The existing Generator + Matrix suites verify behavior.

---

## Phase D — Trivial cleanup (codeanalyzer findings 2, 3, 6, 9, 21)

These are minutes of work each:

- **F2.** `Discovery/this.cs:134, 192` — drop the dead `OriginalDefinition.Name == "@this"` disjunct.
- **F3.** `Discovery/this.cs:194-197` — extract the triple `prop.Type as INamedTypeSymbol` into a local.
- **F6.** `Discovery/this.cs:44` — `RawScalarPropertyDescriptor` from `public` to `internal`.
- **F9.** Rename `LazyParamsGenerator.cs` content references → `ActionGenerator`. The class file is `this.cs` so the class itself is `@this`; just need to update the test that reads `obj/.../generated/PLang.Generators.LazyParamsGenerator/`. The folder name is auto-derived from the generator type name attribute or the type name itself — need to verify which. If it's the type name, rename the type. If it's an attribute, set it explicitly.
- **F21.** `Emission/Property/Data/this.cs:39` — drop the redundant `({InnerType})` cast for enum defaults (the value already has the cast prefix).

---

## Findings explicitly NOT taken in v2

- **Findings 4, 5** — Discovery's parallel classifiers + 70-line `BuildProperty` cascade. Refactor opportunity, not a bug. Future pass.
- **Findings 13, 15, 16, 17, 18** — Transitional / dead code that Phase 5 cleanup of legacy helpers will sweep. Don't touch the same files twice.
- **Finding 14, 23** — Drop `__app`, simplify Provider lazy-fallback. Touches every generated handler; merits its own focused PR. Future pass.
- **Finding 19** — `sb.AppendLine` → verbatim string template. Big refactor of emission, no behavior change. Future pass.
- **Finding 22** — Split four-branch getter into per-shape methods. Readability only. Future pass.
- **Finding 24** — `SubstitutePrimitive` couples Data to Action. Pre-existing. Marker interface refactor is a separate design discussion.
- **Findings 25, 26** — Typed-fast-path duplication, hand-rolled ToBoolean. Pre-existing. Future cleanup pass.
- **Finding 29** — `As<T>` ignores `_type.Convert` (JSON typed Data). Pre-existing, no current handler hits the case. Document for awareness, no fix.
- **Findings 30, 31, 32, 34, 35** — Either pre-existing or pure readability. Defer.
- **Findings 36, 37, 38** — NIT (doc comment, locals scoping, unused parameter on hook). Skip.

---

## Validation gates

1. `dotnet build PLang.sln` — clean.
2. `dotnet run --project PLang.Tests` — TUnit suite green (currently 2427).
3. `dotnet build PLang.sln` and read `.../obj/Debug/net10.0/generated/PLang.Generators/.../*.g.cs` — verify no `__variables`, no `__paramData`, no `ParamData()` accessor.
4. `dotnet /workspace/plang/PlangConsole/bin/Debug/net10.0/plang.dll --test` — runs to completion (no StackOverflow). Report pass/fail count to Ingi.

## Branching

Stay on `runtime2-generator-obp`. Each phase = one commit:
- `v2 Phase A: cycle detection in Data.AsT_Impl + cycle tests`
- `v2 Phase B: ActionClassInfo record + EquatableArray + delete __variables/__paramData + cache+dead-emission tests`
- `v2 Phase C: App.Run OCE comment + non-generic collection comment + behavioral tests`
- `v2 Phase D: trivial cleanup (Findings 2, 3, 6, 9, 21)`

## Decision points needing Ingi's call

1. **Finding 27 fix style** — thread-static visited-set in `Data` (mirroring `Variables._resolvingVars`) vs route the full-match path through `Resolve`. The thread-static is local and self-contained; the route-through-Resolve is uniform but requires Resolve to handle the full-match case (which today is in `Data.AsT_Impl`). Default: thread-static unless you prefer otherwise.
2. **EquatableArray location** — `PLang.Generators/EquatableArray.cs` (private to the generator) vs reuse one if it already exists somewhere in the repo. Default: new file in `PLang.Generators/`.
3. **Dead-emission test scope** — Phase 5 will delete the legacy helpers (`__Resolve<T>`, `__resolutionError`, `__StripPercent`, `__HasParam`). My `NoDeadEmissionTests` will currently flag `__resolutionError` for v4-shape handlers (which never reassign it). Two options: (a) accept the failure now as a known-transitional pre-Phase-5 issue and skip the test, or (b) make the test exclude `__resolutionError` until Phase 5. Default: (b), with a TODO comment to remove the exemption.

## Estimated complexity

- Phase A: ~30 min (one method change + 3 tests)
- Phase B: ~2 hours (record conversion + EquatableArray + 2 dead-field deletions + Roslyn-driver test + dead-emission test)
- Phase C: ~30 min (one comment + 2 tests + 2 contract tests)
- Phase D: ~30 min (trivial mechanical edits)

Total: ~3.5 hours focused work.

# codeanalyzer v3 — review result

Reviewing coder v3 (commit `c9314c5e`) against codeanalyzer v2's 7 findings.
All 7 verified honestly closed. One new NIT-level dead code finding.

## v2 findings — verification

### #40 (MAJOR) — NoDeadEmissionTests heuristic — **CLOSED**

The v2 heuristic was `reads = total − assignments`, which gave the `__variables` shape `reads=1` and didn't flag it. v3 splits the contract into three orthogonal assertions:

| Pattern | Catches | Verified |
|---|---|---|
| In-file dead field (`HasReadOf`) | `__variables`-shape (decl + 1 LHS, no read) | Simulated arithmetic: `reads=2−1−1=0 → flagged dead`. The 5 heuristic regression tests (`Heuristic_VariablesShape_DeclAndOneLhs_NoRead_IsDead` etc.) pin this independently of the live tree. |
| Cross-file unused public method | `__paramData/ParamData()` shape (read in-file, no callers anywhere) | Scans `PLang/`, `PLang.Tests/`, `PLang.Generators/`, `PlangConsole/` (excluding `obj/` and `bin/`) for `\bMethodName\s*\(`. The orphan `ParamData()` would have zero callers and be flagged. |
| Convention pin (`__` prefix) | Future drift that would silently bypass Pattern A | `EveryGeneratedPrivateFieldUsesDoubleUnderscorePrefix` pins the convention. |

Three subtle correctness points all checked:
- `=(?!=)` correctly rejects `==` from the assignment count (heuristic test pins this).
- `_publicMethodCallerExemptions` (`ExecuteAsync`, `SnapshotParams`) correctly carves out framework-dispatched methods that are called via interface from `PLang/App/this.cs`. Production scan would still find these textually, so the exemption is defensive but harmless.
- `decl_line_occurrences` correctly subtracts the declaration line so `private int __orphan;` registers as `reads=0`.

### #44 (NIT) — NoDeadEmission regex __-only restriction — **CLOSED**

Closed by the convention-pin test (`EveryGeneratedPrivateFieldUsesDoubleUnderscorePrefix`). If a future generator emits a non-`__` private field, the convention test fails before Pattern A could silently miss it.

### #39 (MAJOR) — IncrementalCacheTests drives Roslyn — **CLOSED**

Two pipeline-level tests added (the existing 9 carrier-equality tests stay):

- `PipelineCache_RerunWithUnchangedSyntax_StepOutputsAreCachedOrUnchanged` — drives `CSharpGeneratorDriver` with `trackIncrementalGeneratorSteps:true`, runs twice (with an unrelated tree added on second run to force re-evaluation), asserts every output of the `ActionInfoFiltered` step is `Cached` or `Unchanged`. **Regression to non-record carrier** (the v1 `sealed class` shape) would produce `Modified` for every output and fail this test.
- `PipelineCache_ActionClassChanged_StepOutputIsModified` — negative-space sanity: changing the partial property's type from `Data<string>` to `Data<int>` must produce `Modified`. Catches a vacuously-passing always-Cached test.

Generator side: `ActionInfoTrackingName`/`ActionInfoFilteredTrackingName` constants exposed and `.WithTrackingName(...)` calls inserted at the right pipeline stages. The `MinimalSource` bootstrap (lines 146-209 of `IncrementalCacheTests.cs`) brings up just enough of the App namespace skeleton for the predicate to fire and the emitter to run without referencing the real PLang assembly.

### #41 (MINOR) — Expanding-cycle gap — **CLOSED**

`ResolveDepthLimit = 32` constant + `|| _resolvingValues.Count > ResolveDepthLimit` clause added to `Data.@this.AsT_Impl` (`PLang/App/Data/this.cs:399, 417`).

Verified the unwind logic: when the depth-bound trips, the call returns *before* the try/finally, leaving the just-Add'd entry in the set. The root call's `if (isCycleRoot) _resolvingValues = null;` in finally clears the entire set. Correct under depth-bound trip.

`AsT_ExpandingCycle_DepthBoundReturnsGracefully` pins the contract for `%a%="X-%b%", %b%="Y-%a%"` — asserts the result returns at all (no StackOverflowException) and contains `%` (the bound trips before final substitution). Existing `AsT_DeepChain_5Levels_ResolvesCorrectly` confirms 5 levels is well below the bound.

### #42 (MINOR) — OCE asymmetry — **CLOSED**

`StepRunAsync_CancellationTokenCancelled_LetsOCEPropagate` (`AppRunScaffoldingTests.cs:177-194`) drives a pre-cancelled `CancellationTokenSource` through `Step.RunAsync`. Verified the path:
- `PLang/App/Goals/Goal/Steps/Step/this.cs:152` — `context.CancellationToken.ThrowIfCancellationRequested()` in the foreach throws OCE.
- `Step/this.cs:157` — `catch when (ex is not (… or OperationCanceledException))` excludes OCE, lets it propagate.

`Assert.That(...).ThrowsExactly<OperationCanceledException>()` pins the contract. Together with the existing `AppRun_HandlerThrowsOCE_TranslatesToServiceError_DoesNotPropagate` on the App.Run side, both halves of the asymmetry are now pinned — a future "consistency fix" cannot silently break `timeout.after` or cancellation cascading.

### #43 (MINOR) — Cycle test value assertions — **CLOSED**

3 cycle tests upgraded from `IsNotNull()` to specific `result.Value` assertions:

```csharp
// AsT_CyclicVarReference_ReturnsCycleBrokenRawString
await Assert.That(result.Value).IsEqualTo("%a%");

// AsT_SelfReferencingVar_ReturnsRawTemplate
await Assert.That(result.Value).IsEqualTo("%x%");

// AsT_PartialMatchSelfReference_ReturnsUnsubstitutedInterpolation
await Assert.That(result.Value).IsEqualTo("hello %x%");
```

A "fix" that returned `null` or empty string instead of the cycle-broken raw would fail all three. The new `AsT_ExpandingCycle_DepthBoundReturnsGracefully` correctly stays at `IsNotNull` + `Contains('%')` because the exact stop string at depth 32 is not predictable — that's the right looseness for that case.

### #45 (NIT) — Diagnostic location span — **CLOSED**

`DiagnosticInfo` widened from `(PropertyName, ClassName, FilePath, Line, Character)` to `(PropertyName, ClassName, FilePath, StartLine, StartCharacter, EndLine, EndCharacter)`. Discovery captures `loc.GetLineSpan().EndLinePosition`. The orchestrator constructs a real `LinePositionSpan` instead of synthesizing `(line, char + 1)`.

`RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier` drives the generator with a real `path: "BadHandler.cs"` (the orchestrator falls back to `Location.None` when FilePath is empty — the test's path argument is load-bearing) and asserts `spanWidth > 1`. With the v2 synthetic `+1`-char span the assertion would fail.

The `IPropertySymbol.Locations[0]` for `public partial int RawIntProperty` covers the identifier `RawIntProperty` (14 chars). The test's `> 1` lower bound is intentionally lax — anything ≥ 2 catches the regression.

## New v3 findings

### Finding 46 — `ActionInfoTrackingName` (unfiltered) constant + WithTrackingName call are dead

**File:** `PLang.Generators/this.cs:20, 29`

```csharp
public const string ActionInfoTrackingName = "ActionInfo";          // line 20
...
.WithTrackingName(ActionInfoTrackingName)                            // line 29
```

`ActionInfoFilteredTrackingName` (line 21) is used by both pipeline tests via `result.TrackedSteps[ActionInfoFilteredTrackingName]`. `ActionInfoTrackingName` (the unfiltered variant) is referenced **only** at the WithTrackingName call site — no test reads it.

Deletion test: removing line 20 + the WithTrackingName(ActionInfoTrackingName) call on line 29 would leave both pipeline-cache tests passing, the carrier tests passing, and production behaviour unchanged (`trackIncrementalGeneratorSteps` is false in production — tracking names are no-ops there).

**Severity: NIT.** The pre-filter step's tracking name is exposed for symmetry with the post-filter one but earns no test. Either delete it, or add a test that asserts the unfiltered step also reports Cached/Unchanged on rerun (which would catch a regression *before* the Where-filter, e.g., transform-step instability that doesn't propagate past Where because of value-equal ActionClassInfo). The latter would actually be a stronger contract test.

## Production-code 5-pass on v3 deltas

Three production deltas: `ResolveDepthLimit = 32` (Data), tracking names + WithTrackingName (Generators), DiagnosticInfo widening (Discovery).

- **OBP Compliance** — clean. No new outside-class iteration; constants and record fields are added in place.
- **Simplification** — clean. Cycle check `(!Add || Count > Limit)` is dense but commented; can't tighten without losing clarity.
- **Readability** — clean. Comment block above `ResolveDepthLimit` explains the *why* (HashSet-alone misses expanding chains) and notes the 5-level chain test as the "well below the limit" anchor — exactly the kind of comment that earns its place.
- **Behavioral Reasoning** — `isCycleRoot` is load-bearing for the depth-bound case (the early-return path leaves the trigger string in the set; only root-level finally clears the entire set). Removing that line would create cross-call leakage. The current code has it; the contract is robust.
- **Deletion Test** — every line earns its place EXCEPT the unfiltered tracking name (Finding 46). Specifically:
  - `ResolveDepthLimit = 32` deletion → `AsT_ExpandingCycle_DepthBoundReturnsGracefully` StackOverflows.
  - `WithTrackingName(ActionInfoFilteredTrackingName)` deletion → `PipelineCache_RerunWithUnchangedSyntax` `ContainsKey` assertion fails.
  - `DiagnosticInfo` widening deletion → `RawScalarProperty_DiagnosticLocation_UnderlinesIdentifier` `spanWidth > 1` fails.
  - `Microsoft.CodeAnalysis.CSharp 4.13.0` PackageReference deletion → IncrementalCacheTests fails to compile.

## Verdict: CLEAN

All 7 v2 findings honestly closed. The toothlessness gap Ingi flagged in v1 is genuinely resolved — the regression-prevention layer is now real. One NIT (Finding 46, dead unfiltered tracking name) — leave it for cleanup or upgrade it into an additional cache test at coder's discretion; not blocking.

C# tests: 2456/2456 green (locally re-run). Production code on the deltas is clean.

## Suggested next step

**Tester pass on `plang --test`.** The v3 summary reports 169 pass / 48 fail / 5 stale — the 6 additional fails since v2 are claimed pre-existing infrastructure. None tied to v3 production additions per coder spot-check. A tester pass to triage which were broken before vs. surface for the first time in v3 is still worthwhile. Production code itself: clean, ship.

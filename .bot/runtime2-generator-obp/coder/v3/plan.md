# Coder v3 plan — close codeanalyzer v2 findings

Ingi waived the plan/approval step ("you knife what to fix go for it"). Plan kept brief here for the report.

## Phase 1 — `NoDeadEmissionTests` (Finding #40, #44)

**In-file heuristic.** Replace the broken `reads = occurrences − assignments` with `reads = occurrences − assignments − decl_line_occurrences`. Verified by simulation: `__variables` becomes `2 − 1 − 1 = 0` (flagged); `__paramData` becomes `4 − 2 − 1 = 1` (still not flagged in-file — need cross-file scan).

**Cross-file accessor scan.** For each generated file, find all `public … FooName(...)` methods/accessors and scan `PLang/`, `PLang.Tests/`, `PlangConsole/`, `os/` source tree for callers. If zero callers, accessor + any `__field` it solely exposes is dead. Catches `__paramData`-class bugs.

**Generalize the regex (#44).** Drop the `__` restriction; either flag any private field with no reads, or add a separate assertion that every emitted private field is `__`-prefixed (pin the convention).

## Phase 2 — `IncrementalCacheTests` real Roslyn test (Finding #39)

Add a `CSharpGeneratorDriver` cache-hit test using `trackIncrementalGeneratorSteps: true`. One inline source compilation, run generator twice. On second run, assert key tracked steps return `IncrementalStepRunReason.Cached`. Keep existing 9 unit-equality tests as the carrier-level contract, the new test pins the pipeline-level contract.

## Phase 3 — `StepRunAsync_HandlerThrowsOCE_LetsItPropagate` (Finding #42)

Add the symmetry test to `AppRunScaffoldingTests.cs` (or the appropriate Step test file). Reuses the existing `OceThrowingHandler` fixture. Asserts that calling `Step.RunAsync` directly with a handler that throws `OperationCanceledException` lets it propagate (not translated to ServiceError). Pins the asymmetry's other direction.

## Phase 4 — Cycle test value assertions (Finding #43)

Strengthen 3 of 4 cycle tests in `DataAsTResolutionTests.cs` from `IsNotNull` to specific value assertions on `result.Value`. Model on `AsT_DeepChain_5Levels_ResolvesCorrectly`.

## Phase 5 — Expanding-cycle depth bound (Finding #41)

Add a depth bound to `_resolvingValues` in `Data.AsT_Impl`: `if (_resolvingValues.Count >= MAX_DEPTH) return ConvertAndWrap<T>(strVal, ctx);`. Pick MAX_DEPTH = 32 (well past any legitimate expansion). Add test for `%a% = "X-%b%"`, `%b% = "Y-%a%"` shape.

## Phase 6 — Diagnostic location span (Finding #45 / v1 Finding 7)

Widen `DiagnosticInfo` to carry `EndLine` + `EndCharacter`. Discovery captures the full `loc.GetLineSpan().EndLinePosition`. Orchestrator uses the full span when reconstructing `Location.Create`. IDE squiggle now underlines the identifier instead of pointing at one column.

## Validation

- `dotnet build PLang.sln` clean
- `dotnet run --project PLang.Tests` — full TUnit suite green
- Targeted run of new/modified tests
- Manual sanity check: simulate `__variables`-shape regression in a generated file and confirm `NoDeadEmissionTests` flags it
- Manual sanity check: introduce a small deliberate change in a generator step and confirm `IncrementalCacheTests` cache-hit assertion fails on that run

## Out of scope

The 22 v1 findings deferred in v2 (4, 5, 13–18, 20, 22–26, 29–38) stay deferred — same rationale.

# codeanalyzer v1 — runtime2-generator-obp

## Task

Review the Phase 0–6 implementation of the v4 architect plan: **resolution lives in `Data.As<T>(context)`, `Data.Value` is raw, generator restructured into Discovery + Emission hierarchy, `App.Run` owns scaffolding, build-time PLNG001 diagnostic enforces Data<T>/Provider/[VariableName] property kinds.**

Look for OBP violations, simplifications, readability issues, behavioral fragility (Pass 4), and dead code (Pass 5).

## Files in scope (the changes)

### Generator (new hierarchy)
- `PLang.Generators/this.cs` (47 lines — orchestration)
- `PLang.Generators/IsExternalInit.cs` (4 lines — netstandard polyfill)
- `PLang.Generators/Discovery/this.cs` (335 lines — predicate + GetActionClassInfo + property factory + PLNG001 descriptor + ActionClassInfo + DiagnosticInfo + RawScalarValidation records)
- `PLang.Generators/Emission/Action/this.cs` (282 lines — per-handler emission, ExecuteAsync skeleton, snapshot)
- `PLang.Generators/Emission/Property/this.cs` (29 lines — abstract record base)
- `PLang.Generators/Emission/Property/Data/this.cs` (69 lines — DataProperty)
- `PLang.Generators/Emission/Property/Provider/this.cs` (33 lines — ProviderProperty)
- `PLang.Generators/Emission/Property/Legacy/this.cs` (91 lines — LegacyScalarProperty, transitional)
- **Deleted:** `PLang.Generators/LazyParamsGenerator.cs` (779 lines)

### Runtime
- `PLang/App/Data/this.cs` — `As<T>(context)` rewrite, `Value` becomes raw, `_resolved/_rawValue/NeedsResolution/ResetResolution/IsDeferredActionTemplate` deleted, `WalkList`/`WalkDict`/`SubstitutePrimitive`/`IsActionDestination`/`ConvertAndWrap` introduced
- `PLang/App/this.cs` — `App.Run(action, context)` gained the scaffolding (callstack push/pop, save/restore Step/Goal/Event, try/catch/finally, ServiceError translation, frame.SnapshotVariables)
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — added `Action.GetParameter(name, context)`
- `PLang/App/modules/ICodeGenerated.cs` — added `SnapshotParams()` interface method (default impl)
- `PLang/App/Debug/this.cs` — dropped `ResolveTrace`/`OnResolveTrace`/`NeedsResolution` references; LLM file mode preserved
- `PLang/App/Variables/this.cs` — deleted `ResolveDeep` and supporting state (`_resolveDepth`/`_resolveItemCount`/`MaxResolveItems`/`OnResolveTrace`)

### Tests (regression matrix)
- 28 matrix files + `MatrixRunner` fixture under `PLang.Tests/Generator/Matrix/` and `PLang.Tests/App/Fixtures/`
- New tests under `PLang.Tests/App/DataTests/` (split from `PLang.Tests/App/Memory/`)
- `PLang.Tests/App/AppRunScaffoldingTests.cs` — 181 lines of scaffolding contract tests
- `PLang.Tests/App/GetParameterTests.cs` — 128 lines
- `PLang.Tests/Generator/GeneratorValidationTests.cs` — 186 lines exercising PLNG001
- `PLang.Tests/Generator/SnapshotParamsTests.cs` — 124 lines

## Approach (the 5 passes)

I will work through the 5 passes from my character spec in order. **Tests are not in scope** as production code — they're the regression contract. I'll only flag if a matrix test contradicts what I see in production.

1. **Pass 1 — OBP Compliance.** Walk every changed production file against the 6 OBP rules (`/PLang/App/CLAUDE.md`). The big surface is `Data.As<T>` — does it leak knowledge about `Action.@this` or about `Variables` through reflection or container detection that should belong in those owners? Does `App.Run` do too much? Are the generator records true value-equal records or do they leak Roslyn symbols (cache poisoning)?
2. **Pass 2 — Simplification.** Look for dead abstractions, over-parameterized methods, redundant null checks, repeated string-based name comparisons, premature generalization. Specifically: the resolution state in the generated `ExecuteAsync` (`__action`/`__variables`/`__app`/`__resolutionError`) — are all four needed, or is some dead since v4? The `__Resolve<T>`/`__StripPercent`/`__HasParam` legacy helpers — still needed by emission, or pruneable? `EmitDataAndErrorHelpers` — does the per-class emitted helper pair really earn its place over a runtime utility?
3. **Pass 3 — Readability.** Method length, naming, flow clarity. `As<T>` recursion through `AsT_Impl` and `ConvertAndWrap` is the hot spot. The Discovery factory's `BuildProperty` is doing a lot — does the order of attribute checks read cleanly?
4. **Pass 4 — Behavioral reasoning.** Trace data origins:
   - `Data.As<T>` walking lists/dicts: does it match the rehydration shape? Does it walk `IList<object?>` / `IDictionary<string, object?>` correctly, or does an `IList` (non-generic) slip past the type check? Does the action-destination guard cover all the shapes that `IsDeferredActionTemplate` used to cover?
   - `App.Run` catch path: does the `NullReferenceException or OutOfMemoryException or StackOverflowException` carve-out match the user's other catch sites (consistency)? Does `Step.RunAsync`'s reduced catch surface still cover what it needs to? Does `__SnapshotParams()` get called when handler exception happens *before* the handler instance is known (e.g., handler resolution fails)?
   - `Discovery.IsValidActionProperty` — does it correctly identify Data<T> when the type is referenced via aliases (`global::App.Data.@this<X>`) or short form? Does it correctly identify `[Provider]` vs derived-attribute classes?
   - `Backing field reset` in generated ExecuteAsync — does it run when `action == null` (direct C# composition path)? Architect's plan said skip; current code skips. Is the snapshot logic compatible with skipped reset?
5. **Pass 5 — Deletion test.** For every block of generator emission, ask "what fails if this block isn't emitted?" Hot candidates:
   - `__variables` / `__app` fields — `__variables` may be a leftover from pre-v4, since `Context.Variables` is reachable. `__app` is set but `app` local is also set — duplication?
   - `EmitDataAndErrorHelpers` — does any matrix test fail if this is removed?
   - `__StripPercent` legacy helper — only emitted to support `[VariableName]` properties, which are explicitly transitional. Is the helper unconditionally emitted even when the class has none?
   - `__resolutionError` field — when does anything assign to it? (only legacy `__Resolve<T>`).

## Output

- `.bot/runtime2-generator-obp/codeanalyzer/v1/result.md` — full per-file findings, organized by file with OBP / Simplifications / Readability / Behavioral / Deletion sections.
- `.bot/runtime2-generator-obp/codeanalyzer/v1/summary.md` — version summary (what I reviewed, top findings, verdict).
- `.bot/runtime2-generator-obp/codeanalyzer/v1/verdict.json` — `{ "status": "pass" | "fail", "summary": "..." }`.
- Update `.bot/runtime2-generator-obp/codeanalyzer/summary.md` (cross-version index).
- Update `.bot/runtime2-generator-obp/report.json` with `before`, `plan`, `actions`, `after`.

## Out of scope

- Test code quality (matrix is regression contract).
- The `[VariableName]` deletion deferral — coder logged it; architect can decide.
- New features or refactors beyond what the v4 plan delivered.

## Open questions / blockers

None — files are present, build was reported green by coder.

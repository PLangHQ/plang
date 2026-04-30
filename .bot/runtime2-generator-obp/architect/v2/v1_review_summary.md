# v1 review — fresh-eyes summary

## What v1 proposed

Round 1 of an OBP refactor of `LazyParamsGenerator`: extract the 11-way per-property `if/else` ladder into a polymorphic `ActionProperty` hierarchy under `Emission/Property/{kind}/this.cs`. Markers, `ExecuteAsync`, and helpers stay procedural. Hard promise: byte-for-byte identical `.g.cs` output, verified by golden-file diff. Round 2 (contract simplification) deferred as a placeholder.

## What the fresh-eyes review surfaced

Three concerns:

**1. OBP folder convention in a generator project — earned or cosmetic?**
PLang.Generators isn't part of the App object graph. Its inputs are `IPropertySymbol`, its output is a string. Nobody navigates `Discovery.Emission.Property.Provider.@this`. The thirteen-folder layout looked like importing OBP folder shape into a project that isn't navigated.

**Resolved.** Ingi confirmed directory-as-API is a project-wide commitment for LLM-driven maintenance: `ls` should be the documentation across the whole codebase, including internal generators. The thirteen-folder layout is consistent, not cosmetic. Concern dissolves.

**2. v1 is the second-most-impactful work.**
The deferred round 2 — collapse all property getters through a single `__Resolve(name, ParamKind)` runtime dispatcher, move helpers to a shared base, simplify the runtime contract — is the bigger lever. v1's structural refactor is in service of a shape that round 2 partially undoes: round 1 distributes a 20-line `Emit()` per kind across 10 records, then round 2 strips each one to a one-line resolution call. Two rounds, with rework in between.

**Still holds.** v2 combines the structural extraction with the contract simplification so the property hierarchy is born in its final shape — small records carrying `IsMatch` + `BuildResolveCall` + optional emission helpers — instead of being built large and then shrunk.

**3. Per-property logic splits across more sites than v1 acknowledged.**
v1's framing was "extract the per-property `if/else` into subclasses." Reading `GenerateActionCode` carefully, per-property logic actually iterates `info.Properties` in five-to-six places: the partial property impl, the `ExecuteAsync` reset, provider resolution, `IEvent` → `context.Event`, non-null validation, `[IsNotNull]` validation. v1 only relocates one of these into subclasses; the other four–five stay inline in `Emission/Action/this.cs`.

**Still holds.** v2 lets each subclass own its full per-property responsibility via optional emission methods — `EmitProviderInit()`, `EmitValidation()`, `EmitEventCheck()` — so per-kind cohesion lands on the subclass, not just for the getter.

## What changed in v2

- **Combined plan.** Structural extraction + contract simplification in one round. Hierarchy built once in its final shape; helpers move to `App/modules/ActionBase/this.cs`; `ParamKind` enum drives runtime dispatch; per-class generated code shrinks from ~200 lines to ~50 lines.
- **Regression contract changes.** Byte-for-byte golden-file diff is gone (the contract intentionally changes). Replaced by a runtime test matrix covering every `ParamKind` × {nullability, default, type-class} combination — 15–20 minimal test action handlers exercising the kind matrix end-to-end.
- **Trade-off accepted.** Bigger PR, harder upfront test investment, but no churn between rounds and the durable shape lands first time.

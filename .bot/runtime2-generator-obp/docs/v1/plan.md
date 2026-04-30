# docs v1 — runtime2-generator-obp

## Context

Branch landed the v4 plan: source generator restructured into OBP shape (Discovery + Emission/Action + Emission/Property/{Data,Provider,Legacy}), `Data.As<T>(context)` became the v4 resolution entry point with cycle protection (ThreadStatic HashSet) and depth bound (32), the `PLNG001` build-time diagnostic enforces Data<T>/[Provider]/[VariableName] property kinds, and v5+v6 added the `[Sensitive]` snapshot masking + `ServiceError` cycle/depth contract.

Reviewer chain: codeanalyzer/v3 CLEAN → tester/v4 APPROVED → security/v1 PASS → auditor/v1 PASS. Coder/v6 closed auditor finding #1 (Data<T> emission missing the post-Run __resolutionError check) and nits #2/#3 (sensitive snapshot test naming + null-vs-redacted distinction). Final: 2471/2471 C# green.

No prior docs work on this branch. Two CLAUDE.md proposals on file (test-designer/v1, coder/v1).

## Documentation gaps identified

### Architecture / design docs (`Documentation/v0.2/`)
- **`architecture.md`** — "Modules (Action Registry)" section's Source Generator paragraph is one sentence. Doesn't mention the v4 OBP shape (Discovery → ActionClassInfo → Emission/Action emits, with per-property EmitProperty/EmitSnapshotEntry on three leaves). Doesn't mention PLNG001.
- **`data-generic-design.md`** — describes the v4 design but pre-dates the cycle/depth ServiceError contract (v5) and the `__resolutionError` capture pattern (v6). Currently the doc shows `As<T>` as a simple two-line conversion. Reality is 100 lines with cycle protection, depth bound, action-destination carve-out, container walking. Add a section on resolution semantics + ServiceError contract.

### `Documentation/v0.2/good_to_know.md`
Add cross-cutting entries that span multiple files (per memory: this is the right place for those):
- **Data.As<T> cycle/depth ServiceError contract** — explain the two error keys (`VariableResolutionCycle`, `ResolveDepthExceeded`), why a HashSet alone misses expanding cycles (each level produces a new string), the dual-capture pattern that handlers depend on (Data<T> getter sets `__resolutionError` + post-Run check in ExecuteAsync). This is the v6 fix; future devs touching property emission need to know both halves are load-bearing.
- **PLNG001 build-time diagnostic** — what's allowed in action property positions (`Data<T>`, `Data`, `[Provider]`, `[VariableName] string`), where it's enforced (`Discovery.GetActionClassInfo`), and what it intentionally rejects (raw `partial string`, `partial int`, etc.).
- **`[Sensitive]` in ParamSnapshot** — the masking convention for `PrValue` + `FinalValue` matches `SensitivePropertyFilter` in JSON serialization. Document the null-guard distinction (accessed-and-null vs accessed-and-redacted) added in v6 nit #3.
- **Action.GetParameter — pure lookup** — Parameters first, Defaults fallback, NotFound when missing. No resolution side effects (resolution lives in `Data.As<T>(context)`).
- **Source generator OBP shape** — Discovery as the Roslyn boundary, Emission/Action as the per-handler emitter, Emission/Property leaves as polymorphic per-property emission. EquatableArray<T> + record-shaped ActionClassInfo for incremental cache stability. Why this matters: without value equality, the Roslyn cache misses on every recompile and emission re-runs unnecessarily.

### Root `CLAUDE.md`
Stale references this branch invalidates:
- Line 13: `Lazy params: Source generator creates *__Generated records resolving %var% at property access` — STALE. The generator no longer creates separate `*__Generated` records; it emits `partial class` extensions on the action record itself (this was always slightly wrong; v4 makes it actively misleading).
- Line 36: `PLang.Generators/LazyParamsGenerator.cs — source generator for lazy param resolution` — file is gone; entry point is `PLang.Generators/this.cs` with Discovery + Emission/Action + Emission/Property under it.

### XML docs / public surface
Spot-checked the new public/protected types — all have meaningful XML on `Action.GetParameter`, `ICodeGenerated.SnapshotParams` (default-impl interface method), `ParamSnapshot`, `EquatableArray<T>`, `Discovery.@this`, `Emission.Action.@this`, `Emission.Property.{Data,Legacy,Provider}.@this`. Not adding new XML.

### CHANGELOG
No project-level CHANGELOG file exists (verified). Per character spec, "user-visible changes need a CHANGELOG entry in v<N>/result.md". This branch's user-visible changes (PLNG001 build error, the two new ServiceError keys) go in `result.md`.

### PLang user-facing docs (`docs/`)
The generator refactor is invisible to PLang developers writing `.goal` files — they don't see `Data<T>` properties or the generator output. No website-doc updates needed. The two new ServiceError keys (`VariableResolutionCycle`, `ResolveDepthExceeded`) bubble up as standard errors via the existing error-handling mechanism, no new user-facing surface.

## CLAUDE.md proposal decisions

| From | Target | Decision | Reason |
|------|--------|----------|--------|
| test-designer/v1 — folder/namespace clash with global type aliases | `/PLang.Tests/CLAUDE.md` | **applied** (folded into root) | Genuinely canonical: any future test folder mirroring `PLang/App/Data/` or `PLang/App/Variables/` will hit the same `CS0118 'Data' is a namespace but is used like a type` shadow. But `PLang.Tests/CLAUDE.md` doesn't exist. Folding the rule into root CLAUDE.md (under Source Generator → test alias clashes) keeps the tree flat and is in scope of all callers. |
| coder/v1 — Property kinds (v4) | `/PLang/App/CLAUDE.md` | **applied** (folded into root) | Genuinely canonical: the PLNG001 gate enforces this at build time. Future handler authors need to see this BEFORE they reach for a `partial string`. Same target-file-doesn't-exist reasoning as above; root is the single canonical CLAUDE.md and the existing "Source Generator" section is the right place. |

Both rules are added to the existing root `CLAUDE.md` "Source Generator" section. Not creating new per-folder CLAUDE.md files — that would scatter canonical guidance and the rules apply across the whole tree.

## Plan of work (in order)

1. **`Documentation/v0.2/architecture.md`** — expand the "Modules (Action Registry) → Discovery → Handler pattern" subsection with the v4 OBP shape (Discovery / Emission / per-property leaves) + PLNG001.
2. **`Documentation/v0.2/data-generic-design.md`** — append a "Resolution semantics — cycle, depth, error contract" section reflecting the v5+v6 contract.
3. **`Documentation/v0.2/good_to_know.md`** — add 5 cross-cutting entries listed above.
4. **Root `CLAUDE.md`** — replace the stale "Lazy params" + "LazyParamsGenerator.cs" lines, add the two CLAUDE.md proposal contents under "Source Generator".
5. **`.bot/runtime2-generator-obp/docs/v1/result.md`** — CHANGELOG-style entry for user-visible surface changes (PLNG001 build error, two new ServiceError keys).
6. **`docs-report.json`** + **`verdict.json`** — final reports.
7. **`v1/summary.md`** + bot root **`summary.md`**.
8. Commit + push.

## Open questions
None.

# docs v1 — runtime2-generator-obp

## What this is

The first docs pass on `runtime2-generator-obp`, after auditor/v1 (PASS, 1 minor + 3 nit). The branch landed the v4-plan source-generator refactor plus four review-response rounds. My job: fill documentation gaps, decide on the two CLAUDE.md proposals, and gate the merge.

The v4 plan was a structural change with thin behavioural change — but v5 (security response: `[Sensitive]` masking + cycle/depth → `ServiceError`) and v6 (auditor response: dual-capture pattern + null-guard distinction) added a runtime contract that the existing architecture docs don't describe. That's the gap I filled.

## What was done

### CLAUDE.md proposal decisions (both applied, both folded into root)

| From | Target | Decision |
|------|--------|----------|
| test-designer/v1 — namespace clash with global type aliases | `/PLang.Tests/CLAUDE.md` | applied to root |
| coder/v1 — Action property kinds (v4) | `/PLang/App/CLAUDE.md` | applied to root |

Both proposals are genuinely canonical (the namespace-alias rule applies to all future test folders mirroring `App/Data/`-shaped sources; the property-kinds rule is enforced at build time by PLNG001). But neither target file exists, and creating them just to hold one rule each scatters the canonical guidance. Folded both into root `CLAUDE.md` under the existing "Source Generator" section.

### Documentation gaps filled

1. **`Documentation/v0.2/architecture.md`** — expanded the Modules > Source Generator subsection from one paragraph to four: handler pattern, source-generator OBP shape (Discovery + Emission tree), property kinds with PLNG001 table, and the `ICodeGenerated` interface including `SnapshotParams` default-impl. Replaced the stale "creates the ExecuteAsync bridge" wording with the v4-correct partial-class shape.

2. **`Documentation/v0.2/data-generic-design.md`** — appended a "Resolution semantics" section covering the cycle/depth `ServiceError` contract and, critically, the **dual-capture pattern**: the Data<T> getter sets `__resolutionError` *and* `ExecuteAsync` checks it after Run() — both halves are load-bearing, removing either re-introduces the silent-default bug. Auditor's first attempt at the v6 fix was getter-only and would have been dead code.

3. **`Documentation/v0.2/good_to_know.md`** — added five cross-cutting entries:
   - Source generator OBP shape + EquatableArray incremental-cache pattern + test alias-clash convention (consolidates the test-designer proposal)
   - PLNG001 build-time gate + property kinds (consolidates the coder proposal)
   - `Data.As<T>` cycle/depth ServiceError contract + dual-capture pattern with code example
   - `[Sensitive]` masking in ParamSnapshot with the v6 null-vs-redacted distinction
   - `Action.GetParameter` pure-lookup + `ICodeGenerated.SnapshotParams` default-impl conventions

4. **`CLAUDE.md`** (root) — replaced the stale "*__Generated records" line and the "LazyParamsGenerator.cs" path. Added the PLNG001 + EquatableArray + namespace-alias-clash content from the two proposals.

### CHANGELOG (in `result.md`)

User-visible surface changes captured in `v1/result.md`:
- New `PLNG001` build error
- Two new `ServiceError` keys (`VariableResolutionCycle`, `ResolveDepthExceeded`) — and the behaviour change: pre-v6, cycle/depth trips returned the unresolved string; post-v6, they return `Data.FromError`
- `[Sensitive]` masking in `Error.Params` (`PrValue`/`FinalValue`)
- `ICodeGenerated.SnapshotParams()` default-impl interface method
- File path changes: `LazyParamsGenerator.cs` → `Generators/this.cs` tree

### XML docs

Spot-checked the new public/protected types — coder's existing XML on `Action.GetParameter`, `ICodeGenerated.SnapshotParams`, `ParamSnapshot`, `EquatableArray<T>`, `Discovery.@this`, `Emission.Action.@this`, and the three `Emission.Property.{Data,Provider,Legacy}.@this` records is meaningful (covers what + why, not just restating the name). Added none.

### What I did NOT touch

- **Website docs (`docs/`)** — the generator refactor is invisible to PLang developers writing `.goal` files. The two new `ServiceError` keys bubble through existing `error.handle` modifiers; no new user-facing API.
- **PLang `.goal` examples** — flagged as `flagged-for-tester` in `findings`. Tester writes PLang tests, not docs.
- **Stale `Runtime2` paths in root CLAUDE.md** — the codebase moved from `PLang/Runtime2/` to `PLang/App/` on a prior branch; CLAUDE.md still references `Runtime2/`. Out of scope for this branch — separate cleanup.

## Code example — the dual-capture pattern (most subtle thing I documented)

This is the v6 fix to auditor finding #1, which I documented in both `data-generic-design.md` and `good_to_know.md`. Future devs touching property emission need both halves visible, or they'll regress.

```csharp
// (1) In each Data<T> getter — capture the error AS the property is touched:
get {
    if (__Body_backing == null) {
        __Body_backing = __ResolveData("body").As<string>(Context);
        if (!__Body_backing.Success) __resolutionError = __Body_backing;
        __Body_set = true;
    }
    return __Body_backing!;
}

// (2) In ExecuteAsync — surface AFTER Run() completes:
if (__resolutionError != null) return __resolutionError;
var __runResult = await Run();
if (__resolutionError != null) return __resolutionError;
return __runResult;
```

Without (2), a `Run() => Body` style handler would mask the error because the FromError-Data IS the result. Without (1), the post-Run check has nothing to surface — Data<T> getters fire DURING Run, not before.

## Verdict

**PASS.** Build clean post-edits (0 errors). The code is ready to merge.

## Hand-off

- One `findings` entry (minor) flagged for tester: PLang `.goal` example showing `on error key VariableResolutionCycle`. Out of scope for docs; tester decides whether to write the test or skip.
- `Documentation/Runtime2/todos.md` 2026-04-30 entry on `[VariableName]` migration is the architect's hand-off for the next branch — left as-is, no docs change needed.

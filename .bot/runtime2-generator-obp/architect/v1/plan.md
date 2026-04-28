# v1 — LazyParamsGenerator OBP Refactor (Round 1)

## What this is

`PLang.Generators/LazyParamsGenerator.cs` is one ~780-line file with two procedural mega-methods (`GetActionClassInfo`, `GenerateActionCode`). The internal organization has become chaotic: an 11-way `if/else` ladder per property, a 120-line `ExecuteAsync` emission script, and helper boilerplate emitted into every generated class. We're restructuring the generator's *internal* shape into OBP — without changing what it emits.

This is round 1 only. It targets the single largest source of chaos: the per-property emission. Markers, `ExecuteAsync`, helpers, and snapshot stay procedural this round.

## The hard promise

**Byte-for-byte identical `.g.cs` output before vs. after the refactor.** This is the regression contract. We pin the output of the current generator on a representative set of action classes, refactor, rebuild, diff. Empty diff = success. Non-empty diff = bug, fix before merging.

## Scope — round 1

**In scope:**
- Extract the 11-way per-property `if/else` ladder in `GenerateActionCode` into a polymorphic `ActionProperty` hierarchy under `Emission/Property/`.
- Move the `ActionPropertyInfo` factory logic (currently inline in `GetActionClassInfo`) into a discrimination factory that picks the right subclass.
- Top-level generator file shrinks to orchestration: discovery → emission → output.

**Out of scope (deferred to later rounds):**
- Marker auto-injection (`IContext`, `IChannel`, `IAction`, `IStep`, `IStatic`) — stays as inline `if/else` for now. Five branches, simple, low value to extract.
- `ExecuteAsync` emission — stays one procedural method. Cross-cutting state makes splitting into "segments" cosmetic.
- Helper emission (`__Resolve`, `__ResolveData`, `__TryConvert`, `__StripPercent`, `__FormatValue`, `__HasParam`, `__SnapshotParams`) — stays one block. Pure boilerplate, no per-property variation.
- Cleanup of the *emitted* code shape (e.g. unifying property getters through a single runtime `__Resolve(name, kind)` dispatcher). Separate conversation, separate round.

## End shape — round 1

```
PLang.Generators/
  this.cs                                  // LazyParamsGenerator (IIncrementalGenerator) — orchestrates
  Discovery/
    this.cs                                // ActionDiscovery — Roslyn predicate + symbol → ActionEmission
  Emission/
    Action/this.cs                         // ActionEmission — root, owns Properties + emits the partial class shell
    Properties/
      this.cs                              // ActionProperties : List<ActionProperty>
      Property/
        this.cs                            // ActionProperty — abstract base record
        Provider/this.cs                   // ProviderProperty
        DataWrapped/this.cs                // DataWrappedProperty (Data<T>)
        PlainData/this.cs                  // PlainDataProperty (Data.@this with no generic arg)
        Resolvable/this.cs                 // ResolvableProperty (type has static Resolve(string, Context))
        VariableName/this.cs               // VariableNameProperty ([VariableName])
        Defaulted/
          Value/this.cs                    // DefaultedValueProperty (value type with [Default])
          Reference/this.cs                // DefaultedReferenceProperty (ref type with [Default])
        Nullable/this.cs                   // NullableProperty
        Value/this.cs                      // ValueProperty (value type, no default)
        Reference/this.cs                  // ReferenceProperty (reference type, no default — final fallback)
```

Naming convention: `@this` per folder, matches Runtime2. `LangVersion=latest` is already set on the csproj, so records-with-inheritance work natively.

## Property hierarchy contract

Each `ActionProperty` subclass:

- Is a `public sealed record` deriving from `ActionProperty` (abstract base).
- Carries the data it needs: `Name`, `TypeName`, `IsNullable`, `IsValueType`, plus subclass-specific fields (e.g. `DefaultValue` on `Defaulted/*`, inner type on `DataWrapped`).
- Implements one abstract method: `void Emit(EmissionContext ctx)` where `EmissionContext` is a thin wrapper holding the `StringBuilder` and any cross-property state needed (e.g. flag for whether the class implements `IContext`, since the `Provider` getter expression branches on that today).
- Knows nothing about other property kinds — emission is local.

The factory (`ActionDiscovery`) picks the subclass once per property, in the same priority order the current `GenerateActionCode` walks:

1. `[Provider]` → `ProviderProperty`
2. `Data<T>` (generic) → `DataWrappedProperty`
3. `Data.@this` (non-generic) → `PlainDataProperty`
4. Has static `Resolve(string, Context)` → `ResolvableProperty`
5. `[VariableName]` → `VariableNameProperty`
6. value-type with `[Default]` → `Defaulted/Value`
7. reference-type with `[Default]` → `Defaulted/Reference`
8. nullable annotation → `NullableProperty`
9. value type → `ValueProperty`
10. reference (final fallback) → `ReferenceProperty`

This priority order **must** match today's exactly — that's part of the byte-for-byte promise.

## Phases

### Phase 0 — Regression harness (do first, before any refactor)

The whole refactor depends on a tight before/after diff. Without a harness the promise is unverifiable.

1. From a clean `runtime2-generator-obp` checkout, build the solution: `dotnet build` (or `plang p build` — whichever exercises the generator on the most action handlers).
2. Locate the generator's emitted files: `obj/Debug/net10.0/generated/PLang.Generators/PLang.Generators.LazyParamsGenerator/*.g.cs`.
3. Copy them to `.bot/runtime2-generator-obp/architect/v1/golden/` as the pinned baseline.
4. Document the exact build command in `golden/HOW_TO_REGENERATE.md` so anyone can rerun it.

This is mechanical but non-negotiable. The harness defines what "no regression" means.

### Phase 1 — Property hierarchy

1. Create the folder structure under `PLang.Generators/Emission/Property/` (10 leaves, abstract base, smart collection).
2. Pull the per-property emission logic out of `GenerateActionCode` and into the matching subclass's `Emit()`. Each `Emit()` is a verbatim copy of the existing branch — same `sb.AppendLine` calls in the same order, just relocated.
3. Build and confirm output diff against `golden/` is empty.

### Phase 2 — Discovery extraction

1. Create `Discovery/this.cs` with the Roslyn predicate and the factory that picks the right `ActionProperty` subclass.
2. `LazyParamsGenerator.Initialize` becomes: `actionDeclarations` from `ActionDiscovery`, then `RegisterSourceOutput(_, (spc, emission) => emission.Emit(spc))`.
3. Diff against `golden/` again. Empty.

### Phase 3 — Verify and commit

1. Build the full solution. `dotnet run --project PLang.Tests` to confirm runtime tests pass (the generator's contract is "code that compiles and behaves the same"; if any test fails, output drifted).
2. Run a sample PLang build (`plang p build` on a known goal package) to confirm builder integration still works.
3. Final golden diff. Empty.
4. Commit, push, write summary, write changes.patch.

## Files modified / created

**Modified:**
- `PLang.Generators/LazyParamsGenerator.cs` → renamed to `PLang.Generators/this.cs` (per `@this` convention) and shrunk to orchestration. Roughly 50 lines after extraction.

**Created (Round 1):**
- `PLang.Generators/Discovery/this.cs`
- `PLang.Generators/Emission/Action/this.cs`
- `PLang.Generators/Emission/Properties/this.cs`
- `PLang.Generators/Emission/Properties/Property/this.cs` (abstract)
- `PLang.Generators/Emission/Properties/Property/{Provider,DataWrapped,PlainData,Resolvable,VariableName,Nullable,Value,Reference}/this.cs` (8 leaves)
- `PLang.Generators/Emission/Properties/Property/Defaulted/{Value,Reference}/this.cs` (2 leaves)

Total: ~13 small files + the orchestration entry. Each leaf is short and maps to one current `if/else` arm.

**Untouched in Round 1:**
- The emission of marker properties, `ExecuteAsync`, helpers, and `__SnapshotParams` — all stay inline in `Emission/Action/this.cs` for this round.

## Risks

- **Roslyn equality and incremental caching.** Records derived from `IPropertySymbol` data must be `IEquatable<T>` for incremental cache hits. `record` gives us this, but the base class needs `EqualityContract` discipline — already a known footgun (see CLAUDE.md note about filtering `EqualityContract` when scanning virtual props). I'll verify the generator still incrementally re-runs only on changed action classes.
- **Priority order drift.** Today's ordering is implicit in the `if/else` walk. The factory must encode it explicitly. The byte-for-byte diff catches order errors.
- **Subtle diff from formatting.** `StringBuilder.AppendLine` ordering is deterministic; if a refactor accidentally swaps two lines or merges two `AppendLine` calls into one with `\n`, the diff catches it.
- **`netstandard2.0` LangVersion.** Already verified `latest` — no fallback needed.

## Round 2 (placeholder — not part of this plan)

If Round 1 lands cleanly, candidates for Round 2:
- Marker auto-injection extraction (5 small files).
- `ExecuteAsync` segments — only worth it if state-threading turns out cleaner than I expect.
- Cleanup of *emitted* code shape: collapse all property getters through a single `__Resolve(name, ParamKind)` dispatcher. This changes the contract (output diffs intentionally) and needs its own plan.

These are not commitments — only candidates we'd evaluate after Round 1.

## Open questions for Ingi

None blocking. One I want flagged: after Phase 0 (harness), do you want me to commit the `golden/` snapshot to `.bot/`? It's useful documentation but it's also ~N × 200 lines of generated C#. I'm leaning yes — it makes "what changed" trivially auditable for any future refactor — but if you'd rather keep `.bot/` lean, I can keep golden out-of-tree and just diff during the work.

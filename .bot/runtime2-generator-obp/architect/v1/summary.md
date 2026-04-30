# v1 — LazyParamsGenerator OBP Refactor (Design)

## What this is

`PLang.Generators/LazyParamsGenerator.cs` had grown into one ~780-line file with two procedural mega-methods (`GetActionClassInfo`, `GenerateActionCode`). The chaos hotspots: an 11-way `if/else` ladder per property, a 120-line `ExecuteAsync` emission script, and helper boilerplate emitted into every generated class.

This v1 produced the design plan to restructure the generator's *internal* shape into OBP without changing what it emits. The intent of round 1 is to kill the 11-way per-property ladder by replacing it with a polymorphic `ActionProperty` hierarchy. Markers, `ExecuteAsync`, helpers, and snapshot stay procedural this round — they're either too small to extract or share too much state to split cleanly.

## What was done

**Design only — no code touched.** The plan lives at `plan.md` next to this file.

Key decisions captured in the plan:

- **Source generation stays.** No reflection, no runtime compilation. (Considered three alternatives — pre-generated files on disk, runtime Roslyn compilation, reflection-based `ActionInvoker` — all explicitly ruled out by Ingi.)
- **Round 1 = property hierarchy only.** Markers and `ExecuteAsync` are out of scope for v1. Splitting `ExecuteAsync` into "segments" would be wrapping chaos in classes — those segments share too much state (`__previousStep`, `__resolutionError`, `__frame`).
- **Hard promise: byte-for-byte identical `.g.cs` output.** Phase 0 pins current generator output as `golden/`; every later phase diffs against it. Ingi approved committing `golden/` to `.bot/` for audit trail.
- **Naming follows the `@this` per-folder convention.** `LangVersion=latest` is already set on `PLang.Generators.csproj`, so `sealed record` with inheritance works natively.

## Code example — the shape we're moving toward

Today, `GenerateActionCode` runs an 11-way `if/else` per property:

```csharp
if (prop.IsProvider) {
    sb.AppendLine($"private {prop.TypeName}? {backingField};");
    // ... ~5 lines emitting the Provider getter
    continue;
}
if (prop.IsDataWrapped) {
    // ... three sub-cases (nullable / has-default / plain)
}
else if (prop.TypeName.Contains("App.Data.@this") && !prop.TypeName.Contains("<")) { ... }
else if (prop.IsAppResolvable) { ... }
else if (prop.IsVariableName) { ... }
// ... 6 more branches
```

After round 1:

```csharp
foreach (var property in info.Properties)
    property.Emit(ctx);
```

…where `property` is one of `ProviderProperty`, `DataWrappedProperty`, `PlainDataProperty`, `ResolvableProperty`, `VariableNameProperty`, `Defaulted/Value`, `Defaulted/Reference`, `NullableProperty`, `ValueProperty`, `ReferenceProperty` — chosen once at discovery time, each with its own `Emit()` carrying the verbatim `AppendLine` calls from today.

## Status / what's next

- **Done in v1:** plan written, approved by Ingi, branch created off `runtime2`.
- **Next:** implementation. Architect doesn't code — handoff.
- **Recommended next bot:** `coder` directly. There's no test suite to design here — the regression contract is the byte-for-byte `golden/` diff, which is the test. (test-designer might add value for thinking about which sample handlers to include in the golden snapshot, but it's optional.)
- **No code changes in this version** — `changes.patch` is empty. Architect-only session.

## Open question (deferred to round 2)

If round 1 lands cleanly, the candidates for round 2 are: marker auto-injection extraction, possible `ExecuteAsync` segmentation (only if state-threading turns out cleaner than expected), and cleanup of the *emitted* code shape (collapsing all property getters through a single `__Resolve(name, ParamKind)` runtime dispatcher — this last one would intentionally break the byte-for-byte promise and needs its own plan).

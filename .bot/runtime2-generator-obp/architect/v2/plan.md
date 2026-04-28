# v2 — Combined OBP Hierarchy + Contract Simplification

## What this is

`PLang.Generators/LazyParamsGenerator.cs` is a 730-line generator with two procedural mega-methods. Three structural problems:

- An 11-way `if/else` ladder per property in `GenerateActionCode` (priority-ordered dispatch on property kind).
- Helper methods (`__Resolve`, `__ResolveData`, `__StripPercent`, `__TryConvert`, `__FormatValue`, `__HasParam`) emitted as private members into every generated class — pure boilerplate, N copies for N action handlers.
- `ExecuteAsync` emission (~130 lines) that mixes universal scaffolding (frame push/pop, try/finally, context save/restore) with per-class concerns (property reset, provider init, validation).

This version restructures the generator into the directory-as-API shape **and** simplifies the runtime contract in one combined refactor. v1 kept the contract intact and deferred simplification to a round 2 placeholder; v2 commits to both at once because doing them separately produces churn — v1 would build a structure that round 2 partially dismantles.

See `v1_review_summary.md` for the full reasoning behind the combined approach.

## End shape

### Runtime side — `PLang/App/modules/`

```
App/modules/
  ActionBase/
    this.cs                       // protected helpers + ExecuteCoreAsync template
  ParamKind.cs                    // enum: Plain, Nullable, WithDefault, VariableName, Resolvable, DataPlain, DataWrapped, Provider
  ICodeGenerated.cs               // unchanged
  IContext.cs, IChannel.cs, IAction.cs, IStep.cs, IStatic.cs, IEvent.cs   // unchanged
  app/, file/, list/, ...         // unchanged
```

`ActionBase` (abstract) carries:

- Protected fields: `__action`, `__variables`, `__app`, `__resolutionError`, `__paramData`.
- Protected methods: `__Resolve<T>(name, ParamKind)`, `__ResolveData(name)`, `__StripPercent(name)`, `__TryConvert<T>(value, name)`, `__FormatValue(value)`, `__HasParam(name)`.
- Protected method `ExecuteCoreAsync(action, context, run)` that owns the universal scaffolding: `__action`/`__variables`/`__app` setup, `__paramData` init, callstack frame push/pop, context save/restore, try/catch/finally.

The generator emits `partial class X : App.modules.ActionBase, App.modules.ICodeGenerated`. C# unions partial declarations, so the user-side partial declaring interfaces (`IContext`, `IStep`, etc.) still works. Verified across all existing handlers — none currently inherit from a non-interface base, so the union is conflict-free.

### Generator side — `PLang.Generators/`

```
PLang.Generators/
  this.cs                                        // orchestration: IIncrementalGenerator pipeline only
  Discovery/
    this.cs                                      // GetActionClassInfo + factory picking ActionProperty subclass
  Emission/
    Action/
      this.cs                                    // per-class shell + thin ExecuteAsync delegating to ExecuteCoreAsync
    Property/
      this.cs                                    // abstract ActionProperty record
      Provider/this.cs
      DataWrapped/this.cs
      PlainData/this.cs
      Resolvable/this.cs
      VariableName/this.cs
      Defaulted/
        Value/this.cs
        Reference/this.cs
      Nullable/this.cs
      Value/this.cs
      Reference/this.cs
```

## Property contract

```csharp
public abstract record ActionProperty
{
    public string Name { get; init; }
    public string TypeName { get; init; }

    // Factory dispatch — first IsMatch wins, in priority order.
    public abstract bool IsMatch(IPropertySymbol prop);

    // Per-property emission slots. Each subclass overrides what it needs.
    public abstract string BuildResolveCall(string paramName);
    public virtual string? EmitProviderInit() => null;
    public virtual string? EmitValidation() => null;
    public virtual string? EmitEventCheck() => null;
    public virtual string? EmitFieldReset() => $"__{Name}_backing = default; __{Name}_set = false;";
}
```

Each subclass returns the canonical resolution call for the getter. Examples:

- `ProviderProperty.BuildResolveCall(name)` → `app.Providers.Get<{Type}>().Value!`
- `PlainDataProperty.BuildResolveCall(name)` → `__ResolveData("{name}")`
- `DataWrappedProperty.BuildResolveCall(name)` → `__ResolveData("{name}").As<{InnerType}>(Context)`
- `VariableNameProperty.BuildResolveCall(name)` → `__StripPercent("{name}")!`
- `ResolvableProperty.BuildResolveCall(name)` → `{Type}.Resolve(__Resolve<string>("{name}", ParamKind.Plain), Context)!`
- `ValueProperty.BuildResolveCall(name)` → `__Resolve<{Type}>("{name}", ParamKind.Plain)`
- `DefaultedValueProperty.BuildResolveCall(name)` → `__Resolve<{Type}>("{name}", ParamKind.WithDefault, {DefaultValue})`

The emitted property template is **uniform** across all kinds — only the call expression differs:

```csharp
public partial {Type} {Name}
{
    get
    {
        if (!__{Name}_set)
        {
            __{Name}_backing = {BuildResolveCall(Name)};
            __{Name}_set = true;
        }
        return __{Name}_backing!;
    }
    init { __{Name}_backing = value; __{Name}_set = true; }
}
```

## Generated `.g.cs` shrinks substantially

Per-action class today: ~200 lines (helpers + ExecuteAsync + property impls + boilerplate).
Per-action class after v2: ~50 lines (property impls + thin ExecuteAsync).

Helpers move to `ActionBase` — one definition, not N copies. `ExecuteCoreAsync` owns universal scaffolding. The generator emits a thin per-class `ExecuteAsync` that does only per-class concerns: property reset, provider init, validation, then `return await ExecuteCoreAsync(action, context, Run);`.

## Phases

### Phase 0 — Build the regression test matrix

The byte-for-byte golden-file contract is gone. The new contract: **every `ParamKind` × {value type, ref type, string, enum} variant has a dedicated test action handler** with predictable `Run()` output, exercised through a runtime test that builds an Action from synthetic parameters and calls `ExecuteAsync`.

Coverage requirements:

| Variant | Test cases |
|---------|-----------|
| `ParamKind.Plain` | string, int, bool, custom record |
| `ParamKind.Nullable` | string?, int? (null and present) |
| `ParamKind.WithDefault` | value type with default, reference type with default, enum default |
| `ParamKind.VariableName` | `[VariableName]` strip |
| `ParamKind.Resolvable` | type with static `Resolve(string, Context)` |
| `ParamKind.DataPlain` | `Data.@this` parameter |
| `ParamKind.DataWrapped` | `Data<T>` plain, nullable, with default |
| `ParamKind.Provider` | `[Provider]` interface |
| `[IsNotNull]` | rejects null `Data.Value` |
| `IEvent` | `context.Event` propagation from resolved property |
| `IContext`, `IChannel`, `IAction`, `IStep`, `IStatic` | auto-provision |

Estimated 15–20 test action classes, each minimal, in `PLang.Tests/Generator/Matrix/`. **Review matrix coverage with Ingi before Phase 1** — gaps mean undetected drift.

### Phase 1 — Build `ActionBase` + `ParamKind`

1. Create `App/modules/ActionBase/this.cs`. Helper bodies copied verbatim from the current generator's emitted helpers (so semantics are identical, just relocated).
2. Create `App/modules/ParamKind.cs` enum.
3. `ExecuteCoreAsync(action, context, runner)` template carries: `__action`/`__variables`/`__app` setup, `__paramData` init, frame push/pop, context save/restore, try/catch/finally.
4. Phase 0 tests fail at this point (generator hasn't been rewritten) — expected. Helpers exist but nothing inherits.

### Phase 2 — Generator emits inheritance + uniform getters

1. Generator emits `partial class X : App.modules.ActionBase, App.modules.ICodeGenerated`.
2. Generator stops emitting helper method bodies (inherited from `ActionBase`).
3. Property getters emit the uniform template, but the resolution call still comes from the existing 11-branch ladder (we collapse the branches in Phase 3).
4. ExecuteAsync becomes thin: per-class property reset, provider init, validation, then `return await ExecuteCoreAsync(action, context, Run);`.
5. **Phase 0 matrix should pass at this point** — full behavior preserved, helpers and scaffolding moved.

### Phase 3 — Property hierarchy

1. Create the 13-file property hierarchy under `Emission/Property/`.
2. Discovery factory walks priority order (Provider → DataWrapped → PlainData → Resolvable → VariableName → Defaulted/Value → Defaulted/Reference → Nullable → Value → Reference) and returns the matching subclass — same priority as today's implicit `if/else` walk.
3. Each subclass implements `IsMatch` + `BuildResolveCall`.
4. The generator's `GenerateActionCode` shrinks to: discover, then `foreach (var p in info.Properties) p.Emit(ctx)` for each emission slot.
5. Matrix still passes.

### Phase 4 — Validation cohesion

1. Move per-property validation out of `Emission/Action/this.cs` and into each subclass's optional `EmitValidation()`.
2. Provider eager init moves into `EmitProviderInit()`.
3. `IEvent` check moves into `EmitEventCheck()`.
4. `Emission/Action/this.cs` ends up as: emit class shell → `foreach property` calls each emission slot in the right order → emit ExecuteAsync delegation.
5. Matrix still passes.

### Phase 5 — Full suite + ship

1. `dotnet run --project PLang.Tests` — matrix tests + existing runtime tests pass.
2. `plang p build` on system/builder/* — confirm builder integration intact.
3. PLang test suite (`plang --test`) — confirm action handlers work end-to-end.
4. Commit, push, PR targeting `runtime2`.

## Files modified / created

**Created (App side):**
- `PLang/App/modules/ActionBase/this.cs`
- `PLang/App/modules/ParamKind.cs`

**Created (Generator side):**
- `PLang.Generators/this.cs` (renamed from `LazyParamsGenerator.cs`)
- `PLang.Generators/Discovery/this.cs`
- `PLang.Generators/Emission/Action/this.cs`
- `PLang.Generators/Emission/Property/this.cs` (abstract base record)
- `PLang.Generators/Emission/Property/{Provider, DataWrapped, PlainData, Resolvable, VariableName, Nullable, Value, Reference}/this.cs` (8 leaves)
- `PLang.Generators/Emission/Property/Defaulted/{Value, Reference}/this.cs` (2 leaves)

**Created (Test side):**
- `PLang.Tests/Generator/Matrix/*.cs` — 15–20 test action handlers + matrix runner

**Modified:**
- Existing action handlers under `App/modules/`: no source changes. The `ActionBase` base class is added via the generator-emitted partial declaration, unioning with the user-written partial.

**Deleted:**
- `PLang.Generators/LazyParamsGenerator.cs`

## Risks

- **Inheritance + partial classes.** C# unions partial declarations including base classes. Verified that no existing action handler under `App/modules/` inherits from a non-interface base — only interface lists like `IContext`, `IContext, IStep`, `IContext, IConfigure<Config>`. Adding `ActionBase` via generator partial unions cleanly. Phase 2 begins with a single handler to confirm before mass rollout.
- **Roslyn incremental cache.** `record` types must have value-equal fields for cache hits. `IPropertySymbol`-derived data flowing into the records is primitive (string, bool, enum) — equality is cheap. No `List<>` field directly on the records; collections held as `ImmutableArray` or constructed at emit time only.
- **Test matrix completeness.** This is the new regression contract. Coverage gaps mean undetected behavior drift. Phase 0 must enumerate every `ParamKind` × {nullability, default, type-class} combination explicitly. Open question (3) below: do we sit down to review the matrix together before Phase 1?
- **Provider resolution semantics.** Today providers resolve both lazily (in property getter for direct test usage) and eagerly (in ExecuteAsync to short-circuit on missing). Both paths must survive — `ProviderProperty.BuildResolveCall` for lazy, `ProviderProperty.EmitProviderInit` for eager. Test matrix must cover both.
- **`ExecuteCoreAsync` ordering.** Validation, provider init, and property reset must run in the right order. Encoded in the generated thin `ExecuteAsync` — `ExecuteCoreAsync` itself only owns the universal scaffolding.
- **Lazy reset semantics.** Today, when `__action != null` the generated `ExecuteAsync` resets all backing fields. When `__action == null` (direct construction via `init`), backing fields are kept. This branching survives in the thin generated `ExecuteAsync`.

## Files modified by phase

| Phase | Adds | Modifies | Deletes |
|-------|------|----------|---------|
| 0 | `PLang.Tests/Generator/Matrix/*.cs` | — | — |
| 1 | `App/modules/ActionBase/this.cs`, `App/modules/ParamKind.cs` | — | — |
| 2 | — | `LazyParamsGenerator.cs` (rewrite) | — |
| 3 | `PLang.Generators/{Discovery,Emission/Action,Emission/Property/...}/this.cs` | `LazyParamsGenerator.cs` shrinks to orchestration | — |
| 4 | — | property subclasses gain optional emission methods; `Emission/Action/this.cs` shrinks | — |
| 5 | — | — | old `LazyParamsGenerator.cs` after rename to `this.cs` |

## Open questions for Ingi

1. **`ActionBase` placement.** Proposing `App/modules/ActionBase/this.cs`, alongside `ICodeGenerated.cs` and the other interfaces. Consistent with directory-as-API. Acceptable, or do you prefer `ActionBase.cs` flat (matching the flat interfaces)?
2. **Test matrix location.** Proposing `PLang.Tests/Generator/Matrix/`. Each entry is a tiny `[Action]` class. Acceptable, or should they live closer to the generator source?
3. **Phase 0 review.** Worth a separate sit-down on matrix coverage before Phase 1 starts, or trust the implementer (coder bot) to enumerate?
4. **Helper visibility.** `__Resolve` etc. are protected on `ActionBase` (only inherited handlers can call them). Generated code calls `this.__Resolve(...)`. Confirm protected (not `internal`) is the right access level.

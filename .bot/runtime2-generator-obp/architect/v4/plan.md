# v4 — Resolution Is a Read Transformation, Not Stored State

## What this is

v4 supersedes v3. Same overall arc — restructure `LazyParamsGenerator` into OBP shape, move action scaffolding from generated code to `App.Run`, migrate every action property to `Data<T>`, delete `[VariableName]` — but with one architectural sharpening that dissolves a cluster of recent runtime2 patches:

**Resolution lives in `Data.As<T>(context)`, not in `Data.Value`'s getter. `Data` is stateless with respect to resolution.**

See `v3_review_summary.md` for why this changed and what it makes obsolete.

## The hard promise

**Runtime test matrix passes** as the regression contract. Byte-for-byte `.g.cs` output changes intentionally; behavior is what we preserve. v3's risks "Data.Value lazy getter side effects" and "cycle on .Value" are designed out, not tested around.

## The contract

After v4, the rules are:

1. **Every action property is `Data.@this<T>`.** No `partial string Path`, no `partial int Count`. The generator fails the build for any non-`Data<T>` property (except `[Provider]`).
2. **`Data` is `Data<object>`.** Plain `Data.@this` non-generic and `Data.@this<object>` are equivalent in routing.
3. **`Data.Value` is the raw input — read-only, no side effects, no caching.** Whatever construction set, it returns. Consumers never see resolution from `.Value`.
4. **`Data.As<T>(context)` is the read transformation.** It walks `_value`, substitutes `%var%` references via `context.Variables.Get`/`Resolve`, converts to T via TypeMapping, and returns a fresh `Data<T>`. Every call resolves freshly against the current context — there is nothing to cache and nothing to invalidate.
5. **Caching lives on the handler, not on Data.** The generated property's `??=` backing field caches the resolved `Data<T>` for the call. `ExecuteAsync` resets backing fields per call. That is the only cache, and it has the right lifetime by construction.
6. **`Action.@this` owns parameter lookup.** `Action.GetParameter(name, context)` walks Parameters → Defaults → returns `Data.NotFound`. Pure lookup, no resolution.
7. **`App.Run` owns execution scaffolding.** Callstack push/pop, save/restore of `Context.Step/Goal/Event`, try/catch/finally with `ServiceError` translation, `frame.SnapshotVariables` in finally — all wrap `handler.ExecuteAsync` from outside, so the generated handler stays thin.
8. **Build-time validation, runtime trust.** Validation runs in the builder; engine trusts loaded `.pr` files. Type-mismatch errors surface naturally when the action that owns them runs (via `As<T>` returning `Data.FromError`).

## The breakage we accept

The v3 question — *"does any caller read `.Value` on parameter Data expecting the resolved form?"* — is answered by the contract: **no, because that would be a side effect we no longer provide.** Any code that depended on it was depending on something that should never have existed. Let it break during Phase 2; the test matrix surfaces every legitimate access pattern.

## End shape

### Runtime side — three-level execution cake

```
Action.RunAsync(context)              [Action.@this — existing, unchanged structure]
    │  events + modifiers
    │  Handled-override path (mock.intercept, event.skipAction) bypasses dispatch entirely
    │
    ▼
App.Run(action, context)              [App.@this — gains scaffolding wrapper]
    │  resolves handler instance via Modules.Get
    │  pushes callstack frame; saves Context.Step/Goal/Event; sets Context.Step = action.Step
    │  try/catch/finally around handler.ExecuteAsync(action, context)
    │  ServiceError translation on exception (with __SnapshotParams from the handler)
    │  finally: snapshot frame, pop callstack, restore context
    │
    ▼
handler.ExecuteAsync(action, context) [generated — thin, per-class concerns only]
    │  __action = action; Context = context
    │  marker init (Channels, Action, Step, Static if implemented)
    │  eager [Provider] resolution
    │  reset lazy backing fields
    │  per-class validation ([IsNotNull], non-null required)
    │  return Run()                                  // direct, App.Run wraps with scaffolding
    │
    ▼
handler.Run()                         [user-written]
    │  await Provider.NameOfAction(this)
```

### Property emission — one shape, no variation

```csharp
// Per parameter property — uniform across every parameter-sourced kind
private Data.@this<List<LlmMessage>>? __Messages_backing;
public partial Data.@this<List<LlmMessage>> Messages
{
    get => __Messages_backing ??= __action
        .GetParameter("Messages", Context)
        .As<List<LlmMessage>>(Context);
    init { __Messages_backing = value; }
}
```

For `[Provider]`:

```csharp
private ILlmProvider? __Llm_backing;
public partial ILlmProvider Llm => __Llm_backing!;
// __Llm_backing eagerly assigned in ExecuteAsync from app.Providers.Get<ILlmProvider>()
```

### Generated `ExecuteAsync` — thin

```csharp
public Task<Data.@this> ExecuteAsync(Action.@this action, Context.@this context)
{
    __action = action;
    Context = context;                                            // because handler : IContext

    // Eager provider init (only special case)
    var __llmResult = context.App.Providers.Get<ILlmProvider>();
    if (!__llmResult.Success) return Task.FromResult(__llmResult);
    __Llm_backing = __llmResult.Value!;

    // Reset lazy backing fields (so reused handler instances re-derive per call)
    __Messages_backing = null;
    __Schema_backing = null;
    // ... one line per parameter property ...

    // Per-class validation ([IsNotNull], required-non-null)
    // ...

    return Run();                                                 // direct — App.Run wraps with scaffolding
}
```

The callstack push/pop, context save/restore, and try/catch/finally aren't here — they live in `App.Run(action, context)`.

### `Data` — what it owns and what it doesn't

```
PLang/App/Data/this.cs
  // OWNS
  Value (get/init) — raw, post-construction, no side effects
  As<T>(context) — walk + substitute + convert, returns fresh Data<T>
  Type, Name, Error, Success, Properties, Ok, Fail, Merge, FromError, NotFound, …
  
  // GONE under v4
  _resolved cache flag
  _rawValue preservation
  ResetResolution()
  IsDeferredActionTemplate carve-out
  Lazy resolution side effect on Value getter
```

### Generator side — same hierarchy as v3

```
PLang.Generators/
  this.cs                                 // orchestration: IIncrementalGenerator pipeline (~50 lines)
  Discovery/
    this.cs                               // Roslyn predicate, GetActionClassInfo, factory
                                          //  picks Provider vs DataProperty per IPropertySymbol;
                                          //  builds parameter schema metadata for build-time validation
  Emission/
    Action/
      this.cs                             // emits per-handler partial: marker slots, property impls,
                                          //  thin ExecuteAsync, __SnapshotParams body
    Property/
      this.cs                             // abstract record ActionProperty
      Data/this.cs                        // DataProperty — every parameter-sourced property
      Provider/this.cs                    // ProviderProperty — eager init from app.Providers
```

`ActionProperty` exposes two emission slots: `EmitProperty(StringBuilder)` (the property body + backing field) and `EmitSnapshotEntry(StringBuilder)` (one block inside `__SnapshotParams`). Snapshot emission stays per-property because backing-field name and set-flag are property-local.

## How a parameter flows end-to-end

For `llm.query` with `Messages=[{Role:"system", Content:"%comment%"}]`:

1. **.pr load** — Parameter Data constructed with `Value = List<object?> of Dictionary<string, object?>`. `NeedsResolution` flag exists but is no longer the gate for caching — it's just a marker for "this came from .pr, may contain `%var%`".
2. **Goal pumping** — `action.RunAsync(context)` fires BeforeAction events, runs Modifiers; dispatch lambda is `() => context.App.Run(this, context)`.
3. **`App.Run(action, context)`** — resolves the `query` handler instance. Pushes callstack frame, saves `Context.Step/Goal/Event`, sets `Context.Step = action.Step`. Enters try/catch/finally. Calls `handler.ExecuteAsync(action, context)`.
4. **Generated `ExecuteAsync`** — sets `__action`, `Context`; eagerly resolves `Llm` provider; resets backing fields; runs `[IsNotNull]` validation; returns `Run()` directly.
5. **`Run()`** executes — `await Llm.Query(this)`. Provider receives the handler reference.
6. **Provider reads `this.Messages`** — property getter fires:
   - `__action.GetParameter("Messages", Context)` → returns the Parameter Data (raw, no resolution)
   - `.As<List<LlmMessage>>(Context)` → walks the raw list, substitutes `"%comment%"` via `Context.Variables.Get("comment").Value`, converts the resolved structure to `List<LlmMessage>` via TypeMapping, returns `Data<List<LlmMessage>>`
   - Backing field caches the result for the rest of this call.
7. **Provider receives** typed `List<LlmMessage>` with `%comment%` substituted.
8. **Run completes** — `App.Run`'s finally block restores Step/Goal/Event, snapshots frame, pops callstack. Result returns up to `Action.RunAsync`, which fires AfterAction events and writes the result as `__data__` in Variables.

Second invocation of the same action (e.g., next loop iteration, or sub-goal call): step 4 resets the backing field to null, step 6 re-derives from raw — picking up whatever `%comment%` resolves to *now*. No cache to bust on Data; no `ResetResolution` call needed.

## Phases

### Phase 0 — Build the regression test matrix

The contract is "every property kind × type-shape combination has a test action that exercises it." Build these first. Phase 0's exit criterion: matrix is green against today's unmodified generator. Then Phase 1 begins.

```
PLang.Tests/Generator/Matrix/
  Plain/
    StringPlain.cs            // Data<string>
    IntPlain.cs               // Data<int>
    BoolPlain.cs              // Data<bool>
    PathPlain.cs              // Data<FileSystem.Path>  (App-resolvable via static Resolve)
  Nullable/
    StringNullable.cs         // Data<string>?
    IntNullable.cs            // Data<int>?
  WithDefault/
    StringWithDefault.cs      // [Default("hello")] Data<string>
    IntWithDefault.cs         // [Default(42)] Data<int>
    EnumWithDefault.cs        // [Default(MyEnum.A)] Data<MyEnum>
    BoolWithDefault.cs        // [Default(false)] Data<bool>
  DataPlain/
    DataPlain.cs              // Data (== Data<object>)
  DataWrapped/
    DataWrappedString.cs      // Data<string> (parameter Value is "%var%")
    DataWrappedList.cs        // Data<List<LlmMessage>> with nested %var%
    DataWrappedDict.cs        // Data<Dictionary<string, object>> with nested %var%
    DataWrappedActionList.cs  // Data<List<Action.@this>> — sub-actions retain raw %var%
                              //   verifies As<T> does NOT walk into Action.@this
                              //   replaces today's IsDeferredActionTemplate carve-out
  Provider/
    ProviderProp.cs           // [Provider] IFakeProvider
    ProviderMissing.cs        // [Provider] IUnregistered — short-circuits with Data.FromError
  IsNotNull/
    IsNotNullProp.cs          // [IsNotNull] Data<string> rejects null Value
  Markers/
    IContextHandler.cs
    IChannelHandler.cs
    IActionHandler.cs
    IStepHandler.cs
    IStaticHandler.cs
  Resolution/
    FullVarMatch.cs           // value="%path%"          → Variables.Get(name).Value cast to T
    Interpolation.cs          // value="Hello %name%"     → Variables.Resolve(str, context)
    DeepResolutionList.cs     // value=[{"Content":"%x%"}]  → walk + substitute
    DeepResolutionDict.cs     // value={"Inner":"%x%"}    → walk + substitute
    ReResolveAcrossCalls.cs   // same parameter, two ExecuteAsync calls, %x% changes between
                              //   verifies backing-field reset + raw stability + fresh As<T>
    ConcurrentHandlers.cs     // two handler instances run in parallel against same Action
                              //   verifies Data is stateless (no shared mutation)
  Modifier/
    ModifierAction.cs         // [Modifier] Wrap(...) — verifies override path through Modifiers
                              //   doesn't break scaffolding when Handled=true
  Snapshot/
    SnapshotOnError.cs        // handler errors mid-Run; Error.Params contains every property's
                              //   raw value (PrValue), declared type, final value if accessed
```

Estimated 25–28 test action classes. Each has a minimal `Run()` returning predictable `Data.@this`, exercised by a fixture that builds an Action with synthetic Parameters and calls `ExecuteAsync` (via `App.RunAction` or equivalent test helper that exercises the production execution path).

**Phase 0 exit criterion:** matrix green against today's unmodified generator.

### Phase 1 — Add `Action.GetParameter` (pure addition)

1. Implement `Action.GetParameter(name, context)` — walks Parameters, falls back to Defaults, returns `Data.NotFound` if missing. Pure lookup, no resolution.
2. Generator unchanged. New method exists but isn't called yet.
3. Matrix still passes — no behavior change.

### Phase 2 — Resolution moves from `.Value` getter to `As<T>`

This is the architectural move. Single phase, single PR.

1. **Write `Data.As<T>(context)` as the new resolution entry point.** It walks `_value`:
   - `string` with full `^%name%$` match → `context.Variables.Get(name).Value` cast to T (existing helper logic)
   - `string` with partial `%...%` → `context.Variables.Resolve(str, context)` cast to T
   - `IList<object?>` → walk, substitute primitives, convert via TypeMapping → typed list
   - `IDictionary<string, object?>` → walk, substitute primitives, convert via TypeMapping → typed dict
   - `T` has static `Resolve(string, Context)` and `_value` is string → call it (Path, etc.)
   - Otherwise → TypeMapping conversion of the raw value
   - On any failure: `Data.FromError` with a structured error
2. **`Data.Value` becomes read-only post-construction.** No side effects, no resolution. The setter is unchanged.
3. **Delete `Data._resolved`, `Data._rawValue`, `Data.ResetResolution()`, `Data.IsDeferredActionTemplate`.**
4. **Delete `Variables.ResolveDeep` and its supporting state** (`_resolveDepth`, `_resolveItemCount`, `MaxResolveItems`, `OnResolveTrace` if unused outside ResolveDeep).
5. **Generator stops emitting `data.ResetResolution()`** in parameter-Data construction (the `__StripPercent` neighbor goes too — it's a leftover from the same family).
6. Run matrix. **This is the phase where things may break** — handlers reading `.Value` on parameter Data and expecting the resolved form will now see raw. Per Ingi: those are invalid implementations. Fix them by routing through `As<T>` or by reading the property (which already routes through `As<T>` via the generated getter). No code is added to preserve old behavior.

### Phase 3 — Atomic cutover: scaffolding into `App.Run`, generator emits the new shape

Single PR. Sequenced internally:

1. Move scaffolding into `App.Run(action, context)`: callstack push/pop, save/restore Context.Step/Goal/Event, try/catch/finally with ServiceError translation, frame.SnapshotVariables in finally. `App.Run` now wraps `handler.ExecuteAsync`.
2. Generator emits the new uniform property shape:
   ```csharp
   get => __backing ??= __action.GetParameter("name", Context).As<T>(Context);
   ```
3. Generator stops emitting `__Resolve<T>`, `__ResolveData`, `__StripPercent`, `__TryConvert`, `__FormatValue`, `__HasParam` — they were inline duplicates of what `As<T>` now does centrally.
4. Generated `ExecuteAsync` shrinks to the thin form: marker init, provider eager init, backing-field reset, validation, then `return Run()` directly.
5. Generator still has the 11-branch ladder internally (collapsed in Phase 4) but emits the new shape.
6. **`__SnapshotParams` simplifies.** Today it carries `data.ResetResolution()`-related complexity to capture pre/post. Under v4, `PrValue = __action.GetParameter(name, context).Value` (raw, trivially), `FinalValue = __backing?.Value` (post-As<T>, trivially). The generator still emits one entry per property (Phase 4 will move this into `ActionProperty.EmitSnapshotEntry`).
7. Run matrix. Fix any drift.

### Phase 4 — Build the property hierarchy + collapse the dispatch

1. Create `PLang.Generators/Emission/Property/this.cs` — abstract record `ActionProperty` with two emission methods: `EmitProperty(StringBuilder)`, `EmitSnapshotEntry(StringBuilder)`.
2. Create `Emission/Property/Data/this.cs` (`DataProperty`) and `Emission/Property/Provider/this.cs` (`ProviderProperty`).
3. `Discovery/this.cs` factory becomes trivial: `IsProvider ? new ProviderProperty(...) : new DataProperty(...)`.
4. The 11-branch ladder in the old `GenerateActionCode` deletes wholesale.
5. `LazyParamsGenerator.cs` renames to `PLang.Generators/this.cs` and shrinks to ~50 lines orchestration.
6. Run matrix. Green.

### Phase 5 — Migration sweep + build-time validation + `[VariableName]` removal

Sequence within this phase:

1. **Survey first.** Grep `App/modules/` for any property that is not `Data.@this`, `Data.@this<T>`, or `[Provider]`-attributed. Same for `[VariableName]` uses. (`11386f1c` already swept most of this — list is small.)
2. **Convert remaining handlers to `Data<T>`.** Build stays green throughout (existing generator handles `Data<T>` already).
3. **Convert `[VariableName]` call sites.** Each handler that reads from a `[VariableName]`-attributed property switches to reading `data.Name` on a `Data<T>` property. Build stays green.
4. **Enable the build-time check.** Generator's discovery rejects any `[Action]` partial with a property that is neither `Data.@this`, `Data.@this<T>`, nor `[Provider]`-attributed. Build error: `"Property '{name}' on action '{class}' must be Data<T> or [Provider]. Raw scalars are not permitted."` Lands *after* the migration so the build doesn't go red mid-sweep.
5. **Delete `[VariableName]` attribute and class.**
6. Run matrix. Run full PLang test suite. Run sample `plang p build` on `system/builder`.

### Phase 6 — Cleanup sweep

1. **Audit `Data.As<T>`.** It is now the resolution entry point — confirm it covers every case Phase 2's matrix established. Slim only obviously dead branches.
2. **Confirm `Variables.ResolveDeep` is gone** with no lingering call sites. Confirm `_resolveDepth`/`_resolveItemCount`/`MaxResolveItems`/`OnResolveTrace` are gone if unused elsewhere.
3. **Confirm no typed-POCO reflection-walk** anywhere in `Data` or `Variables`. The matrix's `DeepResolutionList`/`DeepResolutionDict` cases prove the JSON-deserialized `List<object?>`/`Dictionary<string, object?>` path covers every realistic shape.
4. **Audit generator emission.** Any leftover `__Resolve`/`__ResolveData`/`__StripPercent`/`__TryConvert`/`__FormatValue`/`__HasParam` emission gets deleted. (Phase 3 did the bulk; this is the audit pass.)
5. Final matrix run. Run full PLang test suite. Run sample `plang p build` on `system/builder`. Commit. Push. PR targeting `runtime2`.

## Files modified / created

**Modified (App side):**
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — adds `GetParameter(name, context)`.
- `PLang/App/this.cs` — `App.Run(action, context)` gains the scaffolding wrap (callstack, save/restore, try/catch/finally) around `handler.ExecuteAsync`.
- `PLang/App/Data/this.cs` — `As<T>(context)` becomes the resolution entry point; `Value` becomes side-effect-free; `_resolved`/`_rawValue`/`ResetResolution`/`IsDeferredActionTemplate` deleted.
- `PLang/App/Variables/this.cs` — `ResolveDeep` and its supporting state removed entirely.
- All handlers under `App/modules/` that use `[VariableName]` — converted to read `data.Name` on a `Data<T>` property.

**Created (Generator side):**
- `PLang.Generators/this.cs` (renamed from `LazyParamsGenerator.cs`) — orchestration only.
- `PLang.Generators/Discovery/this.cs` — Roslyn predicate, schema scan, factory.
- `PLang.Generators/Emission/Action/this.cs` — partial-class shell + thin ExecuteAsync + __SnapshotParams body.
- `PLang.Generators/Emission/Property/this.cs` — abstract `ActionProperty` base record.
- `PLang.Generators/Emission/Property/Data/this.cs` — `DataProperty` (emits property + snapshot entry).
- `PLang.Generators/Emission/Property/Provider/this.cs` — `ProviderProperty`.

**Created (Tests):**
- `PLang.Tests/Generator/Matrix/...` — ~25–28 test handlers (see Phase 0 list).

**Deleted:**
- `PLang.Generators/LazyParamsGenerator.cs` (after rename to `this.cs`).
- All `__Resolve`, `__ResolveData`, `__StripPercent`, `__TryConvert`, `__FormatValue`, `__HasParam` emission code in the generator.
- Generator's `data.ResetResolution()` emission in parameter-Data construction.
- `Data._resolved`, `Data._rawValue`, `Data.ResetResolution()`, `Data.IsDeferredActionTemplate`.
- `Variables.ResolveDeep` and its supporting state (`_resolveDepth`, `_resolveItemCount`, `MaxResolveItems`, `OnResolveTrace` if unused elsewhere).
- `[VariableName]` attribute and all its uses across the codebase.

**Untouched:**
- All existing handlers under `App/modules/` — except those that declare non-`Data<T>` properties or use `[VariableName]`, which Phase 5 sweeps.
- All marker interfaces (`IContext`, `IChannel`, `IAction`, `IStep`, `IStatic`).
- `__SnapshotParams` and `ParamSnapshot` semantics (kept; emission moves into Phase 4's `ActionProperty.EmitSnapshotEntry`).
- `Action.IsModifier`, `Action.Description`, `Action.ModuleDescription` — additive metadata, untouched.
- `Variables.Set` reference-aliasing semantics — orthogonal to resolution, untouched.
- `Action.RunAsync` Handled-override path — orthogonal to resolution, untouched.

## Risks

- **Code that reads `.Value` on parameter Data expecting the resolved form.** This is the contract change. Per Ingi, any such code is invalid; let it break. The matrix surfaces every legitimate access pattern. Phase 2 is where breakage manifests; Phase 5's full PLang test suite + `plang p build` on `system/builder` is the broader catch.
- **Breaking changes to handler authors using raw scalars.** The `partial string Path` style stops compiling after Phase 5's build-time check. `11386f1c` swept most handlers already; the remainder is small. Phase 5 sequences the migration before the check is enabled, so the build doesn't go red mid-sweep.
- **`App.Run` scaffolding ordering relative to `Action.RunAsync`'s Modifiers.** Today `Action.RunAsync` calls `App.Run` inside `Modifiers.RunAsync(dispatch, context)`. Modifiers can wrap, retry, or short-circuit dispatch. The new scaffolding inside `App.Run` runs once per modifier-driven dispatch — a retry modifier that calls dispatch twice should push/pop frames twice, which is correct. Matrix's `ModifierAction` case covers this. The `Handled`-override path bypasses `App.Run` entirely (see runtime2's `Action.RunAsync` change), so scaffolding doesn't apply there — matrix verifies that override results still flow back as `__data__`.
- **`ExecuteAsync` per-class concerns ordering.** Eager provider init must run before validation (so providers are usable in validation if needed). Backing-field reset must run before any property access in validation. Encoded order in the generated thin ExecuteAsync.
- **Roslyn incremental cache.** `ActionProperty` records must use value-equal fields. Avoid `IPropertySymbol` references inside the record — extract to primitives at factory time.
- **`As<T>` performance characteristics.** Each handler-property access does a fresh walk-and-substitute. For the common case (one access per call, cached in backing field) this is identical work to today's first-access path. For pooled handlers running concurrently, each gets its own walk — slight cost vs. shared cache, but no shared mutation. Matrix's `ConcurrentHandlers` case verifies correctness; perf measurement is out of scope unless regression appears.

## Resolved during design (carried from v3, refined for v4)

- **Resolution location: `Data.As<T>(context)`, not `Data.Value`.** This is v4's central architectural sharpening. See `v3_review_summary.md`.
- **No new method on Action; scaffolding moves to `App.Run(action, context)`.** Same as v3.
- **`[VariableName]` is removed**, not deprecated. Same as v3.
- **Typed-POCO reflection-walk is gone** from the resolution path. The matrix's `DeepResolutionList`/`DeepResolutionDict` cases prove primitive-container traversal covers every realistic shape. If a real handler surfaces depending on the reflection branch during Phase 5, lift it back into `As<T>` then; otherwise it stays gone.
- **`__SnapshotParams` survives, simplified.** Implementation under v4 is trivially clean: raw via `.Value`, final via backing field. Stays a per-property contribution, emitted via `ActionProperty.EmitSnapshotEntry` from Phase 4.

## Open questions for Ingi

1. **Phase 0 review.** Same as v3 — want a separate sit-down on matrix coverage before Phase 1, or trust the implementer to enumerate? The matrix IS the contract; gaps mean undetected drift. Recommend a brief review after Phase 0 lands but before Phase 1 begins.
2. **Phase 2 breakage tolerance.** "Let it break and find out" is the stance. Phase 2 is the phase where invalid `.Value` callers surface. If the surface is wider than expected (e.g., dozens of test failures), is the answer "fix them all in Phase 2" or "stop and reconvene"? Recommend: if more than ~10 sites need updating, pause and review the pattern before proceeding — that's a signal the contract change is touching something we didn't anticipate.

## Round 2 candidates (not part of this plan)

- **Auto-emit `Run()`** for handlers that have exactly one `[Provider]` and the provider exposes a method matching the action name. Strong simplification, but its own design pass.
- **`Data` becomes a record struct or readonly record class** once it's stateless w.r.t. resolution. Could shrink memory footprint and clarify the value-shape contract. Worth its own pass after v4 lands.

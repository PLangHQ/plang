# v3 — Generator Refactor: Data Owns Resolution, Action Owns Lookup

## What this is

A near-complete redesign of the action handler runtime contract, alongside the structural extraction of `LazyParamsGenerator`. v3 supersedes v2 because in-conversation re-review surfaced two architectural shifts that simplify the design substantially:

1. Behavior moves to data owners, not to a synthetic `ActionBase` parent.
2. The eleven-arm property dispatch dissolves entirely — generator emits one shape.

See `v2_review_summary.md` for the full re-review and what changed.

## The hard promise

**Runtime test matrix passes** as the regression contract. Byte-for-byte `.g.cs` output changes intentionally; behavior is what we preserve.

## The contract

After v3, the rules are:

1. **Every action property is `Data.@this<T>`.** No `partial string Path`, no `partial int Count`. The generator fails the build for any non-`Data<T>` property (except `[Provider]`, which is its own special case).
2. **`Data` is `Data<object>`.** Plain `Data.@this` non-generic and `Data.@this<object>` are equivalent in routing.
3. **`Data.@this` owns its own resolution.** `Data.Value` lazy getter, when `NeedsResolution=true`, walks `_value` and substitutes `%var%` references via `Context.Variables.Get`. Recursive traversal of `IList`/`IDictionary`/typed objects lives on Data, not on Variables.
4. **`Action.@this` owns parameter lookup and execution scaffolding.** No `ActionBase`. Helpers and frame management live on Action because Action is the data owner.
5. **Build-time validation, runtime trust.** Validation runs in the builder; engine trusts loaded .pr files. Type-mismatch errors surface naturally when the action that owns them runs (via `As<T>` returning `Data.FromError`).

## End shape

### Runtime side — three-level execution cake

```
Action.RunAsync(context)              [Action.@this — existing, unchanged]
    │  events + modifiers
    │
    ▼
App.Run(action, context)              [App.@this — gains scaffolding wrapper]
    │  resolves handler instance
    │  pushes callstack frame, saves Context.Step/Goal/Event
    │  try/catch/finally around handler.ExecuteAsync(action, context)
    │  ServiceError translation on exception
    │  finally: snapshot frame, pop callstack, restore context
    │
    ▼
handler.ExecuteAsync(action, context) [generated — thin, per-class concerns only]
    │  __action = action; Context = context
    │  marker init (Channels, Action, Step, Static if implemented)
    │  eager [Provider] resolution
    │  reset lazy backing fields
    │  per-class validation ([IsNotNull], non-null required)
    │  return Run()                                  // direct, no helper
    │
    ▼
handler.Run()                         [user-written]
    │  await Provider.NameOfAction(this)
```

The scaffolding moves from generated code to `App.Run` — not to a new method on Action. `App.Run` already owns dispatch (resolves the handler, calls `ExecuteAsync`); wrapping that call with frame/context management is its natural job. No new naming, no collision with the existing `Action.RunAsync(context)`.

```
PLang/App/
  Goals/Goal/Steps/Step/Actions/Action/this.cs
    + GetParameter(name, context)         // walk Parameters → Defaults → Data.NotFound

  this.cs                                 // App.@this
    Run(action, context) — gains the scaffolding wrap around the handler call:
      callstack push/pop, context save/restore (Step/Goal/Event),
      try/catch/finally with ServiceError on exception,
      frame.SnapshotVariables in finally.

  Data/this.cs
    + Value getter resolves lazily when NeedsResolution=true
      - string with full %var% match  → Context.Variables.Get(name) → its Value
      - string with partial %var%      → Context.Variables.Resolve(str) → interpolated
      - IList<object?>                 → walk + substitute, return new list
      - IDictionary<string, object?>   → walk + substitute, return new dict
      (typed-POCO reflection branch is removed — see Phase 6)

  Variables/this.cs
    - ResolveDeep removed entirely (no callers after Phase 2)
    - Variables.Get and Variables.Resolve(string) stay as-is
```

### Generator side — much smaller hierarchy

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
                                          //  thin ExecuteAsync calling Action.RunAsync
    Property/
      this.cs                             // abstract record ActionProperty
      Data/this.cs                        // DataProperty — every parameter-sourced property
      Provider/this.cs                    // ProviderProperty — eager init from app.Providers
```

Two leaves under `Property/`. (Possibly one if `Provider` becomes a flag on `DataProperty` — discoverable during implementation.)

### The property emission shape — one line per parameter property

```csharp
// per property — uniform across every parameter-sourced kind
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

That's the entire generator-side variation.

### Generated `ExecuteAsync` — also thin

```csharp
public Task<Data.@this> ExecuteAsync(Action.@this action, Context.@this context)
{
    __action = action;
    Context = context;                                            // because handler : IContext

    // Eager provider init (only special case)
    var __llmResult = context.App.Providers.Get<ILlmProvider>();
    if (!__llmResult.Success) return Task.FromResult(__llmResult);
    __Llm_backing = __llmResult.Value!;

    // Reset lazy backing fields (so reused handler instances re-resolve per call)
    __Messages_backing = null;
    __Schema_backing = null;
    // ... one line per parameter property ...

    return Run();                                                 // direct — App.Run wraps with scaffolding
}
```

The callstack push/pop, context save/restore, and try/catch/finally aren't here anymore — they live one level up in `App.Run(action, context)`, which wraps the call to `ExecuteAsync`. Handlers stay flat — no inheritance from any synthetic base, only marker interfaces (`IContext`, `IChannel`, etc.) and `ICodeGenerated`.

## How a parameter flows end-to-end (for grounding)

For `llm.query` with `Messages=[{Role:"system", Content:"%comment%"}]`:

1. **.pr load** (System.Text.Json deserialization) — Parameter Data constructed with Value as `List<object?>` of `Dictionary<string, object?>`. `NeedsResolution=true`.
2. **Goal pumping** — `action.RunAsync(context)` fires BeforeAction events, runs Modifiers; dispatch lambda is `() => context.App.Run(this, context)`.
3. **`App.Run(action, context)` — the scaffolding layer.** Resolves the `query` handler instance. Pushes callstack frame, saves Context.Step/Goal/Event, sets Context.Step = action.Step. Enters try/catch/finally. Calls `handler.ExecuteAsync(action, context)`.
4. **Generated `ExecuteAsync`** — sets `__action`, `Context`; eagerly resolves `Llm` provider; resets backing fields; runs validation; returns `Run()` directly.
5. **`Run()`** executes — `await Llm.Query(this)`. Provider receives the handler reference.
6. **Provider reads `this.Messages`** — property getter fires:
   - `__action.GetParameter("Messages", Context)` → returns the Parameter Data (no resolution)
   - `.As<List<LlmMessage>>(Context)` → reads `.Value`, which triggers Data's lazy resolution
     - Data sees Value is `List<object?>`, walks each item
     - Inner Dictionary's "Content" is `"%comment%"` → `Context.Variables.Get("comment")` → returns existing Data
     - Returns resolved list of dicts
   - `As<T>` then converts the resolved structure to `List<LlmMessage>` via TypeMapping
7. **Provider receives** typed `List<LlmMessage>` with `%comment%` substituted.
8. **Run completes** — `App.Run`'s finally block restores Step/Goal/Event, snapshots frame, pops callstack. Result returns up to `Action.RunAsync`, which fires AfterAction events and stores the result as `__data__` if successful.

## Phases

### Phase 0 — Build the regression test matrix

The contract is "every property kind × type-shape combination has a test action that exercises it." Build these first, run them against today's generator, confirm green. That is the definition of "no regression."

```
PLang.Tests/Generator/Matrix/
  Plain/
    StringPlain.cs            // Data<string>
    IntPlain.cs               // Data<int>
    BoolPlain.cs              // Data<bool>
    PathPlain.cs              // Data<FileSystem.Path>  (App-resolvable)
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
    DataWrappedString.cs      // Data<string> (parameter Value is %var%)
    DataWrappedList.cs        // Data<List<LlmMessage>> with nested %var%
    DataWrappedDict.cs        // Data<Dictionary<string, object>> with nested %var%
  Provider/
    ProviderProp.cs           // [Provider] IFakeProvider
    ProviderMissing.cs        // [Provider] IUnregistered — short-circuits
  IsNotNull/
    IsNotNullProp.cs          // [IsNotNull] Data<string> rejects null Value
  Markers/
    IContextHandler.cs
    IChannelHandler.cs
    IActionHandler.cs
    IStepHandler.cs
    IStaticHandler.cs
  Resolution/
    FullVarMatch.cs           // value="%path%"          → existing variable Data
    Interpolation.cs          // value="Hello %name%"     → string interpolation
    DeepResolutionList.cs     // value=[{"Content":"%x%"}]  → walk + substitute
    DeepResolutionDict.cs     // value={"Inner":"%x%"}    → walk + substitute
```

Estimated 22–25 test action classes. Each has a minimal `Run()` returning predictable `Data.@this`, exercised by a fixture that builds an Action with synthetic Parameters and calls `ExecuteAsync`.

**Phase 0 exit criterion:** matrix is green against today's unmodified generator. Then Phase 1 begins.

### Phase 1 — Add `Action.GetParameter` (pure addition)

1. Implement `Action.GetParameter(name, context)` — walks Parameters, falls back to Defaults, returns `Data.NotFound` if missing. Pure lookup, no resolution.
2. Generator unchanged. New method exists but isn't called yet.
3. Matrix still passes — no behavior change.

The scaffolding move to `App.Run` is deferred to Phase 3 because it must happen *atomically* with the generator stripping its inline scaffolding emission. Doing them separately would either double-push frames or leave a window where neither layer pushes them.

### Phase 2 — Move resolution into `Data.@this`

1. Move `ResolveDeep`'s logic into `Data.Value`'s lazy getter, gated on `NeedsResolution=true`.
2. Variables.ResolveDeep either becomes a one-line delegate to Data or is removed (callers updated).
3. Fix the typed-list bug in the move (today's `for (int i = ...) ResolveDeep(typedList[i])` discards results — the new code assigns properly).
4. Matrix should still pass — equivalent behavior, different ownership.

### Phase 3 — Atomic cutover: scaffolding into `App.Run`, generator emits the new shape

This phase has to happen as one PR / one commit because the scaffolding owner moves at the same moment the generator stops emitting it. Sequenced internally:

1. Move scaffolding into `App.Run(action, context)`: callstack push/pop, save/restore Context.Step/Goal/Event, try/catch/finally with ServiceError translation, frame.SnapshotVariables in finally. `App.Run` now wraps `handler.ExecuteAsync`.
2. Generator emits the new uniform property shape:
   ```csharp
   get => __backing ??= __action.GetParameter("name", Context).As<T>(Context);
   ```
3. Generator stops emitting `__Resolve<T>`, `__ResolveData`, `__StripPercent`, `__TryConvert`, `__FormatValue`, `__HasParam` — they're gone.
4. Generated `ExecuteAsync` shrinks to the thin form: marker init, provider eager init, backing-field reset, validation, then `return Run()` directly (no scaffolding here — App.Run owns it).
5. Generator still has the 11-branch ladder internally (we collapse it in Phase 4) but emits the new shape.
6. Run matrix. Fix any drift.

### Phase 4 — Build the property hierarchy + collapse the dispatch

1. Create `PLang.Generators/Emission/Property/this.cs` (abstract record).
2. Create `Emission/Property/Data/this.cs` and `Emission/Property/Provider/this.cs` — the two leaves.
3. `Discovery/this.cs` factory becomes trivial: `IsProvider ? new ProviderProperty(...) : new DataProperty(...)`.
4. The 11-branch ladder in the old `GenerateActionCode` deletes wholesale.
5. `LazyParamsGenerator.cs` renames to `PLang.Generators/this.cs` and shrinks to ~50 lines orchestration.
6. Run matrix. Green.

### Phase 5 — Migration sweep + build-time validation + `[VariableName]` removal

Sequence within this phase:

1. **Survey first.** Grep `App/modules/` for any property that's not `Data.@this`, `Data.@this<T>`, or `[Provider]`-attributed. Same for `[VariableName]` uses. Produces the migration list.
2. **Convert handlers to `Data<T>`.** Each non-Data property migrates to `Data<T>`. Build stays green throughout (existing generator handles `Data<T>` already).
3. **Convert `[VariableName]` call sites.** Each handler that reads from a `[VariableName]`-attributed property switches to reading `data.Name` on a `Data<T>` property. Build stays green.
4. **Enable the build-time check.** Generator's discovery rejects any `[Action]` partial with a property that is neither `Data.@this`, `Data.@this<T>`, nor `[Provider]`-attributed. Build error: `"Property '{name}' on action '{class}' must be Data<T> or [Provider]. Raw scalars are not permitted."` Order matters — this step lands *after* the migration so the build doesn't go red mid-sweep.
5. **Delete `[VariableName]` attribute and class.** No remaining call sites by this point.
6. Run matrix. Run full PLang test suite. Run sample `plang p build` on `system/builder`.

### Phase 6 — Cleanup sweep

1. **Slim `Data.As<T>`** — remove its "Value is string + T has static Resolve(string, Context)" branch (lines 403–415 of `App/Data/this.cs`). Redundant after Phase 2: `Data.Value` resolves before `As<T>` reads it.
2. **Delete `Variables.ResolveDeep` entirely** — Phase 2 moved the logic to Data. Confirm no remaining callers; remove the method, the depth/breadth guards (`_resolveDepth`, `_resolveItemCount`, `MaxResolveItems`), and the `OnResolveTrace` event if unused outside ResolveDeep.
3. **Delete the typed-POCO reflection branch in Data's deep resolver** (the equivalent of today's `ResolveDeep` lines 446–480 — `MemberwiseClone` + reflection over public string properties). The matrix's `DeepResolutionList` and `DeepResolutionDict` cases prove the JSON-deserialized path (which yields `List<object?>` of `Dictionary<string, object?>`) handles every realistic case; the reflection branch was for variables holding user-domain typed POCOs with `%var%`-bearing string fields, which is rare and brittle. If Phase 6 surfaces a real handler depending on it, lift it back; otherwise it stays gone.
4. **Audit generator emission** — any leftover `__Resolve`/`__ResolveData`/`__StripPercent`/`__TryConvert`/`__FormatValue`/`__HasParam` emission code in the generator gets deleted. (Phase 3 did the bulk; this is the audit pass.)
5. Final matrix run. Run full PLang test suite. Run sample `plang p build` on `system/builder`. Commit. Push. PR targeting `runtime2`.

## Files modified / created

**Modified (App side):**
- `PLang/App/Goals/Goal/Steps/Step/Actions/Action/this.cs` — adds `GetParameter(name, context)`.
- `PLang/App/this.cs` — `App.Run(action, context)` gains the scaffolding wrap (callstack, save/restore, try/catch/finally) around `handler.ExecuteAsync`.
- `PLang/App/Data/this.cs` — `Value` getter gains lazy resolution dispatching to a private deep-resolve helper. `As<T>` slimmed in Phase 6.
- `PLang/App/Variables/this.cs` — `ResolveDeep` and its supporting state removed entirely (Phase 6).
- All handlers under `App/modules/` that use `[VariableName]` — converted to read `data.Name` on a `Data<T>` property (Phase 5).

**Created (Generator side):**
- `PLang.Generators/this.cs` (renamed from `LazyParamsGenerator.cs`) — orchestration only.
- `PLang.Generators/Discovery/this.cs` — Roslyn predicate, schema scan, factory.
- `PLang.Generators/Emission/Action/this.cs` — partial-class shell + thin ExecuteAsync.
- `PLang.Generators/Emission/Property/this.cs` — abstract `ActionProperty` base record.
- `PLang.Generators/Emission/Property/Data/this.cs` — `DataProperty`.
- `PLang.Generators/Emission/Property/Provider/this.cs` — `ProviderProperty`.

**Created (Tests):**
- `PLang.Tests/Generator/Matrix/...` — ~22–25 test handlers (see Phase 0 list).

**Deleted:**
- `PLang.Generators/LazyParamsGenerator.cs` (after rename to `this.cs`).
- All `__Resolve`, `__ResolveData`, `__StripPercent`, `__TryConvert`, `__FormatValue`, `__HasParam` emission code in the generator.
- `Variables.ResolveDeep` and its supporting state (`_resolveDepth`, `_resolveItemCount`, `MaxResolveItems`, `OnResolveTrace` if unused elsewhere).
- `[VariableName]` attribute and all its uses across the codebase.
- The typed-POCO reflection branch in deep resolution (today's `ResolveDeep` lines 446–480 equivalent).

**Untouched:**
- All existing handlers under `App/modules/` — except those (if any) that declare non-`Data<T>` properties, which Phase 5 sweeps.
- All marker interfaces (`IContext`, `IChannel`, `IAction`, `IStep`, `IStatic`).

## Risks

- **`Data.Value` lazy getter side effects.** Today, `Data.Value` is a property accessor. After v3, it does work the first time it's read (variable lookup, deep traversal). That's a behavior change — code that read `.Value` for inspection now triggers resolution. Test for: code that reads `.Value` on Parameter Data inspection paths (debugger views, trace logs, audit). The `NeedsResolution` flag gates this, so non-Parameter Data is unaffected.

- **Cycle: `Data.Value` → `Variables.Get` → returns Data → reads its Value.** If Variable Data also has `NeedsResolution=true`, infinite recursion. Mitigation: variables are set with `NeedsResolution=false` (they're already resolved at variable-set time). Verify in `variable.set` handler and at entry-point Data construction. Unit test for the cycle case.

- **Breaking changes to handler authors.** The `partial string Path` style stops compiling after Phase 5. Any handler that uses raw scalars must migrate to `Data<T>`. Quick survey before starting the work — if 80%+ already use `Data<T>` (read.cs and llm/query.cs both do), this is a focused sweep. If less, the migration scope is bigger.

- **`App.Run` scaffolding ordering relative to existing `Action.RunAsync`.** Today `Action.RunAsync` calls `App.Run` inside `Modifiers.RunAsync(dispatch, context)`. Modifiers can wrap, retry, or short-circuit dispatch. The new scaffolding inside `App.Run` will execute once per modifier-driven dispatch — verify modifier semantics still hold (e.g., a retry modifier that calls dispatch twice should push/pop frames twice, which is correct). Matrix should include a modifier scenario to cover this.

- **`ExecuteAsync` per-class concerns ordering.** Eager provider init must run before validation (so providers are usable in validation if needed). Backing-field reset must run before any property access in validation. Encoded order in the generated thin ExecuteAsync.

- **Roslyn incremental cache.** `ActionProperty` records must use value-equal fields. Avoid `IPropertySymbol` references inside the record — extract to primitives at factory time.

- **Resolution for typed user-domain objects with %var%-bearing string properties.** Today's `ResolveDeep` reflection-scans these (e.g., `LlmMessage.Content`). After Phase 2, Data owns the same logic. The reflection cost remains the same — moved, not eliminated. Worth measuring on `llm.query`-shaped payloads to confirm parity.

## Resolved during design

- **No new method on Action; scaffolding moves to `App.Run(action, context)`.** The dispatcher is the natural owner of the wrap (frame push/pop, context save/restore, try/catch/finally). No naming collision with the existing `Action.RunAsync(context)` — that one stays exactly as it is, doing events + modifiers + dispatch.
- **`[VariableName]` is removed**, not deprecated. Coder sweeps existing call sites to read `data.Name` from a `Data<T>` property instead.
- **Typed-POCO reflection branch is removed** — Phase 6 deletes it. Matrix `DeepResolutionList`/`DeepResolutionDict` cases prove the JSON-deserialized `List<object?>`/`Dictionary<string, object?>` path covers every realistic shape.

## Open questions for Ingi

1. **Phase 0 review.** Want a separate sit-down on matrix coverage before Phase 1, or trust the implementer to enumerate? The matrix IS the contract; gaps mean undetected drift. Recommend a brief review after Phase 0 lands but before Phase 1 begins.

## Round 2 candidates (not part of this plan)

- **Auto-emit `Run()`** for handlers that have exactly one `[Provider]` and the provider exposes a method matching the action name. Handler becomes pure declaration: properties + provider, no `Run()` body. Strong simplification, but a separate decision worth its own design pass. Ingi loves the idea; saving for later.

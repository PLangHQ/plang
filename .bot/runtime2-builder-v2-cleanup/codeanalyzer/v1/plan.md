# Code Analysis Plan — runtime2-builder-v2-cleanup v1

## Scope

This branch is Ingi's manual cleanup of pieces 1-4 (identity, crypto, signing, http) plus coder-executed engine changes. ~244 files changed. Major themes:

1. **`[Provider]` attribute pattern** — Actions now declare `[Provider] public partial IFooProvider Foo { get; }` instead of manually calling `engine.Providers.Get<T>()`. Source generator wires it.
2. **Data-typed action params** — Action properties changed from `object?` to `Data?` (assert, condition, crypto, signing). Providers access `.Value` explicitly.
3. **Event consolidation** — 6 separate event actions (beforeGoal, afterStep, etc.) → single `on` action with a `Type` property.
4. **Convert module deletion** — Entire `convert` module removed (toInt, toJson, fromBase64, etc.).
5. **Engine cleanup** — `EngineLibraries` → `EngineModules`, `Property` removed, `DataSource` → `ISettingsStore`, `IdentityData` simplified to a pure Data subclass, `GoalCall.Parameters` → `List<Data>`, `CallFrame` stores `Goal` reference (OBP rule #3).
6. **Dead code removal** — PrParser, ChannelData, StepCacheEntry, AssertHelper, Channel.File(), various types.cs files.
7. **New attributes** — `[GoalCallback]`, `[IsInitiated]`, `[IsNotNull]`, `[Example]`.
8. **File module provider extraction** — File operations moved from Path methods to `DefaultFileProvider`.

## Analysis Plan

### Pass 1: OBP Compliance
- **CallFrame**: Verify `Goal` reference is stored, not decomposed fields ✓ (GoalName/GoalPath → Goal)
- **ExecutedStep**: Verify `Step` reference stored ✓
- **File module**: Verify provider pattern follows OBP (action passes `this`, provider navigates)
- **Identity provider**: Verify typed returns (IdentityData, DataList<IdentityData>) follow OBP
- **Streaming callbacks**: Verify GoalCall creation in RunCallbackAsync follows OBP

### Pass 2: Simplification
- Check for dead code left behind after deletions
- Verify no duplicate logic between TypeMapping (static) and Types (instance)
- Check that [Provider] pattern is consistent across all modules

### Pass 3: Readability
- Naming consistency across new attributes
- Flow clarity in DefaultIdentityProvider (many error paths)
- DefaultFileProvider method clarity

### Pass 4: Behavioral Reasoning
- **GoalCall.Parameters type change**: `Dictionary<string, object?>` → `List<Data>`. Trace ALL callers.
- **ICache type change**: `object` → `Data`. Check if StepCache still works.
- **IdentityData removal**: Was lazy-resolving, now uses DynamicData on Variables. Trace resolution path.
- **Data.Name setter**: Was readonly, now has `set`. Check who mutates it and whether that's safe.
- **IEvaluator interface change**: Methods now take action objects, not primitives. Check if `__condition__` is still set.
- **RunCallbackAsync**: Creates new GoalCall per chunk. Check if parameter naming works correctly.

### Pass 5: Deletion Test
- New `PlangSerializer` — any tests?
- `Data.Clone()` — any tests?
- `DataList<T>` — any tests?
- `[GoalCallback]`, `[IsInitiated]`, `[IsNotNull]`, `[Example]` — source generator tests?
- `DefaultFileProvider` methods — test coverage?
- `DefaultAssertProvider` — test coverage?

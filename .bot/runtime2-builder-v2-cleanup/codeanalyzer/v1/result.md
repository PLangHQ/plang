# Code Analysis — runtime2-builder-v2-cleanup v1

## Summary

Large cleanup branch (~244 files). The code quality is high — Ingi's design instincts show. Most changes are pure improvement. I found **3 real findings** and **2 observations** worth noting. No major OBP violations in the new code.

---

## Pass 1: OBP Compliance

### CallFrame — CLEAN ✓
`GoalName`/`GoalPath` → `Goal` reference (OBP rule #3). `ExecutedStep` now stores `Step` reference. `ToSerializable()` correctly extracts fields at serialization boundary. Textbook fix.

### File Module Provider Extraction — CLEAN ✓
Actions pass `this`, provider navigates: `Files.Copy(this)`, `Files.Read(this)`. Provider accesses `action.Context.Engine.FileSystem` — navigate, don't pass. Correct OBP.

### Identity Provider Typed Returns — CLEAN ✓
Methods return `IdentityData` directly instead of `Data.Ok(identity)`. Error carried on the object via `identity.Error = ...`. This is actually better OBP — the object owns its state, including error state.

### Streaming Callbacks — CLEAN ✓
`RunCallbackAsync` creates a new `GoalCall` per chunk instead of mutating shared Variables. Cleaner isolation.

### DefaultEvaluator — CLEAN ✓
`Evaluate(If action)` / `Evaluate(Compare action)` — provider receives the action, navigates for values. Correct OBP.

### Assert Module — CLEAN ✓
Provider receives action objects, navigates `.Value?.Value` for the actual values. Correct.

**No OBP violations found in new code.**

---

## Pass 2: Simplification

### Providers.RegisterDefaults() — Minor Observation
`Providers.RegisterDefaults()` in `Engine/Providers/this.cs:209-220` references concrete types from 5 different module namespaces. This couples the provider registry to every module. Not a violation — it's a single registration point — but if the module list grows, consider letting modules self-register via a convention (e.g., static `Register(EngineProviders)` method discovered via reflection).

**Not actionable now — just noting the direction.**

### TypeMapping/Types Consolidation — CLEAN ✓
Types delegates to TypeMapping. No duplicate logic. `StripGenericArity` helper added correctly. `RegisterDomainTypes()` is the new extension point. Clean.

### Dead Code — CLEAN ✓
PrParser, ChannelData, StepCacheEntry, AssertHelper, Channel.File(), all types.cs files — all correctly identified as unused and removed. No orphaned references found.

---

## Pass 3: Readability

### Event `On` Action Type Parsing
`PLang/App/modules/event/on.cs:30` — `Enum.TryParse<EventType>(Type, ignoreCase: true, out var eventType)` is clean and simple. The consolidation from 6 separate actions to one is a significant readability improvement.

### DefaultIdentityProvider Flow — CLEAN
Many error paths but each is short and clear. The `identity.Error = ...` pattern is consistent. `GenerateIdentity` returns `IdentityData` directly with error state when needed.

### Naming — CLEAN
`[IsInitiated]`, `[IsNotNull]`, `[Provider]`, `[GoalCallback]`, `[Example]` — all self-documenting. Source generator handles them correctly.

**No readability issues found.**

---

## Pass 4: Behavioral Reasoning

### Finding 1: `__condition__` Signal Removed — Verify Complete
**Severity: Medium (verify needed)**

The `__condition__` Variables signal was removed entirely. The new approach in `Steps/this.cs:57` checks `stepResult.Value is bool` directly, gated by `IsConditionStep()`. This is correct for the simple case, but:

**Trace the data flow:**
1. `If.Run()` calls `Evaluator.Evaluate(this)` → returns `Data.Ok(bool)`
2. `If.Run()` then may call `RunGoalAsync(goalToCall)` and returns `evalResult` (the bool)
3. `Steps.RunAsync` checks `stepResult.Value is bool`

**Risk:** If `If.Run()` calls `RunGoalAsync(goalToCall)` and the goal itself returns a non-bool Data, but the method still returns `evalResult` — this works because `evalResult` is the condition bool, not the goal result. ✓ Verified correct.

However, the old `__condition__` was also consumed by tests and potentially by user PLang code checking `%__condition__%`. If any PLang code relies on `%__condition__%`, this is a silent breaking change. **Low risk — internal signal, unlikely to be user-visible.**

### Finding 2: `Data.Name` Setter — Mutation Risk
**Severity: Low-Medium**
**File:** `PLang/App/Engine/Memory/Data.cs:76`

`Name` was changed from `{ get; }` (init-only via constructor) to `{ get; set; }`. This was needed for `IdentityData.Name = action.NewName` in the rename flow. However, Data objects live on Variables and are keyed by Name. If someone changes `data.Name` after it's been Put on the stack, the stack key and the object's Name diverge.

**Current usage:** Only `DefaultIdentityProvider.RenameAsync` mutates Name, and it does save+remove correctly. But the setter is now public on the base class, making it available to all Data consumers.

**Recommendation:** Consider `internal set` to limit mutation surface.

### Finding 3: `Data.Clone()` Uses DeepCloner — Verify Library Available
**Severity: Low**
**File:** `PLang/App/Engine/Memory/Data.cs:216`

`Data.Clone()` uses `Force.DeepCloner` (`_value.DeepClone()`). This is a new NuGet dependency. Verify it's in the csproj. Also: `Clone()` is defined but has zero callers — it's dead code currently.

### Finding 4 (Critical): Engine.Channels Not Disposed
**Severity: Medium**
**File:** `PLang/App/Engine/this.cs:330-376`

Engine creates `Channels = new EngineChannels(this)` at line 232 but never disposes it in `DisposeAsync`. Actors have their own Channels which ARE disposed. The engine-level `Channels` property holds a separate instance that could leak stream handles.

**Note:** This is NOT a regression — the previous code also didn't dispose engine Channels. But with `Property.DisposeAll()` removed, I'm flagging it as part of the disposal audit. `Channels.DisposeAsync()` should be called in `Engine.DisposeAsync`.

### GoalCall.Parameters Type Change — VERIFIED SAFE
`Dictionary<string, object?>` → `List<Data>`. Traced all callers:
- `Engine.RunGoalAsync`: iterates and calls `context.Variables.Put(param)` — correct, Data has Name+Value
- `GoalMapper.MapGoalInfo`: wraps v1 params as `new Data(p.Key, p.Value)` — correct bridge
- `TypeMapping.ConvertTo` for GoalCall: builds `List<Data>` from dict/JsonElement — correct
- `DefaultHttpProvider.RunCallbackAsync`: creates fresh GoalCall with `List<Data>` per chunk — correct
- `BuildGoal.llm` template updated to produce `[{name, value, type}]` format — correct

### ICache Type Change — VERIFIED SAFE
`object` → `Data`. `StepCache.CollectReturnVariables` stores Data in Properties, `RestoreVariables` restores from Properties. Cache stores/retrieves Data. `MemoryStepCache` uses `_cache.Get(key) as Data` which returns null for non-Data entries from old cache. Safe.

---

## Pass 5: Deletion Test

### Finding 5: `Data.Clone()` — Zero Callers
**Lines:** `Data.cs:215-229`
Could delete these 15 lines and no test would fail. The method exists for future use but is currently dead code.

### Finding 6: `PlangSerializer` — Zero Test Coverage
**File:** `PLang/App/Engine/Channels/Serializers/Serializer/PlangSerializer.cs` (94 lines)
No tests in PLang.Tests reference PlangSerializer. Could delete the entire file and no test would fail. This is a new serializer for PLang-to-PLang transport — important functionality that should have tests.

### Finding 7: `DefaultAssertProvider` — Zero Direct Test Coverage
**File:** `PLang/App/modules/assert/providers/DefaultAssertProvider.cs` (159 lines)
No C# tests reference `DefaultAssertProvider` directly. The assert module tests may test through the action records, but the provider's comparison logic (numeric coercion, truthiness, collection contains) needs direct unit tests to prove correctness — especially edge cases like `int 5 equals long 5L`.

### Finding 8: `DefaultFileProvider` — Minimal Test Coverage
**File:** `PLang/App/modules/file/providers/DefaultFileProvider.cs` (211 lines)
Only `PathTests.cs` references `DefaultFileProvider`. The Read/Save/Delete/Copy/Move/List/Exists methods each have multiple error paths. Edge cases (directory delete non-recursive with contents, copy file into directory, move with overwrite) need tests.

### Finding 9: `DataList<T>` — Tests Only Through Identity
`DataList<T>` is only tested indirectly through identity handler tests. The IList<T> implementation, error state propagation, and `Find`/`Exists`/`Where` helpers are untested directly.

---

## Files Analyzed

| File | Verdict |
|------|---------|
| Engine/Cache/* | CLEAN |
| Engine/CallStack/* | CLEAN |
| Engine/Channels/* | CLEAN (except PlangSerializer untested) |
| Engine/Config/this.cs | CLEAN |
| Engine/Context/Actor.cs | CLEAN |
| Engine/Goals/Goal/GoalCall.cs | CLEAN |
| Engine/Goals/Goal/Steps/this.cs | CLEAN |
| Engine/Memory/Data.cs | NEEDS WORK (Name setter, dead Clone) |
| Engine/Memory/Data.Result.cs | CLEAN |
| Engine/Memory/Data.Navigation.cs | CLEAN |
| Engine/Modules/this.cs | CLEAN |
| Engine/Providers/this.cs | CLEAN |
| Engine/Settings/* | CLEAN |
| Engine/Types/this.cs | CLEAN |
| Engine/Utility/TypeMapping.cs | CLEAN |
| Engine/Utility/GoalMapper.cs | CLEAN |
| Engine/this.cs | NEEDS WORK (Channels disposal) |
| Engine/FileSystem/PathData.cs | CLEAN |
| modules/assert/* | NEEDS WORK (provider untested) |
| modules/condition/* | CLEAN |
| modules/crypto/* | CLEAN |
| modules/error/* | CLEAN |
| modules/event/* | CLEAN |
| modules/file/* | NEEDS WORK (provider untested) |
| modules/http/* | CLEAN |
| modules/identity/* | CLEAN |
| modules/module/* | CLEAN |
| modules/signing/* | CLEAN |
| modules/variable/* | CLEAN |
| modules/Attributes.cs | CLEAN |

---

## Overall Verdict: NEEDS WORK

The code quality is high and the design direction is correct. The cleanup is thorough and well-executed. Three actionable findings:

1. **Engine.Channels disposal gap** — Add `await Channels.DisposeAsync()` to Engine.DisposeAsync
2. **Data.Name public setter** — Consider `internal set` to prevent accidental mutation
3. **Test coverage gaps** — PlangSerializer (94 lines), DefaultAssertProvider (159 lines), DefaultFileProvider (211 lines) all have zero or near-zero direct test coverage

The test gaps are the most important. The assert provider in particular handles numeric coercion — a known source of boxing bugs in PLang.

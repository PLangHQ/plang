# The Law of Names — Full Namespace Tree Map

## How to read this

**Left column**: Current file path and class name.
**Right column**: Proposed file path and (if renamed) new class name.
**Convention marker**: `[CW]` = convention-wired by source generator. `[E]` = entity. `[V]` = value type. `[I]` = infrastructure.

---

## 1. Engine Root

The engine becomes a folder. `this.cs` is the entry point.

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/Engine.cs` | `Engine/this.cs` — `Engine` (partial, unseal) | Root. `[CW]` properties are generated onto this. |
| `Memory/Data.cs` → `Data`, `Data<T>`, `DynamicData` | `Engine/Data.cs` | `[V]` Root value type. Used everywhere. |
| `Memory/Data.cs` → `Type` | `Engine/Type.cs` | `[V]` Root value type. Separate file from Data. |
| `Core/Info.cs` → `Info` | `Engine/Info.cs` | `[V]` Used in Goal, Step, Action. Root-level because it's everywhere. |

**Namespace**: `App.Engine`

### Key change: Engine unseals
Currently `sealed`. Must become `partial` for the source generator to emit `EngineGoals Goals { get; }` etc. Can remain effectively sealed — the generator emits a partial class, no one else subclasses it.

---

## 2. Goals (engine.Goals)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/Goals.cs` → `Goals` | `Engine/Goals/this.cs` → **`EngineGoals`** | `[CW]` Smart collection. ConcurrentDictionary-based. |
| `Core/Goal.cs` → `Goal` | `Engine/Goals/Goal.cs` | `[E]` Entity, partial. Visibility enum moves here too. |
| `Core/GoalMethods.cs` → `Goal` partial | `Engine/Goals/Goal.Methods.cs` | `[E]` RunAsync, Load, FormatForLlm |
| `Core/GoalCall.cs` → `GoalCall` | `Engine/Goals/GoalCall.cs` | Invocation descriptor. |

**Namespace**: `App.Engine.Goals`

---

## 3. Steps (goal.Steps)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/Steps.cs` → `Steps` | `Engine/Goals/Steps/this.cs` → **`GoalSteps`** | `[CW]` Smart collection : `List<Step>`. |
| `Core/Step.cs` → `Step` | `Engine/Goals/Steps/Step.cs` | `[E]` Entity, partial. |
| `Core/StepMethods.cs` → `Step` partial | `Engine/Goals/Steps/Step.Methods.cs` | `[E]` RunAsync, Load, HandleError, Retry |
| `Core/ErrorHandler.cs` → `ErrorHandler`, `ErrorOrder` | `Engine/Goals/Steps/ErrorHandler.cs` | Step's error config. Lives with Step. |

**Namespace**: `App.Engine.Goals.Steps`

---

## 4. Actions (step.Actions)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/Actions.cs` → `Actions` | `Engine/Goals/Steps/Actions/this.cs` → **`StepActions`** | `[CW]` Smart collection : `List<Action>`. |
| `Core/Action.cs` → `Action` | `Engine/Goals/Steps/Actions/Action.cs` | `[E]` Entity, partial. |
| `Core/ActionMethods.cs` → `Action` partial | `Engine/Goals/Steps/Actions/Action.Methods.cs` | `[E]` RunAsync |
| `Core/IAction.cs` → `IAction` | `Engine/Goals/Steps/Actions/IAction.cs` | Interface. |

**Namespace**: `App.Engine.Goals.Steps.Actions`

---

## 5. Channels (engine.Channels)

| Current | Proposed | Notes |
|---------|----------|-------|
| `IO/Channels.cs` → `Channels` | `Engine/Channels/this.cs` → **`EngineChannels`** | `[CW]` Stream-based I/O manager. |
| `IO/Channel.cs` → `Channel` | `Engine/Channels/Channel.cs` | `[E]` Single channel entity. |
| `IO/ChannelData.cs` → `ChannelData` | `Engine/Channels/ChannelData.cs` | Channel metadata. |

**Namespace**: `App.Engine.Channels`

---

## 6. Property (engine.Property)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/Property.cs` → `Property` | `Engine/Property/this.cs` → **`EngineProperty`** | `[CW]` Key-value store + GoalCall resolution. Layer 2 dispatch fallback. |

**Namespace**: `App.Engine.Property`

---

## 7. Events (engine.Events)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/EventCollection.cs` → `Events` | `Engine/Events/this.cs` → **`EngineEvents`** | `[CW]` Event binding registry + dispatch. |
| `Core/EventCollection.cs` → `EventBinding` | `Engine/Events/EventBinding.cs` | Binding entity. |
| `Core/EventCollection.cs` → `EventType` enum | `Engine/Events/EventType.cs` | Enum. Currently all in one file — split out. |
| `Core/Lifecycle.cs` → `Lifecycle`, `Bindings` | `Engine/Events/Lifecycle.cs` | Before/After bindings container. |
| `Context/EventScope.cs` → `EventScope` | `Engine/Events/EventScope.cs` | **Moves from Context → Events.** Semantically it's events. |

**Namespace**: `App.Engine.Events`

---

## 8. Serializers (engine.Serializers)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Serialization/SerializerRegistry.cs` → `SerializerRegistry` | `Engine/Serializers/this.cs` → **`EngineSerializers`** | `[CW]` Serializer registry. |
| `Serialization/ISerializer.cs` | `Engine/Serializers/ISerializer.cs` | Interface. |
| `Serialization/JsonStreamSerializer.cs` | `Engine/Serializers/JsonStreamSerializer.cs` | |
| `Serialization/TextStreamSerializer.cs` | `Engine/Serializers/TextStreamSerializer.cs` | |
| `Serialization/View.cs` | `Engine/Serializers/View.cs` | View enum + attributes. |
| `Serialization/ViewPropertyFilter.cs` | `Engine/Serializers/ViewPropertyFilter.cs` | |
| `Serialization/SerializerRegistry.cs` → `SerializeOptions`, `DeserializeOptions` | Same file or separate | Options DTOs. |

**Namespace**: `App.Engine.Serializers`

---

## 9. Cache (engine.Cache)

| Current | Proposed | Notes |
|---------|----------|-------|
| (new) | `Engine/Cache/this.cs` → **`EngineCache`** | `[CW]` Convention-wired wrapper. Delegates to `ICache`. |
| `Core/ICache.cs` → `ICache` | `Engine/Cache/ICache.cs` | Interface for pluggable cache. |
| `Core/MemoryStepCache.cs` → `MemoryStepCache` | `Engine/Cache/MemoryStepCache.cs` | Default implementation. |
| `Core/CacheSettings.cs` → `CacheSettings` | `Engine/Cache/CacheSettings.cs` | Config DTO. |
| `Core/StepCache.cs` → `StepCache` | `Engine/Cache/StepCache.cs` | Behavioral wrapper per-step. |
| `Core/StepCacheEntry.cs` → `StepCacheEntry`, `CachedVariable` | `Engine/Cache/StepCacheEntry.cs` | Cache data. |

**Namespace**: `App.Engine.Cache`

**Resolved**: Convention-wired as `EngineCache` (option A). Wraps `ICache`, delegates to pluggable implementation. Default is `MemoryStepCache`.

---

## 10. Debug (engine.Debug)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/DebugMode.cs` → `DebugMode` (static) | `Engine/Debug/this.cs` → **`EngineDebug`** | `[CW]` Becomes instance class, convention-wired. |

**Namespace**: `App.Engine.Debug`

Currently static. To be convention-wired, needs to become an instance owned by Engine. The `Apply` method becomes `Enable(object debugValue)` or similar.

---

## 11. Testing (engine.Testing)

| Current | Proposed | Notes |
|---------|----------|-------|
| `Core/TestMode.cs` → `TestMode` (static) | `Engine/Testing/this.cs` → **`EngineTesting`** | `[CW]` Test runner, convention-wired. |

**Namespace**: `App.Engine.Testing`

Same static → instance conversion as Debug.

---

## 12. Context (NOT convention-wired)

Per-request state. Not convention-wired because Context is a parameter, not an engine-owned capability.

| Current | Proposed | Notes |
|---------|----------|-------|
| `Context/PLangContext.cs` → `PLangContext` | `Engine/Context/PLangContext.cs` | Per-request. Not `[CW]`. |
| `Context/PLangContext.cs` → `IPLangContextAccessor`, `PLangContextAccessor` | Same file | AsyncLocal accessor. |
| `Context/Actor.cs` → `Actor` | `Engine/Context/Actor.cs` | Actors are context-adjacent. |
| `Core/CallStack.cs` → `CallStack` + serializable types | `Engine/Context/CallStack.cs` | Per-context tracking. |
| `Core/CallFrame.cs` → `CallFrame`, `ExecutedStep`, `ExecutionPhase` | `Engine/Context/CallFrame.cs` | Call frame data. |

**Namespace**: `App.Engine.Context`

---

## 13. Memory

| Current | Proposed | Notes |
|---------|----------|-------|
| `Memory/Variables.cs` | `Engine/Memory/Variables.cs` | |
| `Memory/TString.cs` | `Engine/Memory/TString.cs` | |
| `Memory/Properties.cs` | `Engine/Memory/Properties.cs` | |
| `Memory/IValueNavigator.cs` | `Engine/Memory/IValueNavigator.cs` | |
| `Memory/PlangTypeConverter.cs` | `Engine/Memory/PlangTypeConverter.cs` | |
| `Memory/TypeJsonConverter.cs` | `Engine/Memory/TypeJsonConverter.cs` | |
| `Memory/Navigators/*.cs` | `Engine/Memory/Navigators/*.cs` | Same structure. |

**Namespace**: `App.Engine.Variables`

**Note**: `Data` and `Type` move OUT of Memory → Engine root. Everything else stays.

---

## 14. Libraries (engine.Libraries)

| Current | Proposed | Notes |
|---------|----------|-------|
| `modules/Libraries.cs` → `Libraries` | `Engine/Libraries/this.cs` → **`EngineLibraries`** | `[CW]` Handler resolution registry. |
| `modules/Library.cs` → `Library` | `Engine/Libraries/Library.cs` | Single library entity. |
| `modules/IClass.cs` | `Engine/Libraries/IClass.cs` | Handler interface. |
| `modules/ICodeGenerated.cs` | `Engine/Libraries/ICodeGenerated.cs` | Generated handler interface. |
| `modules/IContext.cs` | `Engine/Libraries/IContext.cs` | Context interface. |
| `modules/ActionAttribute.cs` | `Engine/Libraries/ActionAttribute.cs` | |
| `modules/DefaultAttribute.cs` | `Engine/Libraries/DefaultAttribute.cs` | |
| `modules/VariableNameAttribute.cs` | `Engine/Libraries/VariableNameAttribute.cs` | |

**Namespace**: `App.Engine.Libraries`

---

## 15. Modules (action handler implementations)

These are the action handlers — `variable/set.cs`, `file/read.cs`, etc. They're resolved through Libraries, NOT through the convention graph. They don't follow `{Owner}{Capability}` naming.

**Folder stays as `modules/`.** The name maps to PLang syntax (`module.action`).

| Current | Proposed | Notes |
|---------|----------|-------|
| `modules/variable/*.cs` | `Engine/modules/variable/*.cs` | Same subfolder structure. |
| `modules/file/*.cs` | `Engine/modules/file/*.cs` | |
| `modules/output/*.cs` | `Engine/modules/output/*.cs` | |
| ... (all handler subfolders) | `Engine/modules/{module}/*.cs` | |

**Namespace**: `App.Engine.modules.{module}`

---

## 16. Errors

| Current | Proposed | Notes |
|---------|----------|-------|
| `Errors/*.cs` (all 10 files) | `Engine/Errors/*.cs` | Same structure. |

**Namespace**: `App.Engine.Errors`

---

## 17. Utility

| Current | Proposed | Notes |
|---------|----------|-------|
| `Utility/TypeMapping.cs` | `Engine/Utility/TypeMapping.cs` | |
| `Utility/AppData.cs` | `Engine/Utility/AppData.cs` | |

**Namespace**: `App.Engine.Utility`

---

## 18. Parsing & Mapping

| Current | Proposed | Notes |
|---------|----------|-------|
| `Parsing/PrParser.cs` | `Engine/Parsing/PrParser.cs` | |
| `Mapping/GoalMapper.cs` | `Engine/Mapping/GoalMapper.cs` | |

**Namespaces**: `App.Engine.Parsing`, `App.Engine.Mapping`

---

## Convention-Wired Summary

All `[CW]` classes that the source generator will discover and auto-wire:

| Convention Class | Owner | Property | PLang path |
|-----------------|-------|----------|------------|
| `EngineGoals` | Engine | `engine.Goals` | `%engine.Goals%` |
| `EngineChannels` | Engine | `engine.Channels` | `%engine.Channels%` |
| `EngineProperty` | Engine | `engine.Property` | `%engine.Property%` |
| `EngineEvents` | Engine | `engine.Events` | `%engine.Events%` |
| `EngineSerializers` | Engine | `engine.Serializers` | `%engine.Serializers%` |
| `EngineCache` | Engine | `engine.Cache` | `%engine.Cache%` |
| `EngineDebug` | Engine | `engine.Debug` | `%engine.Debug%` |
| `EngineTesting` | Engine | `engine.Testing` | `%engine.Testing%` |
| `EngineLibraries` | Engine | `engine.Libraries` | `%engine.Libraries%` |
| `GoalSteps` | Goal | `goal.Steps` | `%goal.Steps%` |
| `StepActions` | Step | `step.Actions` | `%step.Actions%` |

---

## Resolved Questions (2026-02-17)

### Q1: Cache — RESOLVED: Option A (convention-wired wrapper)
`EngineCache` wraps `ICache`, convention-wired as `engine.Cache`. Delegates to the pluggable implementation. External caches still plug in via the `ICache` interface.

### Q2: Handler folder name — RESOLVED: Keep `modules/`
The folder stays as `modules/`. The name maps directly to PLang syntax (`module.action`). User-facing naming matters.

### Q3: Data and Type — RESOLVED: Move to Engine root
`Data` and `Type` move from `Memory/` to `Engine/`. Namespace becomes `App.Engine`. They're root value types used everywhere.

### Q4: Dot-naming for partial files — RESOLVED: Use dot convention
`Goal.Methods.cs`, `Step.Methods.cs`, `Action.Methods.cs`. Visual grouping in IDEs.

### Q5: EventScope — RESOLVED: Move to Events/
`EventScope` moves from `Context/` to `Events/`. It's semantically an events container.

---

## File Count Summary

| Area | Current files | After migration |
|------|--------------|-----------------|
| Engine root | 0 | 4 (this.cs, Data.cs, Type.cs, Info.cs) |
| Goals/ | 0 | 4 |
| Goals/Steps/ | 0 | 4 |
| Goals/Steps/Actions/ | 0 | 4 |
| Channels/ | 3 | 3 |
| Property/ | 1 | 1 |
| Events/ | 3 → split | 5 |
| Serializers/ | 6 | 7 |
| Cache/ | 5 (scattered) | 6 (grouped + EngineCache) |
| Debug/ | 1 | 1 |
| Testing/ | 1 | 1 |
| Context/ | 3 + 2 from Core | 4 (EventScope moved to Events) |
| Memory/ | 8 | 6 (Data + Type moved out) |
| Libraries/ | 8 | 8 |
| modules/ | ~89 | ~89 |
| Errors/ | 10 | 10 |
| Utility/ | 2 | 2 |
| Parsing/ | 1 | 1 |
| Mapping/ | 1 | 1 |
| **Total** | ~165 | ~165 |

No files are created or deleted. Every file moves to a new location and gets a new namespace. ~10 classes get renamed to follow the `{Owner}{Capability}` convention.

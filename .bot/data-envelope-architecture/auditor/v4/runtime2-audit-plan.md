# App Full Audit — Phased Plan

## Overview

~8,000 lines of production code, ~18,600 lines of tests across 164 files. Too large for one pass. Split into 8 audit phases, ordered by **ripple impact** — foundation first, layers above inherit confidence.

---

## Phase 1: Data + Type + Variables (Foundation)
**Risk: Highest — every module depends on these**

Files:
- `Engine/Memory/Data.cs` (332 lines) — universal result type, Value setter, Type lazy derivation
- `Engine/Memory/Data.Result.cs` — Ok/Fail/Merge/Error
- `Engine/Memory/Data.Navigation.cs` — GetChild, dot-notation, bracket indexing
- `Engine/Memory/Data.Envelope.cs` (231 lines) — Wrap/Compress/Encrypt pipeline
- `Engine/Memory/Variables.cs` (261 lines) — ConcurrentDictionary, system vars, Clone, variable resolution
- `Engine/Memory/Properties.cs` (109 lines) — IList<Data> wrapper
- `Engine/Memory/Path.cs` (302 lines) — PLangPath rich path type
- `Engine/Memory/TypeJsonConverter.cs` — Type serialization
- `Engine/Types/this.cs` (662 lines) — PLang↔CLR types, Kind, MIME, compressibility
- `Engine/Utility/TypeMapping.cs` (363 lines) — static type conversion (pre-Engine.Types)

Tests: `Memory/DataTests.cs`, `Memory/VariablesTests.cs`, `Types/EngineTypesTests.cs`, `Utility/`, `Modules/Path/`

Focus: Thread safety (Variables concurrent access), Value setter side effects, Type derivation correctness, TypeMapping↔Engine.Types overlap/migration path, navigation edge cases, serialization round-trips.

---

## Phase 2: Engine + Context + Actor
**Risk: High — orchestration root, lifecycle management**

Files:
- `Engine/this.cs` (353 lines) — root object, RunGoalAsync, variable resolution, IAsyncDisposable
- `Engine/Context/PLangContext.cs` (349 lines) — per-request state, system variables, event scope, lifecycle resolution
- `Engine/Context/Actor.cs` — System/Service/User actors
- `Engine/Context/EventScope.cs` — event re-entrance control

Tests: `Core/EngineTests.cs`, `Context/`

Focus: Object lifecycle (disposal, cancellation), context propagation chain, actor isolation, system variable registration (the `!engine`/`!context` security concern), CreateChild/Clone semantics, event re-entrance guards.

---

## Phase 3: Goals → Steps → Actions (Entity Hierarchy)
**Risk: High — execution pipeline**

Files:
- `Engine/Goals/this.cs` (299 lines) — goal collection, load, lookup
- `Engine/Goals/Goal/this.cs` (146 lines) — goal entity
- `Engine/Goals/Goal/Methods.cs` (199 lines) — goal execution
- `Engine/Goals/Goal/Steps/Step/this.cs` — step entity
- `Engine/Goals/Goal/Steps/Step/Methods.cs` (201 lines) — step execution, error handling, retry
- `Engine/Goals/Goal/Steps/Step/Actions/` — action dispatch

Tests: `Core/GoalTests.cs`, `Core/GoalsTests.cs`, `Core/StepTests.cs`, `Core/StepErrorHandlingTests.cs`, `Core/StepRetryTests.cs`, `Core/ActionsTests.cs`, `Core/StartGoalTests.cs`

Focus: OBP compliance (behavior on owner, smart collections), step error handling/retry logic, action dispatch, goal parameter injection, iteration ownership (Steps.Run vs caller loop).

---

## Phase 4: CallStack + Errors
**Risk: Medium — debugging and error reporting**

Files:
- `Engine/CallStack/this.cs` (213 lines) — ConcurrentStack, depth limit, execution history
- `Engine/CallStack/CallFrame.cs` (192 lines) — frame tracking
- `Engine/CallStack/ExecutedStep.cs` — step recording
- `Engine/CallStack/SerializableCallStack.cs` — serialization
- `Engine/Errors/Error.cs` (198 lines) — base error
- `Engine/Errors/Exceptions.cs` (134 lines) — error subtypes
- `Engine/Errors/` — ActionError, ServiceError, etc.

Tests: `Core/CallStackTests.cs`, `Core/CallStackIntegrationTests.cs`, `Core/CallFrameTests.cs`, `Errors/`

Focus: Stack depth enforcement (MaxDepth=1000 — is it tested?), thread safety of ConcurrentStack, error chain construction, error category classification, serializable representation fidelity.

---

## Phase 5: Events + Lifecycle
**Risk: Medium — cross-cutting, re-entrance danger**

Files:
- `Engine/Events/this.cs` (157 lines) — event collection
- `Engine/Events/Lifecycle/` — lifecycle phases (Before/After × Load/Runtime)
- `Engine/Events/Lifecycle/Bindings/` — event binding entities
- `Engine/Events/Lifecycle/Bindings/Binding/this.cs` (142 lines) — binding execution

Tests: `Core/EventCollectionTests.cs`, `Core/EventIntegrationTests.cs`, `Foundation/EventCacheInvalidationTests.cs`

Focus: Event re-entrance prevention (TryEnterEvent/ExitEvent), event cache invalidation correctness, binding resolution order, Before-event Handled flag semantics, lifecycle phase transitions.

---

## Phase 6: Channels + Serializers
**Risk: Medium — I/O boundary, external data**

Files:
- `Engine/Channels/this.cs` (235 lines) — channel management
- `Engine/Channels/Channel/this.cs` (159 lines) — channel entity
- `Engine/Channels/Serializers/this.cs` (171 lines) — serializer registry
- `Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs` (118 lines)
- `Engine/Channels/Serializers/Serializer/TextStreamSerializer.cs` (113 lines)

Tests: `IO/ChannelTests.cs`, `IO/ChannelDataTests.cs`, `IO/IOTests.cs`, `Serialization/`

Focus: Stream lifecycle (disposal, buffering), JSON serialization edge cases, Data↔stream round-trip fidelity, channel direction enforcement, untrusted input handling at serialization boundary.

---

## Phase 7: Action Handlers (Modules)
**Risk: Lower — leaf nodes, well-tested**

Files: ~90 handler files across 14 modules

Tests: `Modules/variable/`, `Modules/file/`, `Modules/list/`, `Modules/math/`, `Modules/convert/`, `Modules/assert/`, `Modules/condition/`, `Modules/error/`, `Modules/event/`, `Modules/goal/`, `Modules/loop/`, `Modules/output/`, `Modules/library/`, `Modules/mock/`

Focus: OBP compliance (navigate, don't pass), error return convention (ServiceError with distinct keys), handler parameter validation at system boundary, mock/test handler isolation, file handler filesystem abstraction usage (never System.IO).

Split into sub-phases if needed:
- 7a: Core handlers (variable, condition, loop, goal, output, error)
- 7b: Data handlers (list, math, convert)
- 7c: I/O handlers (file, library)
- 7d: Cross-cutting handlers (event, assert, mock)

---

## Phase 8: Utilities + Support
**Risk: Lower — but GoalMapper is critical for builder pipeline**

Files:
- `Engine/Utility/GoalMapper.cs` (150 lines) — Building.Model → App mapping
- `Engine/Utility/PrParser.cs` (181 lines) — .pr.json parsing
- `Engine/Cache/` — step cache with TTL
- `Engine/Properties/this.cs` (114 lines) — key-value store
- `Engine/Debug/this.cs` (201 lines) — debugger
- `Engine/Test/this.cs` (217 lines) — test runner
- `Engine/Libraries/Library/this.cs` (174 lines) — external DLL loading

Tests: `Utility/`, `Core/StepCacheTests.cs`, `Modules/LibrariesTests.cs`

Focus: GoalMapper fidelity (does the mapped App goal match what the builder intended?), PrParser error handling for malformed .pr files, cache TTL enforcement, library discovery and action resolution.

---

## Recommended Order

```
Phase 1 (Data/Type/Variables)     ← everything depends on this
  ↓
Phase 2 (Engine/Context/Actor)      ← orchestration root
  ↓
Phase 3 (Goals/Steps/Actions)       ← execution pipeline
  ↓
Phase 4 (CallStack/Errors)          ← debugging infrastructure
  ↓
Phase 5 (Events/Lifecycle)          ← cross-cutting
  ↓
Phase 6 (Channels/Serializers)      ← I/O boundary
  ↓
Phase 7 (Action Handlers)           ← leaf nodes
  ↓
Phase 8 (Utilities/Support)         ← build pipeline + tooling
```

Each phase: read code, check OBP, check thread safety, check error handling, check tests, write findings. Estimate 1 session per phase, maybe 2 for Phase 1 and Phase 3.

# `PLang/App/` — Post-Cleanup Target Tree

What `PLang/App/` should look like after all 15 stages land. This is the *destination*, not a stage-by-stage trace — read this when you want to judge the end state at a glance instead of reading prose.

The tree is annotated with what each stage does to it. Folders/files with no marker are unchanged. Where the destination forks (e.g. "JsonSerializerOptions could go to one home or disperse"), the tree shows the architect's lean and the alternatives are listed under [Open judgment calls](#open-judgment-calls).

## Marker key

| Marker | Meaning |
|---|---|
| `(NEW)` | Folder or file created by the cleanup |
| `(MOVED ← X)` | Same content, relocated from X |
| `(RENAMED ← Y)` | Same content, renamed from Y |
| `(DELETED → Z)` | Removed; behavior absorbed by Z |
| `(SHRUNK)` | Same path, materially smaller after refactor |
| `(deferred)` | Architect-flagged but not in any current stage |
| *(no marker)* | Unchanged |

## End-state tree

```
PLang/App/
├── Actor/
│   ├── Context/
│   │   ├── Trace/this.cs
│   │   └── this.cs
│   └── this.cs
├── Attributes/
├── Build/
│   ├── this.Snapshot.cs
│   └── this.cs                              (SHRUNK; gains build-mode branch from App.Start, stage 12)
├── Cache/
│   ├── MemoryStepCache.cs
│   └── this.cs
├── CallStack/
│   ├── Audit/this.cs
│   ├── Call/
│   │   ├── Children/this.cs
│   │   ├── Diffs/this.cs
│   │   ├── Errors/this.cs
│   │   ├── Tags/this.cs
│   │   ├── this.Snapshot.cs
│   │   └── this.cs                          (gains Call.ExecuteAsync(handler, context), stage 10)
│   ├── Diff.cs
│   ├── Flags.cs
│   ├── RestoredFrame.cs
│   ├── this.Snapshot.cs
│   └── this.cs                              (now exposed as app.CallStack, stage 7)
├── Catalog/
│   ├── ActionSpec.cs
│   ├── ExampleSpec.cs
│   ├── Examples/                            (NEW; collection type, stage 14)
│   │   └── this.cs                          (RENAMED ← ExampleHelpers.cs + ExampleRenderer.cs; "Helpers"/"Renderer" suffixes are Rule A red flags)
│   ├── TypeEntry.cs
│   └── this.cs                              (SHRUNK; gains Describe / GetDefaults / IsVariableNameSlot / DescribeReturnType / FormatDefault / GetChannelInventory, stage 9)
├── Callback/
│   ├── AskCallback.cs
│   ├── ErrorCallback.cs
│   ├── ICallback.cs
│   ├── Signature/
│   │   └── this.cs                          (ExpiresInMs → TimeSpan? Expires, stage 13)
│   └── this.cs                              (may gain Error.Callback materialization, stage 11)
├── Channels/
│   ├── Channel/
│   │   ├── Event/                           (RENAMED ← EventContext.cs, stage 14)
│   │   │   └── this.cs
│   │   ├── Goal/this.cs
│   │   ├── Message/this.cs
│   │   ├── Migration/                       (RENAMED ← MigrationEnvelope.cs, stage 14)
│   │   │   └── this.cs
│   │   ├── Session/this.cs
│   │   ├── Stream/this.cs
│   │   └── this.cs
│   ├── Serializers/
│   │   ├── Serializer/
│   │   │   ├── JsonStreamSerializer.cs
│   │   │   ├── PlangDataSerializer.cs
│   │   │   ├── PlangSerializer.cs
│   │   │   ├── TextStreamSerializer.cs
│   │   │   └── this.cs
│   │   ├── PropertyFilters/                 (deferred — not in current stages; flagged: three filters = missing collection type, Rule B)
│   │   │   ├── SensitivePropertyFilter.cs
│   │   │   ├── TransportPropertyFilter.cs
│   │   │   └── ViewPropertyFilter.cs
│   │   ├── TimeSpanIso8601Converter.cs
│   │   ├── TypeJsonConverter.cs
│   │   ├── UnregisteredMimeType.cs
│   │   └── this.cs
│   └── this.cs                              (SHRUNK; v1 helpers gone — stage 2; ReadAsync<T>(filePath) gone — stage 8; Serializers carry-over gone — stage 1)
├── Choices/
│   └── this.cs                              (private static _registry → instance field, stage 15)
├── Config/
│   ├── IConfig.cs
│   ├── ModuleView.cs
│   ├── Scope.cs
│   └── this.cs
├── Data/
│   ├── Navigators/...
│   ├── PlangTypeConverter.cs
│   ├── Properties.cs
│   ├── Providers/DefaultGrepProvider.cs
│   ├── Providers/IGrepProvider.cs
│   ├── TString.cs
│   ├── this.Compare.cs
│   ├── this.Envelope.cs                     (private static readonly _envelopeJsonOptions → instance, stage 15)
│   ├── this.Navigation.cs
│   ├── this.Result.cs
│   └── this.cs
├── Debug/
│   └── this.cs                              (CallStack property moved out, stage 7; subsystem itself stays — owns debug-mode event bindings, ~748 lines unchanged in scope)
├── Errors/
│   ├── ActionError.cs
│   ├── AskError.cs
│   ├── AssertionError.cs
│   ├── CallChainRenderer.cs                 (deferred — Rule A "Renderer" suffix, not in any stage)
│   ├── CallbackGoalErrors.cs
│   ├── Error.cs                             (Error.Callback materialization may move out, stage 11)
│   ├── ErrorCategory.cs
│   ├── Exceptions.cs
│   ├── GoalError.cs
│   ├── IError.cs
│   ├── ParamSnapshot.cs
│   ├── ProgramError.cs
│   ├── ServiceError.cs
│   ├── SettingsError.cs
│   ├── StepError.cs
│   ├── Trail/
│   │   ├── this.Snapshot.cs
│   │   └── this.cs
│   ├── ValidationError.cs
│   ├── this.Snapshot.cs
│   └── this.cs                              (App back-ref injection dropped, stage 11)
├── Events/
│   ├── EventType.cs
│   ├── Lifecycle/Bindings/Binding/this.cs
│   ├── Lifecycle/Bindings/this.cs
│   ├── Lifecycle/this.cs
│   └── this.cs
├── FileSystem/
│   ├── Default/...
│   ├── IPLangFileSystem.cs
│   ├── Path.cs
│   └── this.cs                              (may gain ReadAsync<T>(filePath), stage 8)
├── Goals/
│   ├── Goal/
│   │   ├── GoalCall.cs
│   │   ├── Methods.cs                       (deferred — could rename to this.Format.cs to match partial-class convention)
│   │   ├── Steps/Step/Actions/Action/Modifiers/this.cs
│   │   ├── Steps/Step/Actions/Action/this.cs
│   │   ├── Steps/Step/Actions/this.cs
│   │   ├── Steps/Step/CacheSettings.cs
│   │   ├── Steps/Step/ErrorOrder.cs
│   │   ├── Steps/Step/this.cs
│   │   ├── Steps/this.cs
│   │   └── this.cs
│   ├── Setup/this.cs
│   └── this.cs
├── KeepAlive/                               (NEW, stage 3)
│   └── this.cs                              (collection — Add / Remove / IReadOnlyList<T> / DisposeAsync; replaces App._keepAlive private list)
├── Modules/
│   └── this.cs                              (SHRUNK 464→~150; lifts six methods to Catalog — stage 9; self-disposes — stage 4)
├── Providers/
│   ├── IProvider.cs
│   ├── this.Snapshot.cs
│   └── this.cs                              (self-disposes, stage 4)
├── Services/
│   ├── Service/this.cs
│   └── this.cs
├── Settings/
│   ├── ISettingsStore.cs
│   ├── SettingsVariable.cs
│   ├── SqliteSettingsStore.cs
│   └── this.cs
├── Snapshot/
│   ├── ISnapshotted.cs
│   └── this.cs
├── Statics/
│   ├── this.Snapshot.cs
│   └── this.cs                              (App.GetStatic shim deleted — stage 5; the Statics @this is unchanged)
├── Test/
│   ├── Coverage.cs
│   ├── Results.cs
│   ├── TestFile.cs
│   ├── TestRun.cs
│   ├── TestStatus.cs
│   ├── this.Snapshot.cs
│   └── this.cs                              (handles ChildAppCreated event differently — stage 15; design TBD)
├── Types/
│   └── this.cs                              (ABSORBS Utils/PlangTypeIndex.cs entirely — stage 15; ConcurrentDictionary caches become instance fields; locks gone because construction is deterministic at App.Start)
├── Utils/                                   (NEARLY EMPTY; only pure utilities and extension methods remain)
│   ├── CommandLineParser.cs
│   ├── MimeTypes.cs                         (deferred — could move under Channels/Serializers/)
│   ├── PathExtension.cs
│   ├── RegisterStartupParameters.cs         (deferred — likely belongs in Build/ or as App.Start helper)
│   ├── StringDistance.cs
│   ├── TypeConverter.cs
│   └── TypeMapping.cs
│
│   (DELETED → elsewhere, all stage 15 unless noted:)
│   • Json.cs                                → JsonSerializerOptions disperse to consumers (see open question)
│   • PlangTypeIndex.cs                      → App/Types/this.cs
│   • ReservedKeywords.cs                    → App/Variables/ (reserved-name constants live with the Variables @this)
├── Variables/
│   ├── IRawNameResolvable.cs
│   ├── Reserved.cs                          (NEW or RENAMED ← Utils/ReservedKeywords.cs, stage 15; well-known variable names belong with the Variables owner)
│   ├── Variable.cs
│   ├── this.Snapshot.cs
│   ├── this.SnapshotAt.cs
│   └── this.cs
├── modules/                                 (unchanged structure — per-handler files; internal cleanups only)
│   └── ...
│   • llm/providers/OpenAiProvider.cs        (private static int _requestCount → instance or deleted, stage 15; design TBD)
│   • test/run.cs                            (internal static event ChildAppCreated → different shape, stage 15; design TBD)
├── GlobalUsings.cs
├── Info.cs                                  (deferred — small loose root, not in any current stage; could become Info/this.cs but trivial)
├── View.cs                                  (deferred — enum loose at root; could move under Channels/Serializers/ since that's where the views are consumed)
└── this.cs                                  (SHRUNK 681→<300; the sum of stages 3, 4, 5, 6, 7, 10, 11, 12)
```

## Open judgment calls

The tree above shows the architect's lean for each fork. These are the places to push back if the lean is wrong.

### 1. JsonSerializerOptions destination (stage 15)

Today: ~7 `JsonSerializerOptions` singletons live as `static readonly` in `Utils/Json.cs` (`CaseInsensitiveRead`, `CamelCaseIndented`, `SnapshotClone`, `DiagnosticOutput`, `PrWrite`, etc.) plus more inline elsewhere.

Three destinations possible:
- **(a) Disperse to consumers** — `SnapshotClone` → `App/Snapshot/this.cs`, `PrWrite` → `App/Build/this.cs`, `EnvelopeJsonOptions` (already inline in `Data/this.Envelope.cs`) stays where it is. Each consumer owns its own options instance.
- **(b) Single home** — `App/Json/this.cs` with all options as instance properties.
- **(c) Under Channels/Serializers** — these *are* serialization config; could live there as instance state.

**Architect's lean: (a) disperse.** OBP says data lives with its owner; most options are genuinely consumer-specific. The "shared" feel is an artifact of having historically put them in one file. The few options that really are reused across consumers (e.g. `CaseInsensitiveRead`) move to whichever consumer owns the canonical use; the rest get their own instance per consumer.

### 2. ReservedKeywords destination (stage 15)

Today: `Utils/ReservedKeywords.cs` is a `static class` with ~25 well-known variable names (`!Test`, `!StartingApp`, `!Signature`, `Identity`, `MyIdentity`, etc.). Mostly `static readonly` strings; one mutable `static string Test = "!Test"` (Rule C hit) and one mutable `static List<string> keywords = new()`.

Two destinations:
- **(a) `App/Variables/Reserved.cs`** — co-locate with the Variables `@this` since these are *names of variables*. Either a static class (constants are fine — `const`) or properties on `App.Variables.@this`.
- **(b) `App/Reserved/this.cs`** — own folder; Variables doesn't have to know about reserved names.

**Architect's lean: (a) under Variables.** These aren't a separate concept from variables; they're a subset (well-known names). The Variables `@this` is the natural owner of "things you can name." Constants stay as `const` strings, which Rule C explicitly permits.

### 3. `OpenAiProvider._requestCount` (stage 15)

Today: `private static int _requestCount` on a class that is already actor-resolved (each Actor has its own provider instance). The static is wrong scope — counter is process-global on something that's per-actor.

Three options:
- **(a) Instance field on the provider** — counter resets per-actor; matches the resolution model
- **(b) Promote to App-level** — `app.LlmStats.RequestCount` if cross-actor metering is wanted
- **(c) Delete** — verify it's actually used; static counters often turn out to be debug-print leftovers

**Architect's lean: (c), check first.** Cheap to verify. If used for live behavior, then (a). (b) only if there's a real metering requirement, which there probably isn't.

### 4. `Test.run.ChildAppCreated` static event (stage 15)

Today: `internal static event Action<App.@this>? ChildAppCreated` — child-app discovery hook for the test runner. Static event = process-global subscription target with no `@this`.

Two shapes:
- **(a) Event on App `@this`** — `app.OnChildAppCreated` raised when this app spawns a child; parent-side test code subscribes through the parent App. Doesn't work if the test infra needs to discover a child it didn't spawn directly.
- **(b) Test runner registry** — the test infrastructure owns a registry of child-app callbacks, parent App raises into it. Moves the static-ness onto the test infra, which is itself a singleton per test process.

**Architect's lean: needs design discussion when stage 15 is approached.** Don't pre-commit. The right shape depends on how the test runner currently discovers child apps. This is one of two stage-15 hits the cleanup plan flags as design-required.

### 5. Loose root files: `Info.cs`, `View.cs`

Not in any current stage. Mentioned in the cleanup plan's "deferred" list. The tree shows them as deferred.

- `Info.cs` (10 lines) — `[PlangType("info")]` record with two properties. Trivial; could become `Info/this.cs` for OBP consistency, but cost ≈ benefit ≈ near-zero.
- `View.cs` (50 lines) — enum with six values, consumed only by serialization property filters. Could move under `Channels/Serializers/View.cs`. Same trivial-cost-trivial-benefit calculation.

**Architect's lean: leave both deferred.** Pick up incidentally if a stage touches their consumers; don't carve a stage for ~60 lines.

### 6. `PropertyFilters/` collection (Rule B candidate)

Today: `Channels/Serializers/SensitivePropertyFilter.cs`, `TransportPropertyFilter.cs`, `ViewPropertyFilter.cs` — three sibling files implementing the same filter shape. By Rule B (`Get<Plural>()` is a missing collection type), and by smell #1 (collection of similar things with no owner), these want a `PropertyFilters/` collection that *is* the registry.

Not in any current stage. Architect flagged as deferred — would be a small extra stage, candidate for Tier 4. Ingi may want to add it as stage 16 or fold it into stage 14 (compound-name + collection). Until decided, the three files stay as siblings.

## What this tree does NOT change

- `Actor/` tree — already OBP-clean.
- `Data/` tree — well-organized; the Envelope JSON options on `Data/this.Envelope.cs` is the only stage-15 hit.
- `Goals/` tree — already deeply nested per the OBP convention; no stage touches the structure.
- `modules/` tree — per-handler files at the leaves; cleanup happens *inside* a few handler files (OpenAiProvider, test/run) but the folder layout is untouched.
- `Events/`, `Services/`, `Settings/`, `Snapshot/`, `Cache/`, `Attributes/`, `Config/` — no stage touches these.

## Summary of net additions / removals

**Folders added: 4**
- `App/KeepAlive/` (stage 3)
- `App/Catalog/Examples/` (stage 14, replacing two loose files)
- `App/Channels/Channel/Event/` (stage 14, replacing `EventContext.cs`)
- `App/Channels/Channel/Migration/` (stage 14, replacing `MigrationEnvelope.cs`)

**Files deleted: 5**
- `App/Utils/PlangTypeIndex.cs` (stage 15 → `App/Types/this.cs`)
- `App/Utils/Json.cs` (stage 15 → disperses)
- `App/Utils/ReservedKeywords.cs` (stage 15 → `App/Variables/Reserved.cs`)
- `App/Catalog/ExampleHelpers.cs` (stage 14 → `App/Catalog/Examples/this.cs`)
- `App/Catalog/ExampleRenderer.cs` (stage 14 → `App/Catalog/Examples/this.cs`)

**Files renamed: 2**
- `App/Channels/Channel/EventContext.cs` → `App/Channels/Channel/Event/this.cs`
- `App/Channels/Channel/MigrationEnvelope.cs` → `App/Channels/Channel/Migration/this.cs`

**Files materially shrunk: 5**
- `App/this.cs` 681 → <300 (multiple stages)
- `App/Modules/this.cs` 464 → ~150 (stage 9)
- `App/Channels/this.cs` 277 → <150 (stages 1, 2, 8)
- `App/Build/this.cs` (gains content from App.Start, stage 12)
- `App/Catalog/this.cs` (gains content from Modules, stage 9)

**Folders unchanged: 14** (Actor, Attributes, Cache, Config, Data, Debug, Events, FileSystem, Goals, Services, Settings, Snapshot, Statics, Variables) — internal file edits in some, but folder layout untouched.

---

This tree is the architect's destination. If a stage's stage-N file later proposes something inconsistent with this tree, the inconsistency is the trigger to revisit the tree — every stage we land changes the design of the next.

# `PLang/App/` — Post-Cleanup Target Tree

What `PLang/App/` should look like after all 15 stages land. This is the *destination*, not a stage-by-stage trace — read this when you want to judge the end state at a glance instead of reading prose.

The tree is annotated with what each stage does to it, and revised against Ingi's review of v1 (2026-05-07). Folders/files with no marker are unchanged.

**Baseline:** trunk after the runtime2-channels merge (`origin/runtime2` at `260ba46f`). The merge already landed two pieces this doc had previously called "stage 14": `Channel/EventContext.cs` is now `Channel/Events/this.cs`, and `Channel/MigrationEnvelope.cs` is gone (the Migration concept dropped). Stage 14's table is updated accordingly.

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
│   ├── Choices/                             (MOVED ← App/Choices/; build-time vocabulary registry — see comment below)
│   │   └── this.cs                          (private static _registry → instance, stage 15)
│   ├── this.Snapshot.cs
│   └── this.cs                              (SHRUNK; gains build-mode branch from App.Start, stage 12)
├── Cache/
│   ├── Memory.cs                            (RENAMED ← MemoryStepCache.cs; "StepCache" was role-suffix soup; per-impl-variant under Cache/)
│   └── this.cs
├── CallStack/
│   ├── Audit/this.cs
│   ├── Call/
│   │   ├── Children/this.cs
│   │   ├── Diffs/this.cs
│   │   ├── Errors/this.cs
│   │   ├── Position.cs                      (RENAMED ← ../RestoredFrame.cs; deserialized positional triple, sibling to Call snapshot — see open question)
│   │   ├── Tags/this.cs
│   │   ├── this.Snapshot.cs
│   │   └── this.cs                          (gains Call.ExecuteAsync(handler, context), stage 10)
│   ├── Diff.cs
│   ├── Flags.cs
│   ├── this.Snapshot.cs
│   └── this.cs                              (now exposed as app.CallStack, stage 7)
├── Catalog/                                 (open: should this whole tree move under Build/? — see open question)
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
│   └── this.cs                              (absorbs Signature/this.cs as a property — stage 13; ExpiresInMs → TimeSpan? Expires; the Signature subfolder goes away)
├── Channels/
│   ├── Channel/
│   │   ├── Events/this.cs                   (already in trunk — ex-EventContext.cs)
│   │   ├── Goal/this.cs
│   │   ├── Message/this.cs
│   │   ├── Session/this.cs
│   │   ├── Stream/this.cs
│   │   └── this.cs
│   ├── Serializers/
│   │   ├── Serializer/
│   │   │   ├── Json.cs                      (RENAMED ← JsonStreamSerializer.cs)
│   │   │   ├── Plang.cs                     (RENAMED ← PlangSerializer.cs OR PlangDataSerializer.cs — see open question on which one survives)
│   │   │   ├── Text.cs                      (RENAMED ← TextStreamSerializer.cs)
│   │   │   └── this.cs
│   │   ├── PropertyFilters/                 (NEW; collection — stage 14 / 16; three filters belong in a registry by Rule B)
│   │   │   ├── Sensitive.cs                 (RENAMED ← SensitivePropertyFilter.cs; suffix dropped)
│   │   │   ├── Transport.cs                 (RENAMED ← TransportPropertyFilter.cs)
│   │   │   ├── View.cs                      (RENAMED ← ViewPropertyFilter.cs)
│   │   │   └── this.cs
│   │   ├── TimeSpanIso8601Converter.cs
│   │   ├── TypeJsonConverter.cs
│   │   ├── UnregisteredMimeType.cs
│   │   └── this.cs
│   └── this.cs                              (SHRUNK; v1 helpers gone — stage 2; ReadAsync<T>(filePath) gone — stage 8; Serializers carry-over gone — stage 1)
├── Config/
│   ├── IConfig.cs
│   ├── ModuleView.cs                        (per-module typed settings view returned by app.Config.For<T>(context); generic, used at runtime — see comment below)
│   ├── Scope.cs
│   └── this.cs
├── Data/
│   ├── Converter.cs                         (RENAMED ← PlangTypeConverter.cs; namespace is App.Data so the prefix is redundant)
│   ├── Navigators/...
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
├── Events/                                  (collapsed — see open question)
│   ├── Binding/this.cs                      (MOVED ← Lifecycle/Bindings/Binding/this.cs)
│   ├── Bindings/this.cs                     (MOVED ← Lifecycle/Bindings/this.cs; collection of Binding)
│   ├── EventType.cs
│   └── this.cs                              (Before/After moved to properties on @this; the Lifecycle/ folder layer is removed)
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
├── Providers/                               (open: rename to Code/, see Provider→Code design discussion below)
│   ├── IProvider.cs
│   ├── this.Snapshot.cs
│   └── this.cs                              (self-disposes, stage 4)
├── Services/                                (in active use — App.Services owns per-outbound-call I/O scopes, see comment below)
│   ├── Service/this.cs
│   └── this.cs
├── Settings/
│   ├── ISettingsStore.cs
│   ├── SettingsVariable.cs
│   ├── SqliteSettingsStore.cs
│   └── this.cs
├── Snapshot/
│   ├── ISnapshot.cs                         (RENAMED ← ISnapshotted.cs; past-participle interface naming is awkward; matches IDisposable/IEnumerable convention)
│   └── this.cs
├── Statics/
│   ├── this.Snapshot.cs
│   └── this.cs                              (App.GetStatic shim deleted — stage 5; the Statics @this is unchanged)
├── Test/                                    (Test prefix dropped on the loose siblings; namespace is App.Test so the prefix was redundant)
│   ├── Coverage.cs
│   ├── File.cs                              (RENAMED ← TestFile.cs)
│   ├── Results.cs
│   ├── Run.cs                               (RENAMED ← TestRun.cs)
│   ├── Status.cs                            (RENAMED ← TestStatus.cs)
│   ├── this.Snapshot.cs
│   └── this.cs                              (handles ChildAppCreated differently — stage 15; design TBD, see comment below)
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
│   • Json.cs                                → JsonSerializerOptions consolidate (single home, see open question on case-mismatch concern)
│   • PlangTypeIndex.cs                      → App/Types/this.cs
│   • ReservedKeywords.cs                    → App/Variables/Reserved.cs (all const/readonly, no mutable static)
├── Variables/
│   ├── Calls/                               (already in trunk — per-call scopes from channels merge)
│   │   ├── Call/this.cs
│   │   └── this.cs
│   ├── IRawNameResolvable.cs
│   ├── Reserved.cs                          (NEW or MOVED ← Utils/ReservedKeywords.cs, stage 15; well-known variable names belong with the Variables owner; all values const/readonly)
│   ├── Variable.cs
│   ├── this.Snapshot.cs
│   ├── this.SnapshotAt.cs
│   └── this.cs
├── modules/                                 (unchanged structure — per-handler files; internal cleanups only)
│   └── ...
│   • llm/providers/OpenAiProvider.cs        (private static int _requestCount → DELETED, stage 15; temp blocker confirmed by Ingi 2026-05-07; todo logged)
│   • test/run.cs                            (internal static event ChildAppCreated → different shape, stage 15; design TBD, see comment below)
├── GlobalUsings.cs
├── Info.cs                                  (deferred — small loose root, not in any current stage; could become Info/this.cs but trivial)
├── View.cs                                  (deferred — enum loose at root; could move under Channels/Serializers/ since that's where the views are consumed)
└── this.cs                                  (SHRUNK 681→<300; the sum of stages 3, 4, 5, 6, 7, 10, 11, 12)
```

## Inline answers to v1 review questions

These are the questions Ingi raised on v1 of this doc that the tree above doesn't fully explain on its own.

### `MemoryStepCache` — should this live under Step/?

The class caches `Data.@this` keyed by string — Step results, but the cached *thing* is Data, not Step. The owner is "the cache subsystem," not Step. Per OBP, the impl-variant pattern keeps `Cache/` as the type and the in-memory implementation as `Cache/Memory.cs`. If a SqliteStepCache or RedisCache lands later, it joins as `Cache/Sqlite.cs` / `Cache/Redis.cs` — same shape as `Settings/SqliteSettingsStore.cs`. Moving the file under `Steps/Step/Cache/` would couple a generic cache concept to the Step entity and complicate later re-use; keeping it at App root preserves the swap-the-impl pattern.

### `RestoredFrame` — isn't it just a Call?

It almost is. The hard difference is lifecycle: a live `Call.@this` has an AsyncLocal "current" pointer, push/pop discipline, and observable diffs. A restored frame can have *none* of that — it's a deserialized (Action, Goal, StepIndex, ActionIndex, Id) tuple used to identify *where* a callback should resume. The class doc explicitly forbids pushing it to the AsyncLocal current.

So the type does have to exist as something distinct from `Call.@this`. But the *name* is a description, not a noun — "RestoredFrame" reads "a frame that was restored." Better candidates: `Call.Position.cs` (it identifies *where* in the call tree), or `Call.@this.Snapshot` as a nested type. The tree above proposes `Call/Position.cs`. That's a stage-15-adjacent rename, not a structural change; happy to fold it differently if the position-vs-frame distinction reads wrong.

### `ActionSpec` / `ExampleSpec` — and the "remove Catalog" thread

`ActionSpec` is a structured node describing one action invocation in an `ExampleSpec` — the LLM-builder examples (`public static ExampleSpec[] ExamplesForLlm()`) are authored as data and rendered by ExampleRenderer into the formal-language string the LLM sees. So the names describe the role: "spec" = the structured shape the renderer consumes. `Spec` suffix is the OBP red-flag form (Rule A) — could become `Catalog/Action/this.cs` and `Catalog/Example/this.cs` if we want to push it.

On "remove Catalog" — I don't have memory of that thread. The current usage pattern is: `Catalog.Build(modules)` produces TypeEntry lists for the LLM; `Modules.Describe()` and `app.Types.ComplexSchemas()` both reach into Catalog data. Catalog is a build-time concept (the LLM only sees it during build) but the data structures are referenced at runtime by Types/Modules. *If* we move Catalog under Build/, those runtime references need a new home (probably move TypeEntry to Types/, and the Examples/ListActions surface stays under Build/Catalog/). I haven't pulled the trigger here because the move is non-trivial and your memory of the prior conversation matters. **Status: open** — please re-mention what the prior decision direction was and I'll fold it into the tree.

### `Callback/Signature/this.cs` — folded

You're right; Data already owns signatures via the lazy `Signature` getter. The whole `Signature/` subfolder is just a 15-line config holder for `ExpiresInMs`. Tree above absorbs it as a property on `Callback/this.cs` (stage 13 carries the TimeSpan rename through). Subfolder goes away.

### Channel/Migration — already gone

Confirmed in the merge from runtime2: `MigrationEnvelope.cs` is deleted, `modules/channel/migrate.cs` is deleted, the Stage 14 list no longer references it.

### Serializer naming — Json/Plang/Text

Tree above renames `JsonStreamSerializer.cs → Json.cs`, `TextStreamSerializer.cs → Text.cs`. The "Stream" qualifier described an implementation detail (uses Streams) not the noun.

The Plang vs PlangData distinction is real but smelly. PlangSerializer is `application/plang` (older PLang-to-PLang transport). PlangDataSerializer is `application/plang+data` (the newer wire shape callbacks ride on). If we genuinely need both mimetypes, two files; if `application/plang` is dead transport, drop it and keep `Plang.cs`. **Status: open** — which mimetypes are still in use? If only `application/plang+data`, the rename is `Plang.cs` and the older one goes.

### `SensitivePropertyFilter` etc. — agreed

Drop the `PropertyFilter` suffix. Tree above puts the three files in `PropertyFilters/` as `Sensitive.cs`, `Transport.cs`, `View.cs`. By Rule B (`Get<Plural>()` is a missing collection type) this also gets a parent `PropertyFilters/this.cs` registry. Three filters with the same shape are exactly the missing-collection smell.

### Choices — moved under Build/

Reading `Choices/this.cs` doc: "Build-time validator and catalog Describe() both go through here." Both consumers are build-time. Validator runs during `plang build`, catalog renders the LLM prompt during build. Runtime never touches Choices. Tree above moves it to `Build/Choices/`. The `[Choices]` attribute on action types stays in `App/Attributes/` (the attribute is the marker; the registry that scans for it is the build concern).

### `Config/ModuleView.cs` — what is it

Generic per-module settings view: `app.Config.For<archive.Config>(context)` returns a `ModuleView<archive.Config>` stamped with the current Actor.Context. Each `view.Resolve<T>("max", default)` walks the scope chain to find the right value. Used at runtime by handlers reading their own settings. Name is fine — it's a per-module view of settings — though `Config/View/this.cs` would be more OBP. Tree leaves it as `ModuleView.cs`; happy to push to `View/this.cs` if you want.

### `PlangTypeConverter.cs` — agreed

Renamed to `Data/Converter.cs` in the tree. Namespace is `App.Data` so the prefix is redundant.

### Events folder — collapse the Lifecycle layer

Today: `Events/this.cs` (registry), `Events/Lifecycle/this.cs` (Before/After bindings holder), `Events/Lifecycle/Bindings/this.cs` (one collection), `Events/Lifecycle/Bindings/Binding/this.cs` (one binding entity).

Reading the code: `Events/this.cs` is the registry that owns event bindings. `Events/Lifecycle/this.cs` is just two properties (`Before` and `After`) that each hold a `Bindings.@this`. Lifecycle is a meaningful axis (when, in the action lifecycle) — but the layer adds two folder hops with very little entity-shape value; Before/After could be properties directly on Events/this.cs.

Tree above proposes the collapse: `Events/this.cs` (registry, gains Before/After properties), `Events/Bindings/this.cs` (collection), `Events/Binding/this.cs` (entity). The Lifecycle/ subfolder goes away. Three folder hops becomes two.

Caveat: I haven't traced every consumer of `App.Events.Lifecycle.Bindings.Binding.@this` (used as an alias `EventBinding` everywhere). The collapse is a rename + alias re-target. Probably stage 14 territory — happy to defer to its own stage if the blast radius is bigger than expected.

### Services — actively used

Yes — `app.Services` is the per-outbound-call I/O scope collection. Each `Service` represents one outbound call's I/O scope (Channels, System Identity, Parent ref). It replaces the runtime1 "Service-as-actor" model — identity is always System; the Parent ref preserves "who triggered this." Used when an Actor makes outbound HTTP/TCP/WS calls; spawned via `app.Services.New(parent)`, `await using` for tear-down. Active referenced from `App.this.cs:229`. Tree leaves it unchanged — this was an architect oversight in v1 saying "what is it"; the answer is "it's the per-call I/O scope" and the OBP shape is correct.

### Settings — no opinion to act on

I don't have a prior architect opinion on Settings classes that I dropped from this tree. Settings looks OBP-clean: `ISettingsStore` is the contract, `SqliteSettingsStore` is the impl variant (per-impl-variant under Settings/, same shape as `Cache/Memory.cs`), `SettingsVariable` is the runtime Data subclass. If you remember a specific opinion, point me at the conversation and I'll fold it in.

### `ISnapshotted` → `ISnapshot` — agreed

Renamed in tree. `IDisposable` not `IDisposed`; `IEnumerable` not `IEnumerated`. Past-participle interface naming reads odd; `ISnapshot` matches convention.

### `Test/TestFile`, `TestRun`, `TestStatus` — drop Test prefix

Tree above renames to `File.cs`, `Run.cs`, `Status.cs`. Namespace is `App.Test` so the prefix is redundant.

### Provider → Code — design discussion

Big rename and worth thinking through, not auto-agreeing.

**The case for Code:** Providers are the C# escape hatch — places where PLang says "this needs to be code, not goals." Calling the namespace `Code` and the interface `ICode` describes literally what they are: code injected into the runtime. It's also philosophically aligned: "everything is plang, except Code" rolls off cleanly.

**The case against:** "Code" is too generic to be a type-system identifier. Every `.cs` file in this repo is "code." If `App/Code/` means "the kind of code you can swap out at runtime," readers without context will not infer that — they'll think it's the kitchen sink for anything not yet sorted. Provider has technical baggage (DI providers) but also technical precision: it *provides* an implementation of an interface that the runtime resolves by name. That's exactly what these are.

**The deeper question:** PLang has two distinct kinds of "swappable code" today.
1. **Internal pluggable defaults** — `DefaultHttpProvider`, `DefaultIdentityProvider`, `OpenAiProvider`. First-class implementations of internal contracts; not user-injected. The runtime ships with a default; you swap by registering a different one.
2. **External user code** — DLLs the user drops in to override defaults (`use 'redis.dll' for caching` in Settings/this.cs).

These are two concepts wearing one name. "Provider" describes (1) accurately; "Code" describes (2) accurately. The cleanest rename might be: keep `Provider` for (1), introduce `Code` for the act of (2) loading an external DLL. But I'd want to check first whether there's actually any (2) infrastructure today that's distinct from (1) — `App/modules/provider/load.cs` suggests the load mechanism *is* provider-loading, which collapses the distinction.

**Architect's recommendation:** rename Provider → Code only if you're committed to also renaming the *concept*: the runtime no longer talks about "providers" — it talks about "code injected to override runtime behavior." That's a story-level change, not just a find-replace. If you mean the latter, I'm in. If you mean a 88-file find-replace with no concept change, the cost-to-benefit is poor; "Provider" carries technical weight that "Code" loses.

What do you mean? **Status: open** — flagged here as a design question to settle before any rename starts.

## Open judgment calls (revised after v1 review)

The questions below are still open. The previously-listed v1 questions are folded into the inline answers above when settled.

### A. JsonSerializerOptions destination (stage 15)

**Resolved direction:** This is a Rule C application (static fields are a missing `@this`) — the question isn't *whether* to evict them, it's *where they go*. Your concern about case-mismatch errors when options disperse to consumers shifts the architect's lean from v1's "(a) disperse" to **(b) single home: `App/Json/this.cs`** with options as instance properties (`app.Json.SnapshotClone`, `app.Json.PrWrite`, etc.). One source of truth; consumers reference, not duplicate. OBP is preserved because the data has an `@this` owner — `App.Json` — even though that owner is mostly configuration and will disagree in shape with most other `@this` types. The exotic-config home gives mismatch protection, which is the dominant concern.

**Action:** stage 15 creates `App/Json/this.cs` as the consolidated home; `Utils/Json.cs` is deleted; consumers (Snapshot, Build, Data.Envelope, etc.) reference `app.Json.<Name>`. The few options that genuinely vary by consumer (e.g. each Serializer's internal options) stay private to that consumer.

### B. Catalog placement — under Build/?

Three options:
- **(a) Catalog stays at App root** (current tree) — both build-time and runtime reach into it for type schemas
- **(b) Catalog moves under Build/** — `App/Build/Catalog/` — and runtime references migrate to `App.Types.@this` (TypeEntry/Field types move with them)
- **(c) Catalog splits** — Examples + ActionSpec + ExampleSpec move under Build (LLM-only); TypeEntry stays at App root or moves to Types

**Status: open until you remember the prior thread.** The architect's lean if pressed: (b) full move under Build, with TypeEntry relocating to `App.Types`. That respects "Catalog is what the LLM sees during build" and removes the cross-tree references. But this is a meaningful refactor — wants its own stage if pursued.

### C. PlangSerializer vs PlangDataSerializer

Two mimetypes today: `application/plang` (older) and `application/plang+data` (newer; canonical envelope used by callbacks).

- **(a) Keep both** — file names become `Plang.cs` and `PlangData.cs`. The "+data" mimetype gets the cleaner name; the older one keeps the legacy name.
- **(b) Drop the older** — only `Plang.cs` survives, mapped to `application/plang+data`. The `application/plang` mimetype goes away, with a migration/back-compat plan.

**Status: open** — depends on whether `application/plang` is still in use anywhere (channels, http providers, settings store?). I'll trace usage when stage 1 (`serializers-stage-6-finish`) is approached and settle then.

### D. RestoredFrame → Call/Position?

Tree above proposes renaming to `Call/Position.cs` (it identifies *where* in the call tree, not "a frame"). Alternative: nest as `Call.@this.Snapshot` partial. Either is fine; the Position rename reads a bit better in callers ("get the position from the callback") but breaks if Call snapshot file already owns positional structure. **Status: open, low priority** — nail down when stage 10 (`app-run-redesign`) lands and Call gets a closer look.

### E. Provider → Code rename

See inline section above. **Status: open** — needs your read on whether this is a story-level rename or a find-replace.

### F. Events sub-foldering collapse

Tree above proposes collapsing `Events/Lifecycle/Bindings/Binding/` to `Events/Binding/` + `Events/Bindings/`, with Before/After moving to properties on `Events/this.cs`. **Status: open** — the alias `App.Events.Lifecycle.Bindings.Binding.@this` is referenced widely as `EventBinding`; the rename is mechanical but the blast radius wants checking before pulling. Probably its own small stage.

### G. Test.ChildAppCreated event shape (stage 15)

You couldn't recall the use case — here's what it is. `internal static event Action<App.@this>? ChildAppCreated` fires once per child App after the test runner constructs it (OsDirectory inherited, Testing.IsEnabled set, CurrentTest assigned), before the test's entry goal runs. Test code in `PLang.Tests/App/Testing/RunActionTests.cs` subscribes to probe child-App state for assertions (parallel count, OsDirectory, IsEnabled). Six test files attach to it.

It's static because there's no other way for the test to *reach* the child App before the child's first goal runs — child Apps are constructed inside the test runner's parallel loop and aren't returned to the caller. The static event is the only point where probing code can hook in.

**Two real options:**
- **(a) Event on parent App `@this`** — `parentApp.OnChildAppCreated += probe` — parent App raises into subscribers when it spawns a child. Tests subscribe via the parent. Loses the "any test can probe any child app" capability if there are multiple parents.
- **(b) Test-runner registry** — `App.Test.@this` (the test runner) owns a list of child-app callbacks. Static-ness moves onto the test runner (which is itself ~singleton per test process). Cleaner because the test runner is the natural owner.

**Architect's lean: (b).** Test runner already owns child-App lifetime; owning the discovery callback is consistent with that. But this is stage-15 material and wants verification that no probe code expects cross-process or cross-runner subscription.

### H. Loose root files Info.cs, View.cs

Unchanged from v1: leave deferred. Cost-to-benefit too thin.

## Summary of net additions / removals (revised)

**Folders added: 5**
- `App/KeepAlive/` (stage 3)
- `App/Catalog/Examples/` (stage 14)
- `App/Channels/Serializers/PropertyFilters/` (stage 14/16; Rule B collection)
- `App/Build/Choices/` (MOVED ← App/Choices/)
- `App/Json/` (stage 15; consolidated JsonSerializerOptions home)

**Files deleted: 6**
- `App/Utils/PlangTypeIndex.cs` (stage 15 → `App/Types/this.cs`)
- `App/Utils/Json.cs` (stage 15 → `App/Json/this.cs`)
- `App/Utils/ReservedKeywords.cs` (stage 15 → `App/Variables/Reserved.cs`)
- `App/Catalog/ExampleHelpers.cs` (stage 14 → `App/Catalog/Examples/this.cs`)
- `App/Catalog/ExampleRenderer.cs` (stage 14 → `App/Catalog/Examples/this.cs`)
- `App/Callback/Signature/this.cs` (stage 13 → property on `App/Callback/this.cs`)

**Files renamed: 9**
- `App/Cache/MemoryStepCache.cs` → `App/Cache/Memory.cs`
- `App/Data/PlangTypeConverter.cs` → `App/Data/Converter.cs`
- `App/Snapshot/ISnapshotted.cs` → `App/Snapshot/ISnapshot.cs`
- `App/Test/TestFile.cs` → `App/Test/File.cs`
- `App/Test/TestRun.cs` → `App/Test/Run.cs`
- `App/Test/TestStatus.cs` → `App/Test/Status.cs`
- `App/Channels/Serializers/Serializer/JsonStreamSerializer.cs` → `Json.cs`
- `App/Channels/Serializers/Serializer/TextStreamSerializer.cs` → `Text.cs`
- `App/Channels/Serializers/Serializer/PlangSerializer.cs` and `PlangDataSerializer.cs` → `Plang.cs` (single survivor) or both renamed (open question C)

**Files materially shrunk: 5**
- `App/this.cs` 681 → <300 (multiple stages)
- `App/Modules/this.cs` 464 → ~150 (stage 9)
- `App/Channels/this.cs` 277 → <150 (stages 1, 2, 8)
- `App/Build/this.cs` (gains content from App.Start, stage 12; gains Choices subfolder)
- `App/Catalog/this.cs` (gains content from Modules, stage 9)

**Folders unchanged:** Actor, Attributes, Cache (just a rename), Config, Data (mostly), Debug, Events (collapsed), FileSystem, Goals, Services, Settings, Snapshot (just a rename), Statics, Variables (gains Reserved.cs).

---

This tree is the architect's destination, revised against your v1 review. Items still open are flagged in [Open judgment calls](#open-judgment-calls). Provider→Code and Catalog placement are the two design discussions that should settle before stage 9 or stage 15 carve their stage briefs.

# `PLang/App/` — Post-Cleanup Target Tree

What `PLang/App/` should look like after all 15 stages land. This is the *destination*, not a stage-by-stage trace — read this when you want to judge the end state at a glance instead of reading prose.

The tree is annotated with what each stage does to it, and revised against Ingi's review of v1 (2026-05-07). Folders/files with no marker are unchanged.

**Baseline:** trunk after the runtime2-channels merge (`origin/runtime2` at `260ba46f`). The merge already landed two pieces this doc had previously called "stage 14": `Channel/EventContext.cs` is now `Channel/Events/this.cs`, and `Channel/MigrationEnvelope.cs` is gone (the Migration concept dropped). Stage 14's table is updated accordingly.

**v3 absorption (2026-05-08):** Reviewed `origin/runtime2-obp-restructure` architect/v1-v3 (the prior cleanup attempt that was deferred). Several insights folded into this doc and the principles file:

1. **Catalog dissolves into `app.Modules.Schema`** — the catalog is "Modules describing itself to an LLM"; Modules is the owner. Builder/UI/trace-viewer are consumers. The v3 thread Ingi remembered. Open question B (Catalog placement) is now resolved.
2. **MIME table splits two ways** — `Utils/MimeTypes.cs` does two jobs: `GetMimeType(ext)` is I/O (extension forward) → `app.Channels.Serializers.Formats`; `TryGetClrType(mimeType)` is type resolution (family rules) → `app.Types.Clr(mimeType)` overload alongside `Clr(plangName)`.
3. **Build → Builder, Test → Tester** rename — gerunds describe state; nouns name objects. Tree below applies the rename throughout.
4. **Two new sharpened rules**: Rule D (gerund-named app-graph properties) and Rule E (decomposed parameters → navigation) added to `plan/principles.md`. Both are detection rules with grep screens.
5. **Modules out of scope** with sharper rationale: the source generator + handler pattern *forces* OBP shape on module action handlers — they can't drift OBP without compile breaks. Cleaner reason than v1's "handler-level cleanup is a separate plan."

**v3 absorption — declined or revised:**

- v3 wanted ReservedKeywords folded into `app.Types/Registry.cs`. ReservedKeywords are *variable names* (`!Test`, `!Signature`, etc.), not types. `app.Variables/Reserved.cs` is the right owner. Tree keeps the Variables placement.
- v3's audit methodology (walk every `/shared/app-tree/*.md`) is **blocked**: that directory doesn't exist in the repo and never has. The methodology is moot until either those surface files get built (a meta-stage) or we pick a different diagnostic substrate (architecture.md, App/this.cs properties walked directly). Flagged in [Open judgment calls](#open-judgment-calls) as item I.
- v3's source-generator update at `LazyParamsGenerator.cs:638` is moot: source gen has been rewritten and no longer references any `App.Utils.*` symbols. Confirmed by grep.

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
├── Builder/                                 (RENAMED ← Build/, stage 16; Rule D — gerund→noun; namespace App.Build → App.Builder)
│   ├── Choices/                             (★ tentative — MOVED ← App/Choices/; re-evaluate after restructure lands; see open question)
│   │   └── this.cs                          (private static _registry → instance, stage 15)
│   ├── this.Snapshot.cs
│   └── this.cs                              (SHRUNK; gains build-mode branch from App.Start, stage 12; gains Choices subfolder if (★) confirmed)
├── Cache/
│   ├── Memory.cs                            (RENAMED ← MemoryStepCache.cs; "StepCache" was role-suffix soup; per-impl-variant under Cache/)
│   └── this.cs
├── CallStack/
│   ├── Audit/this.cs
│   ├── Call/
│   │   ├── Children/this.cs
│   │   ├── Diffs/this.cs
│   │   ├── Errors/this.cs
│   │   ├── Position.cs                      (RENAMED ← ../RestoredFrame.cs; settled per L19 review — Position reads better than RestoredFrame)
│   │   ├── Tags/this.cs
│   │   ├── this.Snapshot.cs
│   │   └── this.cs                          (gains Call.ExecuteAsync(handler, context), stage 10)
│   ├── Diff.cs
│   ├── Flags.cs
│   ├── this.Snapshot.cs
│   └── this.cs                              (now exposed as app.CallStack, stage 7)
│   (App/Catalog/ — DELETED entirely; content moves to App/Modules/Schema/, stage 9)
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
│   │   ├── Formats/                         (NEW; stage 17; mount = app.Channels.Serializers.Formats; absorbs the extension table from Utils/MimeTypes.cs and the MIME/Kind/Compressible block currently inside Types/this.cs)
│   │   │   └── this.cs                      (Mime(ext), Kind(ext), Compressible(kind), KindOf(typeValue), Add(ext, kind, mime), Remove(ext))
│   │   ├── Serializer/
│   │   │   ├── Json.cs                      (RENAMED ← JsonStreamSerializer.cs)
│   │   │   ├── Plang/                       (NEW subfolder; groups the plang-format serializers for future expansion — Protobuf etc.)
│   │   │   │   ├── this.cs                  (RENAMED ← PlangSerializer.cs; application/plang transport)
│   │   │   │   └── Data.cs                  (RENAMED ← PlangDataSerializer.cs; application/plang+data — the canonical envelope for callbacks)
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
│   ├── Schema/                              (NEW; absorbs former App/Catalog/, stage 9; mount = app.Modules.Schema)
│   │   ├── Spec/                            (per Ingi's review — record family for the structured shapes the renderer consumes)
│   │   │   ├── Action.cs                    (RENAMED ← Catalog/ActionSpec.cs; "Spec" suffix dropped, lives in Spec/)
│   │   │   └── Example.cs                   (RENAMED ← Catalog/ExampleSpec.cs)
│   │   ├── Entry.cs                         (RENAMED ← Catalog/TypeEntry.cs)
│   │   │   (App/Catalog/ExampleHelpers.cs DELETED — its single static factory is redundant; record positional constructor `new Example(...)` covers the same use case. Authors switch from `Example("intent", chain)` to `new Example("intent", chain)` — one-line migration per ExamplesForLlm() site.)
│   │   ├── Render.cs                        (RENAMED ← Catalog/ExampleRenderer.cs; instance method navigating this.Modules — Rule E; absorbs the two static formatters in modules/builder/providers/{FluidProvider,DefaultBuilderProvider})
│   │   └── this.cs                          (RENAMED ← Catalog/this.cs; instance method Build() — no parameter, navigates this.Modules — Rule E)
│   └── this.cs                              (SHRUNK 464→~150; example-rendering call becomes local navigation Schema.Render(spec); self-disposes — stage 4)
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
├── Tester/                                  (RENAMED ← Test/, stage 16; Rule D — gerund→noun; namespace App.Test → App.Tester; Test prefix dropped on loose siblings since namespace is App.Tester)
│   ├── Coverage.cs
│   ├── File.cs                              (RENAMED ← TestFile.cs)
│   ├── Results.cs
│   ├── Run.cs                               (RENAMED ← TestRun.cs)
│   ├── Status.cs                            (RENAMED ← TestStatus.cs)
│   ├── this.Snapshot.cs
│   └── this.cs                              (handles ChildAppCreated differently — stage 15; design TBD, see comment below)
├── Types/
│   ├── Conversion.cs                        (NEW partial; stage 15; absorbs Utils/TypeConverter.cs — instance ConvertTo<T> / TryConvertTo / Populate)
│   ├── Registry.cs                          (NEW partial; stage 15; absorbs Utils/PlangTypeIndex.cs + Utils/TypeMapping.cs registry portion — name↔CLR maps as instance fields, [PlangType] scan in ctor)
│   └── this.cs                              (becomes partial; stage 15; gains Clr(mimeType) overload — stage 17; the existing MIME/Kind/Compressible block at lines 215-315 moves out to Channels/Serializers/Formats/)
├── Utils/                                   (NEARLY EMPTY; only pure utilities and extension methods remain)
│   ├── CommandLineParser.cs
│   ├── PathExtension.cs                     (deferred — likely belongs in App/FileSystem/)
│   ├── RegisterStartupParameters.cs         (deferred — likely belongs in Builder/ or as App.Start helper)
│   └── StringDistance.cs
│
│   (DELETED → elsewhere:)
│   • Json.cs                                → DISPERSED to consumers, stage 15 (each consumer owns its own JsonSerializerOptions instance — Snapshot, Build PrWrite, Data.Envelope already do, the "shared" options were a junk-drawer artifact)
│   • MimeTypes.cs                           → splits two ways, stage 17:
│   │                                          • GetMimeType(ext) and friends → App/Channels/Serializers/Formats/this.cs
│   │                                          • TryGetClrType(mimeType) → App/Types/this.cs as Clr(mimeType) overload
│   • PlangTypeIndex.cs                      → App/Types/Registry.cs (partial, stage 15)
│   • ReservedKeywords.cs                    → App/Variables/Reserved.cs (all const/readonly, no mutable static, stage 15)
│   • TypeConverter.cs                       → App/Types/Conversion.cs (partial, stage 15)
│   • TypeMapping.cs                         → App/Types/Registry.cs registry portion + App/Channels/Serializers/Formats forward-lookup callers (stage 15 + 17)
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

### `ActionSpec` / `ExampleSpec` — settled: dissolve into `app.Modules.Schema`

The thread Ingi remembered is on `origin/runtime2-obp-restructure` (architect v1-v3). The argument:

- The catalog is *Modules describing itself to an LLM*. Builder/UI/trace-viewer all *consume* it.
- OBP rule #1 = behavior on the owner, not the consumer. Modules owns "what every action looks like."
- Precedent: `App/Modules/this.cs:252-257` already calls `ExampleRenderer.Render(s, this)` for action discovery — Modules already renders itself today, just from an awkward home (Catalog).
- Side win (Rule E): `Render(spec, modules)` parameter decomposition vanishes — becomes `Schema.Render(spec)` navigating `this.Modules` internally; same fix for `Build(modules)` → `Build()`.
- My v1 worry that "ActionSpec/ExampleSpec are LLM-prompt-only, never run at runtime" was wrong: the runtime *does* exercise the build path through the **builder module** (e.g. `app.modules.builder.providers.DefaultBuilderProvider.Types`), so Schema must be reachable from runtime. `app.Modules.Schema` is the home.

Per Ingi's review: spec records group under `Spec/`, suffix dropped. Tree above:

```
App/Modules/Schema/
├── Spec/
│   ├── Action.cs              (← Catalog/ActionSpec.cs)
│   └── Example.cs             (← Catalog/ExampleSpec.cs)
├── Entry.cs                   (← Catalog/TypeEntry.cs)
                                (ExampleHelpers.cs DELETED — record constructor covers the use case)
├── Render.cs                  (← Catalog/ExampleRenderer.cs; instance method, navigates this.Modules)
└── this.cs                    (← Catalog/this.cs; instance method Build(), no parameter)
```

`App/Catalog/` namespace ceases to exist. The two static formatters in `App/modules/builder/providers/{FluidProvider,DefaultBuilderProvider}` collapse into `Schema.Render` (one rendering home, not three).

Open: should `Spec/Action.cs` and `Spec/Example.cs` be flat files (current proposal) or `Spec/Action/this.cs` + `Spec/Example/this.cs` (full OBP nested form)? Ingi's comment was the flat form; I lean flat too because they're records and records often live as files next to a parent — same pattern as `App/CallStack/Diff.cs`, `App/CallStack/Flags.cs`, `App/Errors/Error.cs`.

### `Callback/Signature/this.cs` — folded

You're right; Data already owns signatures via the lazy `Signature` getter. The whole `Signature/` subfolder is just a 15-line config holder for `ExpiresInMs`. Tree above absorbs it as a property on `Callback/this.cs` (stage 13 carries the TimeSpan rename through). Subfolder goes away.

### Channel/Migration — already gone

Confirmed in the merge from runtime2: `MigrationEnvelope.cs` is deleted, `modules/channel/migrate.cs` is deleted, the Stage 14 list no longer references it.

### Serializer naming — Json/Plang/Text — RESOLVED: Plang/ subfolder

Tree above renames `JsonStreamSerializer.cs → Json.cs`, `TextStreamSerializer.cs → Text.cs`. The "Stream" qualifier described an implementation detail (uses Streams) not the noun.

Per Ingi's L19 review: PLang has multiple plang-format serializers today (`application/plang` + `application/plang+data`) and likely more in the future (Protobuf serialization of plang Data). They group under their own folder:

```
Channels/Serializers/Serializer/
├── Json.cs                     (← JsonStreamSerializer.cs)
├── Plang/
│   ├── this.cs                 (← PlangSerializer.cs; application/plang)
│   ├── Data.cs                 (← PlangDataSerializer.cs; application/plang+data)
│   └── (future: Protobuf.cs)
├── Text.cs                     (← TextStreamSerializer.cs)
└── this.cs
```

Plang gets a folder because it's a family with siblings. Json and Text are single files because there's only one of each. When/if Json or Text grow variants, they get folders too.

### `SensitivePropertyFilter` etc. — agreed

Drop the `PropertyFilter` suffix. Tree above puts the three files in `PropertyFilters/` as `Sensitive.cs`, `Transport.cs`, `View.cs`. By Rule B (`Get<Plural>()` is a missing collection type) this also gets a parent `PropertyFilters/this.cs` registry. Three filters with the same shape are exactly the missing-collection smell.

### Choices — tentatively under Builder/, ★ re-evaluate

Reading `Choices/this.cs` doc: "Build-time validator and catalog Describe() both go through here." Both consumers are build-time. Validator runs during `plang build`, Schema rendering happens at build (or at runtime via the builder module). Tree proposes `Builder/Choices/` as the destination.

Per Ingi's review: ★ tentative. Re-evaluate after Catalog dissolves into `Modules.Schema` — once the build/runtime border is clearer, the right home for Choices may shift (it could land under Modules.Schema if "Choices is what an action declares," or stay under Builder if "Choices is what the builder validates"). Mark and revisit in the same review pass.

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

### A. JsonSerializerOptions destination (stage 15) — RESOLVED: disperse

Per Ingi's L231 review: **`App/Json/` was wrong**. Json is a serialization format, not a domain entity — it doesn't deserve a root mount. v2's "single home" lean is dropped.

**Settled:** Disperse to consumers. `Utils/Json.cs` is deleted; each consumer owns its own `JsonSerializerOptions` instance:
- `App/Snapshot/this.cs` already inlines its own options for clone semantics
- `App/Build/this.cs` (or wherever `PrWrite` lands) holds its own
- `App/Data/this.Envelope.cs` already inlines `_envelopeJsonOptions`
- Each `Channels/Serializers/Serializer/Json.cs`, `Plang/this.cs`, `Plang/Data.cs`, `Text.cs` holds its own internal options

The "case-mismatch" risk I raised earlier is overblown — the options ARE mostly consumer-specific in their actual content; the "shared" feel was a code-organization artifact in `Utils/Json.cs`. When two consumers genuinely need identical options, that's a design conversation at that point, not a justification for a synthetic root home.

### B. Catalog placement — RESOLVED: dissolves into `app.Modules.Schema`

Settled per the v3 thread (`origin/runtime2-obp-restructure`) and Ingi's confirm. See [the inline answer for `ActionSpec` / `ExampleSpec`](#actionspec--examplespec--settled-dissolve-into-appmodulesschema). Folded into stage 9 (renamed: `catalog-dissolve-to-modules-schema`).

### C. PlangSerializer vs PlangDataSerializer — RESOLVED: Plang/ subfolder

Per Ingi's L19 review: PLang has multiple plang-format serializers and likely more in the future (Protobuf). Group them under `Channels/Serializers/Serializer/Plang/`:

```
Plang/
├── this.cs       (← PlangSerializer.cs; application/plang)
├── Data.cs       (← PlangDataSerializer.cs; application/plang+data)
└── (future: Protobuf.cs for protobuf-encoded Data)
```

Json and Text stay flat files because there's only one of each. Plang gets the folder because it's a family. Stage-1 still traces whether `application/plang` is dead transport — if so, the `Plang/this.cs` may end up redundant — but the *structure* is settled.

### D. RestoredFrame → Call/Position — RESOLVED

Per Ingi's L19 review: Position.cs preferred. Rename folded into stage 10 (`app-run-redesign`) since Call gets a closer look there.

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

### I. v3 audit methodology — blocked on missing substrate

The `runtime2-obp-restructure` v3 plan proposed walking `/shared/app-tree/<surface>.md` against the code as a systematic OBP audit. **That directory doesn't exist in this repo and never has** (verified by file system search and git log on the path). The methodology is moot until either:

- **(a) The surface files get built first.** A meta-stage that produces `app-tree/app.md`, `app-tree/types.md`, etc. — each describing what `app.<Surface>` *promises* (member list, type, structure). Then walk the audit. Substantial work; arguably its own plan.
- **(b) Pick a different diagnostic substrate.** `Documentation/v0.2/architecture.md` and `good_to_know.md` exist and describe the App graph; `App/this.cs` itself enumerates the properties. These aren't surface-by-surface promise documents but they're navigable starting points.
- **(c) Skip the methodology entirely.** Continue with stage-by-stage ownership realignments, knowing we miss the systematic coverage.

**Status: open.** Architect's lean: (c) for now — tightening the rules (D, E added today) catches a lot of what the audit would catch. (a) is too much overhead unless we commit to a long-term `/shared/app-tree/` doc artifact.

Per Ingi's recommendation 3 ("check couple of files there and see where it takes us"): we can't, the files don't exist. Question for Ingi: build the surface files as a pre-stage, or skip?

## Summary of net additions / removals (revised)

**Folders added: 9**
- `App/KeepAlive/` (stage 3)
- `App/Channels/Serializers/Formats/` (stage 17; absorbs ext table from Utils/MimeTypes.cs + Types MIME block)
- `App/Channels/Serializers/PropertyFilters/` (stage 14/16; Rule B collection)
- `App/Channels/Serializers/Serializer/Plang/` (stage 14; groups plang-format serializers — Plang/this.cs + Plang/Data.cs, future Plang/Protobuf.cs)
- `App/Modules/Schema/` (stage 9; absorbs former App/Catalog/)
- `App/Modules/Schema/Spec/` (stage 9; record family Action.cs + Example.cs)
- `App/Builder/Choices/` (★ tentative; MOVED ← App/Choices/)
- `App/Builder/` (stage 16; RENAMED ← App/Build/, Rule D)
- `App/Tester/` (stage 16; RENAMED ← App/Test/, Rule D)

**Files deleted: 6**
- `App/Utils/PlangTypeIndex.cs` (stage 15 → `App/Types/this.cs`)
- `App/Utils/Json.cs` (stage 15 → DISPERSED to consumers; each owns its own JsonSerializerOptions instance)
- `App/Utils/ReservedKeywords.cs` (stage 15 → `App/Variables/Reserved.cs`)
- `App/Callback/Signature/this.cs` (stage 13 → property on `App/Callback/this.cs`)
- `App/Catalog/` whole folder (stage 9 → `App/Modules/Schema/`); ExampleHelpers.cs / ExampleRenderer.cs / TypeEntry.cs / ActionSpec.cs / ExampleSpec.cs / Catalog/this.cs all relocate

**Files renamed: 14**
- `App/Cache/MemoryStepCache.cs` → `App/Cache/Memory.cs`
- `App/Data/PlangTypeConverter.cs` → `App/Data/Converter.cs`
- `App/Snapshot/ISnapshotted.cs` → `App/Snapshot/ISnapshot.cs`
- `App/Test/TestFile.cs` → `App/Tester/File.cs` (folder rename via stage 16)
- `App/Test/TestRun.cs` → `App/Tester/Run.cs`
- `App/Test/TestStatus.cs` → `App/Tester/Status.cs`
- `App/Channels/Serializers/Serializer/JsonStreamSerializer.cs` → `Json.cs`
- `App/Channels/Serializers/Serializer/TextStreamSerializer.cs` → `Text.cs`
- `App/Channels/Serializers/Serializer/PlangSerializer.cs` → `Plang/this.cs`
- `App/Channels/Serializers/Serializer/PlangDataSerializer.cs` → `Plang/Data.cs`
- `App/CallStack/RestoredFrame.cs` → `App/CallStack/Call/Position.cs`
- `App/Catalog/ActionSpec.cs` → `App/Modules/Schema/Spec/Action.cs`
- `App/Catalog/ExampleSpec.cs` → `App/Modules/Schema/Spec/Example.cs`
- `App/Catalog/TypeEntry.cs` → `App/Modules/Schema/Entry.cs`
- `App/Catalog/ExampleRenderer.cs` → `App/Modules/Schema/Render.cs`
- `App/Catalog/this.cs` → `App/Modules/Schema/this.cs`

**Folders renamed: 2** (stage 16, Rule D)
- `App/Build/` → `App/Builder/`
- `App/Test/` → `App/Tester/`

**Files materially shrunk: 5**
- `App/this.cs` 681 → <300 (multiple stages)
- `App/Modules/this.cs` 464 → ~150 (stage 9; example-rendering becomes local navigation `Schema.Render(spec)`)
- `App/Channels/this.cs` 277 → <150 (stages 1, 2, 8)
- `App/Builder/this.cs` (gains content from App.Start, stage 12; gains Choices subfolder)
- `App/Types/this.cs` (becomes partial; MIME block moves to Channels/Serializers/Formats; Registry.cs and Conversion.cs partial siblings absorb Utils content)

**Folders unchanged:** Actor, Attributes, Cache (just a rename), Config, Data (mostly), Debug, Events (collapsed), FileSystem, Goals, Services, Settings, Snapshot (just a rename), Statics, Variables (gains Reserved.cs).

---

This tree is the architect's destination, revised against your v1 review and the v3 absorption pass (2026-05-08). Items still open are flagged in [Open judgment calls](#open-judgment-calls). Provider→Code (open question E) is the most consequential remaining discussion; the audit-substrate question (open question I) is the second.

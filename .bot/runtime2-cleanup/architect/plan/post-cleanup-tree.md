# `PLang/App/` — Post-Cleanup Target Tree

What `PLang/App/` should look like after all 20 stages land. This is the *destination*, not a stage-by-stage trace — read this when you want to judge the end state at a glance instead of reading prose.

The tree is annotated with what each stage does to it, and revised against Ingi's review of v1 (2026-05-07). Folders/files with no marker are unchanged.

**Baseline:** trunk after the runtime2-channels merge (`origin/runtime2` at `260ba46f`). The merge already landed two pieces this doc had previously called "stage 15": `Channel/EventContext.cs` is now `Channel/Events/this.cs`, and `Channel/MigrationEnvelope.cs` is gone (the Migration concept dropped). Stage 15's table is updated accordingly.

**v3 absorption (2026-05-08):** Reviewed `origin/runtime2-obp-restructure` architect/v1-v3 (the prior cleanup attempt that was deferred). Several insights folded into this doc and the principles file:

1. **Catalog dissolves into `app.Modules.Schema`** — the catalog is "Modules describing itself to an LLM"; Modules is the owner. Builder/UI/trace-viewer are consumers. The v3 thread Ingi remembered. Open question B (Catalog placement) is now resolved.
2. **MIME table splits two ways** — `Utils/MimeTypes.cs` does two jobs: `GetMimeType(ext)` is I/O (extension forward) → `app.Channels.Serializers.Formats`; `TryGetClrType(mimeType)` is type resolution (family rules) → `app.Types.Clr(mimeType)` overload alongside `Clr(plangName)`.
3. **Build → Builder, Test → Tester** rename — gerunds describe state; nouns name objects. Tree below applies the rename throughout.
4. **Two new sharpened rules**: Rule D (gerund-named app-graph properties) and Rule E (decomposed parameters → navigation) added to `plan/principles.md`. Both are detection rules with grep screens.
5. **Modules out of scope** with sharper rationale: the source generator + handler pattern *forces* OBP shape on module action handlers — they can't drift OBP without compile breaks. Cleaner reason than v1's "handler-level cleanup is a separate plan."

**v3 absorption — declined or revised:**

- v3 wanted ReservedKeywords folded into `app.Types/Registry.cs`. ReservedKeywords are *variable names* (`!Test`, `!Signature`, etc.), not types. `app.Variables/Reserved.cs` is the right owner. Tree keeps the Variables placement.
- v3's audit methodology (walk every `/shared/app-tree/*.md`) is **deferred to a follow-up branch**: that directory doesn't exist in the repo and never has, and building the surface promise-files for every public mount on `app` is substantial work in its own right. Listed under "What's deferred" in `plan.md` — not in scope for this plan.
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
├── Builder/                                 (RENAMED ← Build/, stage 17; Rule D — gerund→noun; namespace App.Build → App.Builder)
│   ├── Choices/                             (★ tentative — MOVED ← App/Choices/; re-evaluate after restructure lands; see open question)
│   │   └── this.cs                          (private static _registry → instance, stage 16)
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
│   └── this.cs                              (absorbs Signature/this.cs as a property — stage 14; ExpiresInMs → TimeSpan? Expires; the Signature subfolder goes away)
├── Channels/
│   ├── Channel/
│   │   ├── Events/this.cs                   (already in trunk — ex-EventContext.cs)
│   │   ├── Goal/this.cs
│   │   ├── Message/this.cs
│   │   ├── Session/this.cs
│   │   ├── Stream/this.cs
│   │   └── this.cs
│   ├── Serializers/
│   │   ├── Formats/                         (NEW; stage 18; mount = app.Channels.Serializers.Formats; absorbs the extension table from Utils/MimeTypes.cs and the MIME/Kind/Compressible block currently inside Types/this.cs)
│   │   │   └── this.cs                      (Mime(ext), Kind(ext), Compressible(kind), KindOf(typeValue), Add(ext, kind, mime), Remove(ext))
│   │   ├── Serializer/
│   │   │   ├── Json.cs                      (RENAMED ← JsonStreamSerializer.cs)
│   │   │   ├── Plang/                       (NEW subfolder; groups the plang-format serializers for future expansion — Protobuf etc.)
│   │   │   │   ├── this.cs                  (RENAMED ← PlangSerializer.cs; application/plang transport)
│   │   │   │   └── Data.cs                  (RENAMED ← PlangDataSerializer.cs; application/plang+data — the canonical envelope for callbacks)
│   │   │   ├── Text.cs                      (RENAMED ← TextStreamSerializer.cs)
│   │   │   └── this.cs
│   │   ├── Filters/                         (NEW; collection — stage 15 / 16; three filters belong in a registry by Rule B)
│   │   │   ├── Sensitive.cs                 (RENAMED ← SensitivePropertyFilter.cs; both Property and Filter suffix dropped — folder says it)
│   │   │   ├── Transport.cs                 (RENAMED ← TransportPropertyFilter.cs)
│   │   │   ├── View.cs                      (RENAMED ← ViewPropertyFilter.cs)
│   │   │   └── this.cs
│   │   ├── TimeSpanIso8601.cs                (RENAMED ← TimeSpanIso8601Converter.cs; Serializers folder owns the role)
│   │   ├── UnregisteredMimeType.cs           (kept — typed exception, conventionally compound)
│   │   └── this.cs                            (TypeJsonConverter relocates to App/Data/Json.cs — lives with the Type it serves)
│   └── this.cs                              (SHRUNK; v1 helpers gone — stage 2; ReadAsync<T>(filePath) gone — stage 8; canonical Serializers owner — stage 1 consolidates the per-Channels and per-Stream duplicates here)
├── Config/
│   ├── IConfig.cs
│   ├── ModuleView.cs                        (per-module typed settings view returned by app.Config.For<T>(context); generic, used at runtime — see comment below)
│   ├── Scope.cs
│   └── this.cs
├── Data/
│   ├── Converter.cs                         (RENAMED ← PlangTypeConverter.cs; System.ComponentModel.TypeConverter for Type)
│   ├── Json.cs                              (RELOCATED ← Channels/Serializers/TypeJsonConverter.cs; System.Text.Json JsonConverter for Type — lives with what it serves)
│   ├── Navigators/...
│   ├── Properties.cs
│   ├── Code/Grep.cs                         (RENAMED ← Providers/DefaultGrepProvider.cs; folder Code/, drop both Default and suffix)
│   ├── Code/IGrep.cs                        (RENAMED ← Providers/IGrepProvider.cs; per-domain interface, suffix dropped)
│   ├── TString.cs
│   ├── this.Compare.cs
│   ├── this.Envelope.cs                     (private static readonly _envelopeJsonOptions → instance, stage 16)
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
├── Code/                                    (RENAMED ← Providers/, settled 2026-05-08; see "Provider → Code" section below)
│   ├── ICode.cs                             (RENAMED ← IProvider.cs; marker for runtime-overridable C# implementations — Name/IsDefault/IsBuiltIn/Source)
│   ├── this.Snapshot.cs
│   └── this.cs                              (self-disposes, stage 4)
├── Services/                                (in active use — App.Services owns per-outbound-call I/O scopes, see comment below)
│   ├── Service/this.cs
│   └── this.cs
├── Settings/                                (REWORKED — stage 13; see "Settings — collection over Data" below)
│   ├── IStore.cs                            (RENAMED ← ISettingsStore.cs; backend interface used by this.cs — stage 13)
│   ├── Sqlite.cs                            (RENAMED ← SqliteSettingsStore.cs; SQLite impl of IStore — stage 13)
│   └── this.cs                              (ABSORBS SettingsVariable; collection of Data values keyed by name, like Goals/this.cs — stage 13)
├── Snapshot/
│   ├── ISnapshot.cs                         (RENAMED ← ISnapshotted.cs; past-participle interface naming is awkward; matches IDisposable/IEnumerable convention)
│   └── this.cs
├── Statics/
│   ├── this.Snapshot.cs
│   └── this.cs                              (App.GetStatic shim deleted — stage 5; the Statics @this is unchanged)
├── Tester/                                  (RENAMED ← Test/, stage 17; Rule D — gerund→noun; namespace App.Test → App.Tester; Test prefix dropped on loose siblings since namespace is App.Tester)
│   ├── Coverage.cs
│   ├── File.cs                              (RENAMED ← TestFile.cs)
│   ├── Results.cs
│   ├── Run.cs                               (RENAMED ← TestRun.cs)
│   ├── Status.cs                            (RENAMED ← TestStatus.cs)
│   ├── this.Snapshot.cs
│   └── this.cs                              (gains ChildAppCreated replacement shape — stage 16; coder settles the exact shape during the stage, options ranged through during prior architect review)
├── Types/
│   ├── Conversion.cs                        (NEW partial; stage 16; absorbs Utils/TypeConverter.cs — instance ConvertTo<T> / TryConvertTo / Populate)
│   ├── Registry.cs                          (NEW partial; stage 16; absorbs Utils/PlangTypeIndex.cs + Utils/TypeMapping.cs registry portion — name↔CLR maps as instance fields, [PlangType] scan in ctor)
│   └── this.cs                              (becomes partial; stage 16; gains Clr(mimeType) overload — stage 18; the existing MIME/Kind/Compressible block at lines 215-315 moves out to Channels/Serializers/Formats/)
├── Utils/                                   (NEARLY EMPTY; only pure utilities and extension methods remain)
│   ├── CommandLineParser.cs
│   ├── PathExtension.cs                     (deferred — likely belongs in App/FileSystem/)
│   ├── RegisterStartupParameters.cs         (deferred — likely belongs in Builder/ or as App.Start helper)
│   └── StringDistance.cs
│
│   (DELETED → elsewhere:)
│   • Json.cs                                → DISPERSED to consumers, stage 16 (each consumer owns its own JsonSerializerOptions instance — Snapshot, Build PrWrite, Data.Envelope already do, the "shared" options were a junk-drawer artifact)
│   • MimeTypes.cs                           → splits two ways, stage 18:
│   │                                          • GetMimeType(ext) and friends → App/Channels/Serializers/Formats/this.cs
│   │                                          • TryGetClrType(mimeType) → App/Types/this.cs as Clr(mimeType) overload
│   • PlangTypeIndex.cs                      → App/Types/Registry.cs (partial, stage 16)
│   • ReservedKeywords.cs                    → App/Variables/Reserved.cs (all const/readonly, no mutable static, stage 16)
│   • TypeConverter.cs                       → App/Types/Conversion.cs (partial, stage 16)
│   • TypeMapping.cs                         → App/Types/Registry.cs registry portion + App/Channels/Serializers/Formats forward-lookup callers (stage 16 + 17)
├── Variables/
│   ├── Calls/                               (already in trunk — per-call scopes from channels merge)
│   │   ├── Call/this.cs
│   │   └── this.cs
│   ├── IRawNameResolvable.cs
│   ├── Reserved.cs                          (NEW or MOVED ← Utils/ReservedKeywords.cs, stage 16; well-known variable names belong with the Variables owner; all values const/readonly)
│   ├── Variable.cs
│   ├── this.Snapshot.cs
│   ├── this.SnapshotAt.cs
│   └── this.cs
├── modules/                                 (unchanged structure — per-handler files; internal cleanups only)
│   └── ...
│   • llm/code/OpenAi.cs                     (RENAMED ← llm/providers/OpenAiProvider.cs; folder code/, drop Provider suffix; private static int _requestCount → DELETED, stage 16; temp blocker confirmed by Ingi 2026-05-07; todo logged)
│   • test/run.cs                            (internal static event ChildAppCreated → moves onto Tester/this.cs as a non-static registry, stage 16)
├── GlobalUsings.cs
├── Info.cs                                  (unchanged — loose root; not in any current stage)
├── View.cs                                  (unchanged — loose root; not in any current stage)
└── this.cs                                  (SHRUNK 681→<300; the sum of stages 3, 4, 5, 6, 7, 10, 11, 12)
```

## Inline answers to v1 review questions

These are the questions Ingi raised on v1 of this doc that the tree above doesn't fully explain on its own.

### `MemoryStepCache` — should this live under Step/?

The class caches `Data.@this` keyed by string — Step results, but the cached *thing* is Data, not Step. The owner is "the cache subsystem," not Step. Per OBP, the impl-variant pattern keeps `Cache/` as the type and the in-memory implementation as `Cache/Memory.cs`. If a SqliteStepCache or RedisCache lands later, it joins as `Cache/Sqlite.cs` / `Cache/Redis.cs` — same shape as `Settings/Sqlite.cs`. Moving the file under `Steps/Step/Cache/` would couple a generic cache concept to the Step entity and complicate later re-use; keeping it at App root preserves the swap-the-impl pattern.

### `RestoredFrame` — isn't it just a Call?

It almost is. The hard difference is lifecycle: a live `Call.@this` has an AsyncLocal "current" pointer, push/pop discipline, and observable diffs. A restored frame can have *none* of that — it's a deserialized (Action, Goal, StepIndex, ActionIndex, Id) tuple used to identify *where* a callback should resume. The class doc explicitly forbids pushing it to the AsyncLocal current.

So the type does have to exist as something distinct from `Call.@this`. But the *name* is a description, not a noun — "RestoredFrame" reads "a frame that was restored." Better candidates: `Call.Position.cs` (it identifies *where* in the call tree), or `Call.@this.Snapshot` as a nested type. The tree above proposes `Call/Position.cs`. That's a stage-16-adjacent rename, not a structural change; happy to fold it differently if the position-vs-frame distinction reads wrong.

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

You're right; Data already owns signatures via the lazy `Signature` getter. The whole `Signature/` subfolder is just a 15-line config holder for `ExpiresInMs`. Tree above absorbs it as a property on `Callback/this.cs` (stage 14 carries the TimeSpan rename through). Subfolder goes away.

### Channel/Migration — already gone

Confirmed in the merge from runtime2: `MigrationEnvelope.cs` is deleted, `modules/channel/migrate.cs` is deleted, the Stage 15 list no longer references it.

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

Drop both the `Property` prefix AND the `Filter` suffix — folder is `Filters/` (settled 2026-05-08, "Property" doesn't add anything; if a non-property filter concept appears later it co-locates here without renaming). Tree above puts the three files as `Filters/Sensitive.cs`, `Filters/Transport.cs`, `Filters/View.cs`. By Rule B (`Get<Plural>()` is a missing collection type) this also gets a parent `Filters/this.cs` registry. Three filters with the same shape are exactly the missing-collection smell.

### Choices — tentatively under Builder/, ★ re-evaluate

Reading `Choices/this.cs` doc: "Build-time validator and catalog Describe() both go through here." Both consumers are build-time. Validator runs during `plang build`, Schema rendering happens at build (or at runtime via the builder module). Tree proposes `Builder/Choices/` as the destination.

Per Ingi's review: ★ tentative. Re-evaluate after Catalog dissolves into `Modules.Schema` — once the build/runtime border is clearer, the right home for Choices may shift (it could land under Modules.Schema if "Choices is what an action declares," or stay under Builder if "Choices is what the builder validates"). Mark and revisit in the same review pass.

### `Config/ModuleView.cs` — what is it

Generic per-module settings view: `app.Config.For<archive.Config>(context)` returns a `ModuleView<archive.Config>` stamped with the current Actor.Context. Each `view.Resolve<T>("max", default)` walks the scope chain to find the right value. Used at runtime by handlers reading their own settings. Name is fine — it's a per-module view of settings — though `Config/View/this.cs` would be more OBP. Tree leaves it as `ModuleView.cs`; happy to push to `View/this.cs` if you want.

### Converters cluster — agreed (settled 2026-05-08)

Three files, three different fixes:

- `PlangTypeConverter.cs` → `Data/Converter.cs` — System.ComponentModel.TypeConverter for Type. Namespace `App.Data` so the prefix is redundant.
- `TypeJsonConverter.cs` → `Data/Json.cs` — JsonConverter relocates to live with the Type it serves (was orphaned in `Channels/Serializers/`). The two files coexist in `App/Data/` because they target the same Type via different mechanisms (component-model vs System.Text.Json) — different consumers, different files.
- `TimeSpanIso8601Converter.cs` → `Channels/Serializers/TimeSpanIso8601.cs` — flat rename, drops the Converter suffix. Stays in Serializers/ where the role is implicit.

No top-level `App/Converters/` folder. Same logic as the rejected `App/Json/`: "Converter" is a mechanism, not a domain. A synthetic root mount for converters would be the same shape error.

### `UnregisteredMimeType` — agreed (kept)

Kept as-is. It's a typed exception (`: System.Exception`) thrown by the serializer registry. Exception names ARE compound by convention (`FileNotFoundException`, `KeyNotFoundException`) — not a Rule A hit. If a second sibling exception ever appears in `Serializers/`, fold them into an `Errors/` subfolder then.

### `DefaultGrepProvider` → `Grep.cs` — agreed (settled 2026-05-08)

Drop both `Default` and `Provider`. Folder is `Code/` (after the Provider→Code rename below) so both prefixes duplicate the parent. Renames:
- `DefaultGrepProvider.cs` → `Code/Grep.cs`
- `IGrepProvider.cs` → `Code/IGrep.cs`

### `OpenAiProvider` → `OpenAi.cs` — agreed (settled 2026-05-08)

Folder is `modules/llm/code/` — the role is in the path. File becomes `OpenAi.cs`. Same pattern as Grep above.

### Events folder — collapse the Lifecycle layer

Today: `Events/this.cs` (registry), `Events/Lifecycle/this.cs` (Before/After bindings holder), `Events/Lifecycle/Bindings/this.cs` (one collection), `Events/Lifecycle/Bindings/Binding/this.cs` (one binding entity).

Reading the code: `Events/this.cs` is the registry that owns event bindings. `Events/Lifecycle/this.cs` is just two properties (`Before` and `After`) that each hold a `Bindings.@this`. Lifecycle is a meaningful axis (when, in the action lifecycle) — but the layer adds two folder hops with very little entity-shape value; Before/After could be properties directly on Events/this.cs.

Tree above proposes the collapse: `Events/this.cs` (registry, gains Before/After properties), `Events/Bindings/this.cs` (collection), `Events/Binding/this.cs` (entity). The Lifecycle/ subfolder goes away. Three folder hops becomes two.

Caveat: I haven't traced every consumer of `App.Events.Lifecycle.Bindings.Binding.@this` (used as an alias `EventBinding` everywhere). The collapse is a rename + alias re-target. Probably stage 15 territory — happy to defer to its own stage if the blast radius is bigger than expected.

### Services — actively used

Yes — `app.Services` is the per-outbound-call I/O scope collection. Each `Service` represents one outbound call's I/O scope (Channels, System Identity, Parent ref). It replaces the runtime1 "Service-as-actor" model — identity is always System; the Parent ref preserves "who triggered this." Used when an Actor makes outbound HTTP/TCP/WS calls; spawned via `app.Services.New(parent)`, `await using` for tear-down. Active referenced from `App.this.cs:229`. Tree leaves it unchanged — this was an architect oversight in v1 saying "what is it"; the answer is "it's the per-call I/O scope" and the OBP shape is correct.

### Settings — collection over Data (settled 2026-05-08 with Ingi)

Today's `SettingsVariable` carries an inheritance smell that this stage closes. It's a `Data.@this` subclass that wears two hats:

1. **Runtime mode** (with `_app`) — mounted on Variables under `"Settings"`, intercepts `%Settings.ApiKey%` via `GetChild` override, calls into the store
2. **Storage mode** (`JsonConstructor`) — represents a single loaded value, no navigation

The `: Data.@this` inheritance exists only to fit through the variable-resolution interception path. Mechanism leaking into shape — exactly the inheritance-over-composition pattern App is moving away from elsewhere.

**Reworked shape** (Ingi's call 2026-05-08):

- `Settings/this.cs` — a collection (like `Goals/this.cs`), holds Data values keyed by name. Public surface is `Set(key, Data)` / `Get(key) → Data` / etc. **Not** a Data subclass.
- `Settings/IStore.cs` — backend persistence interface used by `this.cs`. Rename of `ISettingsStore` (drops the `Settings` prefix that was duplicating the namespace).
- `Settings/Sqlite.cs` — SQLite impl of `IStore`. Serializes Data values to JSON via `PlangSerializer`. Rename of `SqliteSettingsStore`.

The `JsonConstructor`/runtime dual-mode disappears because the two responsibilities split: `this.cs` is the navigation+API root, `Sqlite.cs` is the load/save layer. Data is the value type — it's already what we want to store; no subclassing required.

**Variable resolution** — `%Settings.ApiKey%` reaches `settings.Get("ApiKey")` via a new `Variables.RegisterNavigable(name, resolver)` hook. The variable resolver consults this map during compound-name resolution. Settings registers itself as `"Settings"` with a resolver that delegates to `settings.Get(path)`. The mechanism is general — any future non-Data navigable mount can register the same way; not a Settings-specific workaround.

This carves as its own stage when reached. The `RegisterNavigable` mechanism on Variables is the load-bearing part — settle its exact signature when the stage opens.

### `ISnapshotted` → `ISnapshot` — agreed

Renamed in tree. `IDisposable` not `IDisposed`; `IEnumerable` not `IEnumerated`. Past-participle interface naming reads odd; `ISnapshot` matches convention.

### `Test/TestFile`, `TestRun`, `TestStatus` — drop Test prefix

Tree above renames to `File.cs`, `Run.cs`, `Status.cs`. Namespace is `App.Test` so the prefix is redundant.

### Provider → Code — settled 2026-05-08

The runtime renames Provider → Code, end-to-end. Driving rationale (Ingi's framing): anything currently called a Provider is C# that an external developer can override by dropping in their own DLL. PLang's narrative is "everything is goals (plang), except where you need code" — the runtime's vocabulary aligns with the language by calling the escape hatch what the language calls it. `Provider` was technically precise (DI-flavored "provides an impl of a contract") but PLang-foreign; `Code` is technically loose but PLang-native, and the language designer gets to choose what concepts the runtime advertises.

**Concrete shape**:

- `App/Providers/` → `App/Code/` (the central registry, plus the marker)
- `App/Providers/IProvider.cs` → `App/Code/ICode.cs` — marker interface for runtime-overridable implementations. Fields stay (`Name`, `IsDefault`, `IsBuiltIn`, `Source`); they map directly to the developer-registers-DLL flow (e.g. `- add 'claudeapi.dll' on llm module, set as default` populates these on the registered ICode).
- All per-module `App/modules/X/providers/` → `App/modules/X/code/`
- `App/Data/Providers/` → `App/Data/Code/` (Grep)
- All per-module interfaces drop the `Provider` suffix entirely: `IBuilderProvider` → `IBuilder`, `ILlmProvider` → `ILlm`, `ICryptoProvider` → `ICrypto`, `IHttpProvider` → `IHttp`, `IIdentityProvider` → `IIdentity`, `IAssertProvider` → `IAssert`, `ITemplateProvider` → `ITemplate`. Each per-module interface extends `ICode`.
- Implementations follow the rule "drop both `Default` and `Provider`":
  - When the parent path doesn't already say the role, the impl name IS the variant: `OpenAi.cs`, `Fluid.cs`, `Grep.cs`, `Sqlite.cs`.
  - When the parent path already says the role (the assert / builder / http / identity modules), the default impl is `Default.cs` to avoid `assert.code.Assert` redundancy. Custom DLLs from users carry their own variant names.

**Why the marker stays load-bearing**: today's `IProvider` carries `Name`, `IsDefault`, `IsBuiltIn`, `Source` — the runtime uses all four for registration, default-vs-override distinction, and snapshot/restore (DLL path). These are exactly the fields the user-facing flow exercises (`add ... set as default`). The rename keeps the fields; only the marker's name and folder change.

**Per-module interfaces are not uniform-shape**. Initial intuition was that modules would all expose a `Run` method via the marker. They don't — `IBuilder` has `Actions/Goals/Validate/...` methods, `ILlm` has `Query`, each per-module interface is shaped to its domain. The marker carries only the framework-tracking fields; the per-module interface carries the domain methods.

## Judgment calls — all resolved

Audit trail of design questions raised during planning, with how each landed. All listed below are resolved or deferred-to-coder-execution; nothing in this section blocks the plan.

### A. JsonSerializerOptions destination (stage 16) — RESOLVED: disperse

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

### E. Provider → Code rename — RESOLVED: rename end-to-end

Settled 2026-05-08. The Provider concept becomes Code throughout — folder, marker interface, per-module folders, per-module interfaces (suffix dropped). `IProvider` → `ICode` keeps the framework-tracking fields (`Name`, `IsDefault`, `IsBuiltIn`, `Source`) which map directly to the developer-DLL-registration flow. Implementation naming follows "drop both `Default` and `Provider`" with the parent-path-says-the-role exception (assert/builder/http/identity modules use `Default.cs` to avoid redundancy; llm/ui/data/settings name impls after the variant: `OpenAi.cs`, `Fluid.cs`, `Grep.cs`, `Sqlite.cs`). See "Provider → Code" inline section above.

### F. Events sub-foldering collapse — RESOLVED design; questions defer to coder

Settled in the tree: `Events/Lifecycle/Bindings/Binding/` collapses to `Events/Binding/` + `Events/Bindings/`, with Before/After moving to properties on `Events/this.cs`. The alias `App.Events.Lifecycle.Bindings.Binding.@this` (used widely as `EventBinding`) will need re-targeting; the rename is mechanical but the blast radius hasn't been measured yet. Open execution questions (sequencing within the stage, alias re-target order, whether to split off a separate stage) settle when the coder approaches the rename — likely folded into stage 15 (`compound-name-rename`) or its own small stage if blast radius warrants.


## Summary of net additions / removals (revised)

**Folders added: 9**
- `App/KeepAlive/` (stage 3)
- `App/Channels/Serializers/Formats/` (stage 18; absorbs ext table from Utils/MimeTypes.cs + Types MIME block)
- `App/Channels/Serializers/Filters/` (stage 15/16; Rule B collection — `Sensitive.cs`, `Transport.cs`, `View.cs`, `this.cs`)
- `App/Channels/Serializers/Serializer/Plang/` (stage 15; groups plang-format serializers — Plang/this.cs + Plang/Data.cs, future Plang/Protobuf.cs)
- `App/Modules/Schema/` (stage 9; absorbs former App/Catalog/)
- `App/Modules/Schema/Spec/` (stage 9; record family Action.cs + Example.cs)
- `App/Builder/Choices/` (★ tentative; MOVED ← App/Choices/)
- `App/Builder/` (stage 17; RENAMED ← App/Build/, Rule D)
- `App/Tester/` (stage 17; RENAMED ← App/Test/, Rule D)

**Files deleted: 6**
- `App/Utils/PlangTypeIndex.cs` (stage 16 → `App/Types/this.cs`)
- `App/Utils/Json.cs` (stage 16 → DISPERSED to consumers; each owns its own JsonSerializerOptions instance)
- `App/Utils/ReservedKeywords.cs` (stage 16 → `App/Variables/Reserved.cs`)
- `App/Callback/Signature/this.cs` (stage 14 → property on `App/Callback/this.cs`)
- `App/Catalog/` whole folder (stage 9 → `App/Modules/Schema/`); ExampleHelpers.cs / ExampleRenderer.cs / TypeEntry.cs / ActionSpec.cs / ExampleSpec.cs / Catalog/this.cs all relocate

**Files renamed: ~40** (per-module providers vary; 25 named explicitly + 15+ in the Provider→Code sweep across modules/*/providers/ folders)
- `App/Cache/MemoryStepCache.cs` → `App/Cache/Memory.cs`
- `App/Data/PlangTypeConverter.cs` → `App/Data/Converter.cs`
- `App/Channels/Serializers/TypeJsonConverter.cs` → `App/Data/Json.cs` (relocates — lives with the Type it serves)
- `App/Channels/Serializers/TimeSpanIso8601Converter.cs` → `App/Channels/Serializers/TimeSpanIso8601.cs`
- `App/Channels/Serializers/SensitivePropertyFilter.cs` → `App/Channels/Serializers/Filters/Sensitive.cs`
- `App/Channels/Serializers/TransportPropertyFilter.cs` → `App/Channels/Serializers/Filters/Transport.cs`
- `App/Channels/Serializers/ViewPropertyFilter.cs` → `App/Channels/Serializers/Filters/View.cs`
- `App/Providers/IProvider.cs` → `App/Code/ICode.cs` (marker; folder rename Providers/ → Code/)
- `App/Providers/this.cs` → `App/Code/this.cs` (registry; folder rename)
- `App/Providers/this.Snapshot.cs` → `App/Code/this.Snapshot.cs`
- `App/Data/Providers/DefaultGrepProvider.cs` → `App/Data/Code/Grep.cs`
- `App/Data/Providers/IGrepProvider.cs` → `App/Data/Code/IGrep.cs`
- `App/modules/llm/providers/OpenAiProvider.cs` → `App/modules/llm/code/OpenAi.cs`
- `App/modules/llm/providers/ILlmProvider.cs` → `App/modules/llm/code/ILlm.cs`
- `App/modules/builder/providers/IBuilderProvider.cs` → `App/modules/builder/code/IBuilder.cs`
- `App/modules/builder/providers/DefaultBuilderProvider.cs` → `App/modules/builder/code/Default.cs`
- `App/modules/crypto/providers/ICryptoProvider.cs` → `App/modules/crypto/code/ICrypto.cs`
- `App/modules/crypto/providers/DefaultProvider.cs` → `App/modules/crypto/code/Default.cs`
- `App/modules/assert/providers/IAssertProvider.cs` → `App/modules/assert/code/IAssert.cs`
- `App/modules/assert/providers/DefaultAssertProvider.cs` → `App/modules/assert/code/Default.cs`
- `App/modules/http/providers/IHttpProvider.cs` → `App/modules/http/code/IHttp.cs`
- `App/modules/http/providers/DefaultHttpProvider.cs` → `App/modules/http/code/Default.cs`
- `App/modules/ui/providers/ITemplateProvider.cs` → `App/modules/ui/code/ITemplate.cs`
- `App/modules/ui/providers/FluidProvider.cs` → `App/modules/ui/code/Fluid.cs`
- (any other `modules/X/providers/` follow the same pattern)
- `App/Settings/ISettingsStore.cs` → `App/Settings/IStore.cs`
- `App/Settings/SqliteSettingsStore.cs` → `App/Settings/Sqlite.cs`
- `App/Settings/SettingsVariable.cs` → ABSORBED into `App/Settings/this.cs` (collection over Data; see Settings section)
- `App/Snapshot/ISnapshotted.cs` → `App/Snapshot/ISnapshot.cs`
- `App/Test/TestFile.cs` → `App/Tester/File.cs` (folder rename via stage 17)
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

**Folders renamed: 4+** (stage 17, Rule D + Provider→Code sweep)
- `App/Build/` → `App/Builder/` (Rule D)
- `App/Test/` → `App/Tester/` (Rule D)
- `App/Providers/` → `App/Code/` (Provider→Code)
- `App/Data/Providers/` → `App/Data/Code/` (Provider→Code)
- All `App/modules/X/providers/` → `App/modules/X/code/` (Provider→Code; ~10 modules)

**Files materially shrunk: 5**
- `App/this.cs` 681 → <300 (multiple stages; also loses the `Serializers` shortcut property in stage 20 — `app.Channels.Serializers` becomes the only access path)
- `App/Modules/this.cs` 464 → ~150 (stage 9; example-rendering becomes local navigation `Schema.Render(spec)`)
- `App/Channels/this.cs` 277 → <150 (stages 1, 2, 8)
- `App/Builder/this.cs` (gains content from App.Start, stage 12; gains Choices subfolder)
- `App/Types/this.cs` (becomes partial; MIME block moves to Channels/Serializers/Formats; Registry.cs and Conversion.cs partial siblings absorb Utils content)

**Folders unchanged:** Actor, Attributes, Cache (just a rename), Config, Data (mostly), Debug, Events (collapsed), FileSystem, Goals, Services, Snapshot (just a rename), Statics, Variables (gains Reserved.cs).

**Settings reworked:** `SettingsVariable` absorbed into `Settings/this.cs` as a collection over Data; `ISettingsStore` → `IStore.cs`; `SqliteSettingsStore` → `Sqlite.cs`; new `Variables.RegisterNavigable(name, resolver)` mechanism wires `%Settings.X%` resolution.

---

This tree is the architect's destination, revised against your v1 review and the v3 absorption pass (2026-05-08). All naming/shape questions are settled; the audit trail lives under [Judgment calls — all resolved](#judgment-calls--all-resolved). Coder-side execution detail (e.g. F's Events sub-foldering blast-radius) is settled when each stage is approached.

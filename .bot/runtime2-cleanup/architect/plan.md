# Cleanup Plan — OBP Refactor of `PLang/App/`

## What this is

A long-running cleanup effort that refactors the App tree to fully match the Object-Based Pattern. OBP wasn't fully clear when much of `PLang/App/` was written; both the architect and Ingi have a sharper read on the pattern now, and a number of the smells the architect now sees were either invisible or ambiguous when the original code landed. This plan sets out the work as a sequence of independent, shippable stages — not one big refactor.

The work touches the App spine, the source generator, the action handlers, and the Documentation surface. It does not introduce any new features. It does close two open TODOs (`callstack wire-up`, `ExpiresInMs → ISO 8601`) that fit naturally inside individual stages.

See [`plan/principles.md`](plan/principles.md) for the OBP discipline this work enforces, the per-stage anatomy, and the definition of done. See [`plan/post-cleanup-tree.md`](plan/post-cleanup-tree.md) for the destination tree — what `PLang/App/` should look like after every stage lands, with each stage's effect annotated against the tree. See [`plan/scope-map.md`](plan/scope-map.md) for what's shared (App-level) vs per-actor across the App graph; this distinction matters for every stage.

## Why thin stages

The smells in `App/` aren't located at any one level of the tree. They're at "where data lives in one type and discipline lives in another." That happens at App root (`Run`, `KeepAlive`, `DisposeAsync`), at mid-tree on collections (`Modules.Describe`, `Channels.ReadAsync(filePath)`), and on loose root files (`Info.cs`, `View.cs`). Top-down doesn't fit, and pure folder reorganisation doesn't fix the actual violations.

The unit of work is **one ownership realignment**: a thing moves from the type that has the data to the type that has the responsibility. Each stage is exactly one such realignment, end-to-end (folder + code + callers). Each stage is its own commit (or commit set) on this branch, and the project must build clean and tests must stay green at every stage boundary — so any stage can be reverted independently if it later proves to be a mistake.

The plan is a backlog. Stages are written as we approach them, not all up front — every stage we land teaches us something about the next ones.

## Stage index

Stages 1–21 are ordered roughly by **impact × isolation** — biggest wins that don't entangle with future work go first. Each stage gets its own `stage-N-<slug>.md` file at the architect bot root *when carved*. Stage files don't exist yet; only the spine and `plan/principles.md` do.

### Tier 1 — close active drift, demonstrate the slice rhythm (small, isolated)

| # | Slug | One-liner |
|---|------|-----------|
| 1 | [`serializers-single-home`](stage-1-serializers-single-home.md) | **Complete 2026-05-08 (coder).** Per-actor `Channels.@this.Serializers` is the single home for the registry. App.Serializers deleted; Stream's `_serializers` field/property dropped; `Channel.Channels` back-ref added; 5 external `app.Serializers` callers swept; tests 2755/2755 + 199/199. |
| 2 | [`channels-v1-helpers-drop`](stage-2-channels-v1-helpers-drop.md) | Delete two dead surfaces on Channels.@this: the `WriteAsync(actorName, channelName, ...)` v1 helper (zero external callers) and the contentType-override branch in `WriteAsync(channelName, data, contentType, ...)` (zero callers pass contentType — both the branch and the parameter go). Single-string WriteAsync shrinks to ~5 lines. DefaultHttpProvider callers already use the simplified shape, no migration needed. The other three `is Channel.Stream.@this sc` casts in the file (ReadChannelAsync, WriteTextAsync, ReadTextAsync) stay — same shape smell but out of scope for stage 2. **Brief carved 2026-05-08.** |
| 3 | `keepalive-collection` | `_keepAlive`/`KeepAlive(x)`/`RemoveKeepAlive(x)` becomes `App.KeepAlive.@this` collection with `Add` / `Remove` / its own `DisposeAsync`. |
| 4 | `dispose-self-owns` | `Modules.@this` and `Providers.@this` (renamed to `Code.@this` later in stage 19) implement `IAsyncDisposable` themselves; `App.DisposeAsync` stops peeking into `_modules.All` / `Providers.All()`. |
| 5 | `getstatic-shim-drop` | Delete `App.GetStatic(string)` back-compat shim; sweep callers to `app.Statics.GetBag(name)`. |
| 6 | `app-data-inheritance-drop` | Drop `App : Data.@this<@this>` inheritance — App becomes a plain class. Vestige from before the codebase pivoted to `Data<T>` composition (every other site wraps via composition; App is the only inheritance form left). Removes the `new string Path` shadow and the unused inherited surface (`Type`, `Compressible`, `Properties`, `Error`, `Success`, `OnChange/OnCreate/OnDelete`, etc.); `%!app%` resolution already uses `DynamicData("!app", () => app)` so no migration needed there. |

### Tier 2 — real refactors with one clear win each (medium)

| # | Slug | One-liner |
|---|------|-----------|
| 7 | `callstack-promote-app-property` | Promote `app.Debug.CallStack` to `app.CallStack`. The folder is already at App root; the property placement is the only thing that disagrees. |
| 8 | `read-file-off-channels` | Move `Channels.ReadAsync<T>(filePath)` off Channels (it doesn't read from a channel). Goes to `app.Serializers` or FileSystem. |
| 9 | `catalog-dissolve-to-modules-schema` | Catalog dissolves entirely. The whole `App/Catalog/` folder moves to `App/Modules/Schema/` (records under `Spec/`). `Build(modules)` and `Render(spec, modules)` become instance methods navigating `this.Modules` (Rule E). The two static formatters in `modules/builder/providers/{Fluid,DefaultBuilder}` collapse into `Schema.Render`. Modules drops to ~150 lines; Schema becomes the navigable home for "what every action looks like." Settled 2026-05-08 per `runtime2-obp-restructure` v3 thread. |

### Tier 3 — bigger refactors, design discussion required

| # | Slug | One-liner |
|---|------|-----------|
| 10 | `app-run-redesign` | The headliner: `App.Run` from 85 lines + 8 foreign mutations to 5 lines. Introduces `Call.ExecuteAsync(handler, context)` and `Context.AnchorScope(action)`. Wires dormant `CallStack` as a side-effect (closes the 2026-04-27 TODO). Also removes the `app.Variables` and `app.Context` shortcuts (App.this.cs:240–241) — they delegate to "current actor" which is fragile under parallel multi-Context execution; settled 2026-05-08. If the shortcut sweep is too big to fold, carves out as its own small stage. |
| 11 | `errors-app-backref-drop` | Eliminate the `Errors.App = this` post-construction injection. Probably moves `Error.Callback` materialisation off `Error` itself so the back-ref isn't needed. |
| 12 | `build-branch-to-build-this` | Move the Build-mode branch (and the new-app y/n prompt) from `App.Start` to `Build.@this.Start()`. App.Start reads `if (Build.IsEnabled) return await Build.RunAsync(User.Context);`. |
| 13 | `settings-collection-rework` | Reshape Settings from inheritance to composition AND move SettingsStore from per-actor (dead drift) to App level. `SettingsVariable` (today a `Data.@this` subclass that intercepts `%Settings.X%` via overridden `GetChild`) absorbed into `Settings/this.cs` as a shared (one per app) collection over Data values keyed by name (like `Goals/this.cs`). `ISettingsStore` → `IStore.cs`; `SqliteSettingsStore` → `Sqlite.cs`. SettingsStore moves from `actor.SettingsStore` (per-actor `Lazy<ISettingsStore>` field on Actor — never used by anyone) to `app.SettingsStore` (one shared instance backed by `system.sqlite`); per-actor `_dataSource` field deleted; consumers sweep `app.System.SettingsStore` → `app.SettingsStore` (Goals/Setup, identity provider, llm provider, settings module). New `Variables.RegisterNavigable(name, resolver)` mechanism replaces the Variable-subclass interception path. Real shape change with three pieces: collection-over-Data, app-level relocation of SettingsStore, RegisterNavigable mechanism. Settled 2026-05-08 (incl. shared scope per Ingi). |

### Tier 4 — hygiene sweeps, last (against the settled tree)

| # | Slug | One-liner |
|---|------|-----------|
| 14 | `timespan-iso-8601-sweep` | Sweep `*Ms` int properties → `TimeSpan?` with the ISO 8601 JsonConverter pattern channels established. Known target: `App.Callback.Signature.ExpiresInMs`. (Closes the 2026-05-06 TODO.) |
| 15 | `compound-name-rename` | Sweep all Rule A renames and the sub-rule (role-suffix-duplicates-folder) renames flagged by the two-capital screen. Each is mechanical. Concrete targets: `MemoryStepCache.cs` → `Cache/Memory.cs`; `ISnapshotted.cs` → `ISnapshot.cs` (past-participle convention); `PlangTypeConverter.cs` → `Data/Converter.cs`; `TimeSpanIso8601Converter.cs` → `Channels/Serializers/TimeSpanIso8601.cs`; `TypeJsonConverter.cs` → relocates cross-folder to `Data/Json.cs` (lives with the Type it serves); `Sensitive/Transport/ViewPropertyFilter.cs` → `Channels/Serializers/Filters/{Sensitive,Transport,View}.cs` plus NEW `Filters/this.cs` parent collection (Rule B — three same-shape filters earn a registry parent). `UnregisteredMimeType.cs` kept (typed exceptions are conventionally compound). `MigrationEnvelope` and `EventContext` were absorbed pre-stage by the channels merge. |
| 16 | `static-state-eviction-sweep` | Move every `static` field (incl. `static readonly`) into the `@this` that should own it (Rule C). Mostly mechanical for caches and config singletons: `PlangTypeIndex` → `App.Types.@this`, `JsonSerializerOptions` blocks → dispersed to consumers (no `App/Json/` mount; each consumer owns its instance), `ReservedKeywords` → `App.Variables.Reserved.cs` (all const/readonly). One hit needs a design note before the sweep runs: `OpenAiProvider._requestCount` (per-process counter on an actor-resolved provider — wrong scope; per Ingi: delete, todo logged). Static *methods* and `const` stay. |
| 17 | `builder-tester-rename` | Rename `App/Build/` → `App/Builder/` and `App/Test/` → `App/Tester/`. Property `app.Building` → `app.Builder`, `app.Testing` → `app.Tester`. CLI: `plang build` → `plang --builder`, `plang --test` → `plang --tester`. Rule D — gerund→noun on app-graph properties. Pure rename; no surface-shape change. |
| 18 | `mime-table-split` | `Utils/MimeTypes.cs` does two jobs. Forward-lookup (extension → MIME) is I/O → moves to a new `App/Channels/Serializers/Formats/this.cs` (mount = `app.Serializers.Formats`). Reverse-lookup (MIME family → CLR type) is type resolution → becomes `app.Types.Clr(mimeType)` overload alongside `Clr(plangName)`. The MIME/Kind/Compressible block currently inside `App/Types/this.cs:215-315` moves out to Formats with the rest. Settled 2026-05-08 per the v3 thread. |
| 19 | `provider-to-code-rename` | End-to-end Provider → Code rename. `App/Providers/` → `App/Code/`; `IProvider` → `ICode` (fields stay: Name/IsDefault/IsBuiltIn/Source map to the developer-DLL-registration flow). `App/Data/Providers/` → `App/Data/Code/`. All `App/modules/X/providers/` → `App/modules/X/code/`. Per-module interfaces drop suffix (`IBuilderProvider` → `IBuilder`, `ILlmProvider` → `ILlm`, etc.). Implementations drop both `Default` and `Provider`: variant-named where the role isn't in the parent path (`OpenAi.cs`, `Fluid.cs`, `Grep.cs`), `Default.cs` where it is (`assert/code/Default.cs`, etc.). Driver: PLang-vocabulary alignment ("everything is goals except where you need code") — Provider was DI-flavored and PLang-foreign. Settled 2026-05-08. |
| 20 | `channel-app-backref-drop` | After stage 1's `Channel.@this.Channels` back-ref lands, `Channel.@this.App` becomes redundant — App is reachable via `Channels.Actor.App`. Drop the `App` back-ref on `Channel.@this`; sweep all `channel.App` readers to navigate via `Channel.Channels.Actor.App`. Single navigation point per OBP discipline. Small mechanical sweep; depends on stage 1. Settled 2026-05-08 (architect's call, Ingi delegated). |
| 21 | `navigators-to-variables` | Move `Data.Navigators.@this` from App-mounted to Variables-mounted (Ingi 2026-05-08: "this is per Data… more of a Variables thing since Data is stored and retrieved in the Variables"). Folder `App/Data/Navigators/` → `App/Variables/Navigators/`. Namespace `App.Data.Navigators` → `App.Variables.Navigators`. App-level property `app.Navigators` → `app.Variables.Navigators`. Boot registration `Navigators.RegisterDefaults()` (App.this.cs:321) moves to where Variables is allocated. Pure folder relocation + namespace rename + caller sweep; no behaviour change. |

## What's deferred (architect flags, not stages)

- **`App.Statics` → goal-backed dynamic property** — open design, not yet driven by a real call site. Stays on `Documentation/Runtime2/todos.md`.
- **`Data` parameter-lifecycle (request-scoped vs pr-template)** — the `data.ResetResolution()` smell. Ingi explicitly deferred; not in this plan.
- **v3 audit methodology / `/shared/app-tree/` surface files** — would require building a per-surface promise-document for every public mount on `app` and walking each against the code. Substantial work — its own follow-up cleanup branch when there's appetite. Not in this plan.

## Branch strategy

**Everything lands on this single branch (`runtime2-cleanup`).** Stages are sequential commits; no per-stage feature branches. After every stage, the cleanup branch merges back into `runtime2` only once *all* stages are complete (or the work is explicitly stopped early).

Per-stage rhythm on this branch: refactor → build → C# tests → PLang tests → validate → commit. **No stage commits until its tests pass and the project builds clean.** Each stage's commit is an atomic unit; if a later stage proves the realignment was wrong, the offending commit can be reverted in isolation.

If the work gets out of hand — accumulated commits hard to review, conflicts piling up, an in-flight stage stalling — split the remainder into its own branch then. Default is one branch.

This branch was forked off the `runtime2-channels` tip on 2026-05-07. The architect updates `plan.md` and `summary.md` on this branch as stages land; stage briefs carve at the architect bot root (`stage-N-<slug>.md`) when each stage is approached.

## Cross-cutting decisions (enforced for every stage)

1. **One ownership realignment per stage.** No bundled refactors. "While I'm here" changes go in their own stage.
2. **No new features.** This plan is cleanup. New behaviour goes in its own branch with its own plan.
3. **The architect-sharpened OBP rules apply to every stage.** Rule A (compound class names), Rule B (`Get<Plural>()` is a missing collection type), Rule C (static fields are a missing `@this`), Rule D (gerund-named app-graph properties are a wrong-shape name), Rule E (decomposed parameters that should navigate). See [`plan/principles.md`](plan/principles.md).
4. **Tests stay green.** Every stage rebuilds from clean and runs both the C# (`dotnet run --project PLang.Tests`) and the PLang (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`) suites.
5. **Stage docs are written when carved, not upfront.** The next 1–2 stages get full design docs; the rest stay as one-liners until we approach them. Every stage we land changes the design of the next.
6. **CLAUDE.md proposals only when canonical.** This work will surface per-area conventions worth adding to `/PLang/App/CLAUDE.md`. They go in `.bot/runtime2-cleanup/claude-md-proposals.md`, not direct edits.

## Settled before stage 1

- **Branch model (settled 2026-05-07)** — one branch (`runtime2-cleanup`), stages as commits, merges into `runtime2` after all stages complete.
- **First stage (architect's call 2026-05-07 — Ingi delegated)** — `serializers-stage-6-finish`. Closes drift introduced *during* the channels work and should happen before another stage touches Channels. `keepalive-collection` was the alternative starter (better as a pure pattern teacher); deferring to stage 3 means the same shape lesson lands but after the channels-drift items are out of the way.
- **Cadence (architect's call 2026-05-07 — Ingi delegated)** — default is one stage per session, but flexible. Tier 1 stages are small enough that two could fit in a session if appetite holds; Tier 3 stages (especially `app-run-redesign`) probably need more than one session each. Don't pre-commit to a rigid cadence; settle each one when carving its stage brief.

## Layout

```
.bot/runtime2-cleanup/architect/
├── summary.md                       chronological log of architect sessions
├── plan.md                          this file
├── plan/
│   ├── principles.md                OBP rules, slice anatomy, definition of done
│   └── post-cleanup-tree.md         destination tree for App/ after all stages land
└── stage-N-<slug>.md                created when each stage is carved
```

All bot pipeline output (architect, coder, tester, security, auditor, docs) lands under `.bot/runtime2-cleanup/`. Each stage's outputs accumulate in the same branch tree. The architect's `plan.md` is the cross-stage orchestration document; per-stage files (`stage-N-<slug>.md`) are the local design briefs.

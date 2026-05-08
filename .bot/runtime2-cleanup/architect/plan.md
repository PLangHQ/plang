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

Stages 1–22 are ordered roughly by **impact × isolation** — biggest wins that don't entangle with future work go first. Each stage gets its own `stage-N-<slug>.md` file at the architect bot root *when carved*. Stage files don't exist yet; only the spine and `plan/principles.md` do.

### Tier 1 — close active drift, demonstrate the slice rhythm (small, isolated)

| # | Slug | One-liner |
|---|------|-----------|
| 1 | [`serializers-single-home`](stage-1-serializers-single-home.md) | **Complete 2026-05-08 (coder).** Per-actor `Channels.@this.Serializers` is the single home for the registry. App.Serializers deleted; Stream's `_serializers` field/property dropped; `Channel.Channels` back-ref added; 5 external `app.Serializers` callers swept; tests 2755/2755 + 199/199. |
| 2 | [`channels-v1-helpers-drop`](stage-2-channels-v1-helpers-drop.md) | **Complete 2026-05-08 (coder).** Two dead surfaces on Channels.@this deleted: the `WriteAsync(actorName, channelName, ...)` v1 helper and the contentType-override branch + parameter in single-string WriteAsync. Tests 2755/2755 + 199/199. Three other `is Channel.Stream.@this sc` casts flagged for future stages. |
| 3 | [`keepalive-collection`](stage-3-keepalive-collection.md) | **Complete 2026-05-08 (coder).** `_keepAlive` + 2 methods on App moved to new `App/KeepAlive/this.cs` (IAsyncDisposable). App methods deleted entirely (zero callers verified). Tests 2755/2755 + 199/199. |
| 4 | [`dispose-self-owns`](stage-4-dispose-self-owns.md) | **Complete 2026-05-08 (coder).** `Modules.@this` and `Providers.@this` self-dispose; App.DisposeAsync's two foreach blocks become two delegated calls. Tests 2755/2755 + 199/199. |
| 5 | [`getstatic-shim-drop`](stage-5-getstatic-shim-drop.md) | **Complete 2026-05-08 (coder).** Shim deleted; single caller migrated to `App.Statics.GetBag(key)`. Tests 2755/2755 + 199/199. |
| 6 | [`app-data-inheritance-drop`](stage-6-app-data-inheritance-drop.md) | **Complete 2026-05-08 (coder).** App.this.cs:19 dropped `: Data.@this<@this>`; Path shadow + `: base("!app")` initialiser deleted. Side effect: warnings collapsed 449 → 68. Tests 2755/2755 + 199/199. **Tier 1 complete.** |

### Tier 2 — real refactors with one clear win each (medium)

| # | Slug | One-liner |
|---|------|-----------|
| 7 | [`callstack-promote-app-property`](stage-7-callstack-promote-app-property.md) | **Complete 2026-05-08 (coder).** `app.Debug.CallStack` → `app.CallStack`. 9 production callers swept (brief listed 7; coder caught 2 more in Variables/this.SnapshotAt.cs and Errors/this.cs). Tests 2755/2755 + 199/199. |
| 8 | [`read-file-off-channels`](stage-8-read-file-off-channels.md) | **Complete 2026-05-08 (coder).** Dead-code deletion of `Channels.ReadAsync<T>(filePath)` (zero callers). Tests 2755/2755 + 199/199. |
| 9 | [`catalog-dissolve-to-modules-schema`](stage-9-catalog-dissolve-to-modules-schema.md) | **Complete 2026-05-08 (coder).** Catalog dissolved; folder + namespace + types relocated; Rule E applied to `Build()` and `Render(spec)`; `Modules.@this.Schema` property added; `ExampleHelpers.cs` deleted; 6 action handlers (not 12 as estimated) + 4 test files migrated. Type aliases used to disambiguate `Action`/`Example` records from runtime types. Tests 2755/2755 + 199/199. **Tier 2 complete.** |

### Tier 3 — bigger refactors, design discussion required

| # | Slug | One-liner |
|---|------|-----------|
| 10 | [`app-run-redesign`](stage-10-app-run-redesign.md) | **Complete 2026-05-08 (coder).** App.Run from ~85 lines to ~15 via two new abstractions: `Context.AnchorScope(action)` and `Call.ExecuteAsync(handler, context)`. Five behavior contracts preserved precisely. Tests 2755/2755 + 199/199. |
| 11 | [`errors-app-backref-drop`](stage-11-errors-app-backref-drop.md) | **Complete 2026-05-08 (coder).** Errors.@this takes App via ctor; post-construction injection eliminated. 7 ErrorsScopeTests sites (which constructed Errors directly with `new()`) updated to construct a real App. Tests 2755/2755 + 199/199. |
| 12 | [`build-branch-to-build-this`](stage-12-build-branch-to-build-this.md) | **Complete 2026-05-08 (coder).** 33-line Build branch in App.Start extracted into Build.@this.RunAsync(). App.Start's branch is one line. Tests 2755/2755 + 199/199. |
| 13 | [`settings-collection-rework`](stage-13-settings-collection-rework.md) | **Complete 2026-05-08 (coder).** Settings reshape landed all three pieces: collection-over-Data; SettingsStore at App level (lazy); RegisterNavigable on Variables. Coder kept SettingsStore as Lazy<IStore> internally for boot efficiency; caught Variables.Clone needing to share `_navigables` by reference. Tests 2755/2755 + 199/199. **Tier 3 complete.** |

### Tier 4 — hygiene sweeps, last (against the settled tree)

| # | Slug | One-liner |
|---|------|-----------|
| 14 | [`timespan-iso-8601-sweep`](stage-14-timespan-iso-8601-sweep.md) | Replace `int? ExpiresInMs` with `TimeSpan? Expires` on `Callback.Signature.@this` and the `signing.sign` action record. Use site at `Data/this.Envelope.cs:87` updates. The `TimeSpanIso8601` converter exists today (Channels/Serializers/) and handles serialization to `"PT5M"`/`"PT1H"`/`"PT30S"` form. Closes the 2026-05-06 todos.md entry. Other `*Ms` properties (`CacheSettings.DurationMs`, `RetryOverMs`) flagged out of scope for this stage. **Brief carved 2026-05-08.** |
| 15 | `compound-name-rename` | Sweep all Rule A renames and the sub-rule (role-suffix-duplicates-folder) renames flagged by the two-capital screen. Each is mechanical. Concrete targets: `MemoryStepCache.cs` → `Cache/Memory.cs`; `ISnapshotted.cs` → `ISnapshot.cs` (past-participle convention); `PlangTypeConverter.cs` → `Data/Converter.cs`; `TimeSpanIso8601Converter.cs` → `Channels/Serializers/TimeSpanIso8601.cs`; `TypeJsonConverter.cs` → relocates cross-folder to `Data/Json.cs` (lives with the Type it serves); `Sensitive/Transport/ViewPropertyFilter.cs` → `Channels/Serializers/Filters/{Sensitive,Transport,View}.cs` plus NEW `Filters/this.cs` parent collection (Rule B — three same-shape filters earn a registry parent). `UnregisteredMimeType.cs` kept (typed exceptions are conventionally compound). `MigrationEnvelope` and `EventContext` were absorbed pre-stage by the channels merge. |
| 16 | `static-state-eviction-sweep` | Move every `static` field (incl. `static readonly`) into the `@this` that should own it (Rule C). Mostly mechanical for caches and config singletons: `PlangTypeIndex` → `App.Types.@this`, `JsonSerializerOptions` blocks → dispersed to consumers (no `App/Json/` mount; each consumer owns its instance), `ReservedKeywords` → `App.Variables.Reserved.cs` (all const/readonly). One hit needs a design note before the sweep runs: `OpenAiProvider._requestCount` (per-process counter on an actor-resolved provider — wrong scope; per Ingi: delete, todo logged). Static *methods* and `const` stay. |
| 17 | `builder-tester-rename` | Rename `App/Build/` → `App/Builder/` and `App/Test/` → `App/Tester/`. Property `app.Building` → `app.Builder`, `app.Testing` → `app.Tester`. CLI: `plang build` → `plang --builder`, `plang --test` → `plang --tester`. Rule D — gerund→noun on app-graph properties. Pure rename; no surface-shape change. |
| 18 | `mime-table-split` | `Utils/MimeTypes.cs` does two jobs. Forward-lookup (extension → MIME) is I/O → moves to a new `App/Channels/Serializers/Formats/this.cs` (mount = `app.Serializers.Formats`). Reverse-lookup (MIME family → CLR type) is type resolution → becomes `app.Types.Clr(mimeType)` overload alongside `Clr(plangName)`. The MIME/Kind/Compressible block currently inside `App/Types/this.cs:215-315` moves out to Formats with the rest. Settled 2026-05-08 per the v3 thread. |
| 19 | `provider-to-code-rename` | End-to-end Provider → Code rename. `App/Providers/` → `App/Code/`; `IProvider` → `ICode` (fields stay: Name/IsDefault/IsBuiltIn/Source map to the developer-DLL-registration flow). `App/Data/Providers/` → `App/Data/Code/`. All `App/modules/X/providers/` → `App/modules/X/code/`. Per-module interfaces drop suffix (`IBuilderProvider` → `IBuilder`, `ILlmProvider` → `ILlm`, etc.). Implementations drop both `Default` and `Provider`: variant-named where the role isn't in the parent path (`OpenAi.cs`, `Fluid.cs`, `Grep.cs`), `Default.cs` where it is (`assert/code/Default.cs`, etc.). Driver: PLang-vocabulary alignment ("everything is goals except where you need code") — Provider was DI-flavored and PLang-foreign. Settled 2026-05-08. |
| 20 | [`channel-app-backref-drop`](stage-20-channel-app-backref-drop.md) | After stage 1's `Channel.Channels` back-ref landed, `Channel.@this.App` is redundant — App reachable via `Channels.Actor.App`. Drop the App property + the registration setter; one reader (`MatchingBindings` line 194) navigates via `Channels?.Actor?.App`. Tiny 3-line cleanup. **Brief carved 2026-05-08.** |
| 21 | `navigators-to-variables` | Move `Data.Navigators.@this` from App-mounted to Variables-mounted (Ingi 2026-05-08: "this is per Data… more of a Variables thing since Data is stored and retrieved in the Variables"). Folder `App/Data/Navigators/` → `App/Variables/Navigators/`. Namespace `App.Data.Navigators` → `App.Variables.Navigators`. App-level property `app.Navigators` → `app.Variables.Navigators`. Boot registration `Navigators.RegisterDefaults()` (App.this.cs:321) moves to where Variables is allocated. Pure folder relocation + namespace rename + caller sweep; no behaviour change. |
| 22 | `app-shortcuts-drop` | Remove `app.Variables` and `app.Context` shortcuts on `App.this.cs:222-223` — they delegate to "current actor" which is fragile under parallel multi-Context execution (settled 2026-05-08). Caller sweep: ~2 production sites (`Actor/this.cs:144`, `Errors/Error.cs:265` — both use `app.Context`) + ~20 test sites (mostly `engine.Context` in `PrPipelineTests.cs`, `StartGoalTests.cs`). Each call site decides which actor's Context applies (`engine.System.Context` for app-level, `engine.User.Context` for the dominant test pattern). Originally folded into stage 10's scope; carved out because the sweep is the work, not coupled to App.Run's structural refactor. **Added 2026-05-08.** |

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

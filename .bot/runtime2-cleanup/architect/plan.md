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

Stages 1–22 are ordered roughly by **impact × isolation** — biggest wins that don't entangle with future work go first. Tier 5 (stages 23–27, added 2026-05-09) carves the deferred completion: one cosmetic rename leftover and the Rule C static-eviction tail stage 16 deferred. Each stage gets its own `stage-N-<slug>.md` file at the architect bot root *when carved*.

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
| 14 | [`timespan-iso-8601-sweep`](stage-14-timespan-iso-8601-sweep.md) | **Complete 2026-05-08 (coder).** ExpiresInMs → Expires (TimeSpan? + ISO 8601). 5 test files swept, todos.md entry marked RESOLVED. Tests 2752/2752 + 199/199 (3 ms-specific tests folded into Expires tests). |
| 15 | [`compound-name-rename`](stage-15-compound-name-rename.md) | **Complete 2026-05-08 (coder).** All 16 renames + the new Filters/this.cs collection landed. Tests 2752/2752 + 199/199. |
| 16 | [`static-state-eviction-sweep`](stage-16-static-state-eviction-sweep.md) | **Partially complete 2026-05-08 (coder).** 4 sites converted to instance + 1 deletion (OpenAi) + ReservedKeywords relocated. 4 sites deferred (AskCallback._options, DefaultHttpProvider's 2 statics, Choices' _gate/_registry, all of PlangTypeIndex.cs) — each requires a higher-level refactor of the static-caller chain to make the eviction safe. Reasonable judgment call by coder; the deferred items become follow-up work. Tests 2752/2752 + 199/199. |
| 17 | [`builder-tester-rename`](stage-17-builder-tester-rename.md) | **Complete 2026-05-08 (coder).** Rule D rename landed; Test* prefix on inner files dropped as bonus. Tests 2755/2755 + 199/199. |
| 18 | [`mime-table-split`](stage-18-mime-table-split.md) | **Complete 2026-05-08 (coder).** MIME table split into Formats (app.Formats) + Types.ClrFromMime overload. MimeTypes.cs deleted. Tests 2752/2752 + 199/199. |
| 19 | [`provider-to-code-rename`](stage-19-provider-to-code-rename.md) | The biggest sweep on this branch. End-to-end Provider → Code rename: 12 folder relocations, 22+ file renames, 11 per-module interfaces drop the suffix, marker `IProvider` → `ICode` (fields preserved), `app.Providers` → `app.Code`, 100+ caller sweeps. Driver: PLang-vocabulary alignment — Provider was DI-flavored and PLang-foreign; Code matches "everything is goals except where you need code." `modules/provider/` action folder rename is conditional on .goal-file impact (sweep Tests/). Risk medium-high (volume); behavior preserved everywhere. **Brief carved 2026-05-08.** |
| 20 | [`channel-app-backref-drop`](stage-20-channel-app-backref-drop.md) | **Complete 2026-05-08 (coder).** Channel.@this.App back-ref dropped; second reader at line 249 caught (brief had only 1). Coder corrected the navigation chain — Service-owned Channels have null Actor, so navigation goes via `Channels.App` (made public) instead of `Channels.Actor.App`. Tests 2755/2755 + 199/199. |
| 21 | [`navigators-to-variables`](stage-21-navigators-to-variables.md) | **Complete 2026-05-08 (coder).** Folder + namespace + property relocated. Tests 2755/2755 + 199/199. |
| 22 | [`app-shortcuts-drop`](stage-22-app-shortcuts-drop.md) | **Complete 2026-05-08 (coder).** app.Variables and app.Context shortcuts dropped. Coder caught 4 more production sites the brief missed (unqualified Variables/Context inside App's own partials and adjacent subsystems). Tests 2752/2752 + 199/199. |

### Tier 5 — deferred completion: cosmetic leftover + Rule C static-eviction tail (added 2026-05-09)

After stages 1–22 landed, the audit in `results.md` surfaced a tail of work that fell into two shapes: (i) one cosmetic rename leftover from a stage that got scope-narrowed, and (ii) the four static-eviction sites stage 16 deferred because they need a foundational refactor (TypeMapping → instance-bound) before the cascade can land. Tier 5 carves those into stages 23–27. Bucket C items (Events three-tier writer wiring + structural shape, CallStack scope, App.Statics, Data parameter-lifecycle) stay deferred — see "What's deferred" below.

**Correction note on Callback/Signature.** `results.md` originally listed `Callback/Signature/this.cs absorbs into Callback/this.cs` as a deferred cleanup. That was wrong: the current shape is OBP-correct. Folding the single `Expires` knob into `Callback.@this` would create a compound property name (`Callback.SignatureExpires`) — a Rule A violation. The folder structure preserves the navigation chain (`app.Callback.Signature.Expires`), which *is* the right shape. **Removed from Tier 5; left as-is.**

**Pulled out of Tier 5 (2026-05-09).** The originally planned `events-lifecycle-collapse` stage was based on a misread of the Events tree: `Events.@this` is the per-actor *registry*, while `Lifecycle.@this` is a per-target *view* created by `Context.LifecycleFor(goal/step/action)`. They're different scopes, not redundant nesting — "Before/After become properties on Events.@this" can't be right without conflating per-actor with per-target. The structural-shape question (lift `Bindings/Binding/` out of `Lifecycle/`? move Lifecycle to Context? leave alone?) tangles with the existing Events three-tier scoping todo, so the cleanup-pass finding was folded into that todo (`Documentation/Runtime2/todos.md` 2026-05-08, addendum 2026-05-09) for one combined design pass when it picks up.

| # | Slug | One-liner |
|---|------|-----------|
| 23 | [`callstack-restoredframe-rename`](stage-23-callstack-restoredframe-rename.md) | Single-file rename — `App/CallStack/RestoredFrame.cs` → `App/CallStack/Call/Position.cs`. Cleanup leftover from stage 10. Mechanical. **Landed 2026-05-09 (coder, commit `e96aa1ff`).** |
| 24 | [`askcallback-options-evict`](stage-24-askcallback-options-evict.md) | **Complete 2026-05-09 (coder, `63a9701a`).** Callback/Wire/this.cs created with Options property; both callbacks navigate via ctx.App.Callback.Wire.Options. ErrorCallback's helpers thread options as parameter. Two unused usings dropped on each callback. Tests 2752/2752 + 199/199. |
| 25 | [`http-default-statics-evict`](stage-25-http-default-statics-evict.md) | **Complete 2026-05-09 (coder, `a0d6b4e3`).** `_transportInOptions` → instance; `_jsonOptions` deleted (alias). Brief miss caught by coder: 3 read sites were inside private static async helpers (not instance methods as the brief assumed) — coder converted those helpers + 2 callers static→instance. Same realignment, just bigger surface. Tests 2752/2752 + 199/199. |
| 26 | [`types-keystone`](stage-26-types-keystone.md) | **Complete 2026-05-09 (coder, `82d82a58`).** Three statics → one app.Types subsystem. TypeMapping.cs deleted; PlangTypeIndex → Types/Registry.cs partial; App/Choices/ → App/Types/Choices/. Three coder good-judgment additions: (i) static-forwarder helpers on Types.@this (`GetPrimitiveOrMime`, `GetPrimitiveName`, `GetTypeNameStatic`) for App-less callers — pure-logic, Rule C exception; (ii) `Modules.@this` gains `App` back-ref so Schema/Render can navigate to Types; (iii) `PLang.Tests/Support/TypeMappingTestFacade.cs` preserves ~150 test sites without per-test rewrites by routing legacy static API through a shared App fixture. Tests 2752/2752 + 199/199. |
| 27 | [`utils-empty-out`](stage-27-utils-empty-out.md) | **Complete 2026-05-09 (coder, `6fa6dbda`).** Utils/TypeConverter.cs → Types/Conversion.cs (rename + partial); Utils/Json.cs dispersed to 6 homes — http/Default (CaseInsensitiveRead), App.@this (CamelCaseIndented internal-static), Variables/Data partials (SnapshotClone), new App/Diagnostics/this.cs (DiagnosticOutput + Format), Builder.@this (PrWrite + StoreOnlyModifier), new App/Data/JsonString.cs (ToJson + EmptyStringToNullEnumConverter). Coder kept Conversion methods + Diagnostics as static (Rule C exception for pure-logic helpers and config bags with no instance variation; many callers are in static contexts). Utils/ at exactly 4 files. Test facade extended for ~12 sites. Tests 2752/2752 + 199/199. **Tier 5 closes here.** |

**Ordering rationale.** 23 (done) → 24 → 25 are independent of each other and of the keystone. **26 is the keystone** — it must precede 27 because 27's `TypeConverter` move depends on `Types.@this` being a partial (which 26 establishes) and 27's `Utils.Json` dispersal is the natural follow-on once Types is the spine. Within Tier 5, stages must run sequentially from 26 onward; 24 and 25 can interleave with each other and 23 in any order.

**Why combined stage 26.** First-pass plan had stage 26 (Choices) and stage 27 (TypeMapping) as separate stages, with Choices listed as independent of the keystone. Walking the code surfaced that they share the same static-caller chain: TypeMapping's `GetValidValues(type, context)` calls `Choices.Get(type, context)`, and `GetValidValues` is itself called with `context = null` from 4 of 5 sites. Evicting Choices alone would either need Context threaded through TypeMapping (a heavier change than 26 was scoped for) or wait for TypeMapping to be instance-bound first. They're one realignment ("the type subsystem becomes instance, with sub-types under `app.Types`"). Folding into one stage also lets Choices land in its OBP-correct home (`app.Types.Choices`) instead of a temporary instance at the App root — which mattered: Choices is a small build-time registry and root-mounting overstates its weight.

**Coder-todo guardrail.** When a coder approaching a Tier 5 stage finds a TODO from `Documentation/Runtime2/todos.md` sitting directly on the path of the refactor (i.e., the stage cannot land cleanly without touching it), fold that piece in for that stage. Otherwise leave it for follow-up. We are not extending Tier 5 to chase todos — the rule is "fold in only when the refactor *needs* it." This mirrors what coder did during stages 1–22 (e.g., stage 14 closed the `ExpiresInMs → ISO 8601` todo because it was load-bearing for the rename; stage 16 closed `OpenAi._requestCount` because the static was the eviction target).

## What's deferred (architect flags, not stages)

- **`App.Statics` → goal-backed dynamic property** — open design, not yet driven by a real call site. Stays on `Documentation/Runtime2/todos.md`.
- **`Data` parameter-lifecycle (request-scoped vs pr-template)** — the `data.ResetResolution()` smell. Ingi explicitly deferred; not in this plan.
- **Events three-tier writer wiring** — per-channel / per-actor / app-level writer scoping. Documented in `todos.md` 2026-05-08; needs a design pass beyond shape cleanup.
- **CallStack scope (per-context vs shared)** — `App.Debug.CallStack` was promoted to `App.CallStack` in stage 7 but the per-context-vs-shared semantics weren't settled. Documented in `todos.md` 2026-05-08; too big for this branch.

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

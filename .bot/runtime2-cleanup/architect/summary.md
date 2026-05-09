# architect — runtime2-cleanup

## 2026-05-09 (v24) — Tier 5 carved (stages 23–29) extending the cleanup plan

After delivering 22 of 22 stages, walked the audit with Ingi to decide what to do with the deferred tail. Outcome: extend the same branch with seven more stages, organized as Tier 5.

**Scope settled.**
- Bucket A (cosmetic leftovers): stages 23–24.
- Bucket B (Rule C static-eviction tail): stages 25–29.
- Bucket C (Events writer wiring, CallStack scope, App.Statics, Data parameter-lifecycle, v3 audit): explicitly out of scope for this branch.

**Branch decision.** Same branch (`runtime2-cleanup`). No new branch.

**Granularity decision.** Each Tier 5 item gets its own stage — no bundling. Seven separate stages, each with its own commit.

**Correction landed.** `results.md` and `end-state-tree.md` originally listed `Callback/Signature/this.cs` as a "deferred — absorb into Callback/this.cs" cleanup. That was an OBP error: folding the `Expires` knob onto Callback creates a compound property name (`Callback.SignatureExpires`) — exactly the Rule A violation the cleanup is closing elsewhere. The current shape preserves the navigation chain `app.Callback.Signature.Expires`, which is OBP-correct. **Withdrawn from Tier 5; left as-is.** Note added to `results.md` deviation #3 and to the comparison snapshot in `end-state-tree.md`.

**Coder-todo guardrail noted in plan.** Tier 5 stages don't go chasing items in `Documentation/Runtime2/todos.md` — the rule is "fold a todo in only when the refactor *needs* it" (mirroring stages 14 and 16 from the original plan, which closed two specific todos because they were load-bearing for those renames).

**Stage 23 carve scheduled next session.** Order: 23 (RestoredFrame rename, smallest warm-up) → 24 (Events/Lifecycle collapse, alias re-target) → 25–27 (independent static-eviction warm-ups) → 28 (TypeMapping keystone, likely its own session) → 29 (Utils empty-out, mechanical once 28 lands).

Stage status:
| Tier | Stages | Status |
|------|--------|--------|
| 1–4 | 1–22 | complete |
| 5 | 23–29 | pending — none carved yet |

## 2026-05-09 — stage 19 landed; full plan vs delivered audit (`results.md`)

Stage 19 (the last) landed cleanly: Provider → Code rename, end-to-end. C# 2752/2752, PLang 199/199.

All 22 stages have shipped. Wrote `results.md` at the architect root: side-by-side audit of post-cleanup-tree vs actual end state. Headlines:

- **22 of 22 stages delivered.** All committed, all tested green.
- **9 deviations** from the planned tree, each annotated with reason and verdict. None are regressions.
- **Major deviations**: stage 16 partial (4 of 8 Rule-C sites deferred — static-caller chains require upper-level refactor); `app.Formats` mounts at root not under Channels/Serializers/ (mount path settled later than tree was written); App spine shrunk less than the aggressive line-count target (responsibility slice landed; bulk shrink lags).
- **Cosmetic leftovers** (single-file renames the next pass picks up): `Callback/Signature/` absorption, `Events/Lifecycle/` collapse, `RestoredFrame.cs` → `Call/Position.cs`.
- **One judgment call**: `Data/Code/Default.cs` instead of `Grep.cs` — class can't share a name with `IGrep.Grep()` method. Coder caught the C# constraint the brief missed.

The branch is ready for review. Deferred work itemized in `results.md` and `Documentation/Runtime2/todos.md`.

## 2026-05-08 (latest+16) — stages 15+16 landed (16 partially); stage 19 carved alone (the last)

Stages 15 and 16 landed.

**Stage 15**: 2752/2752 + 199/199. All 16 file renames + Filters/this.cs new collection landed cleanly. Mechanical sweep done.

**Stage 16**: 2752/2752 + 199/199. 4 of 8 hits converted to instance; 1 deletion (OpenAi.requestCount); ReservedKeywords relocated. **4 sites deferred**: AskCallback._options, DefaultHttpProvider's 2 static JsonSerializerOptions, Choices' _gate/_registry, and all of PlangTypeIndex.cs. Coder's reasoning: each requires a higher-level refactor of the static-caller chain (e.g., make TypeMapping instance-bound, or pass context through static helpers). Reasonable judgment call — full eviction would have cascaded into a substantially bigger refactor. The deferred items are real Rule C smells; they become follow-up work rather than blocking stage 16's intended scope.

### Stage 19 carved alone (the last)

Brief at `stage-19-provider-to-code-rename.md`. The biggest sweep on this branch:

- 12 folder relocations (App/Providers/, App/Data/Providers/, 10 per-module providers/ folders).
- 22+ file renames.
- Marker interface `IProvider` → `ICode` (fields preserved — they encode the developer-DLL-registration flow).
- 11 per-module interfaces drop the `Provider` suffix (`IBuilderProvider` → `IBuilder`, etc.).
- Implementations drop both `Default` and `Provider` suffixes — variant names where meaningful (`OpenAi.cs`, `Fluid.cs`, `Grep.cs`), `Default.cs` where the parent path says the role.
- App property `app.Providers` → `app.Code`.
- 100+ caller sweeps across PLang/ and PLang.Tests/.
- Conditional: `modules/provider/` action folder rename pending .goal-file sweep.

**Driver**: PLang vocabulary alignment. "Everything is goals, except where you need code." Provider was DI-flavored; Code matches the language's narrative.

**Risk**: medium-high (volume). Behavior preserved everywhere.

### After stage 19 lands

**Cleanup complete.** All 22 stages done. The runtime2-cleanup branch can merge to runtime2 once stage 19 completes.

Following work (not in this plan):
- Deferred Rule C statics from stage 16 — own follow-up plan when the calling chains can be made instance-bound.
- The deferred Error.@this.App back-ref (mentioned in stage 11 brief) — also follow-up.
- The CallStack scope question (per-context vs shared) — filed in todos.md.
- The Events three-tier writer-path question — filed in todos.md.
- The audit-substrate question (v3 methodology) — own branch when there's appetite.

---

## 2026-05-08 (latest+15) — stages 18+22 landed; stages 15+16 carved (penultimate Tier 4 batch)

Stages 18 and 22 landed cleanly per coder.

**Stage 18**: 2752/2752 + 199/199. Big restructure (Types/this.cs lost ~280 lines of MIME data + methods to the new Formats/this.cs).

**Stage 22**: 2752/2752 + 199/199. Coder caught **4 more production sites** beyond the brief's 2 — unqualified `Variables`/`Context` references inside App's own partials and adjacent subsystems (Errors/this.cs, Debug/this.cs, etc.). Brief grep targeted `app.Variables` / `app.Context` qualified form; the unqualified form inside App-self-partials was invisible. Coder's full sweep caught the lot.

### Stages 15 + 16 carved (penultimate Tier 4 batch)

**Stage 15** (`compound-name-rename`) — Rule A sweep. 16 file renames + 1 new `Filters/this.cs` collection (Rule B). Includes folder reshapes (new `Plang/` subfolder for plang-format serializers; new `Filters/` subfolder for property filters). Plus one cross-folder relocation (TypeJsonConverter → Data/Json.cs). Mechanical but volume + namespace updates make it medium-risk.

**Stage 16** (`static-state-eviction-sweep`) — Rule C sweep. 8 static-field hits per migration table in the brief. Most become per-instance fields. Two file relocations: `Utils/PlangTypeIndex.cs` → `Types/Registry.cs` partial; `Utils/ReservedKeywords.cs` → `Variables/Reserved.cs`. The `OpenAiProvider._requestCount` per-process cap deletes entirely (Ingi 2026-05-07; todos.md confirmed).

### Why batch 15 + 16

- Both Tier 4 hygiene (Rule A and Rule C respectively).
- Both mechanical sweeps with grep verification.
- Independent — coder picks order. If 15 lands first, file paths in stage 16 update accordingly (e.g., `PlangDataSerializer.cs` → `Plang/Data.cs`).
- Together, they finish the rule-driven cleanup. Stage 19 (Provider→Code) is the remaining big sweep.

### After 15+16 land

Only stage 19 (Provider→Code) remains. The biggest sweep on this branch — full namespace/folder relocation across all 11 provider folders + interface drops + class renames + caller sweeps. Its own focused session.

After stage 19: cleanup complete.

---

## 2026-05-08 (latest+14) — stages 17+21 landed; stages 18+22 carved (Tier 4 batch)

Stages 17 and 21 landed cleanly per coder.

**Stage 17**: 2755/2755 + 199/199. Coder also dropped the `Test*` prefix on inner files (TestFile.cs → File.cs, TestRun.cs → Run.cs, TestStatus.cs → Status.cs) — bonus cleanup that fell naturally out of the folder rename.

**Stage 21**: 2755/2755 + 199/199. Coder caught one extra production caller in `App/Data/this.Navigation.cs:281` (an unqualified `Navigators.ValueNavigators` reference that resolved to the old `App.Data.Navigators` namespace from inside `App.Data`). The brief's grep missed it because the grep targeted `App.Data.Navigators` qualified form; the unqualified form was invisible to that pattern.

### Stages 18 + 22 carved

**Stage 18** (`mime-table-split`) — bigger than first sized. Two MIME-table sources today: `Utils/MimeTypes.cs` (static) and `Types/this.cs:14, 215-315` (instance methods + data tables). Consolidate: forward I/O methods (ext → MIME, Kind, Compressible, KindOf, Add, Remove) move to new `App/Formats/this.cs` (mount: `app.Formats`); reverse type-resolution becomes `app.Types.ClrFromMime(mimeType)` overload. `Utils/MimeTypes.cs` deletes.

**Mount path correction**: the plan one-liner originally said `app.Serializers.Formats`. After stage 1's per-actor Serializers reshape (`app.Serializers` no longer exists), the correct mount is `app.Formats` at App root. Brief carries the correction.

**Stage 22** (`app-shortcuts-drop`) — 88 caller sweep (2 production + 86 test). Per-site judgment about which actor's Context applies. Brief includes a heuristic table for test directories (defaults: builder/ → User, errors/ → System, etc.).

### Why batch 18 + 22

- Both Tier 4 hygiene; different concerns; coherent.
- Both medium-sized.
- Independent — either order works.
- Stage 22 has been deferred multiple rounds; landing it clears a long-pending item.

### Tier 4 remaining after 18+22

- Stage 15 (Rule A renames) — substantial enumerated rename list; own session.
- Stage 16 (static-state-eviction) — multiple targets, mechanical-ish; own session.
- Stage 19 (Provider→Code) — biggest sweep on this branch; own session.

After all three land, the cleanup is done.

---

## 2026-05-08 (latest+13) — stages 14+20 landed; stages 17+21 carved (Tier 4 rename batch)

Stages 14 and 20 landed cleanly per coder.

**Stage 14**: 2752/2752 + 199/199 (3 ms-specific tests folded into Expires tests). Coder updated Ed25519Provider (uses `now.Add(expiry)` instead of `AddMilliseconds`) and DefaultHttpProvider's signing forwarder. Closed the 2026-05-06 todos.md entry.

**Stage 20**: 2755/2755 + 199/199. Coder caught 2 things the brief missed:
- Second reader at line 249 (diagnostic Write) — brief grep only found 1.
- Service-owned Channels have null Actor (Service holds Channels but isn't an Actor) — the brief's `Channels.Actor.App` chain breaks for them. Coder corrected: made `Channels.@this.App` a public property, navigation via `Channels.App` instead. Smart fix.

### Stages 17 + 21 carved as Tier 4 rename batch

**Stage 17** (`builder-tester-rename`) — Rule D applied: `App/Build/` → `App/Builder/`, `App/Test/` → `App/Tester/`. Namespaces, properties, CLI form all follow. ~150 caller sweeps (29 Build + 124 Testing). The verb form `plang build` keeps working as legacy syntactic sugar.

**Stage 21** (`navigators-to-variables`) — folder/namespace move; instance stays App-allocated and shared; Variables exposes via delegating property `Navigators => _context!.App.Navigators`. 7 files + 2 caller sites.

### Why batch 17 + 21

- Both Tier 4 hygiene work; no shape changes.
- Both involve folder + namespace + caller sweep.
- Independent (different folders, different concerns).
- Stage 22 (`app-shortcuts-drop`, ~88 caller sweeps with per-call judgment about which actor's Context applies) deferred — its own focused session next round.

### Tier 4 remaining after 17+21

- 15 (Rule A renames) — substantial enumerated rename list; own session.
- 16 (static-state-eviction) — Rule C statics; own session.
- 18 (mime-table-split) — folder/structure split; own session.
- 19 (Provider→Code) — biggest sweep on this branch; own session.
- 22 (app-shortcuts-drop) — caller sweep; can carve standalone.

---

## 2026-05-08 (latest+12) — stage 13 landed (Tier 3 done); stages 14+20 carved (Tier 4 batch)

Stage 13 (Settings rework) landed cleanly per coder — 2755/2755 + 199/199. The largest design refactor on this branch came in cleanly. Coder noted two subtleties beyond the brief:
- Kept SettingsStore as `Lazy<IStore>` internally on App for boot efficiency (apps that never touch settings don't pay for SQLite-file creation at boot).
- Variables.Clone needed to share `_navigables` by reference (resolvers are stateless closures; cloning meaningless) — caught a bug the brief didn't mention.

**Tier 3 complete** (stages 10–13 all done).

### Stages 14 + 20 carved as Tier 4 batch

Both small mechanical cleanups, sanctioned for two-per-session.

**Stage 14** (`timespan-iso-8601-sweep`) — `int? ExpiresInMs` → `TimeSpan? Expires` on `Callback.Signature.@this` and the `signing.sign` action record. The `TimeSpanIso8601` converter already exists; serialization auto-produces `"PT5M"`/`"PT1H"` forms. Other `*Ms` properties flagged out of scope (future stages). Closes the 2026-05-06 todos.md entry.

**Stage 20** (`channel-app-backref-drop`) — drop the redundant `Channel.@this.App` back-ref now that stage 1 added `Channel.Channels`. One reader (`MatchingBindings` line 194) navigates via `Channels?.Actor?.App`. 3-line cleanup.

### What remains

Tier 4 stages 15, 16, 17, 18, 19, 21, 22 still to carve. The bigger ones (15: Rule A renames; 16: static eviction; 18: mime-table-split; 19: Provider→Code) merit own sessions. The smaller ones (17: builder-tester rename; 21: navigators-to-variables; 22: app-shortcuts-drop — though 22 has ~88 sites, mostly tests) can batch.

---

## 2026-05-08 (latest+11) — stages 11+12 landed; stage 13 (Settings rework) carved alone

Stages 11 and 12 landed cleanly per coder — 2755/2755 + 199/199 each. Stage 11 had a small test-side sweep I'd missed (7 ErrorsScopeTests sites that constructed Errors directly with `new()`); coder caught it.

Stage 13 carved as a focused single-stage session — biggest design refactor in this branch.

### Stage 13 carve (`settings-collection-rework`)

Brief at `stage-13-settings-collection-rework.md`. Three tightly-coupled changes that together close the SettingsVariable inheritance smell and move SettingsStore to its right scope:

1. **Settings.@this becomes a collection over Data**, not a Data subclass. Two-method surface (`Get(path, context)`, `Set(key, data)`). Replaces `SettingsVariable` whose `: Data.@this` inheritance was mechanism-leaking-into-shape (existed only to fit through `GetChild` for `%Settings.X%` interception).

2. **SettingsStore moves from per-actor (dead drift) to App level.** Zero User.SettingsStore consumers; all 10 real consumers use `app.System.SettingsStore`. Per-actor `Lazy<ISettingsStore>` allocation on Actor + `CreateSettingsStore` method delete. App gains `SettingsStore { get; }` backed by `system.sqlite`.

3. **`Variables.@this.RegisterNavigable(name, resolver)` mechanism** — new hook on Variables. Each actor's Variables registers `"Settings"` with a resolver that delegates to `app.Settings.Get(path, Context)`. Replaces the Data-subclass `GetChild` interception path. Generalizable to any future non-Data navigable mount.

Plus renames: `ISettingsStore.cs` → `IStore.cs`; `SqliteSettingsStore.cs` → `Sqlite.cs`. `SettingsVariable.cs` deleted.

10-site caller sweep across Goals/Setup, identity provider, llm provider, settings module.

**Risk medium-high** — largest design refactor on this branch. Brief is dense on the integration points (per-actor Context capture in lambda, dispose ordering, InMemory-Testing branch placement, SettingsVariable doc-comment cleanup).

### Why stage 13 alone

Per plan principles, this is the kind of stage that "probably needs more than one session each." Three intertwined design pieces. Coder gets focused attention; not batched.

### After stage 13 lands

Tier 3 done (stages 10, 11, 12, 13 all complete). Tier 4 next: stages 14-22 (timespan, compound-name, static, builder-tester, mime-table, provider-to-code, channel-app-backref, navigators-to-variables, app-shortcuts). Mostly hygiene sweeps; some can batch.

---

## 2026-05-08 (latest+10) — stage 10 (headliner) landed; stages 11+12 carved as Tier 3 batch

Stage 10 (`app-run-redesign`) — the headliner — landed cleanly per coder. 2755/2755 + 199/199. App.Run from 85 lines to ~15 via the two new abstractions (`Context.AnchorScope`, `Call.ExecuteAsync`). All five behavior contracts named in the brief preserved exactly. The expected "may need 2 sessions" landed in one — cleaner than expected.

### Stages 11 + 12 carved as a Tier 3 batch

Both small Tier 3 stages, both touch App.this.cs in different sections, independent of each other.

**Stage 11** (`errors-app-backref-drop`) — minimal scope: fix the post-construction injection only. Errors.@this gets a ctor that takes App; field-init `new()` becomes `new(this)`; internal `App?.CallStack` references become `_app.CallStack` (null operators go away). Out of scope: the separate `Error.@this.App` back-ref via Error.Callback materialisation (deeper refactor; future stage).

**Stage 12** (`build-branch-to-build-this`) — copy-paste extract of the 33-line `if (Build.IsEnabled) { ... }` block from App.Start into `Build.@this.RunAsync()`. Build already has an App back-ref via its ctor; the move uses that. App.Start's branch becomes one line. Risk low-medium (mostly mechanical extract).

### Why batch 11 + 12

- Both Tier 3 (real shape work, but small).
- Both touch App.this.cs in different sections.
- Independent — either order works.
- Coherent narrative: "App stops doing things its sub-systems should own" (echoes stage 4's theme).

Per plan principles, "Tier 1 stages are small enough that two could fit" — stage 11's minimal version is closer to Tier 1 size than the typical Tier 3, so the two-stage cadence still fits.

### Stage 13 (settings-collection-rework) and 22 (app-shortcuts-drop) deferred

Stage 13 is the bigger Tier 3 stage (Settings reshape + IStore + Sqlite + RegisterNavigable + SettingsStore relocation) — its own focused session next round.

Stage 22 (caller-sweep, ~25 sites) can fit anywhere in Tier 4; carve when the time is right.

---

## 2026-05-08 (latest+9) — stage 9 landed; stage 10 (the headliner) carved alone; stage 22 added

Stage 9 (`catalog-dissolve-to-modules-schema`) landed cleanly per coder — 2755/2755 + 199/199. Coder noted lessons:
- **6 action handlers, not 12 as I'd estimated.** My grep for `using static App.Catalog.ExampleHelpers` was right; the count of files was off.
- **4 test files I missed** because they used `using App.Catalog;` namespace form (not the static-helper form).
- **Type aliases used to disambiguate** — `using ExampleSpec = App.Modules.Schema.Spec.Example;` — to avoid `Action`/`Example` record names colliding with `System.Action` delegate, the `[Action]` attribute, and `Example` types elsewhere.
- **Lazy semantics preserved** as the brief specified.

**Tier 2 done** (stages 1–9 complete).

### Stage 10 carved alone

Brief at `stage-10-app-run-redesign.md`. The headliner: App.Run from ~85 lines + ~10 mutations to ~10 lines. Two new abstractions:

- `Context.AnchorScope(action)` — disposable that captures + restores the 4 anchor values (Step, Goal, Event, Step.Context).
- `Call.ExecuteAsync(handler, context)` — wraps handler invocation, error stamping (SnapshotParams + CallFrames), audit-collection, OCE swallowing.

Plus a tight `HandleOverflow` private helper for the CallStackOverflow-at-Push case (overflow happens before the call frame exists, so it can't fold into ExecuteAsync).

Brief is dense on **behavior preservation** — five subtle contracts that must stay precisely intact (CallStackOverflow catch tightness, OCE swallow scope, error stamping order, dispose order, anchor restoration). Risk medium.

### Stage 22 added (`app-shortcuts-drop`)

The earlier plan note that stage 10 might fold the `app.Variables` / `app.Context` shortcut removal is reversed. Sweep is bigger than expected:

- 2 production callers (`Actor/this.cs:144`, `Errors/Error.cs:265`).
- ~20+ test callers (PrPipelineTests.cs alone has 9, plus StartGoalTests.cs and others).

Each call site decides which actor's Context applies (`engine.System.Context` for app-level, `engine.User.Context` for the dominant pattern). That's the work — not coupled to App.Run's structural refactor. Carved as stage 22 (Tier 4 placement; small caller-sweep stage).

Stage count 21 → 22.

### Why stage 10 alone

Per plan principles: "Tier 3 stages (especially app-run-redesign) probably need more than one session each." Stage 10 has substantial design work (two new abstractions, contract preservation). Coder needs focused attention; not batched with stage 11 or 12.

### After stage 10 lands

Tier 3 progresses. Stages 11 (errors-app-backref-drop) and 12 (build-branch-to-build-this) likely batchable next round. Stage 22 can fit anywhere in Tier 4.

---

## 2026-05-08 (latest+8) — stages 7+8 landed; stage 9 carved (Tier 2 finisher)

Stages 7 and 8 landed cleanly per coder — 2755/2755 + 199/199 each. Stage 7's coder caught 2 caller sites I missed in the brief (Variables/this.SnapshotAt.cs and Errors/this.cs); 9 production callers + 9 test files total instead of the brief's 7. The brief's grep was thorough but not exhaustive — coder's final sweep is the safety net.

Stage 9 carved as its own focused session (vs batched) because it's the biggest stage so far.

### Stage 9 carve (`catalog-dissolve-to-modules-schema`)

Brief at `stage-9-catalog-dissolve-to-modules-schema.md`. Substantial restructure:

- **Folder relocation**: `App/Catalog/` (5 files) → `App/Modules/Schema/` with new `Spec/` subfolder for the record family.
- **Type renames**: drop "Spec" suffix on records (`ActionSpec` → `Action`, `ExampleSpec` → `Example` under `Spec/`); `TypeEntry` → `Entry`; `ExampleRenderer` → `Render`.
- **Rule E refactor** (the worked example): `Build(modules)` and `Render(spec, modules)` become instance methods. Schema holds `_modules` set at construction; callers stop passing modules in. `app.Modules.Schema.Build()` replaces `App.Catalog.@this.Build(action.Context.App.Modules)`.
- **`Modules.@this.Schema` property** — Modules constructs Schema in its ctor (`Schema = new Schema.@this(this);`), passing `this` as the parent ref.
- **`ExampleHelpers.cs` deletion** — records' positional ctors cover the use case. 12 action handlers migrate from `Example("intent", chain)` to `new Example("intent", chain)`.
- **Caller sweeps** in Types, TypeMapping (multiple sites), Modules itself, DefaultBuilderProvider, plus the 12 handlers.

**Out of scope, flagged as future work**: the static formatters in `DefaultBuilderProvider.cs` (`FormatValue`, `RenderActionFormal`) and `FluidProvider.cs` (`FormatFormalValue`). The plan one-liner mentioned absorbing them into `Schema.Render` but they're a different layer (value-token rendering, not example-string rendering). Unification needs its own design pass.

### Risk note for stage 9

Largest stage so far. Risk medium — building catches caller misses, tests catch behavior changes. The "Watch for" section names the most-likely failure modes: rename collisions on `Action` / `Example`, lazy-vs-eager Schema build, the 12-handler ExampleHelpers migration.

### After stage 9 lands

Tier 2 done. Tier 3 next (stages 10–12: app-run-redesign, errors-app-backref-drop, build-branch-to-build-this). Stage 10 (App.Run) is the headliner of the cleanup; per plan principles likely needs its own session.

---

## 2026-05-08 (latest+7) — Tier 1 complete (stages 5+6 landed); Tier 2 begins (stages 7+8 carved)

**Tier 1 finished.** Stages 5 and 6 landed cleanly per coder — 2755/2755 + 199/199 each. Notable side effect on stage 6: build warnings collapsed 449 → 68 (the inherited Data surface on App generated a flock of nullability warnings that are now gone).

Tier 1 retrospective — six stages, all passed first time, no scope corrections from coder. The "two Tier 1 stages per session" cadence held throughout: stages 1 alone (initial), then 2 alone (transition), then 3+4, then 5+6.

### Stage 7 carve (`callstack-promote-app-property`)

Brief at `stage-7-callstack-promote-app-property.md`. Property relocation: `app.Debug.CallStack` → `app.CallStack`. Folder structure already names App as owner; only the property placement disagrees. Touches:

- App.this.cs gains the property + `new()` allocation.
- Debug.this.cs loses property (line 76) + allocation (line 101); the one internal use (line 154's `CallStack.Flags = ...`) reaches via Debug's existing App field reference.
- Context.this.cs:48 read-through accessor updates from `App.Debug?.CallStack` to `App?.CallStack`.
- 7 external callers swept (App.Run, Snapshot, Goals, ErrorCallback, etc.).

**CallStack scope (per-context vs shared) is filed in `Documentation/Runtime2/todos.md` and explicitly NOT in scope here.** Stage 7 only changes the navigation path.

### Stage 8 carve (`read-file-off-channels`) — reduced to dead-code deletion

Brief at `stage-8-read-file-off-channels.md`. The plan one-liner anticipated relocating `Channels.ReadAsync<T>(filePath)` to `app.Serializers` or FileSystem. Two findings reduced this to pure deletion:

1. **Zero callers** anywhere (verified by two distinct grep patterns across `PLang/`, `PLang.Tests/`, `Tests/`).
2. **`app.Serializers` no longer exists** — it was deleted in stage 1; per-actor `actor.Channels.Serializers` is the new home. The plan's relocation target is gone.

Just delete the method. If a future caller wants "read a file and deserialize," they write the two-step at the call site.

### Why batch 7 and 8

- Both Tier 2 (medium-sized; 7 is property + 7-caller sweep, 8 is single-method delete).
- Independent (different files, different concerns).
- Stage 9 (Catalog dissolution) is substantially bigger and deserves its own focused session — carving it later, not in this batch.

### Stage 9 punted

`catalog-dissolve-to-modules-schema` is the biggest remaining stage in Tier 2: whole `App/Catalog/` folder relocates into `App/Modules/Schema/`, two static formatters in builder providers absorb into `Schema.Render`, Rule E navigation (`Build()` and `Render(spec)` become instance methods on Modules.Schema). Real restructure. Carve in its own session after 7+8 land.

---

## 2026-05-08 (latest+6) — stages 3 + 4 landed; stages 5 + 6 carved as the Tier 1 finisher

Both stages 3 (`keepalive-collection`) and 4 (`dispose-self-owns`) landed cleanly — coder reported 2755/2755 + 199/199 for each. The Tier 1 batching pattern works.

Carved stages 5 and 6 — the last two Tier 1 stages — as another batch:

### Stage 5 carve (`getstatic-shim-drop`)

Brief at `stage-5-getstatic-shim-drop.md`. Trivial: one-line internal shim with one caller. `App.GetStatic(string)` at App.this.cs:115 deletes; `Actor/Context/this.cs:248` migrates from `App.GetStatic(key)` to `App.Statics.GetBag(key)`. Risk very low.

### Stage 6 carve (`app-data-inheritance-drop`)

Brief at `stage-6-app-data-inheritance-drop.md`. Class hierarchy change: App stops inheriting from `Data.@this<@this>`. Verified by greps that nothing reads inherited surface on `app` (Properties, Type, Error, Success, OnChange/OnCreate/OnDelete — all zero hits) and that `app.Path` has no consumers (the `new string Path => "/"` shadow at line 63 has no readers — delete the property). `%!app%` resolution doesn't depend on inheritance; it goes through `DynamicData("!app", () => app)` wrapping pattern. Risk medium (class hierarchy change; build is the safety net for any caller dependency the greps missed).

### Why batch 5 and 6

- Both Tier 1.
- Both touch App.this.cs (different lines: 115 for stage 5, 19+63 for stage 6).
- Independent — either order works.
- Together they finish the Tier 1 sequence (stages 1–6 all complete after this).

### After stage 6 lands

Tier 1 is done. App's surface is meaningfully smaller: per-actor Channels.Serializers (stage 1), no v1 helpers (stage 2), KeepAlive collection (stage 3), Modules+Providers self-dispose (stage 4), no GetStatic shim (stage 5), no Data inheritance (stage 6). Next session: Tier 2 (stages 7–9 — callstack-promote, read-file-off-channels, catalog-dissolve).

---

## 2026-05-08 (latest+5) — stage 2 landed; stages 3 + 4 carved as a Tier 1 pair

Stage 2 (`channels-v1-helpers-drop`) landed cleanly — coder reported 2755/2755 + 199/199. Codeanalyzer ran on stage 1 with PASS verdict (3 minor findings, none blocking). Pulled and carved stages 3 and 4 as a pair — both Tier 1, both touch `App.DisposeAsync` in different sections, sanctioned by the plan's "two could fit per session" cadence.

### Stage 3 carve (`keepalive-collection`)

Brief at `stage-3-keepalive-collection.md`. Pure extract-class refactor with zero external callers — verified the `KeepAlive(x)` / `RemoveKeepAlive(x)` methods on App have no consumers anywhere in `PLang/`, `PLang.Tests/`, or `Tests/`. Both methods get deleted (not preserved as delegates). The new `App/KeepAlive/this.cs` is a faithful translation of today's lifecycle: Add, Remove (with the same sync-dispose semantics today's RemoveKeepAlive uses), IAsyncDisposable that disposes each entry and clears.

### Stage 4 carve (`dispose-self-owns`)

Brief at `stage-4-dispose-self-owns.md`. Modules.@this and Providers.@this each gain `IAsyncDisposable` + a `DisposeAsync` that iterates their own internal collection (mirroring today's `Modules.All` and `Providers.All()` projections). App.DisposeAsync's two ~8-line foreach blocks shrink to two delegated calls. `Modules.All` and `Providers.All()` lose their only callers but stay as public surface — explicit non-decision in stage 4.

### Why batch 3 and 4

- Both Tier 1 (small, isolated).
- Both touch the same method (`App.DisposeAsync`) in different sections — coder can land them in either order without conflict.
- Coherent narrative: "App stops doing manual cleanup; sub-systems own their own dispose."
- Sanctioned by plan principles: "Tier 1 stages are small enough that two could fit in a session if appetite holds."

If both go well, next session likely batches stages 5 (`getstatic-shim-drop`) and 6 (`app-data-inheritance-drop`) similarly.

---

## 2026-05-08 (latest+4) — stage 1 landed (coder); stage 2 carved

Coder finished stage 1 (`serializers-single-home`) cleanly — 2755/2755 C# + 199/199 PLang, all five caller sites swept, the boot-ordering case the brief flagged caught and fixed in 7 unit tests. Pulled the work; carved stage 2.

### Stage 2 carve (`channels-v1-helpers-drop`)

Brief at `stage-2-channels-v1-helpers-drop.md`. Tighter than the plan one-liner suggested:

- Code dig confirmed both target surfaces are dead. `WriteAsync(actorName, channelName, ...)` has zero external callers (only its own internal redirect references it). The `contentType` parameter on `WriteAsync(channelName, data, contentType, ...)` has zero callers passing a non-null value (DefaultHttpProvider's two call sites at lines 852/907 don't pass contentType).
- DefaultHttpProvider migration that the plan one-liner mentioned isn't actually needed — its callers already use the simplified shape.
- Three other `is Channel.Stream.@this sc` casts in the same file have the same shape smell but are out of stage 2's plan-stated scope (in ReadChannelAsync / WriteTextAsync / ReadTextAsync). Brief flags them under "Watch for" as future-stage candidates.
- Stage 2 risk: very low (dead-code deletion + one parameter removal; build break would catch any missed caller).

Plan.md stage 1 row marked complete; stage 2 row updated with brief link.

---

## 2026-05-08 (latest+3) — scope map authored; six questions settled; two stages added; two todos filed

After stage 1's brief settled, Ingi pushed for an explicit shared-vs-per-actor map for the App graph — "this distinction is very important; we should know what is shared and what is per-actor and agree on it." Authored `plan/scope-map.md` (~200 lines), reviewed across two rounds of comments, settled six scope questions and filed two architectural concerns to `Documentation/Runtime2/todos.md`.

### What changed

**New artifact: `plan/scope-map.md`.** Catalogs every long-lived `@this` and major property on the App graph by scope (shared / per-actor / per-context / per-call). Linked from `plan.md`. Vocabulary section, shared list (~15 items), per-actor list (~7 items), mixed cases (Settings absorbed, SettingsStore relocated, CallStack flagged, app.Variables/Context shortcuts removed), per-operation section (Snapshot, CallStack.Call), three-tier Events explainer, settled-questions trail.

**Six scope questions settled (across two review rounds):**

| Q | Topic | Resolution |
|---|-------|-----------|
| Q1 | Settings.@this after stage 13 | shared, one per app |
| Q2 | `app.Variables` / `app.Context` shortcuts | REMOVE — fragile under parallel execution; folded into stage 10 |
| Q3 | `Channel.@this.App` back-ref after stage 1 | REMOVE — redundant once `Channel.Channels` lands; new stage 20 |
| Q4 | `app.Cache` scope | shared per app, intentional ("global cache") |
| Q5 | `Errors.Trail` scope | leave shared, no change in this branch |
| Q6 | `app.Events` vs `Context.Events` | three-tier design intentional, kept; writer-path design pass filed in todos |
| Q7 | `app.Navigators` placement | move to Variables; new stage 21 |

**Two architectural concerns filed in todos.md (not in this branch):**

- **Events three-tier scoping needs a design pass** — per-channel + per-actor + app-level tiers exist; app-level reader exists but no writer path. Decide whether to build the writer or remove the tier.
- **CallStack scope: shared on App.Debug is wrong for parallel execution** — `_current` is correctly AsyncLocal but `_root`, `Audit`, tree structure are shared. Sequential CLI fine; web-pool parallel breaks. Per-context vs split-config-from-state both real options.

**Two new stages:**

- **Stage 20** `channel-app-backref-drop` — once stage 1's `Channel.Channels` lands, drop `Channel.App` back-ref; sweep readers to navigate via `Channels.Actor.App`.
- **Stage 21** `navigators-to-variables` — folder + namespace + property relocation; `app.Navigators` becomes `app.Variables.Navigators`.

**Stage scopes expanded:**

- **Stage 10** `app-run-redesign` — gains the `app.Variables` / `app.Context` shortcut removal.
- **Stage 13** `settings-collection-rework` — gains the SettingsStore-from-per-actor-to-app-level move (verified: zero consumers use User's SettingsStore today; per-actor allocation is dead drift).

**Principles loosened:**

`plan/principles.md` Context section's "scope test" replaced with "choosing what back-ref(s) a class holds" — Context, App, parent-ref, or none, depending on what the class touches. Smells around god-bag back-refs and implicit per-actor reaches dressed up as app-level. Trade-off named: minimalism over uniformity.

### Stage count

19 → 21.

### Final stage index

| # | Tier | Slug | Status |
|---|------|------|--------|
| 1 | 1 | serializers-single-home | brief carved, ready for coder |
| 2-9 | 1-2 | (channels-v1 through catalog-dissolve) | pending |
| 10 | 3 | app-run-redesign (+ shortcut sweep) | pending |
| 11-12 | 3 | (errors-app-backref-drop, build-branch-to-build-this) | pending |
| 13 | 3 | settings-collection-rework (+ SettingsStore move) | pending |
| 14-19 | 4 | (timespan, compound-name, static, builder-tester, mime-table, provider-to-code) | pending |
| 20 | 4 | channel-app-backref-drop | pending (NEW) |
| 21 | 4 | navigators-to-variables | pending (NEW) |

### Pattern observed

Three rounds of scope-map comments exposed misreadings I made by skimming code:
- Called Events "dead drift" — actually a deliberate three-tier design.
- Listed `app.Snapshot` as shared — actually per-operation, not even a property.
- Listed `actor.SettingsStore` as per-actor — dead drift, never used; should be app-level.
- Listed CallStack as cleanly shared — actually shared *with* AsyncLocal-current that's correct, but other state shared in a way that breaks under parallel.

The lesson: read the actual usage chain (allocation site → readers → writers) before classifying scope. Shape inferred from one file is often wrong.

---

## 2026-05-08 (latest+2) — per-actor Serializers settled; principle loosened; stage 1 brief rewritten

Ingi walked me through the actual PLang construction model (App → System/User actors → each Actor creates its own Context, then its own Channels). Under that model, **Serializers is per-actor — each actor's Channels owns its own registry**. My earlier "shared singleton on App" framing was over-engineered.

The smell stage 1 closes is **not** "three instances of one thing." It's **two surfaces that bypass the per-actor model**:
- `App.@this.Serializers` (App-root shortcut that skips actors)
- Per-`Channel.Stream.@this.Serializers` (third copy lazily allocated per stream, not shared with its parent Channels)

Both go away; per-actor `Channels.@this.Serializers` stays as the home; Stream channels reach their parent via a new `Channel.Channels` back-ref.

### Principle loosened

Earlier "scope test" in `principles.md` (per-actor → take Context, per-app → take App) was too strict. Real rule: **classes hold whatever back-ref(s) they actually need.** Channel needing a Channels back-ref (not Context, not App) is the example. Trade-off acknowledged: minimalism over uniformity. Each class is honest about its dependencies; reading code requires looking at what it touches. PLang chooses minimalism.

Updated `principles.md` Context section: dropped scope test; replaced with "choosing what back-ref(s) a class holds" — Context, App, parent-ref, or none, depending on what the class actually touches. Added smells around god-bag back-refs and implicit per-actor reaches dressed up as app-level.

### Stage 1 brief rewritten (third time)

- Slug: `serializers-single-home` (kept).
- Scope: delete `App.Serializers`; remove Stream's `_serializers`; add `Channels` back-ref to `Channel`; sweep 5 external callers.
- Channels.@this ctor stays `(App app)` — no Context conversion. Channels has an Actor property for per-actor reach; doesn't need Context for what it does today.
- 5 external caller sites identified by file:line — each gets a specific access path (`app.System.Channels.Serializers` for app-level; `action.Context.Actor.Channels.Serializers` for handlers; `Actor!.Channels.Serializers` for the DynamicData lambda).
- One additive change: `Channel.@this` gains a `Channels` back-ref alongside the existing `App` back-ref. Set during `Channels.Register(channel)`.

### Stage count

Stage 20 (`serializers-app-shortcut-drop`) cancelled — its work folds into stage 1. Total stages: 19.

### Pattern observed in this thread

I rewrote stage 1 four times because I kept locking onto an architectural framing before validating it against the codebase. Each rewrite Ingi caught the bug. The lessons:
- Verify property/path existence in code before writing them into briefs (`app.Channels` doesn't exist).
- Don't concede to a strong-sounding architectural argument without checking whether its premise holds.
- Per-actor vs shared is a design decision the user makes, not a default I should assume.

Future stage briefs: read the construction flow before sketching ctor signatures. Validate every navigation path against the actual code.

---

## 2026-05-08 (latest+1) — Serializers ownership clarified, stage 20 added, stage 1 reframed

Ingi pushed back on a quiet inconsistency in stage 1's brief: it routed everything through `app.Serializers` while the destination tree had Serializers under Channels. His architectural argument: serialization only ever happens at I/O boundary crossings, and Channels IS the I/O boundary subsystem in PLang — so the registry belongs under Channels, full stop. The earlier framing (treating Serializers as a "general utility used by Channels among others") was wrong; the OBP-coherent shape is one I/O subsystem owning all I/O concerns.

### What changed

- **Stage 1 reframed.** Slug `serializers-stage-6-finish` (a lineage label from the prior channels plan, confusing for the coder) → `serializers-single-home`. New scope: consolidate to one Serializers instance owned by `Channels.@this`. Drop the per-Channels duplicate ctor allocation and the per-Stream lazy field. Stream channels reach the registry via `App.Channels.Serializers` using the inherited `Channel.@this.App` back-ref. `App.@this.Serializers` becomes a delegate `=> Channels.Serializers` — kept as an ergonomic shortcut, removed in stage 20.
- **Stage 20 added** — `serializers-app-shortcut-drop` (Tier 4). Removes the App-root delegate, sweeps 5+ external callers (Goals, Setup, DefaultFileProvider, Actor.Context.DynamicData) to `app.Channels.Serializers`. Pure call-site sweep.
- **Tree updated** — Channels/this.cs annotation reframed (no more "carry-over gone — stage 1" wording, since Channels is the canonical owner not a former site of carry-over). App/this.cs note added showing the `app.Serializers` shortcut goes away in stage 20.

### Why split stage 1 and stage 20

Two ownership realignments, separable:
- Stage 1: which instance is the registry? (Channels owns the canonical one)
- Stage 20: which path do consumers use? (only via Channels, no shortcut)

The split keeps each stage at "one ownership realignment per stage" per the plan's discipline. The intermediate state between them (App.@this.Serializers as a delegate) is a single line of code that lives one stage — not load-bearing cruft.

### Stage count

19 → 20.

---

## 2026-05-08 (latest) — coder-handoff cleanup pass

Final pass before stage briefs carve. Two structural fixes to the plan, plus a sweep for stale references.

### Stage renumbering (settings moves to its proper tier position)

Audit pass surfaced that `settings-collection-rework` was numbered 19 but is Tier 3 work (real shape change), so it sat after the Tier 4 hygiene sweeps in the run order. The plan's own ordering principle is "biggest wins × isolation first" with hygiene last — the position contradicted that.

Renumbered:

| Old | New | Slug |
|-----|-----|------|
| 13 | 14 | timespan-iso-8601-sweep |
| 14 | 15 | compound-name-rename |
| 15 | 16 | static-state-eviction-sweep |
| 16 | 17 | builder-tester-rename |
| 17 | 18 | mime-table-split |
| 18 | 19 | provider-to-code-rename |
| 19 | 13 | settings-collection-rework |

All cross-references in plan.md, principles.md, and post-cleanup-tree.md updated. Two-pass placeholder substitution; no stage-number collisions left.

### Audit gap closed

Found three gaps where the destination tree promised shape changes that no stage owned:

1. **Settings rework had no stage** → added stage 13 (`settings-collection-rework`) covering the collection-over-Data shape, IStore/Sqlite renames, and the new `Variables.RegisterNavigable(name, resolver)` mechanism.
2. **Stage 14 (`compound-name-rename`)'s one-liner was too narrow** — named only `MigrationEnvelope` and `EventContext` while actually absorbing 8+ renames and a new `Filters/this.cs` collection. One-liner expanded to enumerate.
3. **`TypeJsonConverter` cross-folder relocation had no clear owner** → folded into stage 15's expanded scope.

### Final stage index

| # | Tier | Slug | Status |
|---|------|------|--------|
| 1 | 1 | serializers-stage-6-finish | pending |
| 2 | 1 | channels-v1-helpers-drop | pending |
| 3 | 1 | keepalive-collection | pending |
| 4 | 1 | dispose-self-owns | pending |
| 5 | 1 | getstatic-shim-drop | pending |
| 6 | 1 | app-data-inheritance-drop | pending |
| 7 | 2 | callstack-promote-app-property | pending |
| 8 | 2 | read-file-off-channels | pending |
| 9 | 2 | catalog-dissolve-to-modules-schema | pending |
| 10 | 3 | app-run-redesign | pending |
| 11 | 3 | errors-app-backref-drop | pending |
| 12 | 3 | build-branch-to-build-this | pending |
| 13 | 3 | settings-collection-rework | pending |
| 14 | 4 | timespan-iso-8601-sweep | pending |
| 15 | 4 | compound-name-rename | pending |
| 16 | 4 | static-state-eviction-sweep | pending |
| 17 | 4 | builder-tester-rename | pending |
| 18 | 4 | mime-table-split | pending |
| 19 | 4 | provider-to-code-rename | pending |

Ready to carve stage 1.

---

## 2026-05-08 (later) — v5 review pass: Settings reworked, Provider→Code settled, Rule A sub-rule added

Walked the latest review thread end-to-end. Five small renames + two structural reworks now settled in the tree.

### What changed

**Tree edits (post-cleanup-tree.md):**
- **PropertyFilters → Filters** — folder + file renames; `Sensitive.cs`, `Transport.cs`, `View.cs`, `this.cs` under `Channels/Serializers/Filters/`. Both `Property` prefix and `Filter` suffix dropped (folder says it).
- **Converters cluster** — three different fixes. `TimeSpanIso8601Converter.cs` → flat `TimeSpanIso8601.cs` in `Serializers/`. `TypeJsonConverter.cs` relocates to `App/Data/Json.cs` (lives with the Type it serves). `UnregisteredMimeType.cs` kept (typed exceptions are conventionally compound — not a Rule A hit). No top-level `App/Converters/` folder (same logic as the rejected `App/Json/`: mechanism, not domain).
- **Settings reworked** — `SettingsVariable` carried an inheritance smell (Data subclass acting as both runtime navigator AND storage value). New shape: `Settings/this.cs` is a collection over Data (like `Goals/this.cs`); `IStore.cs` is the persistence interface; `Sqlite.cs` is the impl. `%Settings.X%` resolution now goes through a new `Variables.RegisterNavigable(name, resolver)` mechanism — generalizable hook for any future non-Data navigable mount.
- **Provider → Code, end-to-end** — settled the long-running open question. Driver is PLang-vocabulary coherence ("everything is goals, except where you need code"). `App/Providers/` → `App/Code/`; `IProvider` → `ICode` (fields stay — they map to developer-DLL-registration flow). All per-module `providers/` → `code/`. Per-module interfaces drop suffix (`IBuilder`, `ILlm`, `ICrypto`, `IHttp`, `IIdentity`, `IAssert`, `ITemplate`). Implementations drop both Default and Provider: variant-named where useful (`OpenAi.cs`, `Fluid.cs`, `Grep.cs`), `Default.cs` where the role is already in the parent path (assert/builder/http/identity modules).
- **DefaultGrepProvider / OpenAiProvider** — folded into the Provider→Code sweep.

**Principles edits (principles.md):**
- **Rule A sub-rule added** — "If the class name's role-pattern suffix names the folder it lives in, drop the suffix." With the typed-exception carve-out so the screen doesn't false-positive on `: System.Exception` types.
- **Rule D table fix** — corrected `plang p build` (fictional) → `plang build` as the today form, with `plang --builder` as the after form. Dropped the verb-commands-stay-verbs carve-out (no concrete anchor).

**Plan edits (plan.md):**
- **Stage 18 added** — `provider-to-code-rename`. End-to-end sweep across modules; the largest rename in the cleanup.
- **Stage 16 fixed** — CLI line corrected to `plang build → plang --builder, plang --test → plang --tester` (no fictional verb commands).

### Settled this session

- A. JsonSerializerOptions: disperse to consumers (no synthetic root home)
- B. Catalog placement: dissolves into Modules/Schema (already settled v3)
- C. PlangSerializer naming: Plang/ subfolder (already settled v3)
- D. RestoredFrame → Position (already settled v3)
- E. **Provider → Code**: full rename (settled today)
- F. PropertyFilters → Filters (settled today)
- G. Converters cluster (settled today)
- H. Settings shape: collection over Data, IStore + Sqlite + this.cs, RegisterNavigable mechanism (settled today)
- I. ChildAppCreated (still open; settle when stage 15 carves)

### Open after this session

Nothing genuinely open in this plan. Three items deferred (parked in `plan.md` "What's deferred"):
- `App.Statics` → goal-backed dynamic property
- `Data` parameter-lifecycle (the `data.ResetResolution()` smell)
- v3 audit methodology — its own follow-up cleanup branch when there's appetite

ChildAppCreated, Info.cs / View.cs explicitly dropped (Ingi 2026-05-08).

### Stage index (current)

| # | Tier | Slug | Status |
|---|------|------|--------|
| 1 | 1 | serializers-stage-6-finish | pending |
| 2 | 1 | channels-v1-helpers-drop | pending |
| 3 | 1 | keepalive-collection | pending |
| 4 | 1 | dispose-self-owns | pending |
| 5 | 1 | getstatic-shim-drop | pending |
| 6 | 1 | app-data-inheritance-drop | pending |
| 7 | 2 | callstack-promote-app-property | pending |
| 8 | 2 | read-file-off-channels | pending |
| 9 | 2 | catalog-dissolve-to-modules-schema | pending |
| 10 | 3 | app-run-redesign | pending |
| 11 | 3 | errors-app-backref-drop | pending |
| 12 | 3 | build-branch-to-build-this | pending |
| 13 | 4 | timespan-iso-8601-sweep | pending |
| 14 | 4 | compound-name-rename | pending |
| 15 | 4 | static-state-eviction-sweep | pending |
| 16 | 4 | builder-tester-rename | pending |
| 17 | 4 | mime-table-split | pending |
| 18 | 4 | provider-to-code-rename | pending (NEW) |
| 19 | 3 | settings-collection-rework | pending (NEW) |

Stage 1 (`serializers-stage-6-finish`) is still ready to carve. Stage 19 added retrospectively after the audit pass surfaced that the Settings tree-promised shape change had no stage owner.

---

## 2026-05-08 — v4 round of review settled; plan ready for context clear

Today's work absorbed the prior `runtime2-obp-restructure` branch (architect v1-v3) and walked Ingi's review comments end-to-end. Plan is now in a state where context can clear cleanly — durable artifacts cover everything important.

### What changed since v1

**Sharpened rules grew from 2 → 5.**
- Rule C — static fields are a missing `@this` (added 2026-05-07)
- Rule D — gerund-named app-graph properties + verb-named folders are wrong-shape (added 2026-05-08, from `runtime2-obp-restructure` v3 absorption)
- Rule E — decomposed parameters that should navigate (added 2026-05-08, same source)

**Stage list grew from 14 → 17.**
- Stage 9 reframed: `modules-to-catalog-lift` → `catalog-dissolve-to-modules-schema` (Catalog folder goes away; content moves under Modules/Schema with Spec/Action.cs + Spec/Example.cs)
- Stage 16 added: `builder-tester-rename` (Build/ → Builder/, Test/ → Tester/, Rule D)
- Stage 17 added: `mime-table-split` (Utils/MimeTypes splits two ways: Channels/Serializers/Formats + Types.Clr(mimeType) overload)

**Tree updated for the v3 absorption:**
- Catalog dissolves into `App/Modules/Schema/` (Spec subfolder for record family)
- `App/Build/` → `App/Builder/`, `App/Test/` → `App/Tester/`
- `App/Channels/Serializers/Serializer/Plang/` subfolder for plang-format serializers (this.cs + Data.cs, future Protobuf.cs)
- `App/Cache/MemoryStepCache.cs` → `App/Cache/Memory.cs`
- `App/Snapshot/ISnapshotted.cs` → `App/Snapshot/ISnapshot.cs`
- `App/Tester/{File,Run,Status}.cs` (Test prefix dropped)
- `App/CallStack/Call/Position.cs` (RENAMED ← RestoredFrame.cs)
- `App/Callback/Signature/` folded into `App/Callback/this.cs`
- `App/Catalog/ExampleHelpers.cs` deleted (record constructor covers it)
- ReservedKeywords → `App/Variables/Reserved.cs`, all const/readonly
- OpenAiProvider._requestCount → DELETE per Ingi (todo logged in Documentation/Runtime2/todos.md)

**Open questions resolved this session (post v3 review):**
- A. JsonSerializerOptions destination — disperse to consumers (Ingi: App/Json/ wrong, Json is a format not a domain)
- B. Catalog placement — dissolves into Modules.Schema (Ingi confirmed prior thread)
- C. Plang vs PlangData — Plang/ subfolder with this.cs + Data.cs
- D. RestoredFrame → Position — confirmed
- F. Events sub-foldering (Lifecycle layer collapse) — proposed in tree, low-priority

**Still genuinely open:**
- E. Provider → Code rename — architect lean: only worth it as a story-level concept change ("runtime no longer talks about providers"), not 88-file find-replace. Needs Ingi's read.
- G. ChildAppCreated event shape (stage 15) — architect lean: test-runner-owned registry. Settle when stage 15 carves.

### New artifacts created this session

- `Documentation/v0.2/audit/README.md` + `obp-rules.md` — committed audit folder. Five sharpened rules with grep screens, filter recipes, today's signal/noise counts, worked examples linked to stages. Used end-of-refactor.
- `/shared/bots/obp/{core,coder,architect,codeanalyzer,tester}.md` — mounted-drive suggestions (not committed). Per-bot lens system pointing at consolidated core.md. Architect to revisit at end-of-refactor and decide whether to formalize via proposal mechanism.

### Effectiveness of the rules (audit run 2026-05-08)

Ran each sharpened rule against `PLang/App/`. Findings:
- Rule A: 145 raw → ~30-40 after filter (noisy)
- Rule B: 94 raw → ~5-8 after filter (very noisy without filter, sharp with)
- Rule C: 89 raw → 17 fields-only after filter (sharp)
- Rule D: 1 gerund hit + 2 verb-folder hits (only with both screens — gerund-only missed verb-roots, hence the screen widening)
- Rule E: 4 raw, 2 real (sharp)

Rules together catch ~5 of 17 stages mechanically. The other 12 land via the 4 foundational CLAUDE.md smells + architectural reading. The rules are *finders*, not the whole audit.

### Stage index (current)

| # | Tier | Slug | Status |
|---|------|------|--------|
| 1 | 1 | serializers-stage-6-finish | pending |
| 2 | 1 | channels-v1-helpers-drop | pending |
| 3 | 1 | keepalive-collection | pending |
| 4 | 1 | dispose-self-owns | pending |
| 5 | 1 | getstatic-shim-drop | pending |
| 6 | 1 | app-data-inheritance-drop | pending |
| 7 | 2 | callstack-promote-app-property | pending |
| 8 | 2 | read-file-off-channels | pending |
| 9 | 2 | catalog-dissolve-to-modules-schema | pending |
| 10 | 3 | app-run-redesign | pending |
| 11 | 3 | errors-app-backref-drop | pending |
| 12 | 3 | build-branch-to-build-this | pending |
| 13 | 4 | timespan-iso-8601-sweep | pending |
| 14 | 4 | compound-name-rename | pending |
| 15 | 4 | static-state-eviction-sweep | pending |
| 16 | 4 | builder-tester-rename | pending |
| 17 | 4 | mime-table-split | pending |

### Re-onboarding path after context clear

The next architect session (post-clear) should read in order:
1. `plan.md` — spine and stage index
2. `plan/principles.md` — Rules A-E and the foundational 4
3. `plan/post-cleanup-tree.md` — destination tree with annotations
4. This `summary.md` — chronological log, this entry first
5. `Documentation/v0.2/audit/obp-rules.md` — audit recipes (consult only when running an audit)
6. `/shared/bots/obp/architect.md` — architect-specific OBP lens

Stage 1 (`serializers-stage-6-finish`) is ready to carve.

---

## 2026-05-07 — branch open; v1 cleanup plan ready for review

Forked off `runtime2-channels` tip. **All cleanup work lands on this branch as sequential stage commits**, then merges into `runtime2` once every stage is complete (Ingi's call 2026-05-07 — start simple, can split into per-stage branches later if it gets unwieldy).

### What this branch holds

- `architect/plan.md` — spine: narrative, thirteen-stage index in four tiers, branch strategy, open questions.
- `architect/plan/principles.md` — OBP discipline reference: eight smell tests, two architect-sharpened rules (compound-name red flag, `Get<Plural>()` missing-collection), per-stage anatomy, definition of done.
- `architect/summary.md` — this log; one entry per planning session.
- `architect/stage-N-<slug>.md` — created when each stage is carved (none yet).

### Why

OBP wasn't fully clear when much of `PLang/App/` was written. The architect and Ingi now have a sharper read on the pattern; the smells visible today were ambiguous or invisible at the time. Walking `App/this.cs` (681), `Channels/this.cs` (277), and `Modules/this.cs` (464) on the latest tip surfaced thirteen ownership-disagreement smells worth fixing. Plan sequences them as independent shippable stages.

### Two new OBP rules added during this session

- **Rule A** — class names with two capital letters (`{Noun}{Role}`) are red flags. Quick screen: `grep -E "class [A-Z][a-z]+[A-Z]"`.
- **Rule B** — `Get<Plural>()` returning a list is a missing collection type. Quick screen: `grep -E "Get[A-Z][a-z]+s\("`. `Get(uniqueKey)` returning one item is fine.

Both folded into `plan/principles.md` so every stage applies them.

### Stage index

| # | Tier | Slug | Status |
|---|------|------|--------|
| 1 | 1 | serializers-stage-6-finish | pending |
| 2 | 1 | channels-v1-helpers-drop | pending |
| 3 | 1 | keepalive-collection | pending |
| 4 | 1 | dispose-self-owns | pending |
| 5 | 1 | getstatic-shim-drop | pending |
| 6 | 1 | app-data-inheritance-drop | pending |
| 7 | 2 | callstack-promote-app-property | pending |
| 8 | 2 | read-file-off-channels | pending |
| 9 | 2 | modules-to-catalog-lift | pending |
| 10 | 3 | app-run-redesign | pending |
| 11 | 3 | errors-app-backref-drop | pending |
| 12 | 3 | build-branch-to-build-this | pending |
| 13 | 4 | timespan-iso-8601-sweep | pending |
| 14 | 4 | compound-name-rename | pending |

Stage 6 (`app-data-inheritance-drop`) was added after Ingi confirmed the codebase pivoted from inheritance to composition for `Data<T>` — App is the only remaining inheritance form (`App : Data.@this<@this>`), which makes it stale-vestige cleanup, not an open design question. Moved out of the deferred list and into Tier 1.

### Settled before stage 1

All open questions answered:

- Branch model — one branch (`runtime2-cleanup`), stages as commits, merges into `runtime2` after all stages complete (Ingi 2026-05-07).
- First stage — `serializers-stage-6-finish`, closes channels-Stage-6 drift (architect's call after Ingi delegated 2026-05-07).
- Cadence — default one stage per session, flexible (architect's call after Ingi delegated 2026-05-07).

### Next step

Carve `stage-1-serializers-stage-6-finish.md` on this branch and start work.

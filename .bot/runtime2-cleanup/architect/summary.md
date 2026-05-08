# architect — runtime2-cleanup

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

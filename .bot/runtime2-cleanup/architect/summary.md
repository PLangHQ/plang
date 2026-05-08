# architect — runtime2-cleanup

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

# architect — runtime2-cleanup

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

# v1 plan — stage 5: the builder offers kinds (ruling 10)

**Task (architect, decision 803):** stage 5 is the builder's to *build*, not only measure. Trace first how the decider's
Type question offers types today (pick list, prompt C, type page); send the shape before any change, including which
teaching and goldens move and whether a runtime touch is needed.

**Status: trace done, shape written, sent to architect. BLOCKED on architect/Ingi ruling before any change.**

## Finding (full detail in `stage5-shape.md`)

Stage 5 is **not** teaching-only. The decider's offered type set is a flat list of top-level type **names** produced in
C# (`app/type/list/Registry.cs:60-64` `type.Offers`), rendered as a flat choice (`decider2.template:52-62`), and the
answer is one string read into a flat `{name}` Type (`action/formal/reader.cs`). Kinds exist and navigate
(`app.type["number"].kind.list`) but are never shown and can't be expressed. So kinds become reachable only when the
offered set + question + answer reader carry them — **three C# seams, all locked-core → the coder's** (red-first test via
the architect).

## Steps (after approval)

1. Coder lands the runtime seam (offered set kind-aware; element follow-up; nested `{name, kind}` reader) — routed via
   the architect with a red-first test and trace. **Not mine.**
2. Mine, once the seam exists: `decider2.template` (render kinds / element question), `set.notes.md` (`ask:` wording),
   `properties.template` (prompt C Types section shows kinds), `docs/templates/type.template` (type page shows kinds),
   per-type teaching. Teaching-edit rule: re-run PickListTests/PickOptionTests/ConfirmTemplateTests/LineTwinTests +
   TypePageTests, re-pin the movers in the same commit.
3. Measure: the five baseline sentences, 5 builds each, fresh `cache:skip`, jq, against `baseline-kinds.md`; the two
   guards hold. Each must beat its baseline (decimal → `{number, kind: decimal}`, `list<path>` 5/5, json →
   `{item, kind: json}`, md read → `text`, iso → `{duration, kind: iso}`).

## Design fork for Ingi/architect

S1 (structured answer + "a list of what?" follow-up, faithful to ruling 10) vs S2 (flattened offer strings). I recommend
S1. The offered-set change may be a locked-surface addition → Ingi's yes.

## Blockers

- Architect/Ingi must rule S1 vs S2 and approve the runtime seam before the coder starts; my teaching is downstream.

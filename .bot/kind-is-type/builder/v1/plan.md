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

## Refined design (after reading the real pipeline — decision 807 go)

The earlier "`Values` becomes items with kinds" sketch was wrong about the mechanism. The type/kind system already
anticipates a **follow-up**, and offering every kind inline would bloat every `variable.set` Type question (number alone
has 15 kinds):

- `type.Offers(step)` (`type/this.cs:688`) yields a type's declared offer set, else `kind.Offers` = the step's own
  variables / a closed set's options — **not** the kinds. The kinds live in `type.kind.list(context)` (`type/this.cs`,
  `kind/this.cs:203`), already navigable. `list/kind/element` (exists) is "a list is offered what its element is".
- So: **stage-2 Type question** offers the registry's type names (Registry.Offers, essentially unchanged — pick the
  base type). A **follow-up** then offers, for the chosen type, `chosenType.kind.list` (number→int/long/double/decimal,
  text→md/xml, item→json, duration→iso/…) as faces; for a generic (`list`/`dict`) the follow-up offers the registry's
  types again (the element), recursively → `{list, kind: path}`, nesting `{list, kind: {list, kind: path}}`.
- The answer is the type's own **face** (`number<decimal>`, `list<path>`), matched by the existing `Shown`/`Choose`
  (faces offered as `text.@this`, so no fork in Choose). The `.pr` stores `{name, kind}`; the formal reader already
  parses `name<kind>` one level (`action/formal/reader.cs:282`), I add **nesting** only.
- **No new locked member** (kind.list/element exist). `type.@this.ToString()` (`type/this.cs:595`) writes `name<kind>`
  only for list/dict/choice today — extend to every kinded type if a face must render itself (F4.2); heads-up to the
  coder since it is in `type/this.cs`.

Mechanism cost: a **third decider exchange** in `Decide.goal` (render + llm.decider + build.pick), asked only for steps
whose stage-2 Type is kind-capable or a generic; the step's `Pick` builds the follow-up questions from the stage-2
answers (`pick/list`, build-time C#, mine). A new question `Kind` (the kind/element ask) or an extended `Option`.

### Open before I write locked-core code (sent to the architect)

1. Confirm the **stage-3 follow-up** mechanism (a third `llm.decider` round) — it's the faithful read of ruling 10's
   "a list asks 'a list of what?'", uses only existing members, but adds a decider exchange to every build that has a
   kind/generic Type. (Alternative: pre-expand common `list<X>`/kinded faces into stage 2 — fewer rounds, noisier, no
   true follow-up.)
2. Registry.Offers stays type-names (the base type); the kinds ride the follow-up — confirm that's the division wanted.

## Blockers

- Confirm the refined mechanism (stage-3 follow-up) before I write the pick/decider C#. No new locked member is needed;
  the ToString/face extension (F4.2) gets a heads-up to the coder. Tests already exist and are the acceptance set.

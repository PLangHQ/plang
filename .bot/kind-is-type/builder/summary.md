# Builder — kind-is-type — summary (v1)

**Version:** v1

## What this is

Two tasks on kind-is-type (architect decision 803): (1) rebuild the two `test/module/list` `.pr` for separator's new
kind slot; (2) trace **stage 5** — the builder offering kinds — and produce the shape before any change.

## What was done

**(1) separator .pr rebuild — DONE, pushed `88ffbbc03`, accepted.**
Fresh `cache:skip` rebuild of `test/module/list/.build/start.pr` and `listops.test.pr` on the tip (after separator landed
`812250ab2`). jq confirmed the only real-mapping change in each is the two Separator slot types
`{name: separator}` → `{name: text, kind: separator}`; the rest is the stage-0 `path`/`prPath`→`file` format (these `.pr`
predated it) plus decider-confidence jitter in advisory "Unsure" notes. `cd test/module/list && plang --test`: 23 pass,
0 fail; the lone `[Stale]` is `query/Failure_SortMixedIncomparable` (pre-existing, no `.pr`, a `type=json` sort test —
coder's stage 3/4, parked until the architect says stage 4 json is in).

**(2) stage 5 trace — DONE, shape sent to the architect, BLOCKED on ruling.** Full shape: `v1/stage5-shape.md`.
Headline: **stage 5 is not teaching-only.** The decider's offered type set is a flat list of top-level type *names*
produced in C# (`app/type/list/Registry.cs:60-64` `type.Offers`), rendered as a flat choice
(`decider2.template:52-62`), and the answer is one string read into a flat `{name}` Type
(`action/formal/reader.cs`). Kinds exist and navigate (`app.type["number"].kind.list`) but are never shown and can't be
expressed. Three C# seams — offered set, question/render, nested-answer reader — must carry kinds; all are **locked-core**
(the coder's, red-first via the architect). My teaching (prompt C Types section, type page, the `ask:` wording) rides on
top and re-pins goldens (pick_golden.json for certain; TypePageTests' markdown block if the page shows kinds; the
teaching-edit rule re-runs all four decider fixtures). Design fork for Ingi/architect: **S1** (structured answer +
"a list of what?" element follow-up, faithful to ruling 10 — recommended) vs **S2** (flattened offer strings).

## Code example

The gate (what blocks a kind from being offered), `PLang/app/type/list/Registry.cs:60-64`:

```csharp
internal override ValueTask<IReadOnlyList<item.@this>> Offers(step step)
    => new(Types.Where(t => !t.Internal).Select(t => t.Name)      // names only — no kinds, no generics
        .Distinct(...).Select(name => (item.@this)new text.@this(name)).ToList());
```

Stage 5 needs this (and `question.Values : IReadOnlyList<string>`, and `formal/reader.cs`) to carry `{name, kind}`.

## Stage 5 measurement (decision 807) — BLOCKED on a curation ruling

The architect ruled kinds **inline** in the Type question (from `type.kind.list`), follow-up only for a generic's
element, with a measure gate: ≤~1,500 chars added per question. **Measured (v1/stage5-measure.md): inline-all kinds
breaks the decider.** One Type question went from ~480 chars / ~30 choices (names) to **9,075+ chars / >255 choices**,
which the decider API rejects (`400 "Must have at most 255 choices"`). Cause: `type.kind.list` returns every kind —
host kinds (`item<*>`, list, dict), every text format (goal/llm/template/liquid/yml/ini/…), number's 15 widths, and
list/dict's element = every type again. Inline-all is infeasible.

Decision pending (architect/Ingi): (a) **curate** the offered kinds (needs a clean "offered kind" notion — likely a
locked kind marker → stop rule — or a brittle exclusion filter) vs (b) the kind as a **per-chosen-type follow-up**
(dodges the cap and bloat; 807 kept the follow-up only for a generic's element, but the cap reopens it). My `Offered`
probe (the inline expansion) is **uncommitted WIP**; no teaching/golden touched.

## Next / blockers

- **Blocked:** architect/Ingi must rule S1 vs S2 and approve the runtime seam (possible locked-surface addition) before
  the coder starts; my teaching + the 5-sentence measurement (`baseline-kinds.md`) are downstream.
- **Parked:** build `test/module/list/query/Failure_SortMixedIncomparable` when told stage 4 json is in; report what it does.
- **Environment note:** `.data` was absent on restart; re-saved the decider key via `test/SetDeciderKey.goal`
  (gitignored, nothing committed) — builds read it now.

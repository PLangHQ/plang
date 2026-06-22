# Build-time variable parsing — "compile the reference, step the value"

**Status:** design note, not started (Ingi: don't build now). Branch `variable-as-value`.
**Trigger:** "I dislike all this parsing at runtime — why can't we build this into the
`.pr` so we just step through the exact value with ease?"

This revisits an idea that a prior branch already dissected. Read that first so we
don't re-litigate its verdict, then see what the navigation redesign + variable-as-value
change about the calculus.

## Prior art — `origin/prevars-in-pr` (2026-06-08)

`.bot/prevars-in-pr/architect/plan.md` + `plan/examples.md`. It found the branch was
**two entangled ideas** and split them:

1. **Spans / parsed-form caching** — store the parsed `%var%` form (offsets/segments)
   in the `.pr` so the runtime stops scanning. **Verdict: no-go.**
2. **Build-time value transforms** — the builder compiles a natural-language transform
   into a navigation expression using the variable's *type surface*. **Verdict: alive,
   the real prize.**

The deciding rule (memorize it — it governs this whole area):

> **Store derived data in the `.pr` only when re-deriving it needs the LLM.**

- Spans fail it: a regex/tokenizer rederives them with no LLM → storing them is
  redundant, adds a *second* parser (builder + runtime), and a stale `.pr` carries a
  frozen parse the runtime can't detect.
- Transforms pass it: `%photo% resized to 200x200` → `%photo.Resize(200,200)%` is an
  **LLM inference**. The runtime can't rederive it without re-running the LLM (which
  PLang refuses to do at runtime), so the `.pr` is the only place that mapping can live.

Transforms "barely touch the `.pr`": the compiled expression rides in the existing
parameter `value` field (e.g. `value: "%products.orderbydesc(price).take(3)%"`,
`type: "list<product>"`), `formal` renders it for review. **No new `variables[]` block,
no path-keyed side-channel** — that side-channel is exactly the drift the spans had.

## The two facets, named

Ingi's "build it into the `.pr`" splits along the same seam:

- **(A) Structure of a plain reference** — `%goal.Steps[planStep.index]%` compiled to its
  segments in the `.pr` so runtime steps through them without parsing. This is the
  **spans facet** in new clothes.
- **(B) Natural-language transforms** — `%photo% resized to 200x200` → a navigation
  expression. This is prevars' **live** facet.

They are different in kind and must not be conflated again.

## What the navigation redesign changes about facet (A)

prevars rejected spans partly because **the builder did no `%var%` parsing** — adding it
meant a *second* parser plus a permanent sync obligation, while the runtime scanner stayed
for old/hand-edited `.pr`. That premise has shifted on this branch:

- There is now **one** parser — `app.variable.path.@this.Parse` — producing a typed
  `Segment` list (`Member` / `Index` / `Infra` / `Call`). It is the *only* tokenizer
  (the free-function `ParseNextSegment` is deleted).
- Under variable-as-value, a `%ref%` is a first-class `variable`. Its **wire form** is
  currently its raw string (`variable.Write → w.String(RawValue)`).

So facet (A) is no longer "harvest spans into a side-channel." It is: **make the path the
variable's single serialized form.** The variable serializes its `Segment`s instead of the
raw string; the builder parses once (the one place `Parse` runs); runtime does
`FromWire → walk`, never parsing.

This threads the prevars rule differently than spans did:

- **Not redundant / no drift** — segments *replace* the string, they don't duplicate it.
  There is one representation, not a string plus a cached parse that can disagree.
- **No second parser** — `Parse` at build, `FromWire` at runtime. The runtime tokenizer
  is gone either way (the walk consumes segments).
- **Honest residue** — the *strict* reading of the rule ("a regex rederives it → don't
  store") still says a plain path is rederivable, so storing it buys **determinism and
  clarity, not capability**. That is a real but smaller benefit than (B)'s. The question
  for facet (A) is a value judgement — is "runtime never parses" worth a wire-format
  change to `variable`? — not a principle violation, *provided* segments replace the
  string rather than sit beside it.

Dynamic values still resolve at runtime: `[planStep.index]` keeps its inner as a
sub-path, and *which* element it is (`→ 0`) is resolved per-step against the store. Only
the **structure** is frozen at build; the **values** stay live. That is the clean split.

## Facet (B) is the prize and is orthogonal to the walk

The navigation walk this branch built already *executes* chained expressions
(`%products.orderbydesc(price).take(3)%` is just `Member`/`Call` segments). So (B) needs
**no runtime work beyond what exists** — it is a *builder* change: feed the compile LLM the
variable's type surface, let it emit the navigation expression as the parameter `value`.
The walk runs it deterministically. This is where "parse the natural language once, at
build" actually pays — and it's the half that passes the LLM rule outright.

prevars left these open for (B) (still open):

- **What is navigable** — a marker for which type members are part of the PLang navigable
  surface (also bounds what the LLM is shown). Opt-in per member vs "all public
  value-returning members unless marked."
- **Purity** — only pure, value-returning members are navigable (`Resize` yes, `Delete`
  no — effects are explicit action steps; a value resolves repeatedly, so resolution must
  stay safe).
- **One capability, one home** — a thing is a navigable type member *or* a `module.action`,
  never both.
- **Routing tie-break** — confidence-driven preferred; if static, value-method wins.
- **Collection queries** (`where/select/orderby/take`) — the powerful, risky frontier;
  its own pass.

## Decisions for Ingi (none being built yet)

1. **Facet (A): does `variable`'s wire form become its `path` (segments), replacing the raw
   string?** This is the concrete realization of "step through the exact value." Only worth
   it if segments *replace* the string (one truth) — a side-channel reintroduces the spans
   drift prevars killed.
2. **`variable` IS-a `path` vs HAS-a `path`** — a name is a one-segment path; `%x.y[i]%` is
   multi-segment. Leaning IS-a (collapses two concepts).
3. **Facet (B): pursue build-time transforms?** The bigger prize, but it opens the navigable-
   surface / purity / query-language questions above. Its own branch.

## Pointers

- Prior art: `origin/prevars-in-pr` — `architect/plan.md`, `plan/examples.md`,
  `reminisce-2026-06-07.md`. Stale sketches there: `os/system/modules/MapVariables.goal`,
  `MapVariablesSystem.llm` (variable→pipeline instinct, unwired).
- This branch: the path value + walk it builds on — `navigation-redesign.md` (steps 1–5
  done; this note is the candidate step 6).

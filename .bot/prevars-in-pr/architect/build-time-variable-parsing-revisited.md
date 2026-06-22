# Build-time variable parsing — revisited from the navigation redesign

/ Status: design note appended later (2026-06-22), from work on `variable-as-value`.
/ Updates `plan.md`'s verdict in light of a parser + first-class path that now exist.
/ Nothing being built here.

## Why this is appended

`plan.md` split this branch into two halves and ruled:

- **Spans / parsed-form caching** → no-go.
- **Build-time value transforms** (NL → navigation expression via the type surface) → the
  live prize.

The deciding rule still stands and is not re-litigated:

> **Store derived data in the `.pr` only when re-deriving it needs the LLM.**

But one *premise* under the spans no-go has since changed on `variable-as-value`, and Ingi
re-raised the instinct directly — "I dislike all this parsing at runtime; build it into the
`.pr` so we just step through the exact value with ease." This note records what's different
now so the next person doesn't either re-bury the idea on the old premise or resurrect the
dead half.

## What changed: there is now one parser and a first-class path

`plan.md`'s spans no-go leaned on: *the builder does no `%var%` parsing, so adding it makes a
SECOND parser plus a permanent sync obligation, while the runtime scanner stays for old /
hand-edited `.pr`.* That premise is gone on `variable-as-value`:

- The runtime navigation layer was rebuilt. There is now **one** tokenizer —
  `app.variable.path.@this.Parse` — producing a typed `Segment` list
  (`Member` / `Index` / `Infra` / `Call`). The free-function `ParseNextSegment` and the
  per-type navigator registry are deleted; values navigate themselves (`item.Navigate`).
- A `%ref%` is a first-class `variable` value (name-only, resolved at the consumer). Its
  wire form today is still the raw string (`variable.Write → w.String(RawValue)`).

So "store the parsed form" is no longer "harvest spans into a side-channel keyed by path"
(the thing that had the drift problem). It can be: **make the path the variable's single
serialized form** — the variable writes its `Segment`s instead of the raw string. The
builder parses once (the only place `Parse` runs); runtime does `FromWire → walk` and never
parses.

## Re-running the rule against this shape

- **Not redundant, no drift** — segments *replace* the string; there is one representation,
  not a string plus a cached parse that can disagree. The exact failure mode that killed
  spans (a frozen parse beside a live string) does not exist when there is no live string.
- **No second parser** — `Parse` at build, `FromWire` at runtime. The runtime tokenizer is
  already gone (the walk consumes segments either way).
- **Honest residue** — the *strict* reading still holds: a plain path
  (`%goal.Steps[planStep.index]%`) is regex/parser-rederivable with no LLM, so freezing its
  segments buys **determinism and clarity, not capability**. That is a smaller win than the
  transform half's. So facet (A) is a *value judgement* (is "runtime never parses" worth a
  wire-format change to `variable`?), **not** a principle violation — *provided* segments
  replace the string rather than sit beside it. A side-channel reintroduces the spans drift
  and is still a no-go.

Dynamic values stay live: `[planStep.index]` keeps its inner as a sub-path; *which* element
it resolves to (`→ 0`) is computed per-step against the store. Only the **structure** freezes
at build; the **values** resolve at runtime. That is the clean split the original perf framing
never articulated.

## The transform half is unchanged — and now cheaper to land

`plan.md`'s live idea needs no new runtime: the rebuilt walk already executes chained
expressions (`%products.orderbydesc(price).take(3)%` is just `Member`/`Call` segments). So
build-time transforms remain a **builder** change (feed the type surface, emit the navigation
expression as the parameter `value`), and the open decisions from `plan.md` are untouched:
*what is navigable*, *purity boundary*, *one-capability-one-home*, *routing tie-break*, and
the *collection-query frontier*.

## Two facets, kept apart (don't re-entangle)

- **(A) Plain-reference structure** — segments as the variable's single wire form. The old
  spans instinct, now defensible *only* as a replacement-not-duplicate. Determinism, not
  capability.
- **(B) NL value transforms** — LLM-derived navigation expressions in `value`. The prize.
  Passes the rule outright.

## Decisions for Ingi (nothing being built)

1. Facet (A): does `variable`'s wire form become its `path` (segments), replacing the raw
   string? Only worth it if it *replaces* the string (one truth).
2. `variable` IS-a `path` vs HAS-a `path` — a name is a one-segment path; leaning IS-a.
3. Facet (B): pursue build-time transforms — its own branch, reopens the `plan.md` open
   decisions.

## Pointers

- This branch: `plan.md` (the original verdict + rule), `plan/examples.md` (transform `.pr`
  snippets), `reminisce-2026-06-07.md`.
- The navigation redesign that changed the premise: branch `variable-as-value`,
  `.bot/variable-as-value/coder/navigation-redesign.md` (the path value + walk; the registry
  collapse) and `.bot/variable-as-value/coder/build-time-variable-parsing.md` (the same
  reconciliation from the coder side).

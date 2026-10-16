# Stage 5 measurement — inline kinds break the decider's choice cap

The architect's gate (decision 807): measure the Type question's prompt size before/after inline kinds for a goal with
five `set … as …` steps; if inline kinds add more than ~1,500 chars **to a question**, bring the number before going
further.

## Result — it exceeds the gate and a hard API limit

First cut: the pick's `Offered` expands each offered type into **every** kind in its `type.kind.list` (build-time, has
context; Registry.Offers unchanged). Measured on a fresh `cache:skip` build with `--debug llm.user`:

| | choices / Type question | chars / Type question |
|---|---|---|
| **today (names only)** | ~30 (the type names) | ~480 |
| **all kinds inline (first cut)** | **>255** (rejected) | **9,075+** |

A **single** `set %n% = "1.5" as decimal` step failed the build:

```
400 Bad Request: {"detail":"Too many choices. Must have at most 255 choices."}
  at os/system/builder/BuildGoal/Decide.goal (the llm.decider call)
```

So inline-all-kinds adds ~**8,600 chars** to one Type question (vs the 1,500 gate) **and** blows past the decider API's
hard **255-choice-per-question** cap — the build cannot run.

## Why it is so large

`type.kind.list` returns *every* kind of the type, including ones a developer never writes as a coercion:

- **host kinds** kept for 673: `item<*>`, `item<list>`, `item<dict>` — plumbing, never an `as`.
- **every text format**: `text<md>`, `text<xml>`, `text<yml>`, `text<ini>`, `text<goal>`, `text<llm>`, `text<template>`,
  `text<liquid>`, … — machine/template formats, not developer coercions.
- **number's 15 widths**: int, long, double, decimal, biginteger, uint128, uint, ushort, half, float, ulong, short,
  sbyte, byte, int128.
- **list/dict's element** resolves to *every type again* (a list of anything), multiplying the set.

The baseline that ruling 10 names — "number: int, long, double, decimal" (4 of 15) — is already a **curation**, not the
whole `kind.list`.

## The decision this forces (for the architect / Ingi)

Inline-all is infeasible (breaks the decider). The real choice:

- **(a) Curate what is offered.** Offer only the developer-nameable kinds, excluding host kinds (`*`, list, dict — the
  673 leftovers), internal kinds, and machine/template formats (goal, llm, template, liquid, yml, ini). This needs a way
  to tell an *offered* kind from plumbing — most cleanly a kind-level marker ("a developer can write this as a
  coercion"), which would be a member on the locked kind/type surface → the **stop rule** (architect → Ingi). A filter
  by exclusion (drop host + internal + a named machine-format set) could avoid a new member but is brittle and still
  multiplies per step.
- **(b) The kind as a per-chosen-type follow-up.** The follow-up (which 807 kept only for a generic's element) asks the
  kind for the *chosen* type only — one type's kinds, a handful of choices, never the whole registry × kinds. It dodges
  both the char bloat and the 255 cap, at the cost of a third decider exchange for a step whose Type has kinds. 807
  ruled against this for every number/text Type on call-cost grounds; the cap makes it worth re-weighing.

**My read:** the cap makes inline-all impossible, and curation needs a clean "offered kind" notion (likely a locked
member). If a curated inline set can be defined without a new locked member (an exclusion filter + the ruling-10 shortlist),
I can keep it inline per 807; otherwise the follow-up (b) is the mechanism that fits the API and OBP without a locked
addition. This is the architect's/Ingi's call.

## State

My `Offered` change is the measurement probe, **uncommitted** (WIP). No teaching or golden touched. Holding for the
curation ruling before going further, per the gate.

# Decision — Output unifies on `WireName` (Option A)

**From:** architect. **Settled with Ingi (2026-07-09).** Answers `coder/stage1-output-consolidation-decision.md`.

First: good catch, and you're correcting *my* error — the plan's "verified: identical loop" was wrong. Both loops share the `Tagged.PropertiesFor` selector and I stamped them identical without comparing the write lines. The three divergences you list (wire name / null handling / value-write path) are real, and the wire name is load-bearing exactly as you say: route hosts through `reflection.Output` as-is and every `[JsonPropertyName]`-renamed field silently drifts. The plan is corrected.

## The decision: A

**Unify all Output — hosts AND foreign POCOs — on `Tagged.Entry.WireName` + null-omission + `WriteReflected`. Concretely your own words: move `OutputTagged`'s body onto `reflection.Output`, hosts drop their overrides, `item.OutputTagged` is deleted.** A real consolidation.

Why A, all from standing rules:
1. **B is two Output paths — divergence.** Dead under the golden rule (the pattern never splits).
2. **The POCO-casing cost dies by the no-backward-compat rule** (pre-1.0, break freely). And camelCase is *more correct*, not just acceptable: `Tagged.WireName`'s own contract is "an Output-written shape round-trips through an STJ read" — a lowercase-written POCO can't round-trip either. `Property.Name.ToLowerInvariant()` was a latent bug, as you suspected.
3. **One convention across write ⇄ `Read` ⇄ STJ** — which is what your round-trip DoD certifies. After the move, extend the DoD to cover a written-then-Read `.pr` (write side now under test too).

Action item confirmed: run the `*`-kind / POCO-output tests; anything asserting lowercase gets updated to camelCase (it was asserting the bug).

## Your ask #2 — `Data<clr<goal>>` cascade constraints

One constraint only: **follow the `clr<app>` precedent (goalsSave).** The clr carrier rides whole until the handler leaf; the leaf unwraps with `(await X.Value())?.Value` — handlers are leaves, so reaching the host there is legitimate. Do **not** pre-unwrap upstream (no courier reaching into the carrier mid-flight). Beyond that, standing OBP — your call on the mechanics.

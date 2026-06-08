# Stage 1: The `Comparison` enum

**Goal:** Introduce the single result type every comparison returns, with no sign-bearing numbers — so a "not equal" can never satisfy `< 0` and corrupt a sort.
**Scope:** The enum and the documented boundary mapping. Excludes any dispatch, per-type logic, or consumer wiring (those are later stages). Nothing reads it yet.
**Deliverables:**
- `Comparison` enum: `{ Less, Equal, Greater, NotEqual, Incomparable }`. Place it in `app.data` (next to where `Compare` will live), `public`.
- A short doc comment on each member stating the boundary contract below.
**Dependencies:** None.

## Design

The enum replaces every `int`-sign and `bool` comparison result in the system. The reason it is an enum and not an `int`: a magic `NotEqual = -2` satisfies `< 0`, so a sort built on sign math would silently order "not equal" values — the enum makes that impossible by construction.

Member meanings, and how the **boundary** (operator / sort / assert) maps each — the value never throws; the boundary turns these into operator results or PLang errors:

- `Less` / `Equal` / `Greater` — a real ordering.
- `NotEqual` — the pair was **reconciled and found unequal, but no ordering exists** (equality-only types like `dict`/`bool`, or two differing values whose type has no order). Equality operators use it; ordering operators error on it.
- `Incomparable` — the pair **could not be reconciled at all** (a non-coercible cross-type pair, `dict` vs `number`). *Every* operator errors on it.

The split between `NotEqual` and `Incomparable` is the load-bearing distinction: `NotEqual` means "reconciled, just unequal and unordered"; `Incomparable` means "these two can't be compared at all." That is what lets `dict == dict` work while `dict < dict` (unequal) errors, and lets `dict == number` error while `%x% == null` does not.

Boundary mapping (Stage 5 implements this; the value never throws, the boundary turns the result into an operator value or a PLang error):

| result | `==` | `!=` | `<` `>` `<=` `>=` | sort | membership (`contains`/`in`/`indexof`/`unique`) |
|---|---|---|---|---|---|
| `Less` / `Greater` | false | true | ordered | ordered | no match |
| `Equal` | true | false | by op (`<`→false, `<=`→true) | ordered | match |
| `NotEqual` | false | true | **error** (no order) | **error** | no match |
| `Incomparable` | **error** | **error** | **error** | **error** | **no match** (never error) |

Two things to read off this table:
- `Incomparable` errors on every *comparison/ordering* operator (`==`/`!=`/`<`/`>`/sort) — you can't compare those at all. `NotEqual` only errors on the *ordering* operators (it answered equality fine, it just has no order).
- **Membership is the exception: it never errors.** `contains`/`in`/`indexof`/`unique` only match on `Equal`; a `NotEqual` *or* `Incomparable` element is simply "not this one" — a list of dicts asked whether it `contains` a number answers **false**, it does not throw, and `unique` over a mixed-type list keeps the elements distinct rather than erroring. Membership scans for a match; a type-mismatched element is a non-match, not a bug.

Stage 3 decides which result a given pair produces; Stage 5 implements this mapping (note the membership column differs from `==`).

This stage only defines the vocabulary. The rules that decide *which* member comes back (rank, coercion, the null carve-out, when `Incomparable` vs `NotEqual`) live in Stage 3, and the boundary mapping above is implemented in Stage 5. Keeping the enum standalone means Stages 3–5 compile against a fixed contract.

You own the member names if you find better ones, but keep them sign-free and keep `Incomparable` distinct from `NotEqual` — that distinction is the whole point (one is an ordering failure, the other is a successful "they differ").

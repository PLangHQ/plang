# Stage 1: The `Comparison` enum

**Goal:** The single, sign-free result type every comparison returns — so "not equal" can't satisfy `< 0` and corrupt a sort.
**Scope:** The enum + its documented boundary mapping. No dispatch, no per-type logic, no consumers. Nothing reads it yet.
**Deliverables:**
- `public enum Comparison { Less, Equal, Greater, NotEqual, Incomparable }` in `app.data`.
- A doc comment per member stating the boundary contract below.
**Dependencies:** None.

## Design

Replaces every `int`-sign and `bool` comparison result. It is an enum, not an `int`, by construction: a magic `NotEqual = -2` would satisfy `< 0` and a sign-based sort would silently order "not equal" values.

- `Less` / `Equal` / `Greater` — a real ordering.
- `NotEqual` — reconciled and **unequal, but no order** (equality-only types like `dict`/`bool`, or two differing values whose type has no order). Equality ops use it; ordering ops error.
- `Incomparable` — **could not be reconciled at all** (a non-coercible cross-type pair, `dict` vs `number`). *Every* op errors.

The `NotEqual` vs `Incomparable` split is load-bearing: it makes `dict == dict` work while `dict < dict` (unequal) errors, and `dict == number` error while `%x% == null` does not.

Boundary mapping (Stage 6 implements it; the value never throws — the boundary turns the result into an operator value or a PLang error):

| result | `==` | `!=` | `<` `>` `<=` `>=` | sort | membership (`contains`/`in`/`indexof`/`unique`) |
|---|---|---|---|---|---|
| `Less` / `Greater` | false | true | ordered | ordered | no match |
| `Equal` | true | false | by op | ordered | match |
| `NotEqual` | false | true | **error** | **error** | no match |
| `Incomparable` | **error** | **error** | **error** | **error** | **no match** (never error) |

Two reads: `Incomparable` errors on every comparison/ordering operator; `NotEqual` errors only on ordering. **Membership never errors** — it matches only on `Equal` and treats `NotEqual`/`Incomparable` as "not this one."

Stage 4 decides which member a pair produces; Stage 6 implements this mapping. Keep the members sign-free and `Incomparable` distinct from `NotEqual`.

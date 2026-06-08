# Stage 3: Per-type rank, coercion, and the sync ordering core

**Goal:** Give each type ownership of its own comparison — its rank, how it coerces another value into its kind, and how it orders two of its own — with the ordering math kept synchronous.
**Scope:** Per-type rank (`this.Type.Rank(other) → winner`), per-type coerce-loser-into-own-kind, per-type sync `Order`. Prove `text`, `number`, and the `text`↔`number` cross-pair end to end first, then replicate across `bool`, `null`, the date-family, `duration`, `binary`, `dict`, `list`. Excludes the `Data.Compare` entry point (Stage 4) and consumer wiring (Stage 5).
**Deliverables:**
- A rank on each type (a number/static the type owns), and `Rank(Data other) → driving type` on the type entity (the higher-ranked of `this.Type` and `other.Type`). **Lives on the type, never on `Data`.**
- Per type: coerce-another-into-my-kind (the driver makes one-of-itself from the non-native side) and a sync `Order(a, b) → Comparison` that compares left-vs-right in caller order.
- Full type coverage in the replicate order above, each proven against the matrix in `plan/test-coverage.md`.
**Dependencies:** Stage 1 (the `Comparison` enum), Stage 2 (the per-type views).

## Design

**Rank lives on the type — this is the OBP spine, settled in review.** `Data` does not compare ranks and does not reach into `other.Type`. It asks `this.Type.Rank(other)`, passing the whole other `Data`, and the type returns the **driving type** (the higher-ranked of the two). Internally the type knows its own rank and reads the other's rank — that's type-layer logic; the rule is that the *Data layer* never does it. Rank is **specificity**: `number > text`, the date-family `> text`, with `text` the floor (everything renders to text, so text is least specific). How rank is stored is yours; the call shape `this.Type.Rank(other) → driving type` is fixed.

**A type compares only its own kind, in caller order.** Cross-type never asks one type to compare a foreign value. The driving (higher-ranked) type's `Order(a, b)` coerces whichever of `a`/`b` isn't already its kind — `number` makes a `number` out of `text "10"` via its own `from` — then orders left-vs-right. `a` is always the left operand (`this`) and `b` the right (`other`), so a `Less` means `this < other` exactly as asked — no sign flip. This is what `NormalizeTypes` does today, made explicit and per-type. Because `Rank` returns the same driving type regardless of which operand called, `compare(a,b)` and `compare(b,a)` agree — antisymmetry holds. (If you ever see `text "10" < number 9` and `number 9 < text "10"` both true, the rank isn't being consulted symmetrically — that's the bug this stage exists to prevent.)

**The ordering math is sync.** `Order` runs on values that are already materialised — Stage 4's `Compare` awaits both through the door and hands the raw values in. So `Order(a, b)` takes the two materialised values (the non-native side is coerced inside) and returns `Comparison`, synchronously, doing no I/O. This is what lets sort hoist all awaits into key materialisation and keep its comparator sync (Stage 5) — never `GetAwaiter().GetResult()`.

**Which `Comparison` member a pair produces (`Incomparable` vs `NotEqual` is the subtle part — see Stage 1's boundary table):**
- **Non-coercible cross-type** (`dict` vs `number`) → `Incomparable`. The higher-ranked type drives; if it can't make one-of-itself from the other, it returns `Incomparable`. Because the same type drives both ways, `Incomparable` is symmetric, and every operator errors on it — that is how `dict == number` errors, which is the intended behaviour.
- **Reconciled but unequal, no order** (two different `dict`s, two different `bool`s) → `NotEqual`. Equality operators use it (`==`→false); ordering operators error on it. This is distinct from `Incomparable`: the pair *was* reconciled, it's just unequal and unorderable.
- **Reconciled and ordered** → `Less`/`Equal`/`Greater`. Orderable types (`text`, `number`, date-family, `duration`, `list` lexicographic) produce these.
- **null is never `Incomparable`** — anything vs `null` yields `Equal` or `NotEqual`, so `%x% == null` works for every type and never errors. `nulls last` in ordering.

So equality-only types (`bool`, `dict`, `binary`, `choice`, `null`) return `Equal`/`NotEqual` for same-type pairs and `Incomparable` for a non-coercible cross-type pair; their *ordering* asks land on `NotEqual` (unequal, no order) or `Equal`, which the boundary errors on for `<`/`>`. Orderable types add `Less`/`Greater`.

**Prove text + number + the cross-pair before replicating.** The `text`↔`number` pair exercises rank, coercion direction, and antisymmetry in one — get it green (both directions agree) before touching the other ten types, so a mistake in the shape is caught once, not eleven times. Then replicate: `bool`, `null`, date-family (`date`/`time`/`datetime`), `duration`, `binary`, `dict`, `list`. Equality-only types (`bool`, `dict`, `binary`, `choice`, `null`) return `Equal`/`NotEqual` and `Incomparable` for any ordering ask.

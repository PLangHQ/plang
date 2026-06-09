# Stage 4: Per-type `Compare` (rank + coerce + the enum)

**Goal:** Give each type ownership of its own comparison — its rank, how it coerces another value into its kind, and how it orders/equates two of its own — returning the `Comparison` enum.
**Scope:** Per-type rank, coerce-into-own-kind, and a single `Compare` returning the enum (unifying today's `AreEqual`/`Order`). Prove `text`, `number`, and the `text`↔`number` cross-pair end to end first, then replicate. The `Data.Compare` entry (Stage 5) and consumers (Stage 6) are separate.
**Deliverables:**
- A **static** rank per type (a small enum/order the type owns: `text < number`, date-family `> text`, `text` the floor).
- One unified comparison interface (e.g. `IComparableValue`) with `Compare` → `Comparison`; **ordering is opt-in** so an equality-only type (`dict`/`bool`) answers `Equal`/`NotEqual` and returns `Incomparable`/`NotEqual` for `<`/`>`.
- Per type: coerce-another-into-my-kind (the driving type makes one-of-itself from the other side) and the `Compare` that returns the enum, in **caller order**.
- Full type coverage in replicate order, each proven against `plan/test-coverage.md`.
**Dependencies:** Stage 1 (enum), Stage 2 (typed values + door). Part of the 2–6 green unit.

## Design

**Rank lives on the type (static), and decides cross-type direction.** Data never compares ranks — it asks `this.Type.Rank(other)` (whole other operand, never `other.Type`), which returns the **driving type** (the higher-ranked of the two). Rank is **specificity** (`number` > `text`; date-family > `text`; `text` the floor). Static for now (may change later). The call shape `this.Type.Rank(other) → driving type` is fixed.

**A type compares only its own kind, in caller order.** Cross-type never asks one type to compare a foreign value. The driving type's `Compare(a, b)` coerces whichever of `a`/`b` isn't already its kind (`number` makes a `number` from `text "10"` via its own `from`), then orders/equates left-vs-right. `a` is the left operand (`this`), `b` the right (`other`) — `Less` means `this < other`, **no sign flip**. Because the same driver is chosen regardless of operand order, `compare(a,b)` and `compare(b,a)` agree (antisymmetry).

**The ordering math is sync.** `Compare` runs on already-materialised typed values — Stage 5 awaits both operands through the door and hands them in. It does no I/O. (This is what lets `sort` hoist all awaits into key materialisation — Stage 6.)

**Which `Comparison` member a pair produces** (see Stage 1's table — `Incomparable` vs `NotEqual` is the subtle part):
- **Non-coercible cross-type** (`dict` vs `number`) → `Incomparable` — the driver can't make one-of-itself from the other. Symmetric (same driver both ways), every op errors → that's how `dict == number` errors.
- **Reconciled but unequal, no order** (two different `dict`s, two different `bool`s) → `NotEqual` — equality uses it, ordering errors.
- **Reconciled and ordered** → `Less`/`Equal`/`Greater` (orderable types: `text`, `number`, date-family, `duration`, `list` lexicographic).
- **null is never `Incomparable`** — anything vs `null` → `Equal`/`NotEqual`, so `%x% == null` works for every type. nulls last in ordering.

**Prove `text` + `number` + the cross-pair before replicating.** That trio exercises rank, coercion direction, and antisymmetry in one — get both directions agreeing before touching the other types, so a shape mistake is caught once. Then replicate across the **11** types that implement comparison today (`binary`, `bool`, `choice`, `date`, `datetime`, `dict`, `duration`, `list`, `null`, `text`, `time` — i.e. the current `IEquatableValue`/`IOrderableValue` implementers, now unified onto `Compare`). Equality-only types (`bool`/`dict`/`binary`/`choice`/`null`) implement equality and return `NotEqual`/`Incomparable` for ordering. **`item` itself must *not* implement the unified interface** — it deliberately doesn't today (`item/this.cs:23-25`), so `dict : item` doesn't inherit an order it can't honor; ordering stays opt-in per concrete type.

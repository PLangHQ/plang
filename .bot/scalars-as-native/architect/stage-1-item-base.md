# Stage 1 — `item.@this` apex + the universal contract

**Seam:** the new foundation. Stand up `item` as the apex *and* the un-narrowed type, fold the `object` name into it, and prove it on the one already-complete wrapper (`number`) + the two collections (`dict`/`list`).

> **You own the final shape.** Class name, member names, whether `item` is instantiated directly for the un-narrowed case or has a concrete un-narrowed form under it, default-vs-pure-opt-in for equality — all yours. Keep the disposition: `item` carries only the *universal* contract (truthiness + the lazy narrow); ordering and value-equality stay opt-in interfaces.

## What `item` is (read first — this changed)

`item` is **two things at once**: the apex of the value lattice (`%x% is item` always true) **and** the type tag an un-narrowed value carries before examination. `read file.json` → `Data{ type=item, kind=json, value=<raw blob> }`, type not yet judged; the narrow (`{` vs `[` → `dict`/`list`) reads `Data`'s raw value on first touch and re-stamps the type.

**The un-narrowed serialized form rides on `Data`, not on `item.@this`** (coder pin). Stamping a type does not parse — `type=item` is a promise about the shape, the raw value rides on `Data` until touch-time (lazy materialization, `type-system.md`). So `item`-the-type is **storage-free** — "the apex stores nothing" holds — and it carries *behavior* (truthiness + the narrow, a method that reads `Data`'s raw value), **not** a blob field. That avoids OBP smell #6: no narrowed subtype (`number`/`dict`/`Ask`) inherits a dead un-narrowed field. Whether `item.@this` is abstract or a trivial concrete form is a free C# call (it stores nothing either way). So `item` is more than a marker (it has behavior) but not a storage hub.

**`object` folds into `item`.** The tree currently names the un-narrowed tree type `object` (`config.json → (object, json)`). This branch makes it `item` — `(item, kind=json)`. Both senses of `object` collapse in: the PLang `object` type → `item`, and CLR `Data<object>` → `Data<item>`. There is no enduring PLang `object`.

## Build

Create `app/type/item/this.cs` — the apex/un-narrowed type, base of every value type. It carries **only the universal contract**:

- **Truthiness** — virtual-with-default (un-narrowed / reference-ish items truthy-if-present; concrete types override). Keep a **sync** path reachable (`Data.ToBoolean()`) so a hot `if %bool%` doesn't eat an async hop; `IBooleanResolvable.AsBooleanAsync` is only for I/O truthiness (`path`).
- **The lazy narrow** — behavior that reads `Data`'s raw value at touch and re-stamps the type (`{` vs `[` → `dict`/`list`), **not** a field on the base. (Reuse / relocate whatever today drives `(object, json)` → `dict`/`list`.) The default for an already-typed subtype (`number`/`dict`/`Ask`/…) is a **no-op — "already narrowed, return self"** — so typed subtypes carry no un-narrowed state.

It does **NOT** carry:
- **Ordering.** `IOrderableValue` stays opt-in per type — `list` and the orderable scalars implement it; `dict`/`bool`/`null`/`Variable`/`Ask` do not. `item` must not implement it, or `dict : item` inherits an order it can't honor (`dict` is equality-only; its `Compare.Order` throws — the model `type-system.md` documents).
- **Value-equality.** Stays opt-in via `IEquatableValue` (what `Compare` dispatches on). `item` may carry a reference-identity default so reference-ish items (`Ask`) need no stub — your call.

The backing (`int`/`string`/`List<data>`) stays on each subtype; `item` carries behavior, not a value slot.

## Retarget + pin the inheritance

- **`number.@this`** — `: item.@this`; rewire truthiness to the `item` member; keep `IOrderableValue`/`IEquatableValue` (number is orderable). Net behavior unchanged.
- **`dict.@this` / `list.@this`** — `: item.@this` **here, not later** (it's free — they already implement the interfaces they honor: `list` all three, `dict` everything but `IOrderableValue`). Pinning it now keeps the constraint story clean and is the proof that a non-orderable value (`dict`) sits under `item` without gaining an order.
- **`Compare.cs`** — no change: the existing `lv is IOrderableValue`/`IEquatableValue` dispatch already routes `item` subtypes correctly. Confirm `dict` still throws `NotOrderableException` on sort (it must — that's the regression guard for this stage).

## Acceptance

- `number` arithmetic / compare / truthiness / `→ returns int` unchanged.
- `dict : item` **and** `dict` still throws on `Compare.Order` (no inherited order); `list : item` still sorts.
- A C# test treats a `number` and a `dict` as `item`, gets truthiness through the base, and confirms `dict` exposes no ordering.

## Green

Both suites pass unchanged. `number`, `dict`, `list` inherit `item`; the rest follow as each is built out. The `object → item` rename can start here (the un-narrowed type) or land with the Stage-5/7 serialization+constraint work — sequence it where it stays green, but it is in this branch.

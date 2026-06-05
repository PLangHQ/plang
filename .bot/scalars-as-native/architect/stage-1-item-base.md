# Stage 1 — `item.@this` base + the value contract

**Seam:** the new foundation. No behavior changes yet — this stands up the abstract base and proves it on the one already-complete wrapper (`number`).

> **You own the final shape.** Class name, member names, which interfaces `item` implements vs. declares abstract — all yours. Keep the disposition: one abstract base carrying the shared value contract, `number` inheriting it with no behavior change.

## Why this is first

Every later stage hangs a wrapper off `item.@this`. The base has to exist before `text`/`datetime`/`bool`/… can inherit it, and before `Data<T> where T : item` (Stage 7) can mean anything. Doing it first, and making only `number` inherit, keeps the blast radius to one type while the contract shape is settled.

## Build

Create `app/type/item/this.cs` — `public abstract partial class @this` (the apex; `item` has no folder today, only a type *name* in `type/this.cs`). It carries the value contract as **abstract members** and implements the dispatch interfaces once, forwarding to them:

- `IOrderableValue` / `IEquatableValue` (the interfaces `Compare.cs` already dispatches on) — implemented on `item`, backed by abstract `Order(other)` / `AreEqual(other)`.
- `IBooleanResolvable` (`AsBooleanAsync`) — implemented on `item`, backed by an abstract/virtual truthiness member.
- the bare-serialization hook (whatever `Normalize`/the json writer dispatch on for a leaf value — Stage when each wrapper renders bare; `item` declares the contract).

The point of a base over N parallel interfaces (`plan.md` "contract delivery"): a wrapper overrides members, it doesn't re-implement three interfaces each. `item` is **abstract** — it never holds a value itself (the apex stores nothing).

Decide here whether `item` carries a `Value`-shaped abstraction or leaves the backing type to each subtype. `number` holds `int`/`long`/`decimal`; `text` holds `string`; `list` holds `List<data>`. The backing differs per type, so `Value` likely stays on the subtype; `item` carries only the behavior contract. Your call.

## Retarget

- **`number.@this`** (`type/number/`) — make it `: item.@this`. It already has compare/truthiness/equality/convert; rewire those to *override* the `item` members (or to satisfy them) instead of standing alone. Net behavior: unchanged.
- **`Compare.cs`** — no change needed if `item` implements `IOrderableValue`/`IEquatableValue`; the existing `lv is IOrderableValue` dispatch already catches an `item`. Confirm `number` still routes through self-compare, not `ScalarComparer`.

## Acceptance

- `number` arithmetic, compare, truthiness, and `→ returns int`/`decimal` behave exactly as before.
- `item.@this` cannot be instantiated (abstract); `number.@this` is-a `item.@this`.
- A C# unit test constructs a `number`, treats it as `item`, and gets compare/equality/truthiness through the base.

## Green

Both suites pass unchanged. Only `number` moves this stage. `dict`/`list` must also become `: item` for the Stage-7 constraint to hold (they're values, and `Data<list>`/`Data<dict>` must satisfy `where T : item`) — they already implement `IOrderableValue`/`IEquatableValue`/`IBooleanResolvable`, so making them inherit `item` is mechanical. Do it here if it's free, or any stage before 7; just not optional.

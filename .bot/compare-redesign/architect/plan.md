# Comparison redesign — plan

This supersedes the rules in `.bot/compare-redesign/coder/compare-redesign-plan.md`. Where the two disagree, this file wins. Settled with Ingi on the whiteboard; verified against the real code on this branch (`compare-redesign`, cut from `scalars-as-native`).

## Why

Comparison today is spread across five pieces: a static `app.data.Compare` mediator, `ScalarComparer`, `Operator.NormalizeTypes`, the `IEquatableValue`/`IOrderableValue` interface pair, and a per-type `AreEqual`/`Order` on each value class. On top of that the value is stored inconsistently — sometimes a raw CLR scalar, sometimes a `text.@this`/`number.@this` wrapper — so the mediator has to handle both forms and coercion lives in a separate symmetric pass. Comparison should be owned by the value's type, over a value that lives in exactly one place, reached through one door, so `if a > b`, `sort`, `contains`, and `assert` can't drift apart — and so a file-backed value can be compared without sync-over-async.

## The model (settled)

The value lives once, in `Data`, as the raw CLR object. The type is behavior — a view built over the Data that reads the value through it. Comparison is owned by the type, compares only its own kind, and cross-type pairs are reconciled by a per-type rank before anyone compares. Reading a value is async (it may do I/O); the ordering math on values you already hold is sync. Sort and any I/O-bearing key are async by materialising first, then ordering.

1. **Value lives once, raw, in Data.** `data.Value()` returns the CLR object — `5`, `"hello"`, a `Dictionary`, a `List`, `byte[]`. Never a type wrapper. The wrapper-stored-in-`Data.Value` shape goes away.
2. **The type is a view over the Data.** The per-type class (`text`, `number`, …) is constructed with the Data instance and reads the value through it. It holds a pointer to the one home, not a copy — so there is no second place the value lives. `text(data)`, not `text(string)`.
3. **One value door: `await data.Value()`.** It materialises (does I/O if the Data is a not-yet-read file; sync parse if `_raw` is already in memory), caches, and returns the value. The sync `.Value` property is removed — the value comes out exactly one way.
4. **`_raw` and `ScalarValue` stay.** Three rungs: `path` (unread) → `_raw` (read bytes / json text, in memory) → `Value` (parsed). The `path → _raw` step is I/O (async); the `_raw → Value` step is a sync parse. `_raw` earns its place in the uses that stop before parsing — verbatim write-back (no lossy round-trip), byte length, binary, upload, http pre-dispatch. `ScalarValue` stays for those lazy non-parse reads.
5. **Comparison is owned by the type and compares only its own kind.** A type never compares a foreign kind. Cross-type is resolved first by coercion: the higher-ranked of the two operands' types wins, coerces the loser into its own kind (its own `from`), then compares two of its own.
6. **Cross-type direction is decided by a per-type rank — this is load-bearing.** Each type declares a rank (specificity): `number > text`, the date family `> text`, with `text` as the floor. The dispatcher reads both ranks and lets the higher-ranked type drive, regardless of operand order. This is what guarantees antisymmetry: `text"10".Compare(number 9)` and `number 9.Compare(text"10")` both let `number` drive, so both compare numerically and agree. Distributing coercion as "the left operand's type coerces the right" (the coder plan's rule 4) breaks this — the two directions disagree and `sort` corrupts. The rank just makes explicit what `NormalizeTypes` bakes in today ("always coerce text→number").
7. **The ordering math is sync; reading the value is async.** `await data.Compare(other)` is async at the Data surface: internally `await this.Value()`, `await other.Value()`, then the sync ordering core on the two materialised values + their types. Conditions call this directly — the evaluator is already async.
8. **Sort (and any I/O-bearing key) is async, two-phase, with no sync-over-async.** Phase 1 awaits each key (`await item.Value()`, or `await item.<key>` such as `path.size`) — all I/O lives here. Phase 2 orders the in-hand keys with the sync ordering core inside `List.Sort` — no await in the comparator. `GetAwaiter().GetResult()` appears nowhere, because the awaits are hoisted ahead of the sort and the comparator only ever sees values already pulled through the one door. A type's *default* compare must stay sync (e.g. `path` by name); anything that needs I/O to compare is expressed as `sort by <key>`, so the read lands in phase-1 key materialisation, never inside the comparator.
9. **`Comparison` enum** `{ Less, Equal, Greater, NotEqual, Incomparable }` — no sign-bearing numbers (a magic `-2` would satisfy `< 0` and corrupt sort). `==` is `Equal`; `<`/`>` read `Less`/`Greater`. `Incomparable` is **ordering-only**: `<`/`>` across types that can't be ordered → a PLang error at the boundary. For equality: a coercible cross-type pair (`%count% == "5"`) coerces then compares; a non-coercible cross-type **non-null** pair (`dict == number`) → error (the developer is comparing things that can't be compared); **null is always comparable for equality** — `%x% == null` / `%x% != null` never errors, for any type. `nulls last` in ordering. The value never throws; `NotEqual`/`Incomparable` and the equality errors surface at the operator/sort/assert boundary.
10. **Dispatch reuses the one routing that already exists.** The type entity already routes name → family behavior (`type.Convert` via `App.Type.Conversions`, the registry resolving `App.Type[Name].ClrType`). Comparison is one more method on the behavior that routing already produces — `data.Compare(other)` picks the winning type, that type's view is built over the winning Data, and it compares. No new compare registry, no `Type.Name == "..."` switch anywhere. The type entity carries `Context` (stamped at `Variables.Set` / `Action.RunAsync`), so the routing is reachable; compare always runs on a stamped type (it came off a real Data), never on an unstamped identity entity.

## Dispatch, concretely

```
// inside Data
public async Task<Comparison> Compare(Data other)
{
    var winner = Rank(this.Type) >= Rank(other.Type) ? this : other;
    var loser  = ReferenceEquals(winner, this) ? other : this;

    var wv = await winner.Value();   // one door, async, I/O hoisted here
    var lv = await loser.Value();

    return winner.Type.Order(wv, winner.Type, lv, loser.Type);  // sync ordering core
}
```

The winner is always one of the two operands' existing types — no new type is minted in a compare (minting only happens in `set %x% = "5" as number`, which is conversion, not comparison). `winner.Type.Order(...)` routes through the existing name→family path to the winner's compare, which coerces the loser into its own kind and compares two of its own. The signatures above are the shape, not the final names — you own those.

## What this replaces

- `app.data.Compare` (static mediator), `ScalarComparer`, `Operator.NormalizeTypes`, `IEquatableValue`, `IOrderableValue`, and every per-type `AreEqual`/`Order` in the current shape.
- The stored-wrapper shape: `Data.Value` stops holding `text.@this`/`number.@this`; those classes become views over a Data, and their backing-holding constructors (`text(string)`, the boxed numeric on `number`) move to reading through the Data.
- The sync `.Value` property — replaced by the single async `await data.Value()`.
- The golden-diff method on `Data` (`this.Compare.cs`) is renamed to `Diff` (rename the file too) so the value-`Compare` owns the name. 18 `.Compare(` call sites, ~14 in tests, no production callers.

## Consumers to move

- **condition operators** (`==`, `!=`, `<`, `>`, `<=`, `>=`, `contains`, `in`) — the registry is already async; wire each to `await data.Compare(other)` reading the `Comparison` enum.
- **assert** (`Equals`, `NotEquals`, `GreaterThan`, `LessThan`, `Contains`, `NotContains`) — go async, await `Compare`.
- **sort** — the two-phase shape above; drop the `Comparer<object>.Default` sync comparator on raw values.
- **list ops** (`contains`, `indexof`, `unique`) — await `Compare` per element on materialised values.

## Execution order

A rough order, not stage files. Re-read this plan before each step; push after every step.

0. Confirm green from clean (C# + PLang) on this branch.
1. Add the `Comparison` enum.
2. Make `await data.Value()` the single value door; remove the sync `.Value` property; keep `_raw`/`ScalarValue`/materialise. Move the per-type classes to views over the Data.
3. Per-type rank + each type's coerce-and-compare-own-kind, with the sync ordering core. Prove `text` and `number` (and the `text`↔`number` cross-pair) end to end before replicating across `bool` / `null` / date-family / `duration` / `binary` / `dict` / `list`.
4. `data.Compare(other)` (async) wiring the rank + the door + the sync core, through the existing name→family routing.
5. Move the consumers (conditions, assert, sort, list ops) to async `Compare`.
6. Delete the old mediator, `ScalarComparer`, `NormalizeTypes`, the two interfaces, the old per-type `AreEqual`/`Order`. Rename golden-diff `Compare` → `Diff`.
7. Green both suites from clean. Triage residual; flag any real semantic bug rather than papering over it.

## You own this

The code shapes, signatures, and names in this file are suggestions to make the design concrete — not a spec to copy. You own the final shape: method names, where the rank lives (a static on each family class, read through the type entity, is the natural fit), how the sync ordering core is factored, and how the view is constructed. The parts that are *not* yours to change without coming back: the value lives once in Data (raw, one async door); cross-type direction is decided by rank so antisymmetry holds; the ordering math is sync and all I/O is hoisted so nothing does `GetAwaiter().GetResult()`; no `Type.Name` switch and no second compare registry. If implementing forces one of those to bend, stop and flag it.

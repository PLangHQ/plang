# Stage 5: Move the consumers onto `Compare`

**Goal:** Route every comparison consumer through `data.Compare(other)` and the `Comparison` enum, and implement the boundary mapping (which result becomes which operator value or error). Land the two-phase async sort with no sync-over-async.
**Scope:** Condition operators, `assert`, `sort`, list ops (`contains`/`indexof`/`unique`). Excludes deleting the old machinery (Stage 6) — the old path may still exist underneath until then.
**Deliverables:**
- **condition operators** — `PLang/app/module/condition/Operator.cs`: the registry's `==`/`!=`/`<`/`>`/`<=`/`>=` (and the element side of `contains`/`in`) call `await left.Compare(right)` and map the `Comparison` per Stage 1's boundary table. The registry is already `Func<…, Task<bool>>`, so awaiting fits.
- **assert** — `PLang/app/module/assert/code/Default.cs`: `Equals`/`NotEquals`/`GreaterThan`/`LessThan`/`Contains`/`NotContains` await `Compare`.
- **sort** — `PLang/app/module/list/sort.cs`: the two-phase shape below; drop `Comparer<object>.Default`.
- **list ops** — `PLang/app/module/list/contains.cs`, `indexof.cs`, `unique.cs`: await `Compare` per element.
**Dependencies:** Stage 4 (`data.Compare`).

## Design

**The boundary mapping is the contract.** Each operator translates a `Comparison` into its result, and `NotEqual`/`Incomparable` into errors per Stage 1's table:

- `==` → `Equal`? true : (`Incomparable` ? error : false). `!=` → the negation, but `Incomparable` → error.
- `<` → `Less`? true : (`Less`/`Greater`/`Equal` ? false : error). I.e. `NotEqual` and `Incomparable` both error for ordering; `>`/`<=`/`>=` likewise.
- `sort` → `NotEqual` and `Incomparable` both surface as an error (you can't order what has no order / can't be reconciled).

This is where "the value never throws" is honoured — the result is a value; the *operator* decides error-or-result.

**Sort is two-phase — this is the no-`GetResult` shape:**

```csharp
// PHASE 1 — materialise keys. ASYNC. All I/O lives here.
var keyed = new List<(object? key, Data item)>();
foreach (var item in items)
    keyed.Add((await KeyOf(item), item));   // await the key — for `sort by size`, KeyOf does p.Size() (I/O)

// PHASE 2 — order. SYNC. Every key is already in memory.
keyed.Sort((x, y) => ToInt(CompareKeys(x.key, y.key)));   // sync comparator, no await inside
```

Phase 1 awaits every key (the default key is the item's value via `await item.Value()`; an explicit `sort by <expr>` awaits that expression — `path.Size()`/`ReadText()` do the file I/O). Phase 2 orders the in-hand keys synchronously. **No `await` inside the comparator, so no `GetAwaiter().GetResult()`.** `CompareKeys` is the sync ordering core fed already-materialised keys (Stage 3); a `NotEqual`/`Incomparable` between keys is the sort-can't-order error. Do not call `await … .Compare(…)` from inside `List.Sort`'s comparator — that is the exact footgun this shape exists to avoid.

**`unique`/`contains`/`indexof`** walk elements and ask `Compare` for equality. They are async (`Run` returns `Task`), so awaiting per element is fine; key on `Equal`. `unique` dedupes by `Equal`; `contains`/`indexof` stop at the first `Equal`.

**Default compare must stay sync-capable.** `sort %files%` with no key compares items by their default compare — that must not do I/O (e.g. `path` orders by name). Anything that needs a read to compare is written `sort by <key>` so the read lands in phase 1. If a type's default compare needed I/O, phase 2 would be forced to block — so keep default compares cheap.

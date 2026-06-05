# Architect handoff — the chunk/row list model

**To:** coder · **From:** architect · **Step 2 of 2** (do after `coder-handoff-compare.md`). Design source: `list-rope-model.md` (Ingi's).

**You own the final shape.** The contract and the surface table are the anchors — match the behavior, pick the implementation that reads best.

## One core idea

A list is a list of **rows**. Each `add` appends one row holding the Data you added. Every read (`count`, `foreach`, `print`) shows the **flattened** items by walking the rows. The list only physically flattens into one list when `sort`/`where`/`unique`/`map` build a **new** list.

```
set %a% = [10, 20, 30]        rows: ⟨[10,20,30]⟩                  %a.count% = 3
add 40 to %a%                 rows: ⟨[10,20,30]⟩⟨40⟩             %a.count% = 4
add %b% to %a%   (b=[50,60])  rows: ⟨[10,20,30]⟩⟨40⟩⟨[50,60]⟩   %a.count% = 6

foreach %a%   →  10, 20, 30, 40, 50, 60      (6 iterations)
print %a%     →  10, 20, 30, 40, 50, 60
```

`add` never reads or rewrites the existing rows — it's a one-row append. The three rows above stay three rows; `count` is 6 because it sums each row's weight, not because anything flattened.

## The contract

**Row weight** — how many leaves a row contributes:
- `add 40` (scalar) → weight 1
- `add %b%` (b=[50,60], a list) → weight 2
- `add %d%` (d is a dict) → weight 1 — a dict is one item

**count** = sum of row weights, kept as a running counter on add/remove. Never a walk.

**Reads are flattened.** `count`, `foreach`, `print`, `first`, `last`, `get/set/remove at N`, `indexof` all present the flattened view. `N` is a **flattened** index — map it to `(row, offset)` using the weights.

**Transparency.** Walking descends only into **list** rows. A scalar, dict, or table row is weight‑1 and yielded whole — so `[{...}, {...}]` still iterates as two dicts, not their fields.

**Collapse only on a new list.** `sort`/`where`/`unique`/`map`/`reverse` produce a new list — flatten the rows into one flat list **there**, at materialization. Not on read.

## What changes in `app/type/list/this.cs`

Today `list.@this` is a flat `List<Data> _items`. It becomes a list of rows + a weight counter. The **public surface stays flattened**, so the ~20 consumers of `.Items` / `EnumerateItems` keep working unchanged:

| member | today | row model |
|---|---|---|
| `Count` | `_items.Count` | running **sum of row weights** |
| `Items` / enumerator | `_items` | walk rows → yield the **flattened leaves** (every `.Items` consumer stays correct) |
| `At(N)` / `First` / `Last` | `_items[i]` | flattened `N` → `(row, offset)` |
| `Add(item)` | `_items.Add` | append **one row** (weight = item's flattened count) |
| `Insert` / `RemoveAt` / `SetAt(N)` | `_items[i]` ops | flattened `N` → `(row, offset)` → edit |
| `Remove(value)` | `FindIndex` | find the flattened leaf → remove |
| `SortByValue` / `SortByField` / `Reverse` | `_items.Sort/Reverse` | **collapse rows → flat**, then order (a new order is a new flat list) |
| `ToRaw` | per item | flatten rows → raw |

`FromRaw` (the build-at-edge) wraps its source as a **single row**. The in-place mutators' promote-and-write-back stays — the mutation is now a row edit.

## The one decision that defines this branch

**What does a row store?** `add %b%` (b=[50,60]) — the row holds [50,60]. Two ways:

- **Raw payload** — store `[50,60]` as a plain value. Cheap (one row Data per `add`, not one Data per leaf — this is the whole point, your reason for the model). But the leaves 50/60 have no per‑element `Data`/signature sitting in memory.
- **Native list** — the row holds a `list.@this` (a Data per leaf). Each leaf keeps its signature, but you're back to a Data per element — **no memory win.**

Recommended: **store raw, and wrap each leaf as a `Data` on iteration** (re-deriving its type from the value), keeping a real stored `Data` only for the few leaves that actually carry a signature. That's what makes the chunk model a memory win instead of just a reshuffle. **Confirm with Ingi** — it's the crux.

## Other open items

- **Flat-index → `(row, offset)`** for `get/set/remove at N` — cheap with the running weights; spell it out so no read API leaks the row structure.
- **Wire shape** — a leaf rides bare when its type is recoverable from the JSON token, envelope only when not (signature, decimal-vs-double, datetime-as-string, bytes). Ties into the F5 / "type-driven nested Data" todo — same decision, don't solve twice.
- **`matrix` type** for 2‑D positional data — deferred; flagged only so "lists are flat sequences" has a home for grids.

## Done when

`set`/`add` build rows (O(1) append, no read of existing); `count` = weight sum; `foreach`/`print`/`first`/`last`/`at N` all show the flattened items; `sort`/`where`/`unique`/`map` collapse to a flat list; both suites green. The chunk structure is never observable — every public read reports the flattened view.

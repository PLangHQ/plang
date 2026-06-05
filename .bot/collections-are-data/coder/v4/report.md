# Coder — collections-are-data — v4

Resolves both architect handoffs: **`coder-handoff-compare.md`** (step 1) and
**`coder-handoff-list-chunks.md`** (step 2). Both suites green from clean rebuild:
**C# 4087/0**, **plang 273/273**. Build clean, 0 errors.

## Step 1 — compare lives on the value

The `Compare` helper was re-deriving type with `is`-switches (`is dict`/`is list`,
`Family()` switch, `Orderable` HashSet). Moved that behavior onto the values:

- **Two interfaces** in `app/data/` (mirroring `IBooleanResolvable`):
  `IEquatableValue { bool AreEqual(object?) }`, `IOrderableValue { int Order(object?) }`.
- **`dict`** implements `IEquatableValue` (structural, key-based) — equality-only, so
  `Compare.Order` throws for it.
- **`list`** implements `IEquatableValue` (structural positional) **and** `IOrderableValue`
  (**lexicographic** — item-by-item, first differing pair decides, a prefix sorts first).
- **`ScalarComparer`** (new) is the one legal `is`-site for raw scalars. Number routes
  **both** equality and order through the number tower (`Number.CompareTo`), so it answers
  the two questions the same way — kills the old split (order via tower, equality via boxed
  `decimal.Equals`).
- **`Compare`** thins to a mediator: null policy + `NormalizeTypes` coercion + dispatch
  (`is IEquatableValue`/`is IOrderableValue` → delegate, else scalar comparer). `Family()`
  and the `Orderable` set are **deleted**.
- **Recursion contract:** `dict`/`list` compare each child by calling back into the mediator
  (`Compare.AreEqualValues`/`Compare.Order`), so nested numbers still widen and nested text
  is still case-insensitive.

Consumers unchanged (all behind `Order`/`AreEqual`/`AreEqualValues`). Test change: the
`Compare_EqualityOnlyType_Throws` assertion that **list** is equality-only was dropped (lists
are now orderable) and replaced with `Compare_TwoLists_Lexicographic`.

## Step 2 — the chunk/row list model

`list.@this` is now a list of **rows** (`_items`, one per `add`); the **public surface is the
flattened view**. The row (chunk) structure is never observable.

- **`add`** appends one row, O(1) — never reads or merges the existing rows. A scalar/dict row
  weighs 1; a **list** row weighs its own flattened count.
- **`Count`** = sum of row weights (walked on demand — a row can alias a list mutated
  elsewhere, so a stored counter would stale; deviates from the handoff's "running counter"
  for that correctness reason).
- **`Items`/`At`/`First`/`Last`** present the flattened leaves; a private `Locate` maps a
  flattened index → `(row, offset)`. Walking descends into **list** rows only — a dict/scalar
  row is yielded whole, so `[{...},{...}]` still iterates as two dicts.
- **`RemoveAt`/`SetAt`/`Insert`** take a flattened index (mapped via `Locate`; an emptied
  nested chunk is dropped). **`Remove(value)`** finds the first flattened leaf.
- **`sort`/`reverse`** collapse the rows into one flat list at materialization (`ResetTo`),
  not on read. `ToRaw`/`ToString`/`AsBoolean` all read the flattened view.
- `FromRaw` left building one row per source element (observably identical to a single chunk
  under flattening, and it's what the parse seam already does).

**Observable change:** `add list to list` changes the **flattened read view** — `count`,
`foreach`, `print`, `at N` now see the added list's elements (so `count` jumps by the added
list's length), where before the list was one nested element (`count` +1). Nothing merges
*physically* on `add`: the added list stays one row; the rows only collapse into a flat list
when `sort`/`where`/`reverse`/etc. build a new list. Confirmed by the new `RowModelTests`. No
plang goal relied on the old nested-element behavior (273/273). `matrix` (2-D positional)
remains out of scope — until it exists, a nested list literal `[[1,2],[3,4]]` reads flat, per
"lists are flat sequences."

New tests: `RowModelTests` (added-list flattens into the read view, flattened `count`/`at N`,
nested-`RemoveAt`, dict weight-1, sort-collapses).

Hand back to **codeanalyzer**.

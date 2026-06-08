# Comparison + value-model redesign — plan

This is a **rewrite**. An earlier draft of this plan built on a "value is raw CLR, the type is a view over it" model; that was the wrong call and is abandoned. The model below — **the type holds the value** — is settled with Ingi and is the direction this branch (`scalars-as-native`) was already heading. The six raw-model stage files and the two raw-model test docs have been deleted; stages will be re-carved from this spine.

## Why

PLang is a typed language, so the runtime currency should be the **typed value** — a `text`, `number`, `binary`, `dict`, … that owns its own behaviour (compare, truthiness, length, arithmetic, wire shape). Today comparison is a static mediator (`app.data.Compare`) over `int` signs, coercion is a separate symmetric pass (`Operator.NormalizeTypes`), and the value slot is inconsistent (sometimes a typed wrapper, sometimes raw CLR). This redesign makes the value uniformly typed, moves comparison onto the value (async, antisymmetric, enum-valued), and makes I/O lazy — a file/http value reads only when used.

## The model — the type holds the value

1. **The value slot holds a PLang typed value** — an `app.type.item.@this` subtype (`text`, `number`, `bool`, `null`, the date-family, `duration`, `binary`, `dict`, `list`, `path`, `image`, …). `set %x% = 5` stores `number`, not raw `5`. The value owns its behaviour; this is the existing `item.@this` design, finished and made uniform.
2. **`data.Value()` returns the typed value** (async — see lazy). It does **not** return raw CLR. Raw CLR is the **leaf exception**: `value.ToRaw()` at typed-conversion returns (`→ returns string`/`int`/…), at CLR/`System.IO`/sqlite interop, and inside the per-type writer. Behaviour stays on the value; you reach for raw only at the edge.
3. **The value's life is one representation at a time, refining on demand:**
   ```
   path('file.json') ──read (async I/O)──► binary/text ──narrow/parse (sync)──► dict/list/…
   ```
   Each step **replaces** the prior — no parallel copies, no `_raw` slot. `_raw` dissolves: "the unparsed form" *is* a `binary` (bytes) or `text` (decoded string) value. `Peek()` (renamed from `ScalarValue`) returns the current rung without forcing the next. **Verbatim passthrough is the never-parsed path** — read → write-out without navigating stays `binary`/`text` and emits the original bytes; once you navigate (`%x.field%`) the value refines to `dict` and re-serialises from there. Display is passthrough, not a parse. Provenance (the source path) lives in `Properties`, not the value rung.
4. **`data.Value()` is async and lazy** — `ValueTask<...>`, sync-complete when the value is already at the needed rung, async only when it must read (the one I/O step). `Task.FromResult` would allocate on every read; `ValueTask` allocates only on the real-read path. A lazily-read value holds its source (path/handle) until first `Value()`. The serialize-time walk (`Data.Load()`) drives `await Value()` across the graph before the sync writer wall (the writer can't `await`).
5. **`data.Type` tracks the value's *current* type** and refines with it (`path` → `binary` → `dict`); `kind` (json/png/…) rides the tag. One type home — the tag reflects what the value *is* now, not a separate "declared" slot.
6. **Comparison is owned by the value's type** and compares only its own kind. `await data.Compare(other)`: await both operands (now typed values), then the higher-**ranked** type drives — it coerces the other into its own kind and orders two of its own. The value already implements compare (today's `AreEqual`/`Order`, unified into one `Compare` returning the enum); there is no view to construct.
7. **Rank lives on the type, decides cross-type direction — load-bearing for antisymmetry.** Data never compares ranks: it asks `this.Type.Rank(other)` (whole other operand, never `other.Type`), which returns the **driving type** (specificity: `number` > `text`, date-family > `text`, `text` the floor). Ordering is in **caller order** (`Order(a, b)` → `Less` means `this < other`) — no winner-vs-loser flip. Because the same driver is chosen regardless of operand order, `compare(a,b)` and `compare(b,a)` agree.
8. **Reading is async; the ordering math is sync.** `Order` runs on already-materialised typed values, returns the enum synchronously, no I/O. `sort` is two-phase — phase 1 awaits/materialises keys (all I/O here, e.g. `sort by size` reads `path.Size()`), phase 2 orders the in-hand keys synchronously (`List.Sort`, no `await` in the comparator). No `GetAwaiter().GetResult()` anywhere; default compares stay sync (`path` orders by name), I/O-bearing comparisons are written `sort by <key>`.
9. **`Comparison` enum** `{ Less, Equal, Greater, NotEqual, Incomparable }` — no sign numbers. `NotEqual` = reconciled-but-unequal-and-unordered (equality ops use it, ordering ops error). `Incomparable` = couldn't-reconcile-at-all (every op errors — `dict == number`). `null` is always equality-comparable (`%x% == null` never errors). **Membership never errors**: `contains`/`in`/`indexof`/`unique` match only on `Equal` and treat `NotEqual`/`Incomparable` as "no match." nulls last in ordering. The value never throws; the boundary turns the result into an operator value or a PLang error.

## What this replaces / deletes

- `app.data.Compare` (static mediator), `ScalarComparer`, `Operator.NormalizeTypes` (+ `IsTextLike`/`IsNumberLike`) — comparison + coercion move onto the value (rank + per-type `Compare`).
- `IEquatableValue` / `IOrderableValue` + per-type `AreEqual`/`Order` — unified into the value's single `Compare` returning the `Comparison` enum (one interface, e.g. `IComparableValue`, where ordering is opt-in so `dict` answers equality but not order).
- The special `_raw` byte slot on `Data` — dissolves into the `binary`/`text` rung; `Materialize` becomes the `binary`/`text` → parsed conversion (the reader registry / `Narrow`).
- The public sync `.Value` property — replaced by the async `await data.Value()` door.
- `ScalarValue` → `Peek()`; the golden-diff `data.Compare` → `Diff`.

## What changed from the abandoned raw draft

The pivot is rule 1–2: **value is the typed value, not raw CLR + a view.** Consequences: there is **no "value-as-raw flip"** (the old Stage 2's biggest, riskiest piece) and **no view construction** in compare — the value already is the typed thing and already owns its behaviour. The redesign shrinks to: make the door async/lazy, dissolve `_raw` into the `binary`/`text` rung, unify `AreEqual`/`Order` into an async `Compare` with rank + enum, and move consumers onto it. Decisions that carried over unchanged: the `Comparison` enum and its boundary mapping, rank-owned-by-the-type + caller-order ordering, two-phase sort with no sync-over-async, membership-never-errors, the `Peek`/`Diff` renames.

## Open points to settle before re-carving stages

- **Is raw-CLR access genuinely bounded to leaves** (`ToRaw` at typed returns / interop / the writer), or pervasive through handlers? If bounded, the typed model wins cleanly — confirm by sampling a few handlers.
- **`number` storage** — a typed `number` over a boxed CLR numeric is one extra indirection vs raw boxed; confirm it's acceptable (it almost certainly is — Data boxes anyway).
- The exact unified compare interface shape (`IComparableValue` with opt-in ordering) and where `rank` is stored on each type (a static the type owns).

## Stages

To be re-carved from this spine once it's approved — the raw-model stage files are deleted. Expected shape (smaller than the raw plan): (1) `Comparison` enum, (2) async/lazy value door + `_raw`→`binary`/`text` rung + `Peek`, (3) per-type `Compare` (rank + coerce + enum, unifying `AreEqual`/`Order`), (4) `data.Compare` entry, (5) consumers + boundary mapping + two-phase sort, (6) delete the mediator/`NormalizeTypes`/interfaces + `Diff` rename. Test docs (`plan/test-strategy.md`, `plan/test-coverage.md`) rewritten alongside.

## You own this (coder)

Shapes and names here are suggestions; you own the final shape. Non-negotiable invariants: the value is the typed value (`data.Value()` returns it, raw only via `ToRaw()` at leaves); one representation at a time, refine-and-replace, no `_raw` slot and no double-storage of bytes+parsed; comparison owned by the value with rank-on-the-type for antisymmetry and caller-order ordering; the ordering math is sync with all I/O hoisted (no `GetAwaiter().GetResult()`); membership never errors. If implementing forces one of those to bend, stop and flag it.

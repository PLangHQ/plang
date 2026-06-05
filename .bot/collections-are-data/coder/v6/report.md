# Coder — collections-are-data — v6

Resolves codeanalyzer v3. Both suites green: **C# 4089/0** (+2 tests), **plang 273/273**.
Build clean.

## F1 (BLOCKING) — `add list to list` no longer aliases the source — FIXED

`add %b% to %a%` stored `%b%`'s list **by reference**, so the two variables were entangled:
`set item N of %a%` rewrote `%b%`, and a later `%b%` mutation changed `%a%`'s count.

Fix: a **list value is structure-copied** at the merge boundary. `list.@this.CopyStructure()`
returns a new list with its own rows (recursive on nested lists, so no mutable list structure is
shared at any depth); leaf element Data are shared by reference — safe, because `set %x% = …`
rebinds rather than mutates. Applied in `list.add` (`add.cs`) and `list.set` (`set.cs`) — the two
handlers that store a user-provided value into a list. It's O(k) in the *added* list and still
never reads the existing rows, so the "append, don't touch existing" contract holds. Scalar/dict
elements are still stored by reference (rebind-safe).

Tests (both directions, the case the suites missed):
- `ListTests.Add_List_DoesNotAliasSourceVariable` — end-to-end via the `add` action with `%b%`
  in a second variable: `set` into `%a%` leaves `%b%` untouched; `%b%.add` doesn't change `%a%`.
- `RowModelTests.AddList_StructureCopy_NoAliasBothDirections` — the `CopyStructure` mechanism.

## F2 (deferred) — 2 signing tests

Unchanged — disabled pending the `signature-as-schema-wrapper` redesign (analyzer accepted this as
documented, not the blocker). Don't merge to main ahead of that branch.

## F3 (perf) — O(n²) in structural ops — FIXED

`Count`/`Items`/`At` are O(rows) walks; calling them per-iteration made the loops O(n²). Hoisted
`Items` to a local in `AreEqual`/`Order` (compare by index off the materialized view), and in
`Remove` (scan once, single `RemoveAt`). `unique` now accumulates into a plain `List<Data>` so the
inner dup-scan doesn't re-materialize `Items` each outer iteration.

## F4 (nits) — stale comments — FIXED

- `Wire.cs` `LiftDataIfShaped` comment rewritten (described the deleted `name+value` sniff).
- `dict/this.cs`: `app.type.catalog.@this` → `app.type.list.@this`.
- `scheme=data` → `@schema:data` across the recognizer comments (matches the `WireSchema = "@schema"`
  constant).

Ready for **codeanalyzer** re-review.

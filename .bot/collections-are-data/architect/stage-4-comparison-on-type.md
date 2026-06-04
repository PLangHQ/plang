# Stage 4 — comparison onto the type — one compare path

**Leaf-trace row:** G (`Operator.Compare` / `NormalizeTypes` / `AreEqual`). The compare contract is settled (below).

**You own the final shape.** Anchors for the design — change what reads wrong, keep the dispositions.

## The state today

Two compare paths that drift:
- `module/condition/Operator.cs:101` (`Compare`), `:160` (`NormalizeTypes`), `:87` (`AreEqual`) — static helpers that normalize raw `.Value` and call CLR `IComparable`. `IsNumeric` (`:194`) already recognizes `number.@this`, so cross-kind numeric compares (`5` vs `5m`) widen instead of failing.
- `module/list/sort.cs:19` — `Comparer<object>.Default.Compare`, a *different* path that can't sort by a field or compare `Data`.

`number.@this` already owns a `CompareTo` for its own arithmetic (`type/number/this.Equality.cs:41`), but `Operator.cs` doesn't call it — it reaches the unwrapped CLR value.

## Do

- Relocate the typed compare onto the types: `number`/`datetime`/`primitive` own `Compare` (extend the `IBooleanResolvable` pattern to ordering).
- Define the adapter seam: an entry that takes two element `Data`, picks the type, and compares — this is where `data.sort` meets the type.
- Route **both** the condition operators (`>`,`<`,`==` via `Operator.cs`) **and** `data.sort` through it. One path.

Then `if age > 18`, `where age > 18`, and `sort by "age"` use the same comparison.

## The settled contract

- **Within a type — natural order.** number numerically (across kinds via numeric widening), datetime chronologically, duration by length, text lexically.
- **Nulls sort last.**
- **Ordering two genuinely different value types throws** a clear error ("cannot order X against Y") — no invented cross-type order. Preserve the operator coercions `NormalizeTypes` already does (numeric widening, string↔number) on the `if` path; the throw governs ordering distinct value types (sort/list).
- **Orderable:** `number`, `datetime`, `duration`, `text`. **Equality-only:** `dict`, `list`, `bool`, `table`, `null`. `sort` on an equality-only type throws; `group`/`unique`/`==` work on any type.

The adapter seam (the entry that takes two element `Data`, picks the type, compares) is yours to shape — dispatch to the element type's compare, throw the mixed-type error when the two differ, nulls last.

## Acceptance

- `sort %people% by "age"` orders numerically; `by "name"` lexically; `by "born"` chronologically.
- `if %a.age% > %b.age%` and `sort by "age"` agree (same compare path).
- nulls sort last; sorting a list with two different value types throws "cannot order X against Y".
- `sort` on a `list` of `dict` throws (equality-only); `unique`/`group` on the same list work.

## Green

Both suites pass. The condition-operator tests are the regression surface — relocating compare must not change `if` semantics for the existing types.

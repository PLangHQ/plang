# Stage 4 — comparison onto the type — one compare path

**Leaf-trace row:** G (`Operator.Compare` / `NormalizeTypes` / `AreEqual`). **Blocked on the compare contract (see plan "Open decision").**

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

## Blocked — settle the contract first

This stage cannot land until the compare contract is settled (Ingi, when he digs into the `list` module):
1. mixed-type ordering + null-element placement (a defined total order).
2. which types are orderable vs equality-only (`table` likely equality-only — `group`/`unique` need equality, not ordering).

#3 (the adapter seam) is plumbing you shape. #1 and #2 are product calls. Don't block other stages on this — Stages 1–3 are independent.

## Acceptance (once unblocked)

- `sort %people% by "age"` orders numerically; `by "name"` lexically; `by "born"` chronologically.
- `if %a.age% > %b.age%` and `sort by "age"` agree (same compare path).
- mixed-type / null elements behave per the settled contract.

## Green

Both suites pass. The condition-operator tests are the regression surface — relocating compare must not change `if` semantics for the existing types.

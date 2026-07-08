# Coder review — unified master plan (`plan: unify into one master`, 2f68c5d9c)

Traced the load-bearing claims against source. Root cause and the three decisions
hold up. Five items — four issues, one endorsement.

## 1. Stage 0 undersold as "pure signature sweep"

- **Implementor count is wrong.** Only **5** types override `Create`
  (`list/this.Generic.cs:35`, `permission/this.cs:165`, `clr/this.Generic.cs:25`,
  `type/this.cs:439` and `:516`). The rest inherit the `static virtual` default in
  `item/ICreate.cs`. "~40 implementors follow" is off by ~8× — good news (smaller
  sweep), but the demolition math is wrong.
- **The async ripple reaches the write path in Stage 1, not just the dispatch.**
  `SetValueOnObject` (`variable/list/this.cs:364`) is a **private *sync*** method,
  and its bracket-index arms are the Stage-1 targets (`:440,:456`). Routing them
  through the now-async `Create` forces `SetValueOnObject` async. Its only caller
  `Set` (`:111`) is already async, so mechanical — but this is Stage-1 behavior on
  the write path, not the "isolated, no-behavior-change" `data/this.cs:512`
  dispatch that Stage 0 advertises.

## 2. (Nail before Stage 2.) The write index-arms need a CLR-Type-*targeted* Create door — which is exactly what `OfStatic` provides and Stage 2 deletes

```
// today, variable/list/this.cs:440
value = iv.Clr(elementType);   // elementType is a runtime System.Type (action.@this)
```

The arm holds a **runtime `System.Type`**, not a generic `T` — so it cannot call
`T.Create`. It must resolve `type[elementType].Create(...)` (entity keyed by CLR
type). `type.@this.Create(raw)` at `:439` is the wrong door — it is *polymorphic*,
inferring the type from `raw` and discarding the target. Confirm the surviving
`OwnerOf`/`_ownership` index exposes a **targeted `entity.Create`** reached by CLR
type, not just the polymorphic lift. This is the seam that actually fixes the
builder blocker; if it collapses into the polymorphic `Create(raw)`, the clr(json)
→ `Actions.@this` write still has no target type.

## 3. `Create` contract drift — preserve `static virtual`

Plan writes `static ValueTask<TSelf?> Create(item value, data data)`; source is
`static virtual TSelf? Create(@this value, data data)` (default interface method).
Dropping `virtual` breaks the ~50 inheritors relying on the default. The contract
block should show the DIM shape is retained.

## 4. Stage 3 identity-move is runtime-hot, not "mechanical" like the 5 reparent registries

The plan groups `Renderers`/`KindHooks`/`Compares`/`Scheme`/`Choices` (pure
reparent, the designated release valve) alongside the identity move. But
`[name]→entity` / `[clr]→entity` is the lookup hit on **every `.pr` read** to pick
which `Create` to call — Decision A depends on it. Sequence and test it apart from
the reparent tail; it is the Stage-3 item that can regress runtime, the
reparenting is not.

## 5. Endorsing — the Data-leaf seam guard

The JsonElement door reusing `FromRaw` (review I3) plus the sign-identical
round-trip DoD (I7) is the right guard. `%var%`/template/signing must stay
byte-identical, and that round-trip test is the only thing proving it. Keep it as
the hard Stage-1 gate.

---

Everything else verified against source and agreed: root cause
(`set %goal.Steps[i].Actions%` → bracket arm blind-lowers clr(json) into
`Actions.@this`), the one-door collapse, `list<type>` / `list<module>`, and
keeping `item.Clr` as the distinct lower exit.

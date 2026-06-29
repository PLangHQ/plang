# Stage 6 (LAST) — retire `Build`/`Judge` + finish context-never-null

**Design authority:** `plan.md` "Phase 6 (LAST)" + Ingi's framing (2026-06-29).

## The framing — the `object?` is the root, the fork is downstream
The value-ctor reasons in **CLR terms**: `type.Build(object?)` and `type.Convert(object?)` take a raw
`object?`, then immediately pattern-match **back** to `item.@this`/`text.@this`/`source` — the
`.Clr`-and-re-lift smell. By the time `Build` is called, the value is ALREADY a plang value (the ctor's
`Create`/parse ran first); its only two callers pass `_item` (an `item.@this`):
```
data/this.cs:205   _item = type.Build(_item);
data/this.cs:249   _item = declared.Build(_item);
```
So "retire `Build`/`Judge`" is really: **make construction a plang-typed message on the value/type, not
a CLR-typed dispatch in the ctor.** Change the signature first; the branches then have nowhere to live
except on the owning type.

## The work
1. **`type.Build(object?)` → `Build(item.@this)`.** Then:
   - `if (value is null)` → `if (value is @null.@this)`; `is item {IsLeaf:false} native` → `!value.IsLeaf`.
   - The two parity branches I added dissolve **onto the types** (where they belong):
     - a `variable` resolves its own name in its `Convert`/construction (`@this.Resolve(name)`),
     - a `text` returns a `%ref%`-bearing text unchanged in its `Convert` ("a template defers").
   - The coercion becomes a message — `value.ConvertTo(this)` / the family hook — not `Convert(object?)`.
2. **Delete `Judge`** (`type/this.cs:538`) — `Build` now has parity, so the no-context path is redundant.
3. **Delete the ctor's context-less arms** — `data/this.cs:211` (`else _item = type.Judge(_item)`) and
   `:252` (`Declare`'s `else Judge`). Context is structurally present after step 4.
4. **Finish context-never-null** (UNBLOCKED — WireLocal is gone):
   - `Wire._context` (`Wire.cs:73`, still `?`) → non-null; remove the `_context!` guard (`Wire.cs:219`).
   - `source._context` (`source.cs:30`, still nullable) → non-null; delete `source.Read` **branch 2**
     (`source.cs:126`, the context-less `_value is string → Convert` fallback).
5. **(Scope call) the value-ctor retirement** — every `new Data(name, value[, type])` no-type site →
   holder ctor / `Data.From`. This is the BIGGER scope (needs the no-type call-site count, uncounted).
   Step 1–4 (the `Build`/`Judge` + context-never-null finish) can land WITHOUT step 5.

## Entry
- ✅ Output-unification complete; WireLocal/Normalize/Wire.Write/PrWrite gone; `Build` has path/`%ref%`
  parity (added in Stage 4). The `else Judge` arms are already dead — this is removal, not redesign.
- **Scope call (Ingi):** land step 1–4 only, or also step 5 (full value-ctor retirement) in this branch?

## Related (separate, offered) — `WriteReflected` IWireWritable collapse
Not part of Stage 6, but the obpscan borderline #1: give `item.@this` + `data.@this` a shared
`IWireWritable` marker so `WriteReflected`'s two self-writer branches collapse to one polymorphic check;
the C#-collection bridge then dies with the `steps`/`modifiers`/`parameters` → `item.@this` IList
cleanup, leaving the irreducible 2-branch (plang self-writer vs C# scalar). Self-contained; can land
independently.

## Shipped + deltas from plan
_(coder fills.)_

# Stage 6 — `item` apex — register the top of the lattice

**Leaf-trace row:** P (`type.Is` ancestry — already built). **Separable follow-on, off the F1 critical path. Parallelizable once `dict`/`list` exist.**

**You own the final shape.** Anchors for the design — change what reads wrong, keep the dispositions.

## Already in place

`type.Is(other)` walks CLR inheritance + facets transitively (`type/this.cs:305`, `Reaches` `:317`). The lattice query works. This stage is small — it does **not** rebuild that machinery.

## Do

- Register `item` as the apex type (≈ C# `object`): `number`, `dict`, `list`, `path`, `image` all is-a `item`. Wire it so `data.Type.Is("item")` is true for any value, and `Is` stays true through the lazy narrow (`item`+`kind=json` → `dict`/`list`).
- If the query surface needs it, add a name-string overload `Is(string)` next to the existing `Is(@this? other)` (`type/this.cs:305`), so `if %x% is dict` / `if %x% is number` resolve from a PLang type name without minting a comparison `type`.
- Keep `Is` distinct from `Kind`: `Kind` is the format axis (image/text → compression/MIME, `type/this.cs:41`); `Is` is the value-type-lattice axis. Same entity, two questions.

## Why it's separable

F1 and the native types stand alone — they don't need the `Is(name)` developer query. The lazy `item`→`dict` narrowing they *do* need is Stage 1/3 (the `Materialize` repoint), not this. This stage is only the developer-facing IS-A surface (`if %x% is list`), so it can land any time after `dict`/`list` exist.

## Acceptance

- `if %x% is item` true for any value; `if %x% is dict` true after an object narrows, false for a list.
- `if %x% is number` true for `set %x% = 1`.
- IS-A survives the lazy narrow: an unexamined json value is-a `item`; after first touch it is-a `dict` (or `list`) *and* still is-a `item`.

## Green

Both suites pass. Small surface — mostly new tests for the `is` query.

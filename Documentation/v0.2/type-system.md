# Type System Notes — retired

These notes are **retired**. The current type-system reference lives inside the
code tree, as `start.md` next to the source:

- [`PLang/app/type/start.md`](../../PLang/app/type/start.md) — the type **entity**
  (`type.@this` = `{Name, kind, Strict, Template}`) vs the **registry**
  (`type.list.@this`), the CLR↔plang mapping (`PlangName`), kinds (a file format
  is a kind of its type), `app.type`/`type.root` (the format in play),
  `property`, the `current<T,L>` generic, and birth vs make.
- [`PLang/app/type/item/start.md`](../../PLang/app/type/item/start.md) — the
  value lattice: the `item` apex, `item.Create` (the inferred lift), how
  `item.list` works, `item.dict`, the scalar value types with their ranks, and
  `number.kind.int` as the concrete "kind".
- [`PLang/app/type/item/variable/start.md`](../../PLang/app/type/item/variable/start.md)
  — the `variable` value: Text + Code (hops), the parser, reading and writing.

Cross-cutting runtime notes and value-semantics gotchas live in
[`good_to_know.md`](good_to_know.md). The historical content of this file is in
`git` history.

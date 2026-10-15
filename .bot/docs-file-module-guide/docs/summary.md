# docs — summary

## Version
v2

## What this is

A fresh, code-mirrored **developer doc tree** for PLang Runtime2, aimed at the
**C# developer** working on the runtime (not the PLang user writing `.goal`
files). The docs live **inside the code tree**: each folder's `start.md` is its
overview, sitting next to that folder's `this.cs`. One rule drives it —
**path = namespace = doc location** — the same convention plang uses (`start.md`
is a folder's doc because `Start.goal` is the entry point). Every page answers:
*what is this, where is the source, how does the plang I write reach this C#?*

- **v1** shipped the fundamentals Ingi named: the app root and the module system.
- **v2** extends the tree to the **type system** and the **value lattice** — the
  other half of the fundamentals ("how `item.list` works" was only sketched in
  v1).

## What was done

### v1 (prior)
- `PLang/app/start.md` — app root + "how to read this codebase" primer.
- `PLang/app/module/start.md` — the module system.
- `PLang/app/module/file/start.md` — the `file` module, worked example.

### v2 (this version)
- **`PLang/app/type/start.md`** — the type system. Keeps the two things called
  "type" distinct: the **entity** (`type.@this` = `{Name, kind, Strict,
  Template}`, itself an `item`) and the **registry** (`type.list.@this`,
  selection + lifecycle only). Covers the CLR↔plang map (`PlangName`:
  int→number, `List<T>`→`list<kind>`, enum→choice, unowned→clr), "a file format
  is a kind of its type", kind-is-behaviour, `app.type`/`type.root` (the format
  in play), `property` + the catalog, the `current<T,L>` generic, and birth vs
  make. Distributed CLR ownership via each family's `this.Owns.cs`.
- **`PLang/app/type/item/start.md`** — the value lattice. The apex `item.@this`
  (storage-free, truthiness + lazy narrow), `item.Create` (the inferred lift, 6
  rungs, with raw int/string/List walk-throughs), a full **"how `item.list`
  works"** section (store-raw-type-on-read, chunks, snapshots, the query
  surface, and the tie-back: a domain `X.list` IS a `list<X>`, so no plurals),
  `item.dict`, the scalar types (number/text/bool/null/date-family/duration/
  choice/path) each with backing + rank, `number.kind.int` as the concrete
  "kind", the full rank/coercion table, and Data-vs-item.

### How v2 was produced

The two pages were drafted by **Haiku (haiku-5.5) subagents**, each briefed with
(a) a shared style brief derived from the v1 pages, (b) a verified-facts file I
assembled from reading the spine source myself plus two Explore surveys of the
registry and value-lattice internals, and (c) the v1 pages as the voice
reference. I then **verified every precise claim against source** before
committing — in particular the full rank table (all 18 values matched the
`Rank =>` overrides exactly) and every cited filename (`this.Owns.cs`,
`this.Equality.cs`, `this.Ladder.cs`, `this.Generic.cs`, `choice/set/this.cs`).

## Code example — the pattern every page follows

*What → where → plang mapping.* E.g. the CLR↔plang spine on the type page:

```
int        -> number (kind int)     item/number/this.Owns.cs
List<T>    -> list<T>                ContainerFamily in type/list/this.cs
enum       -> choice (set name)      item/choice/set/this.cs
```

and a value's rank decides coercion: `"5" == 5` drives in `number` (rank 300),
coercing the `text` (rank 100) up through `number.Create`.

## One finding (minor, flagged for coder)

The XML doc on `type.list.@this`'s `this[System.Type clrType]` says it returns
`null` on a miss, but the code routes an unowned CLR type to the `clr` entity
(via `PlangName` → `("clr", null)`) and never returns null. Stale source
comment; the doc page describes the actual behaviour. Recorded in
`docs-report.json`.

## What's next (later versions)

- `start.md` for `goal`, `data`, `channel`, `event`, `actor` as the tree grows.
- Retire / redirect the stale `Documentation/architecture.md` and
  `type-system.md` once the in-tree tree covers their ground.

## Blockers

None. The fundamentals (app, module, type, value lattice) are complete and
verified for their scope.

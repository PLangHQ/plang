# Architect handoff — Collections are Data

**To:** coder · **From:** architect

A core data-path refactor: **make collection elements first-class `Data`, end to end, and remove the decompose-on-read / wrap-on-write round-trips.** Root-cause fix for the code-analyzer F1 — "a list element might be raw or `Data`."

Full design and the leaf-trace contract: [plan.md](plan.md). Per-stage handoffs: [stage-1](stage-1-dict.md) … [stage-6](stage-6-item-apex.md). Tests: [plan/test-strategy.md](plan/test-strategy.md), [plan/test-coverage.md](plan/test-coverage.md).

## One-line model

Everything is `Data`, **including the contents of collections**. A list is `Data<List<data>>`; an object/map is `Data<Dictionary<string,data>>`. The `Data` graph is built **once** at the edge (parse / read) and **flows** — never decomposed into raw CLR on the way in and re-wrapped on the way out. `dict` and `list` are native value types that own their navigation, serialization, and comparison; the action modules only expose them.

## The two wrong decisions this kills

1. **Decompose-in, recompose-out.** The `Data` ctor unwraps every json token into raw CLR (`UnwrapJsonArray → List<object?>`, `data/this.cs:1329`), and the read side rebuilds a `Data` per element on access (`Element` raw branch `navigator/List.cs:54`, `WrapItem` `data/this.cs:516`). A literal `[1,"two"]` ends up raw; `list.add` stores a whole `Data`. So one list holds `[raw, Data]` — F1.
2. **`List<data>` overloaded as "property bag."** `List<app.data.@this>` is how a C# domain record is represented for the wire (`NormalizeObject` `data/this.Normalize.cs:170`, writer `channel/serializer/json/writer.cs:152`). A plain array can't *also* be `List<Data>` — the writer couldn't tell `[]` from `{}`. So `dict` must own the object shape before arrays become `List<data>`.

## What you build on (don't rebuild)

The type/kind/value foundation is in place: `Kind` is a stored axis (`type/this.cs:41`), the lazy read-seam exists (`FromRaw` `data/this.cs:249`, `Materialize` `:281`, `ForceMaterialize` `:314`, reader registry, `type.Convert` `type/this.cs:257`), `type.Is` ancestry walks the lattice transitively (`type/this.cs:305`), and `add.cs:43` shallow-clones the `Data` rather than json-deep-cloning. The collections work points the lazy seam's json branch at native `dict`/`list`, collapses the navigators into those types, and rebinds `set`. See plan rows N/O/P for the exact surfaces already in place.

## Sequencing (each stage builds + both suites green)

1. **`dict`** the native object type — unblocks everything (the property-bag deletion must precede arrays-as-Data).
2. **`set` rebinds** — prerequisite for storing element `Data` by reference.
3. **arrays hold `Data`** — F1 dies here. The load-bearing stage.
4. **comparison onto the type** — blocked on the compare contract (Ingi's call).
5. **list/dict ops + `where`** — Phase B, reuses Stage 4.
6. **`item` apex** — separable follow-on, off the F1 path.

## The load-bearing test

`sign %x%` → `add %x% to %list%` → `verify %list[0]%` must **fail before Stage 3 and pass after**. That round-trip is the exact gap that hid F1: a signed `Data` has to survive at rest inside a collection.

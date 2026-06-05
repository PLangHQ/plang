# List semantics — the rope/chunked model (proposed)

> **SUPERSEDED (2026-06-05).** This flatten/rope model is **not** the direction. A PLang
> `list` nests like every other type (a list element can be a list), `add` adds exactly one
> element, and `list` is orderable lexicographically. See
> `architect/coder-handoff-compare.md` § "List — orderable lexicographically, and it nests"
> for the reasoning. Kept for the record; do not implement.

**Status:** proposed design, not implemented · **Owner:** Ingi · **Date:** 2026-06-05 ·
Captured from a design discussion on the `collections-are-data` branch.

## The decision

A PLang `list` is a **flat sequence**. There is no observable nested list — adding a
list merges its elements into the sequence; it never stores a list *as one element*.
Two-dimensional / nested data is a **separate type** (`matrix` for a positional grid,
`table` for a headered one — out of scope here, just noting where nesting lives).

Internally the list is a **rope**: a list of `Data` *chunks*, one per `add`. The chunk
structure is an implementation detail — a developer only ever sees the flattened view.

## Why

Three things drove it:

1. **`add` should never read or rewrite the existing collection.** Today `list.add`
   promotes the variable to native and appends into one flat `List<Data>` — a
   read-modify-write. The rope makes `add` an O(1) append of a chunk that never touches
   the existing leaves. Fits "collections hold `Data` end to end, don't decompose."
2. **`count`/`foreach`/print must agree.** A model where the structural item-count leaks
   out next to a different flattened display (count says 2, print shows 5) is the thing
   to avoid. Here the structural count is *internal* and never surfaces — `count`,
   `foreach`, and print all report the same flattened number.
3. **The wire shape.** Each chunk can serialize as its own natural JSON; the per-element
   `{name,type,value}` envelope on a plain `[1,2,3]` is the artifact we're removing.

## The model

```
set %a% = [10, 20, 30]      chunks: ⟨[10,20,30]⟩                  %a.count% = 3
add 40 to %a%               chunks: ⟨[10,20,30]⟩⟨40⟩             %a.count% = 4
add %b% to %a%  (b=[50,60]) chunks: ⟨[10,20,30]⟩⟨40⟩⟨[50,60]⟩   %a.count% = 6

internal chunks: 3        (weights 3 + 1 + 2)
%a.count%:       6        running leaf counter, never a walk
foreach %a%:     10,20,30,40,50,60      (6 iterations)
print %a%:       10,20,30,40,50,60
```

The internal "3" never surfaces; every public read says 6.

### Operation contract

```
add / remove / insert     O(1) chunk edit. NEVER reads existing leaves.
                          add appends the whole Data chunk (keeps its type/signature).

count                     running leaf counter, maintained on add/remove. Not a walk.

foreach / print / ==      GetEnumerator WALKS the chunk structure and yields the next
                          leaf in order. No flatten op, no intermediate list, O(1)/step.

sort / where / unique     output is a NEW list, so the chunks collapse into a flat list
/ map                     HERE — at materialization, not on read.
```

### Transparency rule

The enumerator descends only into **list**-chunks (they are transparent). A scalar,
dict, matrix, or table chunk is **weight-1** and yielded whole — so `[{...},{...}]`
still iterates as two dicts. `count` sums chunk weights.

## What changes observably

- **`add list to list` shifts from nest to merge.** Today `add %b%` (b=`[50,60]`) stores
  `[50,60]` as one element → `count` 4. Under this model it merges → `count` 6, flat.
  Scalar `add` is unchanged (`add 40` → `[…,40]`, +1 either way).
- A developer can no longer hold a list-of-lists; that data becomes a `matrix`/`table`.

## Open / to-design before implementing

- **Read-side flat addressing.** `get item N`, `set item N`, `remove at N` now address the
  *flattened* index → must map flat-index to (chunk, offset). Cheap with the running
  counters, but every read API (`get/first/last/indexof/foreach/count/print/==`) must
  present the flattened view or the abstraction leaks.
- **Internal representation.** Replace the flat `List<Data>` in `app/type/list/this.cs`
  with the chunk list + leaf counter; `FromRaw` becomes "wrap as a single chunk." The
  in-place mutators' promote-and-write-back stays, but the mutation is a chunk edit.
- **Wire shape (related, see todos).** The logical flat array is what serializes; a leaf
  rides bare when its type+kind is recoverable from the JSON token, and keeps an envelope
  only when it isn't (signature, decimal-vs-double, datetime-as-string, path, bytes).
  Ties into "Fully type-driven nested Data" and the F5 discriminator already tracked in
  `Documentation/Runtime2/todos.md`.
- **`matrix` type** (positional grid) — deferred; only flagged so "lists are flat" has a
  home for 2-D data.

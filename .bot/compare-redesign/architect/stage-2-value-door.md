# Stage 2: The typed value door + the `.`/`!` resolver

**Goal:** One async, lazy door to the value that returns the **typed value** (a PLang `item` subtype), never raw CLR; and the two access planes — `.` (data) and `!` (property) — with the type answering both.
**Scope:** The door, `Peek()`, the `_raw` → `binary`/`text` rung for bare values, the `.`/`!` navigation resolver, the no-`ToRaw` rule + gate stub. The reference types (Stage 3), per-type compare (Stage 4), and the full `!`-surface typing (Stage 7) are separate.
**Deliverables:**
- `public ValueTask<object?> Value()` on `Data` — the single public value accessor. Sync-complete (zero alloc) when present; async only when it must read. Remove the public sync `.Value` property; keep a private `_value` + `_present`.
- A **private** backing read for a type's own methods (sync, in-memory) — never a public accessor.
- `ScalarValue` → `Peek()` (the current rung, no forced materialise).
- `_raw` byte slot **removed** — a bare value's unparsed form is a `binary`/`text` value (the rung); `Materialize` becomes `binary`/`text` → parsed (reader registry / `Narrow`).
- The `.`/`!` navigation resolver: `.` resolves against the value's **data**, `!` against the type's **property surface** — and the **type answers both** (no central case-table).
- No generic `item.ToRaw()`; `text.Value` (public raw string) → private. Stand up the gate **stub** (warning) that becomes the Stage-7 error.
**Dependencies:** None to start. **Stages 2–6 are one green unit** — the value model + compare land together; the old mediator stays until Stage 6.

## Design

**The door.** `ValueTask`, not `Task`: the common case (value already in memory) must allocate nothing, and it's the system's hottest accessor. It returns a **typed value** (`text`/`number`/`file`/…), never raw CLR.

```csharp
private object? _value;     // a PLang item subtype (or null)
private bool _present;
public ValueTask<object?> Value()
{
    if (_present) return new ValueTask<object?>(_value);   // in memory: sync, zero alloc
    return Load();                                          // pending: async, allocates only here
}
```
`ValueTask` rule: **await once** (no store-and-await-twice, no `.Result` before completion). Back it with an analyzer/grep gate, not just prose — easy to violate in a loop at hundreds of sites.

**Sync vs async is the produce/read split.** The async boundary is *producing* the value (the door — may read I/O). Once you **hold** a typed value its content is in memory; a type's own methods read its backing **synchronously and privately**. There is no public sync raw accessor. Migration: `Data`-receiver `.Value` → `await Value()`; a value-type's own `.Value`/backing read stays sync but goes **private**. (Of the ~990 `.Value` hits, ~56 are `Lazy`/`KeyValuePair`/`Nullable`/`JsonElement` — leave; the rest split by receiver.)

**The two planes (Stage 2 builds the resolver; the type fills both).**
- **`.` = data** — navigate the value's content (a `dict`'s keys, a `list`'s elements, a record's fields). The resolver asks the value's type; the type answers.
- **`!` = the property plane** — the value's typed properties/metadata (`%list!count%`, `%text!length%`, `%x!type%`). This plane **is the Stage-7 surface**; every `!` accessor returns a PLang type. Leading `!` = the property plane against the implicit root (existing `%!app%`/`%!data%`).
- The **sigil picks the plane**, so a content key named `size` (`.size`) never shadows the property (`!size`); the resolver never guesses. **The type decides** what each plane means for it (a `dict` navigates entries on `.`; a `text` has only `!`) — no central enumeration.

**Kinds are not values.** `json`/`csv`/… are *kinds* — they pick a deserializer that turns bytes into an `item` (`dict`/`list`). Stage 2's materialise step is "bytes → item via the kind's deserializer"; there is no "json value."

**No generic `ToRaw` — raw is private.** Raw CLR leaves a type only through its own `Write(IWriter)` (feeds the writer its primitive), `As<T : item>` (type→type, returns a typed value), and gated per-type interop accessors (`path.Absolute`, Stage 7). `item.ToRaw()` gone; `text.Value` public-raw → private. Gate as a **warning** here; Stage 7 flips it to error.

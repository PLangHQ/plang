# Stage 2: The typed value door + the `.`/`!` resolver

**Goal:** One async, lazy door that returns the **typed value** (a PLang `item` subtype), never raw CLR; and the two access planes — **`.` = the content/data**, **`!` = the value's own properties + the envelope**.
**Scope:** The door, `Peek()`, the `_raw` → `binary`/`text` rung, the `.`/`!` resolver, the no-`ToRaw` rule + gate stub. The reference types (Stage 3), per-type compare (Stage 4), and the full `!`-surface typing (Stage 7) are separate.
**Deliverables:**
- `public ValueTask<object?> Value()` on `Data` — the single public value accessor. Sync-complete (zero alloc) when present; async only when it must **read** (I/O). Remove the public sync `.Value` property; keep a private `_value` + `_present`.
- **The async value source (the largest net-new piece — Part A).** Today `Materialize()` is **sync** (`this.cs:316`, parses an in-memory `_raw`); the only async content load is per-type `ILoadable.LoadAsync()` (`image`). The async **read** seam is net-new (`ILoadable` folds into it); the **parse** logic folds *into* the door — `Materialize()` as a separate sync method **disappears**.
- **Navigation goes async** — `GetChildValue`/`ForceMaterialize`/`GetChild` route through `await Value()`. The *only* sync value access that remains is reading an **already-materialised** value's private backing (a field read — `text` returning its string — **not** a parse); the sync framework-contract methods use that, and never parse or navigate.
- `ScalarValue` → `Peek()` (the current rung, no forced materialise).
- `_raw` byte slot **removed** — a bare value's unparsed form is a `binary`/`text` value (the rung).
- The `.`/`!` resolver: **`.` = the content** (for a reference, `.` forwards into its content); **`!` = the value's own properties + the envelope**, with the reserved core (`@schema`/`type`/`error`/`success`) protected. See "The two planes."
- No generic `item.ToRaw()`; `text.Value` (public raw string) → private. Gate **stub** (warning) → Stage-7 error.
- **Door normalisation** for the slots that hold raw CLR today (var-ref `string`s, JSON `List`/`Dictionary` containers).
**Dependencies:** None to start. **Stages 2–6 are one green unit** (green gate at the 2→6 boundary).

## Design

**Part A — the async value source: `Materialize` disappears (do this first; it's the bulk, net-new).** `await _source.Read()` reads as if it exists — it doesn't. There is **no async I/O read path on `Data`** today; `Materialize()` (`this.cs:316`) is sync and parses an *already-in-memory* `_raw`, and the only async content load is per-type `ILoadable.LoadAsync()` (`image` only). The door is **the one async path**, and it does both steps:
- **read (async, net-new)** — fetch a file/url's bytes into memory; `ILoadable.LoadAsync()` folds into this (a reference's source does the scheme I/O). Almost nothing exists; design it here.
- **parse (folds into the door)** — bytes → `dict`/`list`/scalar, via the reader registry / `Narrow`. This was the sync `Materialize()`; it moves *inside* the door's load path, so `Materialize()` as a separate method **goes away**.

**Navigation goes async too** — `GetChildValue`/`ForceMaterialize`/`GetChild` go through `await Value()`. Verified safe: their callers are async action handlers (`list/count`, `first`, `get`, `any`, `group`, `where`) and the variable resolver (async-capable); **no sync framework-contract method** (`ToString`/`Equals`/`GetHashCode`/operator) navigates or parses. The one sync-navigate-inside-compare site, `list/this.cs:250` (`Compare.Order(a.GetChild(field), …)`), becomes the **two-phase sort** (Stage 6): key-extraction in async phase 1, sync compare on materialised keys. So the rule is: **async to produce / read / navigate; sync only to read what's already in hand** (a materialised value's private backing — a field read, never a parse).

**The door.** `ValueTask`, not `Task`: the common case (value already in memory) must allocate nothing, and it's the hottest accessor. Returns the typed value (see the promise scope below).

```csharp
private object? _value;     // a PLang item subtype (or null)
private bool _present;
public ValueTask<object?> Value()
{
    if (_present) return new ValueTask<object?>(_value);   // in memory: sync, zero alloc
    return Load();                                          // pending: async read + parse, allocates only here
}
```
`ValueTask` rule: **await once** (no store-and-await-twice, no `.Result` before completion). Back it with an analyzer/grep gate, not just prose — easy to violate in a loop at hundreds of sites.

**The two planes — `.` = content, `!` = the value's properties + envelope.** The wire makes the split concrete:

```json
{
  "@schema": "data",
  "type": { "name": "file", "kind": "json" },
  "value": { "name": "", "size": 10 },                 // the content — what . navigates
  "properties": { "path": "/config.json", "size": 28 } // the value's own properties + envelope metadata — what ! reaches
}
```

- **`.` = the `value` slot (the content/data).** `%config.size%` → `10` (the content's field); `%config.name%` → `""`. For a **reference** (`file`/`url`), `.` **forwards into its content**, so `%config.database.host%` reaches the parsed data directly — no phantom `.content` wrapper.
- **`!` = the value's own properties + the envelope** (the Data wrapper). `%config!size%` → `28` (the file's byte size, in `properties`); `%config!type%` → `file`; `%config!cost%` → the bag. A value-type's own properties (a file's `size`/`path`) serialise **into the `properties` bag** and are reached by `!`.
- **The sigil picks the plane**, so the content's `size` (`.size` → 10) and the value's `size` (`!size` → 28) never collide — different slots, different sigils.
- **Reserved core** (a value-type may **not** declare a property with these names): `@schema`, `type`, `error`, `success`. `@schema` is also blocked as a data key (it's the wire marker; `@` isn't a legal C# identifier anyway). **`name` is removed** from the envelope — it was the binding label, not the value's; dropping it stops the wire carrying the variable name and frees `name` as an ordinary field (so `%config.name%` is the content's, with nothing to shadow it). `name` was already excluded from the signed hash, so this is consistent.
- **`!` resolution:** reserved core first (protected — unshadowable); else the value's own properties / the `properties` bag. So `%x!cost%` is **kept** (it's the bag), `%file!size%` is the file's, `%x!type%`/`%x!error%` are the protected envelope.

**Kinds are not values.** `json`/`csv`/… are *kinds* — they pick a deserializer that turns bytes into an `item` (`dict`/`list`). The door's parse step is "bytes → item via the kind's deserializer"; there is no "json value." A content key named `type` is fine — it lives in `value`, reached by `.type`; the envelope's `type` is `!type`.

**What the door does *not* yet promise — door normalisation.** "Value is always a typed `item`" is true for born-native literals (`set %x% = 5` → `number`), but `_value` legitimately holds raw CLR today: a `string` for every `%var%` reference / partial interpolation (`VarString => _value as string`, `this.cs:146`), and raw `List`/`Dictionary` off JSON ingestion (`EnumerateItems`, `this.cs:539-553`). So scope the promise honestly: `Value()` returns the typed value **when the slot is typed**, and **var-ref strings and raw containers normalise to their typed value at the door** — that normalisation is an explicit Stage-2 deliverable, not an assumption.

**No generic `ToRaw` — raw is private.** Raw CLR leaves a type only through its own `Write(IWriter)`, `As<T : item>` (type→type, returns a typed value), and gated per-type interop accessors (`path.Absolute`, Stage 7). `item.ToRaw()` gone; `text.Value` public-raw → private. Gate as a **warning** here; Stage 7 flips it to error.

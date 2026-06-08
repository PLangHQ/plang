# Stage 2: The typed value door + the `.`/`!` resolver

**Goal:** One async, lazy door to the value that returns the **typed value** (a PLang `item` subtype), never raw CLR; and the two access planes — `.` (data) and `!` (property) — with the type answering both.
**Scope:** The door, `Peek()`, the `_raw` → `binary`/`text` rung for bare values, the `.`/`!` navigation resolver, the no-`ToRaw` rule + gate stub. The reference types (Stage 3), per-type compare (Stage 4), and the full `!`-surface typing (Stage 7) are separate.
**Deliverables:**
- `public ValueTask<object?> Value()` on `Data` — the single public value accessor. Sync-complete (zero alloc) when present; async only when it must **read** (I/O). Remove the public sync `.Value` property; keep a private `_value` + `_present`.
- **The value source — the async *read* abstraction (the largest net-new piece; see Part A).** Today `Materialize()` is **sync** (`this.cs:316`, parses an *in-memory* `_raw`) and the only async content load is per-type `ILoadable.LoadAsync()` (`ILoadable.cs`, one type: `image`). This stage defines the async **read** seam `ILoadable` folds into, and keeps **parse sync**.
- A **private, sync** read of the materialised backing — for a type's own methods *and* for sync navigation (`GetChildValue`/`ForceMaterialize` parse-on-touch today, `this.Navigation.cs:238`). Never a public accessor; never async.
- `ScalarValue` → `Peek()` (the current rung, no forced materialise).
- `_raw` byte slot **removed** — a bare value's unparsed form is a `binary`/`text` value (the rung); `Materialize` becomes `binary`/`text` → parsed (reader registry / `Narrow`), **sync**.
- The `.`/`!` navigation resolver: `.` resolves the value's **data**, `!` the **property surface** (the type answers both) — and `!` must **coexist with today's Data-infrastructure `!`** (`GetInfrastructureValue`, see below).
- No generic `item.ToRaw()`; `text.Value` (public raw string) → private. Gate **stub** (warning) → Stage-7 error.
- **Door normalisation** for the slots that legitimately hold raw CLR today (var-ref `string`s, JSON `List`/`Dictionary` containers) — see "What the door does *not* yet promise."
**Dependencies:** None to start. **Stages 2–6 are one green unit** (green gate at the 2→6 boundary, not within) — the value model + compare land together; the old mediator stays until Stage 6.

## Design

**Part A — the value source: async *read* vs sync *parse* (do this first; it's the bulk, and it's net-new).** `await _source.Read()` reads as if it exists — it doesn't. There is **no async I/O read path on `Data`** today; `Materialize()` (`this.cs:316`) is sync and parses an *already-in-memory* `_raw`, and the only async content load is the per-type `ILoadable.LoadAsync()` (`image` only). So the door conflates **two** needs that this stage must split:
- **(a) read — async, net-new.** Fetch a file/url's bytes into memory. This is the abstraction `ILoadable.LoadAsync()` folds into (a reference's source does the scheme-specific I/O). Almost nothing exists; design it here.
- **(b) parse — sync, and must *stay* sync-reachable.** bytes/`_raw` → `dict`/`list`/scalar. `GetChildValue` (`this.Navigation.cs:238`) is a **sync** navigation method that parses-on-touch; if the door were the *only* materialise seam and it were async, sync navigation would lose its parse path. So parse remains a sync step reachable off the held value (the private sync backing read), independent of the async read.

So: `Value()` is async only because of (a); (b) stays sync. A value that's already read (its `_raw`/bytes in memory) parses synchronously; only a not-yet-read file/url awaits.

**The door.** `ValueTask`, not `Task`: the common case (value already in memory) must allocate nothing, and it's the system's hottest accessor. It returns a **typed value** (`text`/`number`/`file`/…) — see the promise scope below.

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

**`!` is a *change of owner*, not just "fill in the resolver" — pin the coexistence.** Today `!` resolves against **Data's own infrastructure**: `GetInfrastructureValue` (`this.Navigation.cs:356`) reads `Properties` first, then reflects over `Data`/its subclass (`Name`, `Error`, `Success`, `Type`, `Llm`, …). Live sites ride that meaning — `%result!Error%`, `%result!Llm%`, `%x!cost%` (a `Properties` key). And `%text!length%` does **not** resolve today (`length` lives on the `text` *value* wrapper, `text/this.cs:74`, which `GetInfrastructureValue` never reflects). So this stage *repoints* `!` to the value's type surface. The coexistence contract: **`!` resolves the value's type-property surface first, then falls back to Data-infrastructure** (`Properties`, then `Error`/`Success`/`Type`/`Llm`/`Name`). So `%text!length%` resolves on the value; `%result!Error%`/`%x!cost%` still resolve via the fallback; `%x!type%` works either way (it's both Data's `Type` and the value's type). The one collision — a value type whose property name shadows a Data-infra name — is value-wins, rare, and the codeanalyzer flags it. **Pin this before the resolver is touched**, or the migration has no contract. (If a cleaner split is wanted instead of value-first-fallback, decide it here — but it has to be decided here.)

**Kinds are not values.** `json`/`csv`/… are *kinds* — they pick a deserializer that turns bytes into an `item` (`dict`/`list`). Stage 2's materialise step is "bytes → item via the kind's deserializer"; there is no "json value."

**What the door does *not* yet promise — door normalisation.** "Value is always a typed `item`" is true for born-native literals (`set %x% = 5` → `number`), but `_value` legitimately holds **raw CLR** in live paths: a `string` for every `%var%` reference / partial interpolation (`VarString => _value as string`, `this.cs:146`), and raw `List<object?>`/`Dictionary<string,object?>` off JSON ingestion (`EnumerateItems`, `this.cs:539-553`). So the door **cannot** blanket-promise "always an `item` subtype" for free. Scope it honestly: `Value()` returns the typed value **when the slot is typed**, and **var-ref strings and raw containers normalise to their typed value at the door** (a string → `text`, a raw `Dictionary`/`List` → `dict`/`list`) — that normalisation is an explicit Stage-2 deliverable, not an assumption. Don't write the door as if every slot is already typed; it isn't yet.

**No generic `ToRaw` — raw is private.** Raw CLR leaves a type only through its own `Write(IWriter)` (feeds the writer its primitive), `As<T : item>` (type→type, returns a typed value), and gated per-type interop accessors (`path.Absolute`, Stage 7). `item.ToRaw()` gone; `text.Value` public-raw → private. Gate as a **warning** here; Stage 7 flips it to error.

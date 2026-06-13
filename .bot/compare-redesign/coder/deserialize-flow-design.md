# Deserialize flow — Data reads {name, type}, the type reads its own value

**Status:** design agreed with Ingi (2026-06-13), for architect review before implementation.
**Scope:** how a `.pr` / wire Data is deserialized. Replaces the current reactive
mix in `app.data.Wire.ReadBody` + `app.type.@this.Deserialize`.

This came out of trying to delete `Judge` and move kind/strict birth onto the
types. The kind-birth move is right, but the deserialize path underneath it is
wrong in a way that produced a string of reactive patches (JsonElement unwrap,
try/catch swallowing reader throws, an `item.source` fallback). Those patches are
symptoms; this is the root design.

---

## The model

A Data on the wire is **one flat layer**:

```json
{ "@schema": "data", "name": "count", "type": {"name":"number","kind":"long"}, "value": 42 }
```

- `@schema` is **optional**. Present or absent, the object **is** a Data (absent ⇒
  defaults to data). It is only a confirmation marker, never a branch.
- There is **no "nested Data inside a value slot"** concept. One layer. The
  current `LiftDataIfShaped` / `_readDepth` machinery solves a problem that does
  not exist in this model and should go.
- `name` = the binding. `type` = the identity descriptor `{name, kind?, strict?}`.
  `value` = the payload.

**`value` is not Data's business — it is the type's.** Data reads `name` and
`type`. Then it hands the **value reader, positioned at the value, to the type**,
and the type constructs itself from it:

```
read @schema   (optional, ignored — only confirms "this is a Data")
read name
read type      → typeRef        ("data detects type")
value          → DO NOT decode here; hand the reader to typeRef
construct      → new Data(name, typeRef.Read(reader))   // the type parses its own value
```

`number` does `reader.Long()`, `text` does `reader.String()`, `list` loops
`StartArray → element → EndArray` (each element recursing back through the Data
envelope), `dict` the same over an object. Data never looks inside `value`.

---

## Decision 1 — it's a reader, serializer-independent (mirror of `IWriter`)

The type's value-parse must **not** know JSON. The write side already has this:
types serialize through `app.channel.serializer.IWriter`
(`number.serializer.Default.Write(number, IWriter)` calls `writer.Long(...)` —
never touches `Utf8JsonWriter`). The read side is the symmetric **`IReader`**:

- The type's value-parse pulls from an abstract `IReader` (`reader.Long()`,
  `reader.String()`, `reader.StartArray()`, …).
- Each format implements `IReader`: JSON over `Utf8JsonReader`, protobuf later, etc.
- The code building the Data sees **a reader**, not JSON.

This is the symmetric counterpart of `IWriter`, which is a strong signal it's
right. It also resolves the "format-coupling" tension: types stay
format-agnostic (they know `IReader`, not `Utf8JsonReader`).

## Decision 2 — single pass, no DOM, no double-parse

`Wire : JsonConverter<Data>` already tokenizes the bytes **once** into a
forward-only `Utf8JsonReader`; the envelope walk is single-pass. The waste is
**only at the value slot**, where today we materialize an intermediate and then
re-parse it:

- `JsonSerializer.Deserialize<object?>(value)` → a `JsonElement` **DOM** (parse #2),
  then the type re-reads the DOM (parse #3).
- the `deferredRaw` path: `JsonDocument.ParseValue` (DOM) → `GetRawText()`
  (**re-stringify**) → type parses the string again. Triple.

Because `type` precedes `value` on the wire (a writer invariant we keep),
`typeRef` is known when we reach `value`. So hand the **positioned reader**
straight to the type — it consumes the value tokens off the *same* single pass.
Zero intermediate DOM, zero re-stringify.

- Scalars: one token → CLR value → type.
- Containers (`list`/`dict`): the **container type** streams its own elements
  (each element recurses through the Data envelope). The array/object handling
  currently in `ReadBody` (`LiftArrayElements`, the `StartObject`/`StartArray`
  branches) moves **out of Data and into the `list`/`dict` types** — a container
  parses its own raw value exactly like `number` parses `42`.

## Decision 3 — `value` stays a sibling of `type` (do NOT nest value inside type)

We considered `{@schema, name, type:{name, kind, value}}` (value inside type,
since "the type owns the value"). **Rejected.** "The type owns the value" is a
*behavioral* truth (the type parses the value); it does not require putting
`value` physically inside `type`. Keeping `value` a sibling because:

- **`type` stays a pure, reusable identity** `{name, kind?, strict?}`. It must
  serialize **standalone when there is no value at all**: a parameter slot's
  declared type, a type annotation riding ahead of its value, reading
  `%x!type%`. Nesting forces an empty/awkward `value` into every such case and
  makes the type entity's own converter know about values.
- **Write/read symmetry stays decoupled.** The writer emits `value` via the
  type's `IWriter` into the value slot; `type` is written by its own
  `{name,kind,strict}` converter independently. Siblings keep those two
  serializations from entangling.
- **Three distinct concerns stay addressable:** `name` (binding), `type`
  (identity), `value` (payload). Signing signs over the value slot; lazy capture
  grabs the value slot.
- **No-value case reads cleanly:** a bare type annotation is just
  `{@schema?, name, type}` with no `value` key = "typed absence". Uglier if
  `value` is buried in `type`.

So: `type` and `value` are siblings on the wire; the type owns the *parsing* of
value, not its *physical location*.

---

## What this removes

- `Wire.ReadBody`: the `value` eager-decode (`Deserialize<object?>` → JsonElement),
  the `IsDeferrableShape` branch, `deferredRaw`/`FromRaw` re-stringify,
  `LiftDataIfShaped`, `LiftArrayElements`, the `_readDepth` counter.
- `type.Deserialize`: the JsonElement unwrap, the try/catch around the reader,
  the reader-registry `Shared` fallback (those were all working around the DOM
  and the missing reader contract).
- `Judge`'s kind/strict reconciliation: kind is born by the type as it reads
  its own value from the `IReader`; strict/declared-identity rides the type
  descriptor that Data already read.

## Open questions for architect

1. **`IReader` shape.** Pull-model mirror of `IWriter`
   (`Long()/String()/Bool()/Bytes()/Null()/StartArray()/...`)? Confirm the exact
   surface and how `StartArray`/object iteration is expressed so `list`/`dict`
   can stream elements that themselves recurse through the Data envelope.
2. **Type-before-value invariant.** We rely on `type` preceding `value` so
   `typeRef` is known at the value token. Confirm we enforce this on the writer
   for every path (it's already how Data writes today). Hand-authored `.pr` with
   value-first would need buffering — do we reject it or tolerate it?
3. **`item` / `object` (polymorphic, no declared type).** When `type` is absent
   or polymorphic, who reads the value? Proposal: a default reader that builds
   the natural item from the token (number token → number, string → text, array
   → list, object → dict) — i.e. the `object` type owns "read whatever's there".
4. **Containers owning element parsing.** Confirm `list`/`dict` read their
   elements via `IReader` (each element = a Data envelope read), so the recursion
   lives on the container type, not in `Wire`.

## Current branch state

The reactive production patches (`Wire.cs`, `type.cs`, `reader.cs`) have been
**reverted** to the milestone — they were superseded by this design. The
test-caller migration that unblocked `dev.sh build` (the `Get<T>`/`Set` callers)
is committed and stands on its own. Implementation of this design awaits
architect review.

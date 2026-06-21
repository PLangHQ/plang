# Design (deferred): format-agnostic slot read — `ReadData<TReader>`

**Status:** designed, not built. Captured mid-`template-stamping-at-read` (Ingi spotted
the smell). Do this *after* the template work lands.

## The smell
`list`/`dict` typed readers call `app.type.item.serializer.json.ReadSlot(...)`. That
binds a container's slot read to the **json serializer** even though the operation is
format-agnostic. Two distinct couplings:

1. **Location** — `ReadSlot` lives in `app.type.item.serializer.json`, so the container
   readers reach into a json type for a neutral operation. Pure misplacement.
2. **Structured fallback** — `ReadSlot`'s `_ => ParseRaw(reader.RawValue())` →
   `JsonDocument.Parse → json.Parse`. This one genuinely knows json: for a nested
   array/object or an `@schema:data` element it captures raw bytes and DOM-parses
   instead of streaming. It is the deferred shortcut from the IReader approach-B pick.

Everything else in `ReadSlot` is already neutral: `reader.Peek()/Bool()/Number()/
String()` is the `IReader` surface; `StringSlot` (the template stamp) is `IReader` +
`ReadContext`.

## Target — a slot read that knows only `IReader`
A format-neutral value read in `app.type.reader` (NOT under any format's serializer):

```
ReadValue<TReader>(ref TReader reader, ReadContext ctx)   // returns a slot (raw | item | Data)
  scalar  → raw (Null/Bool/Number/String) + the StringSlot template stamp   // already agnostic
  array   → recurse list.Read(ref reader, …)                                 // generic, agnostic
  object  → @schema:data?  ReadData(ref reader, …)  :  dict.Read(ref reader, …)
```

- `list`/`dict` call `ReadValue` (neutral), never `json`.
- Nested containers recurse the **container readers** themselves (already generic over
  `IReader`) — no DOM, no json.
- The only piece that must be built: **`ReadData<TReader>(ref TReader, ReadContext)`** —
  the generic envelope read (`{@schema, name, type, value, properties}`) off any
  `IReader`. Today that envelope read lives in `Wire` over STJ; this is the streaming,
  format-free version (the "own the read" / approach-A flavor we set aside).

## The one hard part — `@schema:data` detection on a forward-only reader
You cannot peek the first property of an object without consuming it. So distinguishing
"a dict" from "a Data envelope" means: `BeginObject`; `NextName(out first)`; if
`first == "@schema"` read its string value → `"data"`/`"signature"` ⇒ it's a
Data/layer, continue the envelope read having already consumed `@schema`; else it's a
plain dict whose first entry is `(first, value)` — continue `dict.Read` with that entry
already in hand. (The writer always emits `@schema` first, so first-property detection
is sufficient.) This is the crux that makes `ReadData<TReader>` more than a copy of
`Wire.ReadBody`.

## Scope / payoff
- Removes the json coupling from `list`/`dict` entirely; the container element walk
  becomes truly format-agnostic (protobuf/cbor reuse it for free).
- Deletes the `RawValue()`-DOM-reparse shortcut for nested structures (the residual
  re-encode), so nested containers stream like scalars.
- Lets `Wire.ReadBody` itself delegate to `ReadData<TReader>` (one envelope read,
  STJ side just adapts `Utf8JsonReader` → `json.Reader`) — converging the two.

## Why deferred
`ReadData<TReader>` is real work (the envelope read + signature-layer + properties +
`Data<T>` wrap + the `@schema:data` first-property detection), on the load-bearing
deserialize spine. The template work is independent and ships first; this is the clean
follow-up that makes the slot read honest about format.

## Smaller intermediate step (optional)
If the full version is too big for one pass: move just the **agnostic** part of
`ReadSlot` to `app.type.reader` (so containers stop importing `json`), and leave the
structured fallback as a single, clearly-named json delegation (`StructuredFallback`)
— the coupling shrinks to one quarantined spot instead of being woven through.

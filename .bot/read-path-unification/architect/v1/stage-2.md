# Stage 2 — `read(IReader, View)` + `@schema` dispatch + raw value capture

**Design authority:** `plan.md` "Phase 2" + Leg A. Stub — **firm up when Stage 1 is green + pushed** (entry/exit may shift with what Stage 1 actually landed). Re-verify line numbers then.

## Entry
- Stage 1 green + pushed (total reader registry).

## Exit
- The read entry is `async read(IReader, View) → Task<Data>` (not `JsonSerializer.Deserialize<Data>`); `json.Reader` is one `IReader`.
- `@schema` dispatched via `App.Reader(schema)` (`data`/`signature` registered readers, no `if signature`).
- The `data` reader captures `value` **and every property value** via `IReader.RawValue()` — no DOM. The `signature` reader awaits verify (View-gated).
- Thin `JsonConverter<Data>` STJ adapter bridges sync at the perimeter. Build + both suites green.

## Coder plan (firmed up — Stage 1 + the source.Value read door landed `6c4df92ca`)

### The load-bearing reason this is a rewrite, not a patch
`IReader.RawValue()` (no-DOM raw capture) only works when the reader **owns the buffer**.
Inside the STJ `JsonConverter<Data>.Read` you have the `ref Utf8JsonReader` but NOT the
buffer — which is exactly why today's `ReadBody` DOMs (`JsonDocument.ParseValue` +
`GetRawText`, `Wire.cs:~397`). So the `read(IReader)` entry must exist first; then
`json.Reader` owns the buffer and `RawValue()` slices the value with no DOM.

### Target flow (Leg A)
```
read(IReader r, View v):                        async; mirror of value.Write(IWriter)
  r.BeginObject(); r.NextName(out first)        first name is "@schema"
  schema = r.String()
  return App.Reader(schema).Read(r, v)          registry dispatch — no `if signature`

"data" reader.Read(r, v):
  while r.NextName(out name):
     name       -> r.String()
     type       -> read the {name, kind?, strict?} entity
     value      -> r.RawValue()                 raw bytes, NO DOM, NO eager born/Build
     properties -> each prop value -> r.RawValue() too (invariant 1, transitive)
  r.EndObject()
  return new Data(slotName, new source(rawValue, typeRef, format=application/plang))

"signature" reader.Read(r, v):                  outer wrapper; awaits verify (View-gated)
  read+verify the wrapper; inner = read(r, v) recurse to "data"; return verified inner
```

### What collapses (every value -> source, always lazy)
- `typeRef.Build(value)` eager born (`Wire.cs:334`) — DIES; value defers to source.
- inline `Typed.Read` born (`:406-413`) + `born` var — DIES.
- `refValue` / `%x%` variable special-case (`:294-303,386-392`) — moves to the
  text/variable reader (gated on `ReadContext.Template`), DIES here.
- `deferredRaw` DOM capture (`JsonDocument.ParseValue`+`GetRawText`, `:~397`) -> `RawValue()`.
- `IsDeferrableShape` (`:460`), `EmitRawVerbatim` (`:467`) — DIE.
- `ReadPropertiesObject`/`ReadPropertyPrimitive` (`:485,505`) -> the `data` reader pulls
  props via IReader.
- `@schema` `if signature` probe (`:175-181`) -> `App.Reader(schema)` registry dispatch.
- STJ `JsonSerializer.Deserialize<Data>` entry -> `read(IReader)`; a THIN
  `JsonConverter<Data>` adapter remains (wraps a json.Reader, calls `read(r)`) for STJ-driven
  outer objects — this is where the buffer becomes available.

### Stays
- The `type` entity read ({name,kind,strict}) — structural, format-agnostic.
- The invalid-schema throws (now inside the `data` reader).
- `_readDepth`/`MaxReadDepth` recursion guard -> into `read(IReader)`.
- TEMP: `goal.call` inline branch + the action/GoalCall FromWire reconstruct — until
  goal.call gets a reader (follow-on, not a Stage-2 blocker).

### Slices (each: build + Data + Wire green via targeted 15s runs)
1. **`read(IReader)` entry + `data` reader + thin STJ adapter.** The adapter wraps a
   json.Reader (buffer in hand) and calls `read(r)`; `data` reader captures value via
   `RawValue()` -> source. Eager born/Build/deferredRaw-DOM die. Signature stays on the
   old path for now (the adapter still routes `@schema:signature` to `ReadSignatureLayer`).
2. **`@schema` registry dispatch** — `App.Reader(schema)`; register `data` + `signature`
   readers; delete the `if signature` probe.
3. **`signature` reader** — move `ReadSignatureLayer` (async verify, View-gated) onto the
   registered reader; recurse to `data`.
4. **Sweep dead code** — `IsDeferrableShape`, `EmitRawVerbatim`, `ReadPropertiesObject`,
   `ReadPropertyPrimitive`, `FromWireShape` family (where not goal.call-TEMP).

### Open / risk
- Full laziness: today some values born eagerly; after slice 1 ALL defer to source. Lazy
  tests should pass (materialize-on-read); watch for any test asserting eager identity.
- `%ref%`/variable: must land in the text/variable reader before its Wire special-case is
  removed, or full-match `%x%` values regress. Verify with the ResolveValue_* tests.

## Shipped + deltas from plan
_(coder fills as slices land.)_

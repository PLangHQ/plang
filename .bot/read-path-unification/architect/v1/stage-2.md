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

## LANDED (f9e82571e) — Leg A core: wire defers all values to lazy sources
The wire read no longer borns values eagerly. Every value slot rides as a verbatim lazy
`source` and materializes through the read door on first touch (json object→dict, array→list,
scalar→its wrapper). Data + Wire neutral; TableXlsx throw green.
- `source._template` (authored mode) → `ReadContext` → text reader interpolates `%ref%`.
- `source.Write` keys inline-vs-quote on `_format` (json inline = byte-identical relay, so a
  signature over an untouched dict/list still verifies; value content quoted). THIS was the
  fix for the signature round-trip — verbatim source + correct Write, not eager dict.
- format chosen by the captured token (string → value/text, else → plang/json).
- registry `Reader` returns null (not throw); the serializer decides: **structured json with
  no reader → natural dict/list; scalar with no reader → throw loudly** (table/xlsx). This is
  the host-object case (a `type`-entity value `{name,kind}` → natural dict, the OLD behavior).
- new `variable` reader (name-slot → `variable.Resolve`).
- GONE: `typeRef.Build` eager born, inline `Typed.Read`, `json.Parse` natural, `IsDeferrableShape`.
  STILL eager: `%x%` full-match, `goal.call` (TEMP).

### LANDED — incremental thinning + read entry (a9ba4802b, 0c8f6cb64)
- `serializer.Read(source)` — no decompose (OBP); the serializer navigates source.Raw/Mint.
- `type` is a descriptor with a real reader (it IS an item.@this) — NO "untyped"/clr/dict
  fallback. registry `Reader` throws again for a genuine gap (table/xlsx). `json.Untyped` gone.
- Dead-code sweep: `typeRef.Build` eager born at EndObject, `IsDeferrableShape`, `value` local — gone.
- `source.Write` format discriminator fixed (was `== application/plang`, mis-quoted an
  application/json source) → `== Text.Mime` → quote; else inline. `Text.Mime` const.
- **Read entry: `DeserializeAsync` owns the buffer** — reads bytes, drives the Wire converter
  over a `Utf8JsonReader` on those bytes (not `JsonSerializer.Deserialize<Data>`). Both prod
  and the sync test path route here. This is the prerequisite for no-DOM. Neutral.

### LANDED — no-DOM + data-reader extraction (335812089 … ad84b39d3)
- **Read entry owns the buffer** — `DeserializeAsync` reads bytes, `Wire.ReadBuffered` drives
  the read over them (not `JsonSerializer.Deserialize<Data>`). The serializer holds the Wire
  per view (no `OfType<Wire>` reach).
- **No-DOM value capture** — on `json.Reader.RawValue()`: string off the token, number/bool via
  ValueSpan, structured sliced from the owned buffer. Only buffer-less STJ-nested + signature
  inner + error-preview still DOM. `ReadBody`'s value switch collapsed to two lines.
- **`data` reader extracted** — `ReadBody` + property readers moved to `app.data.reader.@this`
  (the `@schema:data` reader). `Wire.ReadCore` dispatches: signature → ReadSignatureLayer, else
  → `data.reader.Read(...)`. Wire ~600 → 418 lines.

### Decided: signature stays at the Wire boundary
`ReadSignatureLayer` is the verify-on-read security gate (coupled to `View`, the signing module,
fail-closed-on-no-context) — it belongs at the wire boundary, not scattered into a separate
reader. So the `@schema` registry (`App.Reader(schema)`) is dropped as over-engineering for one
real dispatch target. Wire = STJ boundary + verify-security + `@schema` dispatch + write; the
common envelope read delegates out. The stage's read-extraction is substantially complete.

### Still ahead (all in stage-final-cleanup.md — deliberate later passes, not this stage)
- **no-DOM `RawValue()`**: TWO parts — (a) route `ReadBody`'s value capture through
  `json.Reader.RawValue()` (needs careful ref write-back across the json.Reader↔Utf8JsonReader
  boundary — `_r` is held by value, write back via `.Inner`); (b) make `RawValue()` slice the
  OWNED buffer span instead of its current internal `JsonDocument.ParseValue` (reader.cs:119) —
  needs `json.Reader` to carry the `byte[]`. The buffer is now in hand at the entry; nested Data
  (STJ-driven, no buffer) keeps DOM → the value capture forks buffer-vs-no-buffer (design it so
  it's not two shapes). FRESH-SESSION work.
- `@schema` registry dispatch + `signature` reader (async verify, View-gated; security-sensitive).
- `%ref%`→variable reader migration (defer `%x%` too) — has an IsVariable/build-validation
  subtlety; verify with ResolveValue_* tests.

## Shipped + deltas from plan

### Slice-1 attempt (reverted to protect green — findings captured)
Tried the conservative version of slice 1: keep the STJ converter + DOM, but make
`ReadBody`'s value switch **defer ALL typed values** to `source` (kill the eager
`typeRef.Build` / inline `Typed.Read` / `json.Parse` born), keep `%x%` full-match eager
and `goal.call` TEMP, and let `FromRaw` pick the reader by kind (no forced format).
Result: Wire neutral, Data 11 new → fixed down to **3** before reverting.

What it proved (do these as prerequisites in the real slice):
1. **Template must thread onto `source`.** Deferring template-bearing values (partial
   `"hello %x%"`, goal params) breaks interpolation unless `source` carries `_template`
   and `source.Value` passes it in `ReadContext` (→ text reader interpolates). Add
   `_template` to `source` + `FromRaw(template:)`; Wire's deferred `FromRaw` passes `_template`.
2. **`variable` needs a reader.** A `type:variable` name-slot defers and throws — add
   `app/variable/serializer/Reader.cs` (`variable.@this.Resolve(reader.String(), ctx)`).
   (This fixed the goal/ResolveValue/most-Defaults cluster.)
3. **The remaining 2 tie into the `clr`/host-object work, NOT "add a reader":**
   - a **`type`-entity value** (`new type("number","long")` as a param value) defers and
     throws `no reader for type 'type'` — it's a host object → should be `clr`, not a plang
     value type. (Defaults_ParameterOverridesDefault / ResolvedWhenParameterMissing.)
   - an **object/json config** round-trips (serialize→deserialize) to a **`clr`** instead of
     a `dict` → `clr.Navigate` NRE (Cut1_NavigatedConfigJson). Needs tracing — likely the
     serialize-of-source / re-read loses the type, or the object reader path yields clr.

   These connect to the goal→clr / clr-carrier cleanup. The throw-on-missing-reader is
   right for plang types (table/xlsx) but a **host-object value should ride as `clr`**, not
   throw — so the read door needs to distinguish "plang type with no reader (throw)" from
   "host object (clr)". Resolve that before/with this slice.

### CORRECTION (Ingi) — json → dict/list, NOT a verbatim source
The slice-1 attempt deferred **every** value (incl. structured json) as a verbatim-raw
`source`. That's wrong for structured json and is what broke the 2 remaining tests:
- **Cut1 config = a SIGNATURE round-trip.** The wire is signature-wrapped; on read
  `back.Type` came back null (`null.@this`) — verify FAILED. Root: a json object deferred
  as a verbatim `source` re-serializes **verbatim** (the captured raw text), which does not
  match the `dict`'s **canonical structural** serialization the hash was computed over →
  hash mismatch → verify null. (Not a reader/clr bug — a canonicalization bug.)
- The type-entity case is the separate host-object→`clr` issue (unchanged).

**The fix is the right split:**
- **structured token (object → `dict`, array → `list`)** is parsed AT READ into the lazy
  container (the container is built; its *entries* are the deferred `source`s). A `dict`/`list`
  serializes structurally = canonical, so signatures stay stable.
- **scalar token (string/number/bool)** → a verbatim `source` (lazy).

So the `data` reader (and the interim `ReadBody` defer) must branch on the value's token:
object/array → build dict/list now (entries lazy); scalar → source. NOT "defer everything."
This also means the no-DOM `RawValue()` capture is only for the SCALAR leaves; structured
values stream into dict/list via the existing `item.json` reader (lazy entries).

### Recommendation for the fresh pass
Do prerequisites (1) template-on-source and (2) variable reader FIRST as their own tiny
commits (they're correct regardless), THEN the defer-everything switch, THEN the
host-object-as-clr distinction for (3). Each green via targeted 15s Data/Wire diffs.

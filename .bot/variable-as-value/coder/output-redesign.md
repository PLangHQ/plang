# Output redesign — the item writes itself to the wire (branch `variable-as-value`)

**Date:** 2026-06-23. Agreed with Ingi. Successor to the Navigate redesign — same
principle (the item owns it), now for serialization.

## The disease

Serialization is a TWO-walk, sync pile:
1. `Text.cs:28` / channel pre-resolves the WHOLE value: `await data.Value()` (async) —
   walk #1, materializes everything up front.
2. `data.Normalize(View)` (sync) flattens domain objects into a tree (`NormalizeValue`
   procedural type-switch in `data/`), then `json.Writer.Value` / `Wire.cs` (STJ sync
   `JsonConverter.Write`) walks the tree — walk #2.

Two wrongs: (a) double walk (resolve-all, then serialize-all); (b) the flatten logic
(`NormalizeValue`/`NormalizeObject`) lives in `data/`, a type-switch, not on the items.
`variable` is a leaf passthrough → writes the raw `"%msg%"`, never its value.

## The model — `item.Output(IWriter, View)`

ONE async pass: each item WRITES ITSELF to the wire, resolving lazily as it reaches each
node. Merges `Normalize` (flatten) + `Write` (render) into one. No pre-resolve walk, no
intermediate Normalize tree. The `await`s happen in OUR walk between `Utf8JsonWriter`'s
SYNC buffer writes; one `FlushAsync` to the stream at the end (confirmed: Utf8JsonWriter
has no per-value async; async is stream-flush only — that's fine, we await in the walker).

```csharp
// data.@this  (replaces Normalize)
public ValueTask Output(IWriter writer, View mode = View.Out) => _type.Output(writer, mode);

// item.@this  (replaces Write + Normalize)
public abstract ValueTask Output(IWriter writer, View mode);
//   leaf (text/number/datetime/bool/guid/duration/date/time/binary/null)
//        → writer.String(...) / writer.Long(...) / ...           (sync write, no await)
//   dict → writer.BeginObject; foreach e: writer.Name(e.Name); await e.Output(w,mode); EndObject
//   list → writer.BeginArray(n); foreach x: await x.Output(w,mode); EndArray
//   variable → await Value(); resolved.Output(w,mode)   ← resolved HERE, lazily (self-ref guarded)
//   clr  → BeginObject; reflect host [Out] (Tagged.PropertiesFor); each value:
//            item child → await child.Output; IDictionary/IEnumerable → object/array;
//            primitive → leaf; foreign object → new clr(it).Output; else → throw OutputException.
//          (absorbs today's NormalizeObject + the raw IDictionary/IEnumerable/primitive arms)
```

## Invariants

- **No non-plang types flow through `Output`.** Every value is an item (leaf/dict/list/
  variable/domain) or a `clr` carrier (foreign objects — wrapped at Lift: `type.cs:395`,
  `data.cs:476`). `clr` is the ONLY CLR boundary. A raw type reaching `item.Output` is a
  Lift bug → `OutputException` (no silent fallback, no `OutputValue` static helper).
- **`data.Output → _type.Output`** — Data delegates, never `data.Instance.Output`.
- **Reflection lives in `clr`** (same as Navigate). A domain item we own either defines its
  own `Output` or is reflected via `clr` — TBD per type during the build.
- **View filter** ([Out]/[Store]/[Debug], [Sensitive]/[Masked]) applies in `clr.Output`'s
  reflection (was `Tagged.PropertiesFor` in NormalizeObject) — symmetric to today.

## Scope / order (build + test per step)

- [ ] `IWriter` already has the surface (BeginObject/Name/EndObject, BeginArray/EndArray,
      Null/Bool/Int/Long/Float/Double/String/DateTime/DateTimeOffset/TimeSpan/Guid/Enum/
      Decimal/Bytes). Confirm it needs no additions.
- [ ] `item.@this.Output(IWriter, View)` abstract; `data.@this.Output` → `_type.Output`.
- [ ] leaf overrides (text/number/datetime/date/time/duration/bool/guid/binary/null/image)
      — mostly the body of their current `Write`.
- [ ] dict/list overrides (object/array walk, await children).
- [ ] variable override (resolve + delegate; self-ref guard — `_resolveDepth` in Value
      stays as the loud backstop).
- [ ] clr override (reflect host [Out] + raw arms; the CLR boundary; throw on non-plang).
- [ ] channel serialize entry: drive `data.Output(writer)`, `await FlushAsync()`. Replace
      the STJ `JsonConverter.Write` (`Wire.cs`) + `json.Writer.Value` walk + `Text.cs:28`
      pre-resolve.
- [ ] delete `Normalize`/`NormalizeValue`/`NormalizeObject`; `NormalizeException` →
      `OutputException`. Update ~10 callers.
- [ ] full `./dev.sh full` — serialization changed for every type; suite is the gate.

## Context

The Wire READER (`Wire.cs ReadBody`, born-on-wire) is the read half — UNCHANGED here;
this is the write half. Current blocker that motivated it: `variable.Cacheable=false`
(so a goal-call `planStep=%item%` re-resolves per call instead of memoizing the shared
descriptor) exposed that serializing `%msg%` (EmitBuildEvent) resolves a variable; the
lazy single-pass Output is the clean home for that resolution.

## Follow-up (to the end)

- The `type` object's `kind`/`strict` are written generically in `data.Output`. The TYPE
  should own which tags it emits — `strict` is only meaningful for image/number, `kind`
  varies per family. Move the type-tag emission onto the type/item (a `type.WriteTag(writer)`
  or similar) once the main Output pass lands. (Ingi, 2026-06-23.)

## Folder structure (locked with Ingi, 2026-06-23)

Make WRITE mirror READ — per type, in the `serializer/` subfolder:

```
type/<x>/serializer/
   Reader.cs    — ITypeReader: tokens → type (pull, single pass)        [exists]
   Writer.cs    — item.Output:  type → tokens (push, single pass)        [NEW — mirrors Reader]
   Default.cs   — raw CLR value → type (lift; no write mirror — inverse is .Clr on the type)
```

- The 14 per-type `type/<x>/Json.cs` JsonConverters are **deleted** — `item.Output` via a
  `json.Writer` replaces them (verify no raw-STJ path, e.g. goalsSave `PrWrite`, depends
  on them first).
- Per-FORMAT writers live centrally, mirroring readers:
  `channel/serializer/<format>/writer.cs` — `json/writer.cs` (exists), `text/writer.cs`
  (new), `plang/writer.cs` (new). `plang.Writer.EmitsSchema = true`; json/text = false.
- `data.Output` gates on `w.EmitsSchema`: plang → `{@schema,name,type,value,properties}`;
  json/text → bare value. ONE walk, three writers.
- **`data/Wire.cs` moves OUT of `data/`** (never belonged there) → `channel/serializer/plang/`
  as the plang READER. Its Write half is gone (item.Output owns write).
- Channel write dispatch: MIME → pick the format writer → `data.Output(writer)`.
- Later cleanup: `Reader.cs` (tokens→type) and `Default.cs` (raw→type) overlap; once the
  single-pass IReader is the only read path, fold `Default` into `Reader`. Separate task.

### Build order (additive-green first, risky flip last)
1. Per-format writers: `text/writer.cs`, `plang/writer.cs` (+ `EmitsSchema` on IWriter). [green]
2. `data.Output` gated on `EmitsSchema`. [green]
3. Move per-type `item.Output` bodies → `serializer/Writer.cs`. [green]
4. Channel MIME → writer dispatch (THE flip: replaces Normalize/Wire.Write/Text pre-resolve). [risky]
5. Move `Wire.cs` → `channel/serializer/plang/reader.cs`; delete Json.cs + Normalize. [cleanup]
6. Reader: dict entries now read as @schema Data. Full `./dev.sh full`. [gate]

## Separate, still-open: the %msg% self-ref (the actual builder blocker)
The output channel is `text/plain` → `%msg%` resolves via `Text.cs await data.Value()`,
NOT the JSON path. This redesign does NOT fix it — `%msg%`'s slot resolves to
`variable(msg)` (a self-reference) exposed by `variable.Cacheable=false`. Root unknown
(why `render … write to %msg%` leaves msg pointing at itself). Must be traced+fixed
separately to unblock the builder, regardless of this redesign.

## Refined design — writers own the stream + per-format override dispatch (Ingi, 2026-06-23)

Two corrections that supersede the "inline writer.Format branch" idea:

1. **Writers own the stream** — a writer is constructed with the Stream and writes THROUGH
   it as each token arrives (streaming, no buffer-to-string-then-copy). `json.Writer` already
   wraps `Utf8JsonWriter(stream)`; `text.Writer(stream, encoding)` writes leaf bytes straight
   to the stream. Serializer = `new writer(stream); await data.Output(writer); await stream.FlushAsync()`.

2. **No `if (writer.Format)` in types; no json inside `text.Writer`.** The text writer is PURE
   text (leaves only). A container has no plain-text form, so its text rendering lives in a
   **per-format override file** dispatched by format:
   - `type/<x>/serializer/<format>.cs` — a static `Output(item value, IWriter writer, View, ctx)`.
     `dict`/`list`/serializer/text.cs → `writer.String(JsonSerializer.Serialize(value, value.GetType()))`.
   - **Dispatch** (in `data.Output`'s value slot): reflect `<item-namespace>.serializer.<format>`
     for a static `Output` (cached, like the `Convert` hooks). Found → use it; else → `item.Output`.
     So `dict + text` → `dict/serializer/text.cs`; `dict + json/plang` → no override → `item.Output`
     (structural). Leaves → no override → `item.Output` → `writer.String` (text plain / json quoted).
   - `text.Writer.BeginObject/BeginArray` THROW — a container always hits its override first, so
     structural tokens never reach the text writer.

Net: types stay format-neutral, the writer renders, containers' text form is one isolated file
per type, dispatch is by format (no `if`), writers stream to the Stream. Read stays untouched.

## DECISIONS LOG (Ingi + coder, 2026-06-23) — the per-format serialization model

This supersedes the earlier "reflection registry" sketch. Captured so context survives.

### The model (agreed)
- A value serializes itself by emitting **format-neutral tokens** via `item.Output(IWriter)`
  (`BeginObject`/`Name`/`String`/`Long`/…). The per-format **writer** (`text.Writer`,
  `json.Writer`, `plang.Writer`, later `protobuf.Writer`) renders those tokens. **Format
  lives in the writer**, not in the type. One `Output` per type; writer owns format.
- `data.Output`: **Data owns its `@schema` layer**. The `{@schema,name,type,…}` envelope
  opens ONLY for `writer.EmitsSchema` (application/plang); json/text write the bare value.
  The value-write happens **once** (envelope just wraps it). References (variable) resolve
  before output (the variable branch); self-ref guarded (`OutputSelfReference`, loud).
- `clr.Output` owns ONLY **reflect + lift**: a foreign host has no plang shape → render as
  its `[Out]` fields (`Tagged.PropertiesFor`); each field VALUE is raw CLR → lifted to its
  item via `type.@this.Create(raw)` → THAT item writes itself. So text/number/dict/… own
  their own output; `clr` owns just "reflect my host." **`OutputAny` is DELETED** (it was
  the `NormalizeValue` type-switch reborn).
- `text.Writer` is **pure text**, owns the `Stream`, streams leaves straight through
  (`String→bytes→stream`); structural tokens (`BeginObject`) THROW (a container never
  reaches it — see the exception below).

### The one exception — non-leaf × text
- A container/object (`dict`/`list`/`clr`) on a **text** channel has no plain-text form,
  so it serializes as **JSON** — a different serialization than "render my tokens." This
  is the only `(type, format)` pair that needs bespoke per-format logic today.
- Mechanism (agreed shape, NOT the rejected one): each diverging type owns a small
  **per-type registry** of **`IOutput` INSTANCES** — `interface IOutput { ValueTask
  Output(item.@this value, IWriter writer, View mode, context? ctx); }`. The type lists its
  own format serializers (e.g. `dict/format/text.cs : IOutput` → `writer.String(json)`),
  default = its own token output. **No central registry, no reflection-to-call, no static
  classes** (instance classes + a static *field* Dictionary is fine).
- The exact **dispatch** (how `Output` picks the format serializer) is STILL OPEN — string
  key + lookup vs a typed `writer.Format`. Deferred (do not implement yet).

### `clr` moves to its own namespace (agreed)
- `app.type.item.clr` → **`app.type.clr.@this`** (follow the `app.type.<name>.@this`
  convention, like dict/list). Removes the class-vs-namespace clash (`clr/format/text.cs`
  works) and the "exception" — every type keys uniformly. ~10 real references
  (`global::app.type.item.clr` in data.cs ×4, type.cs:395, item.cs `new clr`); add
  `global using Clr = global::app.type.clr.@this;`. (computed/source can stay under
  app.type.item — no format overrides, no collision.)

### Rejected (do not reintroduce)
- `if (writer.Format == "text")` branching inside a type.
- A central `OutputAny`/`NormalizeValue` type-switch.
- JSON logic inside `text.Writer`.
- A central serializer registry scanned by reflection + static `serializer/<format>.cs`
  classes (`_generatedWrite` + `Delegate.CreateDelegate`). OBP violation; statics forbidden.
- Resolve-then-serialize (walks the tree twice). Single pass only.

### State in the tree right now (mid-refactor)
- DONE/committed: `item.Output` base (→`Write`), `dict`/`list.Output` (tokens),
  `clr.Output` (reflect+lift), `data.Output` (@schema gate + value-once),
  `text.Writer` (streaming pure), `IWriter.EmitsSchema`, `text` channel → `data.Output`.
- STILL THE REFLECTION REGISTRY (to be replaced by per-type `IOutput` registries):
  `type/reader/this.cs` `_generatedWrite` + `Output(itemType, format)` lookup, and
  `dict`/`list`/serializer/text.cs are STATIC classes. **Replace these** with the
  per-type `IOutput`-instance registries + move `clr`.
- NOT DONE: the big flip of plang/json channels off `Wire`/`Normalize` onto `data.Output`;
  delete `Normalize`/`NormalizeValue`/`NormalizeObject`; rename `this.Normalize.cs`→Output.

### Separate, still-open blocker (orthogonal to all the above)
- The builder fails on `%msg%`: the `render … write to %msg%` stores a `%!data%`
  self-reference (SETMSG trace: `value=variable raw=%!data%`), so resolving `%msg%` loops.
  Now LOUD (`OutputSelfReference`) instead of stack-overflow. NOT a serializer bug.

## #2 DECISIONS (Ingi, 2026-06-23) — the plang/json channel flip

- **@schema is the LAYER marker**, not a per-Data tag: it discriminates `data` vs
  `signature` vs `encryption` vs `compression` at a layer boundary (top-level payload, a
  signed payload, …). A plain typed value carries `type` only.
- **Dict entries (and nested typed values) carry `type`, NOT `@schema`.** Wire shape:
  ```jsonc
  { "@schema":"data", "type":{"name":"dict"}, "value": {
      "name": { "type":{"name":"text"},               "value":"x" },   // entry: type+value, no @schema
      "age":  { "type":{"name":"number","kind":"int"}, "value":30 }
  }}
  ```
  This supersedes the earlier "every Data self-describes with @schema" — refined to
  "@schema at layer boundaries; typed values carry type." More verbose `.pr` is fine
  (more accurate; types round-trip).
- **Sync `plang.Serialize`/`Store` → async-ify the callers** (everything is async in plang).
- **RawUntouched/EmitRawVerbatim**: preserve on the module/file read path (a raw payload
  relayed un-reparsed); NOT a concern for `.pr` plang values.

### Implementation (incremental — reader-lenient first, then flip)
1. Wire.ReadBody: ALSO accept `{type, value}` entries (no @schema) — lenient, reads old
   (bare) AND new shapes. [green — old .pr still read]
2. data.Output: a `layer` flag — top/layer boundary writes `@schema` + type + value;
   nested (entries, value-slot children) write `{type, value}` (no @schema). [additive —
   plang still on Wire, so not live yet; green]
3. Flip plang/json serializers to drive `data.Output` (async, sign-if-missing ported)
   instead of `Wire.Write`/STJ; json = bare (EmitsSchema=false), plang = layer
   (EmitsSchema=true). Regenerate `.pr` (entries now carry type).
4. DELETE: data.Normalize/NormalizeValue/NormalizeObject + NormalizeException;
   json.Writer.Value/BeginRecord/EndRecord + IWriter.Value/BeginRecord/EndRecord;
   Wire.Write → stub (Wire stays as the READ converter only). json.Writer → pure tokens.
5. `./dev.sh full`.

## (a) FULL SPEC — data.Output is the ONE write serialization (Ingi, 2026-06-23)

The plang flip is a UNIFICATION: `data.Output` becomes the single write serializer for
EVERY write path; `Wire.Write`/`Normalize` are deleted. `Wire.Read` stays (read).

### Wire shapes
- **Data (layer):** `data.Output(layer:true)` → `{@schema:"data", [name], type:{...}, value, properties?}`.
- **Typed value (nested):** `data.Output(layer:false)` → `{type:{...}, value}` — NO @schema. (dict
  entries, value-slot children.)
- **Layer (signature/encryption/compression):** the layer's OWN serialization, e.g. signature:
  `{ "@schema":"signature", "type":"ed25519", "nonce":"…", "created":"…", "signature":"<b64>",
     "value": { "@schema":"data", "type":…, "value":… } }`
  — i.e. `@schema:<kind>` + the layer's fields + `value = data.Output(inner, layer:true)`
  (the inner is a full @schema:data Data). Same shape for encryption/compression.

### Write paths to flip (ALL of them)
1. **channels** — `plang.SerializeAsync` → drive `data.Output`. sign-if-missing becomes
   `await RunAction(sign,…)` (kills today's `.GetAwaiter().GetResult()` sync-over-async).
2. **layer dispatch at the serializer boundary (ONE place):** after signing, if the top is a
   layer → `layer.Output(writer)`; else → `data.Output(writer, layer:true)`. So `data.Output`
   stays clean (@schema:data only); the layer owns @schema:<kind>.
3. **layer types** (`signature`/`encryption`/`compression`): add `Output` writing the envelope
   above via the IWriter tokens + `data.Output(inner, layer:true)` for `value`. (Ports them off
   `json.Writer.Value`/`BeginRecord`.)
4. **signing hash canonicalization** — `crypto/code/Default.cs:61`
   (`JsonSerializer.SerializeToUtf8Bytes(data, OutboundOptions)` = STJ+Wire) → `data.Output`
   bytes (MemoryStream). Stable (fixed key order, entries insertion-order). sign+verify both
   use it → consistent; the wire bytes == the canonical bytes (cleaner than today's two).
5. **`.pr`** — `goalsSave`/`PrWrite` (STJ+Wire) → `data.Output`. (Old `.pr` regenerate — Ingi:
   don't worry about existing files.)
6. **sync legacy removal** — delete `plang.Serialize`/`Store`/`Load`/`Deserialize(string)`;
   route sqlite (`settings/Sqlite.cs` Get/Set) + snapshot through the async stream API
   (`SerializeAsync`/`DeserializeAsync` over a MemoryStream ↔ the stored string). Everything async.

### Reader
- dict/list reader reads `{type, value}` entries (read `type`, recurse `value` via the existing
  Parse, apply type). No global heuristic — the container knows its entries are typed values.
- Layer read (verify) stays in spirit (read @schema:<kind>, extract inner, re-canonicalize via
  `data.Output`, verify).

### Deletions (after the above)
- `Wire.Write` → gone (the converter keeps only `Read`); `data.Normalize`/`NormalizeValue`/
  `NormalizeObject` + `NormalizeException`; `json.Writer.Value`/`BeginRecord`/`EndRecord` +
  the matching `IWriter` members. `json.Writer` collapses to pure tokens (like `text.Writer`).

### Confirmed by Ingi
- Stable hashing via data.Output: yes. `.pr` through data.Output: good. Layer shape: as above
  (`@schema:signature` outer, `value` = inner @schema:data Data). Same for encryption/compression.

## FLAGGED — revisit together (Ingi, 2026-06-23)
- **Hash canonicalization shape is wrong (works, but wrong means).** `crypto/code/Default.cs`
  now hashes via `data.Output` → MemoryStream → `byte[]` → algorithm. Behaviour is correct
  (sign/verify/round-trip all green) and `MarkOuterForHash` is gone. But the right shape is
  for `data.Output` to produce its hash **intrinsically** — write into a *hashing writer* —
  not via an intermediate buffer. Revisit when doing (a)'s wire flip (the wire write and the
  hash should be the one same `data.Output` walk).

## (a) progress + correction (2026-06-23)
- **Piece 1 DONE + verified + committed** (`f8f8c20a4`): signing hash canonicalizes via
  data.Output. Green: VerifyActionTests/SignActionTests/Stage3_ArraysAsDataTests/HashTypeTests.
  (Shape flagged — hashing-writer, revisit.)
- **Pieces 3–7 are ONE atomic landing**, not green-incrementable: SerializeAsync (writer) +
  dict/list reader + PrWrite/.pr must flip together (emit {type,value} ⇒ reader must read it ⇒
  every .pr/channel read changes at once). Needs full C# suite + a builder run to verify.
- **CORRECTION — layers are NOT uniform** (spec assumed signature/encryption/compression alike):
  - `signature` = wrapping layer, `value` = inner Data, serialized via `Write(IWriter)` (w.Value).
    Port: async `Output` writing `{@schema:signature, <leaf fields via field.Write(w)>,
    value: data.Output(inner, layer:true)}`. Leaf field renders confirmed: text→String,
    datetime→DateTimeOffset, binary→Bytes.
  - `archive` = LEAF (`IsLeaf`, value=compressed bytes), serialized via its OWN `archive/Json.cs`
    STJ converter — different mechanism. Port needs an `Output` writing `{@schema:archive,
    type, value:bytes}` AND retiring/replacing its Json.cs. Per-layer design, not mechanical.
  - The hoist (`Wire.cs:564`) currently checks `is signature` only — archive's layer write path
    must be confirmed (likely via its Json.cs today). Trace before flipping.

## (a) progress — 4 of 7 done (2026-06-24)
- **DONE + committed, no regression, signing green:**
  - P1 `cfb…` chain: hash canonicalization → data.Output (`f8f8c20a4`).
  - P2 signature.Output (async layer envelope; sync Write kept for Wire.Write paths).
  - P3 plang.SerializeAsync → data.Output (async sign + layer dispatch).
  - P6 json.Parse recognizes {type,value} entries (no @schema) → Wire.ReadBody.
  - (`cfb286e2f` carries P2/P3/P6.)
- **BASELINE IS RED — 27 pre-existing Wire-slice failures** (NOT from this work; confirmed
  by stash-compare: 27 with AND without the edits). Root: the type-object transition is
  half-done — `json.Writer.BeginRecord` writes `type` as a BARE STRING (`record.Type?.Name`)
  for NESTED Data records, but `Wire.ReadBody` now REQUIRES a type-object → every
  nested-record round-trip via the sync `plang.Serialize`/`Deserialize` fails. (The .pr
  top-level write goes through Normalize and writes a type-object, so the builder itself
  isn't blocked by this — it's nested-record serialization via BeginRecord.)
  ⚠️ Earlier `./dev.sh full` "exit 0" was MISLEADING — its exit code doesn't reflect TUnit
  per-test failures. Always read the `failed:` count from the slice exe (`dotnet <dll>`),
  not the exit code.
- **REMAINING (the tail — fixes the 27):**
  - P5: delete sync `Serialize`/`Store`/`Load`/`Deserialize(string)`; route sqlite
    (`settings/Sqlite.cs` Get/Set) + snapshot through `SerializeAsync`/`DeserializeAsync`
    (MemoryStream ↔ stored string). Update the ~27 Wire tests (they round-trip via the
    sync `plang.Serialize` + a `Render` helper using `json.Writer.Value`) to async + the
    new {type,value}/@schema-at-layer shape.
  - P4: `goalsSave`/`PrWrite` → data.Output.
  - P7: delete `Wire.Write` (keep `Wire.Read`), `Normalize`/`NormalizeValue`/
    `NormalizeObject`, `json.Writer.Value`/`BeginRecord`/`EndRecord` + IWriter members;
    signature.Write goes too.
  - P1-followup (flagged): hashing-writer shape (one walk, no intermediate buffer).

## SETTLED DESIGN — sync→async migration + typed settings (Ingi, 2026-06-24)
Long design discussion; conclusions:
- **Serializer is STREAM-only.** `ISerializer`: `SerializeAsync(Stream, Data, View=Out)`,
  `DeserializeAsync(Stream, View=Out)`, `DeserializeAsync<T>(Stream, View=Out) where T:item`.
  NO string methods, NO sync. The non-generic stream `DeserializeAsync(Stream)→Data` stays —
  but ONLY for the channel transport (receiving an arbitrary message whose value is an item),
  NOT as a settings escape hatch.
- **string↔stream belongs to the store, not the serializer.** sqlite (it chose a TEXT column)
  owns it: read via `reader.GetStream(col)` (real stream, no string); write buffers to a
  MemoryStream then binds the TEXT param (a bytes→string hop — accepted; DB stays TEXT for
  debuggability, NOT blob). Tests get a `string` convenience via a test-only shim.
- **No untyped settings `Get`.** Every stored value is either a defined C# class (`Identity`,
  an item) or a value from plang code (an `item` — the union of plang types). So
  `Get<T> where T:item` covers BOTH; the untyped `Get(table,key)` is DELETED. KEY: `Get<item>`
  is NOT verbose-`Get` — it FORCES the value to resolve to a plang item (no raw/source string
  leaks through), a stronger contract. `Get<Identity>`→`Data<Identity>`→`.Value()` is the flow.
- **`T:data` was vestigial** (only `permission` used it, with `T=data.@this` base). The typed
  generic unifies on `T:item`; the clash dissolves.
- Callers: identity→`Get<Identity>`; settings.get/this + llm cache/config→`Get<item>`;
  permission→`GetAll<permission>` (`Grant = app.type.permission.@this`, an item).
- Test churn (~100 sites) → ONE test-only extension shim (`Serialize`/`Deserialize` sync
  wrappers over the async stream API), so call-sites compile unchanged.

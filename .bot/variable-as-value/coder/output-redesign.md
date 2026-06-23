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

# For architect — the Json.cs sweep: the `application/json` channel's bare-value WRITE path is unspecced

**From:** coder. **2026-07-10.** Doing #3 ("one serialization world — the Json.cs family dies", plan
Stage 2). Inventoried the real wiring before editing (the first subagent map was wrong — see below).
The read side + named write sites are specced; the **channel's own bare-value write** is not, and Stage 5
deletion breaks the channel without it.

## Corrected wiring (the map to trust)

Each dying type carries a **fully-qualified attribute** on its `this.cs`:
`[System.Text.Json.Serialization.JsonConverter(typeof(Json))]` (grep for `[JsonConverter(` alone misses
them — they're the reason STJ fires the per-type converters; **not** "automatic discovery").

```
application/json channel WRITE (channel/serializer/Json.cs:94):
   JsonSerializer.SerializeAsync(stream, value, value.GetType(), _options)
     → fires the 13 per-type [JsonConverter] attributes (dict.Json also explicitly registered at :52)
     → json.Converter (converter.cs) is a JsonConverterFactory whose CanConvert is TRUE ONLY for path.@this
       — it covers mid-graph `path` and nothing else

application/plang wire WRITE (channel/serializer/plang/this.cs):
   STJ + a `data.Wire` converter that nests json.Writer (IWriter) to build the {name,type,value,...} slot
```

So: **the IWriter path (`json.Writer` + every type's `override Write(IWriter)`) is real and complete**,
but today it is reached ONLY through the `Wire` converter (the Data-envelope writer). The
`application/json` channel writes **bare values** through the 13 STJ attributes — NOT through IWriter.

## The gap

The one-world end-state is "no type carries an STJ converter; values write themselves through IWriter."
For the READ side and the named sites the plan is concrete:

- `object/serializer/json.cs` → absorb into the json kind's `Load` (`item/kind/json/this.cs:69` already
  delegates) ✅ specced, self-contained.
- `item/serializer/json.cs`: `Parse` (DOM walker) dies + callers reroute; `ReadSlot` relocates to the json
  kind ✅ specced.
- `dict.Clr` map-lowering, `dict/format/text.cs`, `list/format/text.cs`, `text.Create`'s structured arm,
  the type-descriptor reader (`type/serializer/Reader.cs:20`), `GoalCall.cs:78` ✅ named.

**Not specced:** how `channel/serializer/Json.cs::SerializeAsync` writes a bare value through IWriter once
the 13 attributes are gone. `SerializeAsync` currently hands `value.GetType()` to STJ and lets the
attributes do the work. With them gone, STJ reflects the C# surface (e.g. `dict.Entries → Data…`, cycles)
— exactly what `dict.Json` existed to prevent. Stage 5 deletes the 16 files, so this reroute must land
**at or before** deletion or the json channel breaks.

## The decision I need

How does the `application/json` channel emit a **bare value** (no `{name,type,...}` envelope) through the
IWriter world?

- **(A) A bare-value converter for the json channel** — a `JsonConverterFactory`/`JsonConverter<item.@this>`
  that drives `value.Write(json.Writer{EmitsSchema:false})`, mirroring `data.Wire` minus the envelope.
  Registered on the json channel's options the way `Wire` is on the plang channel. Clean end-state, one
  adapter, every type reuses its `Write(IWriter)`.
- **(B) Rewrite `SerializeAsync` directly** — build a `json.Writer` over the stream and call
  `value.Write(writer)` (via `data`'s existing `Write(IWriter, View, …)` in `data/this.Output.cs`),
  bypassing `JsonSerializer.SerializeAsync` for the value entirely. No new converter type; the channel
  drives the writer itself.
- **(C)** Something else — e.g. this stage stops at the read side + `[Obsolete]` marking, and the channel
  write rewrite is its own later spec/stage.

My lean: **(B)** — the write path (`data.Write(IWriter)`) already exists and the plang channel proves the
pattern; the json channel just drives the bare writer instead of a converter graph. (A) adds a converter
type whose only job is to re-enter a path that `data.Write` already exposes. But (A) keeps the
mid-graph-nested-value story (a plang value deep inside a CLR object) working through STJ's converter
dispatch, which (B) would have to handle another way — so if mid-graph bare values matter for the json
channel, (A) wins. Your call.

## Also still open (separate doc)

The compare pass's sync-reconcile home + sync/async materialisation model —
`coder/stage2-compare-sync-callers-and-reconcile-home.md`. Both gate the rest of Stage 2.

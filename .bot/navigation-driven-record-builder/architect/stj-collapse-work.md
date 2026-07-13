# Work — (B) the STJ collapse: seven packages, traced, with suggested code

For coder, from architect. Settled direction with Ingi 2026-07-13 (the "remove STJ / make read-write independent of STJ" conversation). Every `file:line` verified against HEAD (25e10a06e) during this trace.

**You own this.** Every code block is a suggestion — bodies, naming, factoring yours. Where I say "verify", the trace left a fact for you to confirm before leaning on it.

## The target, precisely

**STJ has two layers. The SERIALIZER layer dies; the TOKENIZER stays.**

- Dies: `JsonSerializer.Serialize/Deserialize`, `JsonConverter`, `JsonSerializerOptions` bags, TypeInfo modifiers — the reflection + policy layer.
- Stays: `Utf8JsonReader`/`Utf8JsonWriter`/`JsonDocument`/`JsonElement` — the byte codec our `json.Reader`/`json.Writer` are built on, and the json kind's host DOM. Writing our own JSON lexer buys nothing.

Good news from the trace — three things assumed outstanding are ALREADY done: the plang WRITE is writer-driven with sign-if-missing at the serializer boundary (`plang/this.cs::SerializeAsync`); the top-level plang READ is buffer-owning (`DeserializeAsync` reads bytes and drives `wire.ReadBuffered` itself — "rather than letting STJ drive it", `plang/this.cs:211-223`); and crypto.Hash canonicalizes through `data.Output` + `json.Writer` (`crypto/code/Default.cs:68-76`) so hashed-bytes ≡ wire-bytes is already by construction. What remains is the shell: nested STJ restarts, the http module's private STJ rig, the json channel read, and the options-bag plumbing.

## W1 — `json.Writer` drops `_options` (do this first; it deflates everything else)

`json/writer.cs:18,28` — `_options` is stored and NEVER used since you nativized the type-descriptor emit. Delete the field + ctor param. Callers to update (all just stop passing it): `channel/serializer/Json.cs:93-94`, `plang/this.cs` `SerializeAsync` + `SerializeItemAsync`, `crypto/code/Default.cs:71-73`.

Immediate payoff in crypto (`crypto/code/Default.cs:46-59`): the serializer lookup + `SerializerMismatch` guard + the `StoreOptions`/`OutboundOptions` fetch exist ONLY to source options for the writer. The writer drive (`:68-76`) stands alone — delete `:46-59` whole. (`hash.Write` keeps its TODO about the hashing-writer shape — untouched.)

This kills the last consumers of `plang.OutboundOptions`/`StoreOptions` (`plang/this.cs:126,135`) — delete both.

## W2 — json channel read → `Kind[json].Parse` (the one decode)

`channel/serializer/Json.cs:102-130`. Today: `JsonSerializer.DeserializeAsync<object?>(stream, _options)`. Reroute to ruling 8's one decode:

```csharp
// NEW — DeserializeAsync body: bytes → the json kind's one Parse (clr(json) | native scalar)
if (stream.CanSeek && stream.Length == 0) return _context.Ok();
using var ms = new MemoryStream();
await stream.CopyToAsync(ms, cancellationToken);
if (ms.Length == 0) return _context.Ok();
var item = _context.App.Type.Kind["json"].Parse(ms.ToArray(), _context);
return _context.Ok(item);   // Parse declines (null) only for non-string/bytes raw — can't happen here
```

`DeserializeAsync<T>` mirrors the plang serializer's shape (`plang/this.cs:266-271`): base read then `data.As<T>()` — no `Deserialize<T>`.

Then the class sheds its STJ weight — delete after re-verifying zero callers at delete time (ForView/`With*` were caller-less at my last grep): `_options` + the ctor options plumbing, `_viewCache`, `_boundView`, `ForView`, `WithIndentation`, `WithConverter`, `WithModifier`, `ForInbound`. `Json` shrinks to: ctor(context), `SerializeAsync` (writer-driven, unchanged), `DeserializeAsync`×2. The `Sensitive.Strip` STJ modifier dies with the bag — masking already rides the writer path (`reflection.Output` `:187` masks; items own their faces).

## W3 — the nested-Data STJ restarts → `data.reader.Read(bytes, ctx)`

`item/serializer/json.cs:99,169` — a `@schema:data`-marked element restarts STJ: `JsonSerializer.Deserialize<Data>(element, NestedOptions())`. The no-STJ door already exists — the `@schema:data` reader's bytes entry (`data/reader/this.cs:26-34`, "a caller with the value's own verbatim bytes reads a Data without knowing this reader's format"):

```csharp
// NEW — both sites (:99 and :169); NestedOptions() + its Wire.ReadOptions use die
? new global::app.data.reader.@this().Read(
      System.Text.Encoding.UTF8.GetBytes(element.GetRawText()),
      new global::app.type.reader.ReadContext(_context, Verify: false))   // nested: outer signature covers it — same fact NestedOptions encoded (:25)
```

`GetRawText→bytes` is a copy, same cost class as today's element-fed `Deserialize`. Delete `_nestedOptions`/`NestedOptions()` (`:21-28`).

## W4 — goal.call reads ONE way (its own reader, no STJ)

Two STJ sites, one owner after:

- **`goal/call/Reader.cs:20-23`** — currently `Deserialize<GoalCall>(reader.RawValue(), Wire.ReadOptions(...))`. Its own doc calls this the temp ("Folding this into the streaming read is the goal.call follow-on" — this is that follow-on). GoalCall is a host shape; the reflection kind already reads hosts, and its `ReadValue` routes `List<Data>` props through the `@schema:data` reader sign-identically (`reflection/this.cs:117-119,129-142`):

```csharp
// NEW — Reader.Read body
public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
    global::app.type.reader.ReadContext ctx)
    where TReader : global::app.channel.serializer.IReader, allows ref struct
    => (global::app.type.item.@this?)new global::app.type.item.kind.reflection.@this()
           .Read(ref reader, typeof(global::app.goal.GoalCall), ctx with { Verify = false })
       ?? new global::app.type.item.@null.@this("goal.call", kind);
```

  **Verify first**: `reflection.Read` matches props via `Tagged.PropertiesFor(target, View.Store)` wire names (`reflection/this.cs:81-82`) — confirm GoalCall's stored props (`Name`, `Parameters`, `PrPath`, …) resolve through that selector (tag them `[Store]` if needed — GoalCall lives in the `.pr`, it should declare its stored face anyway). Pin with the params sign-identical test.

- **`GoalCall.cs:75-86`** — the `JsonElement` arm in `Convert` restarts STJ with `Options.Read()`. Route it through the SAME reader (one owner for goal.call reads):

```csharp
// NEW — the JsonElement arm: element bytes → json.Reader → goal.call's own reader
case System.Text.Json.JsonElement je:
{
    var bytes = System.Text.Encoding.UTF8.GetBytes(je.GetRawText());
    var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
    utf8.Read();
    var reader = new global::app.channel.serializer.json.Reader(utf8, bytes);
    return context.Ok(new global::app.goal.call.Reader().Read(ref reader, null,
        new global::app.type.reader.ReadContext(context, Verify: false)));
}
```

(Keep the try/catch → `GoalCallDeserializationFailed` envelope; your shape.)

## W5 — http's private STJ rig → the registered transport

`http/code/Default.cs:527,575,847` deserialize `application/plang` bodies via `Deserialize<data.@this>(body, TransportIn(context))` — a private Wire+options rig (`:41-60`) duplicating what the plang serializer's read door already does (buffer path, deferred verify, one owner). Reroute all three:

```csharp
// NEW — per site: the registered transport reads the wire (matches channel/this.cs:283)
using var ms = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
var read = await context.Actor!.Channel.Serializers.Transport.DeserializeAsync(ms, global::app.View.Store);
```

Note the behavior DELTA to verify: `TransportIn` verified inline (sync-wait inside the ref-struct reader); the transport door defers + awaits (`plang/this.cs:226-245`) — strictly better, but confirm the http error envelopes (`PlangDeserializeError` etc.) still shape the same for the tests. Delete `_transportInOptions` + `TransportIn` (`:41-60`). (`_caseInsensitiveRead` at `:64` is http's own json-body parse — a separate straggler; if it survives this piece, mark it.)

## W6 — Wire sheds the STJ shell; the plang options bags die

After W3-W5, nothing STJ-drives the `Wire` converter. `data/Wire.cs` becomes a plain class:

- **Dies**: `: JsonConverter<@this>` base + `CanConvert` (`:97-98`) + the STJ `Read` override (`:115-116`) + the `Write` throw (`:201-203`) + `ReadOptions` (`:106-111`, last consumers died W3/W4) + `WrapAsTyped` (`:174-194` — only STJ's typed-cast path used it; `ReadBuffered` always passes base) + the `options` params on `ReadBuffered`/`ReadCore` (verified UNUSED in `ReadCore`'s body — it drives `json.Reader` + the schema registry only).
- **Stays**: ctor facts (View/Sign/context/template/verify/deferVerify), `ReadBuffered(byte[])`, `ReadCore`, the depth guard (update its comment — the "LiftDataIfShaped restarts STJ" rationale is history; the guard itself still bounds hostile nesting).

Then `plang/this.cs`: `_outbound`/`_inbound`/`_store` bags + `BuildOptions`'s enum/path-converter/Transport-modifier rows lose every consumer → delete. **Exception, marked**: `_snapshot`/`SnapshotOptions` (`:89-98,:142`) — `snapshot/this.Wire.cs:54` + `snapshot/Io.cs` still STJ-read the snapshot wire, and snapshot is the deferred branch. Keep the ONE snapshot bag + the minimal plumbing it needs, `[Obsolete]`-marked pointing at the ISnapshot todo. `filter/Transport.cs` and `json/options.cs` + `json/converter.cs` die when their last consumer goes (Transport.ForOutbound may survive only inside the snapshot bag — marked).

## W7 — the straggler riding the dying plumbing: `App.Save`

`app/this.cs:423-443` — `CamelCaseIndented` (with `TimeSpanIso8601` + `json.Converter`) serializing the 5-field app.pr stamp. Hand-drive the writer:

```csharp
// NEW — Save body: five fields, the writer's own primitives; CamelCaseIndented dies
await using var utf8 = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = true });
var w = new global::app.channel.serializer.json.Writer(utf8);
w.BeginObject();
w.Name("id"); w.String(Id); w.Name("name"); w.String(Name);
w.Name("created"); w.DateTime(Created); w.Name("updated"); w.DateTime(Updated);
w.Name("version"); w.String(Version);
w.EndObject();
```

(Field list/types from the current anonymous object `:438` — match exactly; golden the file.) `TimeSpanIso8601` then has zero consumers outside the snapshot bag — delete or fold into it, marked.

## Order, acceptance, and the gate

Order: **W1 → (W2 | W3 | W4 | W5 independent, any order) → W6 → W7.** Each package is its own commit with its own pin.

Acceptance:
- Wire goldens byte-identical (write side is semantically untouched everywhere).
- Sign/verify suite green; goal.call params **sign-identical** test (W4's real risk); http plang integration (W5).
- Baseline suites vs recorded reds.
- Grep gates at the end: `JsonSerializer\.` and `JsonConverter` and `JsonSerializerOptions` in production → **only** the marked snapshot files (`snapshot/Io.cs`, `snapshot/this.Wire.cs`, the plang `_snapshot` bag) + ruling-8's `item/serializer/json.cs` narrow (whose own STJ restarts died in W3 — re-grep it: only `JsonElement`/`JsonDocument` tokenizer types should remain there).

Proposed closing step (needs Ingi's explicit go — not part of this piece until he says so): a **PLNG003 analyzer** — `JsonSerializer`/`JsonConverter`/`JsonSerializerOptions` at error severity in production outside the sanctioned list, same mechanism as PLNG002 for `System.IO`. That's what keeps the count at zero.

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| one decode (`Kind[json].Parse`) at the json channel read | ruling 8 applied at the last channel entrance; no second parse | ✓ |
| nested Data via the `@schema:data` reader's bytes entry | one owner for the Data wire shape, both entrances | ✓ |
| goal.call: one reader, both feeders | the element arm routes through the type's OWN reader — no parallel door | ✓ |
| http reads through the registered transport | the module stops owning wire mechanics; the serializer registry is the selection door | ✓ |
| `Wire` as a plain reader entry | sheds a base class whose contract (`JsonConverter`) nothing exercises; dead `options`/`WrapAsTyped` deleted, not kept "just in case" | ✓ |
| snapshot bag survives MARKED | known debt with a named exit (ISnapshot branch), not silent scope creep | ✓ |
| writer primitives for app.pr | the one writer serves even the 5-field stamp; no options bag for a leaf task | ✓ |

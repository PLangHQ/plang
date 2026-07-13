# Investigation — get the type-descriptor round-trip off STJ (kill the 14th converter), + the read-side collapse it belongs to

Branch: `navigation-driven-record-builder`. Coder investigation for Ingi ("get both the writer and reader off STJ"). The 13 item `[JsonConverter]`s are stripped; the **type-entity converter** (`app.type.json`, the 14th) is the last one, kept because its **read** side is still STJ. This maps what it takes to retire it, and the larger read collapse it's the tip of.

## Where the 14th fires — exactly two sites (grep-verified, HEAD d8a356075)

```
WRITE:  json/writer.cs:88   JsonSerializer.Serialize(_writer, record.Type, _options)   // the envelope's `type` slot
READ:   type/serializer/Reader.cs:20   JsonSerializer.Deserialize<app.type.@this>(reader.RawValue())
ATTR:   type/this.cs:32     [JsonConverter(typeof(json))]  (+ the converter body type/this.json.cs)
```

The `type` slot is the Data envelope's **metadata** — the descriptor `{name, kind?, strict?}` — not the value. The value path is already fully STJ-free (the 13-strip). This is the one remaining STJ write, plus its symmetric read.

## Why it's contained — the pieces already exist

**Write side.** The type entity ALREADY writes itself through `IWriter` primitives — `type/this.cs:38-49` `Output`:
```csharp
writer.BeginObject();
writer.Name("name"); writer.String(Name);
if (Kind != null) { writer.Name("kind"); writer.String(Kind.Name); }
if (Strict)       { writer.Name("strict"); writer.Bool(true); }
if (!string.IsNullOrEmpty(Template)) { writer.Name("template"); writer.String(Template!); }
writer.EndObject();
```
So `writer.cs:88`'s `JsonSerializer.Serialize(record.Type, _options)` → the type slot writes **itself** (same as every other value now). The value already owns this render.

**Read side.** `IReader` has the token primitives to parse the descriptor directly — `BeginObject` / `NextName(out name)` / `String()` / `Bool()` / `EndObject()` (`IReader.cs`). `type/serializer/Reader.cs` is already the registered `ITypeReader` for `type` (reached token-by-token by the schema reader); it just delegates to STJ internally. Replace the one `Deserialize<type>` line with a 3-field token parse → `new type.@this(name, kind, strict) { Context = ctx.Context }`.

**Then** strip `type/this.json.cs` + the `type/this.cs:32` attribute — no firing site remains.

The rest of the Data-envelope read is ALREADY token-based: `Wire.ReadCore` (`data/Wire.cs:152-156`) wraps the STJ `Utf8JsonReader` in the plang `json.Reader` (`IReader`) and delegates to `data.schema.@this.Reader(schema).Read(ref jr, ctx)` — the envelope is parsed token-by-token, the type slot dispatching to the type reader above. STJ is only the *entry wrapper*.

## Open question for you — scope: just the 14th, or the whole read entry?

Two nested scopes; I want your ruling on where to stop:

**(A) Kill the 14th only (contained).** Nativize `writer.cs:88` (type writes itself via its `Output`), token-parse `type/serializer/Reader.cs`, strip `type/this.json.cs` + attribute. The Data-envelope read STILL enters through the STJ `data.Wire` `JsonConverter` + `plang.DeserializeAsync` — but that converter no longer touches the 14th; the type slot is token-parsed inside. **Result: zero `[JsonConverter]` under `type/` (all 14 gone); the write path is 100% STJ-free.** The STJ that remains is only the *entry* `Utf8JsonReader`/`JsonConverter<Data>` wrapper (`data.Wire`), which drives the already-token-based body read.

**(B) The full read-side collapse (the logged asymmetry, 2026-07-10).** Retire the STJ *entry* too: `plang.DeserializeAsync` + the `data.Wire` `JsonConverter<Data>` → a direct `json.Reader`-over-bytes read (no `Utf8JsonReader`/`JsonConverter` boundary), the target the fork answer named `channel read → Kind[json].Parse → the reader registry`. Bigger: touches the plang channel's read entry, `Wire.ReadBuffered`/`ReadCore`, `json.Converter` (`Json.cs:58`, read-side-live), and the depth/verify/signature plumbing currently riding STJ options.

## Coder read
- **(A) is small, self-contained, and finishes the write-side story cleanly** — the type descriptor writes itself, both directions token-based, all 14 converters gone. The building blocks (type `Output`, `IReader`, the schema reader) all exist; risk is low. Sync note: `type.Output` is `async ValueTask` but writes only sync primitives — `writer.cs:88` is sync, so either a small sync `WriteDescriptor(IWriter)` on the type entity, or inline the 3-field emit in `BeginRecord`. Your call on placement.
- **(B) is the genuine asymmetry cleanup** but is the separate logged piece; it drags in the read entry, verify/signature, and `json.Converter`. I'd do (A) now (it stands alone and is verifiable byte-for-byte), and keep (B) as its own piece unless you want them together.

Recommendation: **ruling on (A) now** (kill the 14th, contained), (B) sequenced after. Confirm the sync-`Output` placement (entity `WriteDescriptor` vs inline in `BeginRecord`) and whether the read parse belongs in `type/serializer/Reader.cs` (yes, its stated home) or on the type entity as a static `FromWire`-style parser.

# Plan: the type layer loses formats — source/wire split, declaration-driven materialization

Branch: `wire-source-split` (from `navigation-driven-record-builder`). Settled with Ingi in session 2026-07-12. **Supersedes** `.bot/navigation-driven-record-builder/architect/format-is-a-birth-fact.md` — that doc's center (a family-owned mime fact) was wrong; do not implement it.

> **You own this.** The design decisions below are settled (what owns what, what dies, the two source kinds, all names). Every code block is a suggestion — bodies, private plumbing, and mechanics are yours. Existing members quoted here were verified against source during design; re-verify against HEAD as you go. Flag back anything that doesn't survive contact.

## Why

A source's materialization today is selected by a mime string (`source._format`): capture sites guess it (`GetByType(mime) ?? Text` stamps `text/plain` onto gif bytes), `type.RawFormat` guesses it by a Name-switch (and mints unregistered mimes like `image/gif` → `UnregisteredMimeType`, the 5 strict-image reds), and the guess is then used for a channel-registry lookup whose entire payoff is choosing between two IReaders. Meanwhile `plang.Read` and `Json.Read` are byte-for-byte identical — nothing transport-ish ever happened at materialization — and the reader registry is already keyed by the declaration (`(type, kind)`, kind documented as "the encoding within the type's shape"). The format string is a second, contradicting copy of what the declaration already carries, expressed in channel vocabulary inside the type layer.

The investigation's endpoint: **the declaration is the whole selector.** The only fact the declaration cannot carry is "these bytes are still in the capturing document's encoding" — which is capture knowledge, held as an object reference (the capturing serializer), never as a format name. That fact is a kind of source, not a field: `wire : source`. PLang must stay serializer-independent — a `.pr` could be bson/protobuf someday — so no type, source, or variable may name a format; the wire kind knows only *who* captured it.

## The model

- **`source`** — the value's own raw form (decoded content, file bytes, an authored literal). One step of processing remains and it is the type's: the type reads itself. Example: text holds `line1↵line2` (a real newline).
- **`wire : source`** — raw text still in the capturing document's encoding, born holding the serializer that captured it (the capture passes ITSELF). Two steps remain and the first — undoing the document's syntax — belongs to the capturer. Example: the .pr slot `"line1\nline2"` (quotes + escape are the .pr's syntax, not the value's). Writes back verbatim, byte-for-byte, so an untouched relay's signature still verifies.

Why the split is irreducible: read-side, only the capturing serializer can decode document syntax (a bson slice of a dict is not the dict's literal); write-side, a source must be re-encoded (quoted) while a wire must be copied verbatim — one class cannot carry both write behaviors without the mode field this plan deletes. Today read-side coincidence (wire==json==container literal form) would let everything be a source; that coincidence is exactly the json-dependence being removed.

Naming rulings from the design sessions (recorded so they don't get relitigated): mime strings/`Format`/`Encoding` on the type layer — rejected (channel vocabulary, and Encoding was a synonym); an `IsJson` bit — rejected (format name in disguise); `fragment` — rejected (opaque); `input` — considered and withdrawn (over-broad: file/channel bytes are also I/O input but are plain sources; collides with the input channel); **`wire`** — settled (the codebase's existing word for the encoded document: `data.Wire`, the wire reader, the wire shape).

**Invariant that must survive every step: laziness.** The wire reader parses only the Data's structure (`{name, type, value-slot, properties}`); value slots ride raw and materialize at first touch. Nothing below changes that. (One deliberate exception, existing: string tokens are unescaped at capture — that is the format's own work, kind-independent, and the `%ref%` security gate needs the content at birth. The kind-parse of that content stays lazy.)

## Leaf trace — the incumbent and every call site

The incumbent owner of "which reader materializes this raw" is `source._format` (mime string) + `type.@this.RawFormat` (the guess) + `Serializers[_format].Read` (the lookup) + the three `ISerializer.Read` bodies. Call-site dispositions:

| Site | Today | Disposition |
|---|---|---|
| `type/this.cs:279` (wire-raw arm) | `new item.source(raw, this, context, format)` | format param gone; mints `source` |
| `type/this.cs:289` (re-birth arm) | `new item.source(src.Raw, this, context, src.Format)` — type reaches into source's fields | `src.Declared(this)` — the source re-births itself; wire's override preserves its serializer |
| `type/this.cs:329` (tail) | forwards `format` | forwards nothing |
| `data/reader/this.cs:90-91,117` (wire capture) | computes `deferredFormat` (text/plain vs application/plang) | string token → decode → content `source`; any other token → verbatim slice → `wire`, capture passes itself. Locals `deferredRaw`/`deferredFormat`/`born` collapse to one `item.@this? value` |
| `path/file/this.Operations.cs:73-75, 86, 105` | `GetByType(mime) ?? Text` → format | the three format lines die; `type.Create(bytes, Context)` — the mime already crossed at `:65-66` as the declaration (`TypeFromMime`) |
| `channel/this.cs:294-315` (`StampValue`/`StampType`) | same `?? Text` guess + format | both methods die, inlined into `StampReadAsync` (each had exactly one caller — verified) |
| `data/this.cs:250` (literal path) | `type.Create(parsed, _context)` — never passed a format | unchanged |
| `source.cs:182` (`Read`) | `serializers[_format].Read(this, …)` — registry lookup, throws `UnregisteredMimeType` | source: type-reader over `value.Reader`; wire: `_reader.Read(this, …)` by reference |
| `source.cs:227` (`Write`) | `_format == Text.Mime` → quoted vs inline | source: always quoted/bytes; wire: always verbatim `w.Raw` |
| `ISerializer.Read` + `Json.Read`/`plang.Read`/`Text.Read` | reached via mime lookup | interface member STAYS (it is the wire kind's door, reached by reference); the lookup dies. `plang.Read` ≡ `Json.Read` duplication stays (debt, below); `Text.Read` likely orphaned (verify, candidate delete) |

## Issues found while coding the design (fixes are part of this plan)

1. **`{object, json}` breaks under naive value-dispatch.** `object`/`item` readers push string tokens through `ReadSlot`, which returns text — a `.json` file would come back an unparsed string. Fix: `object/serializer/json.cs` (today an Of-mode static) becomes an `ITypeReader` with `Kind => "json"` — the exact shape `table/serializer/Reader.cs` already has for csv (`Kind => "csv"`, takes `reader.String()` whole).
2. **The `TypeOf` narrowing trap.** `kind.@this.Type` resolves kind→type via `App.Type.Reader.TypeOf(name)` (`type/kind/this.cs:50`), and `TypeOf` scans only the static-mode tables (`_runtime`/`_generated`). Converting `object/json` to a typed reader silently breaks `{binary, json}` → `object` narrowing. Fix: `TypeOf` also scans `_runtimeTyped`/`_generatedTyped` (or the registration keeps a static-table entry). No compile error guards this — it must land with issue 1 in the same commit.
3. **Pre-existing: the structural throw escapes the failure story.** `source.Value`'s catch filters `JsonException or FormatException or InvalidOperationException` (`source.cs:157`), but `value.Reader`'s structural pulls throw `NotSupportedException` (`value/reader.cs:77-78`) — today a `{dict}`-declared source on the value dispatch throws past MaterializeFailed into the courier. Fix: add `NotSupportedException` to the filter.

## Step 0 — mark the demolition list `[Obsolete]` first (Ingi's ruling)

Before any behavior change, annotate every member on the demolition worklist with `[System.Obsolete("wire-source-split: dies with this branch — see architect/plan.md")]`. Usages light up project-wide from day one, nothing new grows against a dying member, and the final cleanup commit is mechanical: delete everything still carrying the attribute.

## Code, file by file

### 1. `PLang/app/type/item/source.cs`

```csharp
// ctor: the `string? format = null` param GONE; the line
//     _format = format ?? type.RawFormat(value, context);
// GONE; the `_format` field + its comment block GONE; `public string Format => _format;` GONE.
// Guards and the IsVariable/%ref% birth gate unchanged.
public source(object value, global::app.type.@this type, actor.context.@this context)

// Read() — replaces `serializers[_format].Read(...)` + the "channel not wired" throw
// (dies: this needs only Context.App, guaranteed born-with):
/// <summary>The type reads its own raw form — the declaration is the whole selector.
/// One token over the raw; the (type, kind) reader owns the decode (a container parses
/// its own literal, csv its text, image its bytes, goal its payload).</summary>
private protected virtual global::app.type.item.@this Read()
{
    // EXISTING door: type/reader/this.cs Reader(typeName, kind, context) — includes the
    // binary→kind narrowing, throws loudly on a genuine reader gap (its documented contract).
    var typeReader = Context.App.Type.Reader.Reader(_type.Name, _type.Kind?.Name, Context);
    var reader = new global::app.channel.serializer.value.Reader(_value);   // EXISTING one-token reader
    return typeReader.Read(ref reader, _type.Kind?.Name,
        new global::app.type.reader.ReadContext(Context, _type.Template));  // EXISTING ctor, same args as today
}

// Write() — the Text.Mime compare dies. Serialization is a USE (Ingi's ruling: a list literal
// writes out as a list, never as a quoted blob of its raw). A template persists as AUTHORED —
// verbatim, quoted, never resolved at write (the %ref% must survive in the .pr). Plain content
// materializes and the VALUE writes itself — Read() is the same first-touch parse Value() runs;
// a bad literal fails loud at write instead of corrupting the document. Bytes short-circuit
// (they already ARE the value's byte form — no point materializing an image to dump its bytes).
public override void Write(global::app.channel.serializer.IWriter w)
{
    if (_value is byte[] b) { w.Bytes(b); return; }
    if (_value is string s && (IsVariable || _type.Template != null)) { w.String(s); return; }
    Read().Write(w);   // the value's own render — dict/list via their converter, number inline, text quoted
}
// Consequence (value-faithful, Ingi-approved direction): a literal "42" declared {number} now
// writes 42 inline (was quoted via text/plain); reads back as a wire {number} — value-identical.
// Coder verifies every item type reachable here implements Write(IWriter) sensibly.

// NEW — re-birth under a new declaration (replaces type/this.cs reaching into src.Raw/src.Format):
internal virtual source Declared(global::app.type.@this type) => new source(_value, type, Context);

// Value()'s catch filter gains NotSupportedException (issue 3):
catch (System.Exception ex) when (ex is System.Text.Json.JsonException or System.FormatException
    or System.InvalidOperationException or System.NotSupportedException)
```

`sealed` comes off the class (wire extends it). Everything else — `Value()`'s failure story, the container round-trip guard, `Peek`, `Clone`, navigation, `IsTruthy`, `Cacheable` — is untouched and inherited by wire.

### 2. NEW `PLang/app/type/item/wire/this.cs`

Ingi's ruling: wire IS-A item (`wire : source : item.@this`), so it gets its own folder per the @this convention — `type/item/wire/this.cs`, namespace `app.type.item.wire`, class `@this`. (References read `item.wire.@this`; the class body below keeps the name `wire` in prose.)

```csharp
namespace app.type.item.wire;

/// <summary>
/// A still-encoded slice of a larger document — raw text in the CAPTURE's encoding (a .pr
/// value slot), born holding the serializer that captured it. The capture passes ITSELF;
/// a wire never names a format — PLang stays serializer-independent (a bson .pr slices bson;
/// nothing here changes). Materializes through that serializer's reader; writes back verbatim,
/// byte-identical (an untouched relay's signature still verifies). A string token is never a
/// wire — the capture decodes it to bare content (a <see cref="source"/>); only
/// structured/number/bool slices ride here. (Unrelated to the input/output channels.)
/// </summary>
public sealed class @this : global::app.type.item.source
{
    // The format owner that sliced this raw — an object reference, never a name.
    private readonly global::app.channel.serializer.ISerializer _reader;

    public @this(string slice, global::app.type.@this type, actor.context.@this context,
        global::app.channel.serializer.ISerializer reader)
        : base(slice, type, context)
        => _reader = reader ?? throw new System.ArgumentNullException(nameof(reader));

    // ISerializer.Read's contract, reached by the reference held since birth — the lookup
    // (`serializers[mime]`) is what died, not the door.
    private protected override global::app.type.item.@this Read()
        => _reader.Read(this, new global::app.type.reader.ReadContext(Context, Type.Template));

    // Verbatim — already document text, rides inline unquoted, byte-identical.
    public override void Write(global::app.channel.serializer.IWriter w) => w.Raw((string)Raw);

    internal override global::app.type.item.source Declared(global::app.type.@this type)
        => new @this((string)Raw, type, Context, _reader);
}
```

(The inherited `IsVariable` birth check is provably inert on a wire — a structured slice cannot full-match `%x%` — cover with one test.)

### 3. `PLang/app/type/this.cs`

```csharp
// RawFormat (:185-197) — DELETED whole.

// Create signature: the `string? format = null` param GONE:
public item.@this Create(object? raw, global::app.actor.context.@this? context)

// the wire-raw arm:                          // was: new item.source(raw, this, context, format)
if (raw is string or byte[])
    return new item.source(raw, this, context);

// the re-birth arm:                          // was: new item.source(src.Raw, this, context, src.Format)
if (raw is item.source src)
    return src.Declared(this);                // covers wire via its override — capture knowledge survives re-declaration

// the tail:                                  // was: Create(..., context, format)
return Create(context.App.Type.Create(raw, context), context);

// NEW — the capture door beside the content door (same verb; the capture's knowledge as an argument):
/// <summary>A still-encoded slice + the serializer that sliced it — the capture hands
/// over itself. Mints the lazy wire; the parse stays at first touch.</summary>
public item.@this Create(string slice, global::app.actor.context.@this context,
    global::app.channel.serializer.ISerializer reader)
    => new item.wire.@this(slice, this, context, reader);
```

### 4. `PLang/app/data/reader/this.cs` — construct at the point of knowledge

```csharp
// the three locals `deferredRaw` / `deferredFormat` / `born` collapse to one (a source IS an item):
global::app.type.item.@this? value = null;

case "value":
    if (typeRef is { IsNull: false } && typeRef.Name == "goal.call")   // existing eager arm, unchanged (on Ingi's future list)
        value = ctx.Context.App.Type.Reader.Reader("goal.call", null, ctx.Context).Read(ref reader, null, ctx);
    else if (typeRef is not { IsNull: false })
        throw new JsonException(...);                                   // existing no-declared-type guard, unchanged
    else if (reader.Peek() == global::app.channel.serializer.TokenKind.String)
        // a string token is CONTENT — the unescape is the capture's format work; the
        // kind-parse stays lazy on the content source (the %ref% gate needs content at birth)
        value = typeRef.Create(reader.String(), ctx.Context);
    else
        // any other token: a still-encoded slice — the capture passes ITSELF with it
        value = typeRef.Create(System.Text.Encoding.UTF8.GetString(reader.RawValue()), ctx.Context, _owner);
    break;

// tail — the born/deferred twin arms merge:
if (value != null)
{
    var d = new Data(name, value);           // EXISTING born-typed ctor (name, instance)
    if (properties != null) d.Properties = properties;
    return d;
}
// typed-null tail unchanged
```

`_owner` is the serializer this reader reads for — the plang serializer hands itself down when invoking the Data read (ctor or field; plumbing is yours). Quirk to verify in the merge: today's deferred arm does `new Data("", …)` then `data.Name = name` while the born arm passes name directly — confirm `CleanName` makes them equivalent.

### 5. `PLang/app/type/item/path/file/this.Operations.cs`

The declaration stamp stays (`var mime = Context.App.Format.Mime(Extension); var type = Context.App.Format.TypeFromMime(mime);`). These three lines die whole (the mime was crossing twice):

```csharp
var serializers = Context.Actor?.Channel.Serializers;
var serializer = serializers?.GetByType(mime) ?? serializers?.Text;
var format = serializer?.Type ?? "application/plang";
```

Both returns become `type.Create(snapshot, Context)` / `type.Create(bytes, Context)`.

### 6. `PLang/app/channel/this.cs` — `StampValue` and `StampType` die, inlined

Each had exactly one caller (`StampReadAsync:285` → `StampValue:294` → `StampType:312`). Delete both; `StampReadAsync`'s fallback becomes:

```csharp
// Value content: the mime stamps the declaration — {binary, kind} (jpg→image, json→object via
// the kind narrowing); octet-stream / unset → binary, no kind. The type reads its own raw.
var context = Actor?.Context;
var type = Channels?.App?.Format?.TypeFromMime(Mime ?? "")
           ?? global::app.type.@this.Create("binary", null, context: context);
return new global::app.data.@this(Name, type.Create(raw, context), context: context);
```

(The `?? binary` fallback line is existing behavior, existing debt. `StampReadAsync`'s own verb+noun name and its `is plang.@this` serializer type-check stay on the debt list — not this branch.)

### 7. `PLang/app/type/item/dict/serializer/Reader.cs` (+ list, mirrored with `BeginArray`)

```csharp
if (reader.Null()) return new global::app.type.item.@null.@this("dict", kind);
if (reader.Peek() == global::app.channel.serializer.TokenKind.String)
{
    // the text form of me — a container literal is json text; I parse it MYSELF
    // (goal/serializer/Reader.cs pattern: open my parser over my token, re-enter my walk)
    var bytes = System.Text.Encoding.UTF8.GetBytes(reader.String());
    var utf8 = new System.Text.Json.Utf8JsonReader(bytes);
    utf8.Read();
    var inner = new global::app.channel.serializer.json.Reader(utf8);
    return Read(ref inner, kind, ctx);
}
reader.BeginObject();                    // existing walk, unchanged from here
```

"A list literal is json" now exists only inside list's own decode — private business, exposed nowhere. The pathological double-string (`"\"hello\""` declared dict) recurses once, fails as invalid json → MaterializeFailed — terminates; needs a test.

### 8. `PLang/app/type/object/serializer/json.cs` + `PLang/app/type/reader/this.cs`

`json.cs`: static Of-mode → `ITypeReader` with `Kind => "json"`, same decode body over `reader.String()` (the `table/serializer/Reader.cs` csv shape). **Must land with the `TypeOf` fix** (issue 2): `TypeOf`'s scan loops extend over `_runtimeTyped`/`_generatedTyped` keys (or registration keeps a static-table entry) so `{binary, json}` → `object` narrowing survives.

## Demolition worklist (member-by-member; nothing on this list survives the branch)

| Member | Where | Dies with |
|---|---|---|
| `type.@this.RawFormat(object, actor.context.@this)` | `type/this.cs:185-197` | step 3 |
| `format` param on `type.@this.Create(object?, ctx, string?)` | `type/this.cs:261, :329` | step 3 |
| `source._format` field + its comment block | `source.cs:26, :51` | step 1 |
| `source.Format` property | `source.cs:68` | step 1 (sole consumer `type/this.cs:289` → `Declared`) |
| `format` ctor param on `source` | `source.cs:46` | step 1 |
| registry lookup + "channel not wired" throw in `source.Read()` | `source.cs:179-188` | step 1 |
| `Text.Mime` compare in `source.Write` | `source.cs:227` | step 1 |
| `channel.@this.StampValue(byte[])` | `channel/this.cs:294-304` | step 6 |
| `channel.@this.StampType(context)` | `channel/this.cs:307-316` | step 6 |
| `deferredRaw` / `deferredFormat` / `born` locals + twin tail arms | `data/reader/this.cs:39-41, tail` | step 4 |
| file's serializer/format lines | `path/file/this.Operations.cs:73-75` | step 5 |
| `serializer.list.@this.Text` property | `channel/serializer/list/this.cs:130` | after steps 5+6 remove both callers — delete it AND its tests (Ingi's ruling; an orphaned door invites new callers) |
| `UnregisteredMimeType` reachable from materialization | via `Serializers[_format]` | unreachable after step 1 (the type STAYS for channel routing) |

**Stays-list (explicit):** `ISerializer.Read` (the wire kind's door — reached by reference, never lookup); `plang.Read` ≡ `Json.Read` twin bodies (both now legitimate doors; dedup is separate debt); `Text.Read` (likely orphaned after this branch — verify and delete if so); `ResolveForWrite` (channel write policy — different fact, shape flagged as debt); `StampReadAsync` (smaller, still verb+noun — debt); the wire reader's eager string-token decode (settled: format work + the `%ref%` birth gate); `value.Reader` / `json.Reader`; the reader registry; laziness end-to-end.

## Behavior changes + open question

- **The 5 strict-image reds green** ({image,gif} bytes → value dispatch → image reader `Bytes()`), and `{text}`-declared bytes stop throwing too.
- **Settled (Ingi): serialization is a use — a content source writes as its VALUE.** A list literal writes as a list (materialize-on-write, the value renders itself), a number literal writes inline (`42`, was quoted), a template persists as authored, a wire passes through byte-identical. See the `Write` body in §1.
- **OPEN — double-encoded leniency, awaiting Ingi:** a `{dict}`-declared slot arriving as a json *string containing json* (`"value": "{\"a\": 1}"` instead of `"value": {"a": 1}`) now parses via the literal arm where today it fails at first touch. Same mechanism that makes authored container literals work; question is whether tolerating double-encoding producers is a feature or a masked producer bug.

## Sequencing (Ingi's ruling)

`wire-source-split` finishes FIRST, then merges into `navigation-driven-record-builder` (which inherits the greened reds and must not attempt its own stopgap meanwhile).

## Coder verify list

Round-trip every wire slot type write→read→write (number, bool, date, dict, list, item, `@schema:data`, goal.call); byte-identical relay of an untouched signed `.pr` (the `wire` kind's whole reason); `.pr` read incl. the build-snapshot arm (string raw → content source → goal reader); `{object,json}` file read and `{binary,json}` channel read (issues 1+2 together); `%ref%` template sources (should be untouched — `IsVariable` short-circuits before `Read()`); the double-string recursion terminates as MaterializeFailed; the wire's inert `IsVariable`; `Text.Read` orphan check; test doubles implementing `ISerializer`; the `Data("", …).Name = name` vs `Data(name, …)` CleanName equivalence.

## In scope, design pending (Ingi: "I would love to see them gone — the noun+verb tells us")

`StampReadAsync` and `ResolveForWrite` are IN SCOPE for this branch — their replacement designs are being settled with Ingi and will land as a plan section. **Do not start on either until that section exists here.**

## Flagged debt (recorded, NOT this branch)

`plang.Read` ≡ `Json.Read` byte-for-byte duplication. `Text.Read` orphan (delete when confirmed). `IReader`/`json.Reader`/`value.Reader` live under `app.channel.serializer.*` but serve the type layer's own door — namespace home worth a future look. goal.call's eager arm in the wire reader (confirmed out of scope — Ingi's future list). `StampReadAsync`'s `?? binary` fallback fork (survives into whatever replaces it unless that design says otherwise).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `wire : source` | variant kind, not a mode field; each kind owns its Read + Write; born whole at the point of knowledge; single transparent word already meaning "the encoded document" | ✓ |
| no format name in any identifier | added identifiers: `wire`, `slice`, `reader`, `value`, `Declared` — grep-clean of json/mime/format | ✓ |
| `Create(slice, ctx, reader)` capture door | same verb, overload; the capture passes itself (an object), never a name | ✓ |
| `source.Declared(type)` | the source owns its re-birth; kills the type's reach into `src.Raw`/`src.Format` AND the capture-vs-resolved latent bug | ✓ (name is coder's) |
| container literal arm | the knowledge "my literal is json" private to its owner, exposed nowhere | ✓ |
| `object/json` kind reader | matches the existing table/csv shape; kind = the encoding axis, already the registry key | ✓ |
| inlined `StampValue`/`StampType` | two single-caller verb+noun helpers deleted, not re-authored | ✓ |
| no new switches/forks | the string-token arm in the wire reader is capture knowledge (settled); `?? binary` is pre-existing debt | ✓ |
| app-model plang types | no new app-tree leaves; `wire._reader` is C# infrastructure (a serializer reference), not an exposed property | ✓ |

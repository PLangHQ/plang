# Plan: the type layer loses formats — source/wire split, declaration-driven materialization

Branch: `wire-source-split` (from `navigation-driven-record-builder`). Settled with Ingi in session 2026-07-12. **Supersedes** `.bot/navigation-driven-record-builder/architect/format-is-a-birth-fact.md` — that doc's center (a family-owned mime fact) was wrong; do not implement it.

> **Coder review v1 folded (2026-07-12, Ingi's rulings):** the string-slot routing blocker is fixed per **option B — strictness** (Ingi: "I would prefer strictness"): non-template string tokens ride as wires via a NEW verbatim `Slice()` capture on `json.Reader` (see the Slice note in §4) — NOT the token-kind split the review proposed, which would have let `value:"23"` under `{number}` parse leniently against Ingi's invalid-`.pr` ruling. `_owner` resolved per the review (registry `Transport` door at the mint site). Both minors accepted (§1 guard, `FormatException` in plang's serialize catch).

> **Member-level inventory:** [`surface-inventory.md`](surface-inventory.md) — the flat created/deleted/modified tables for quick reference during implementation and the final `[Obsolete]` cleanup.

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
    if (_value is string s && _type.Template != null) { w.String(s); return; }   // coder: `IsVariable ||` was dead — IsVariable is only ever set under Template != null (source.cs:57)
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
    else if (typeRef.Template != null && reader.Peek() == global::app.channel.serializer.TokenKind.String)
        // THE TEMPLATE GATE — the only decode (Ingi: the only time a string token under a
        // non-text type is legal is a builder-stamped %ref%/template). The IsVariable birth
        // gate needs the decoded content; the template rides on the TYPE slot
        // ({name, kind, strict, template} — type/this.json.cs), so typeRef.Template is in hand.
        // reader.String() directly — no GetBytes/GetString round-trip.
        value = typeRef.Create(reader.String(), ctx.Context);
    else
        // EVERY other slot — string tokens included — is a wire: a VERBATIM slice (see the
        // Slice note below — RawValue() decodes strings, so it cannot be used here), with the
        // capturing serializer named at the mint site from the registry's Transport door (the
        // data reader stays stateless — coder's resolution; the wire itself never knows a
        // format name). Face validation is free: the type's own pull IS the validator —
        // json.Reader's number pull (GetDouble) THROWS on a still-quoted string token, so
        // `"23"` under {number} fails right there (Ingi's invalid-.pr ruling, enforced with no
        // catalog); the dict pull fails at BeginObject; a date under {date} pulls String() and
        // lives (a string token IS a date's json face). MaterializeFailed at first touch; the
        // BUILD must never emit a mismatched token.
        value = typeRef.Create(reader.Slice(), ctx.Context,
            ctx.Context.Actor?.Channel.Serializers?.Transport ?? throw ...);
    break;
```

**The Slice note (coder's blocker, Ingi's ruling B).** `json.Reader.RawValue()` DECODES string tokens — `json/reader.cs:120-125` is `UTF8.GetBytes(_r.GetString())`, quotes stripped, escapes resolved (its doc says so: "a string is its UTF-8 content (unescaped)"). Decoded content can never be a wire — wire's contract is "still document text, written back verbatim." So the wire arm needs a NEW verbatim capture on `json.Reader` — suggested name `Slice()` — returning the raw token span *including quotes and escapes*: `Utf8JsonReader.TokenStartIndex` + the owned buffer, the same slicing `RawValue()` already does for object/array tokens; extend it to every token kind. Two coder cares: (1) the buffer-less STJ-nested fallback path (`RawValue`'s doc: "falls back to a single JsonDocument round-trip") needs its own verbatim story — JsonDocument re-serialization normalizes escapes, which breaks byte-identity; if verbatim is unattainable on that path, say so loudly in the code rather than silently normalizing; (2) a relay test with escapes (`"line1\nline2"` slot) proving byte-identical write-back. With `Slice()`, string slots DO relay byte-identical — the coder's "casualty" note is repaired, not accepted.

```csharp

// tail — the born/deferred twin arms merge:
if (value != null)
{
    var d = new Data(name, value);           // EXISTING born-typed ctor (name, instance)
    if (properties != null) d.Properties = properties;
    return d;
}
// typed-null tail unchanged
```

The capturing serializer is named AT THE MINT SITE from the registry's `Transport` door (coder's resolution to the stateless conflict: the data reader is documented stateless — `data/reader/this.cs:13` — and is reached through the static schema registry at `Wire.cs:156`, so nothing can hand itself down without an interface ripple or ReadContext pollution; the mint site looking it up keeps the reader stateless, and the wire itself still never knows a format name). Quirk to verify in the merge: today's deferred arm does `new Data("", …)` then `data.Name = name` while the born arm passes name directly — confirm `CleanName` makes them equivalent.

**Write-side failure story (coder's minor, accepted):** materialize-on-write (`Read().Write(w)` in §1) can throw `FormatException` on a bad runtime-made literal, and `plang.SerializeAsync`'s catch filters only `JsonException or NotSupportedException or IOException` (`plang/this.cs:180`) — add `FormatException` there; the serializer boundary owns its failure story. (`.pr`-borne slots can't hit this under the strictness rulings — the wire arm never decodes.)

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

### 7. dict/list readers — UNCHANGED (the literal arm was cut by Ingi's strictness rulings)

An earlier draft gave dict/list a "string token → parse my own literal" arm. Ingi's rulings deleted the cases it served: a string token under a declared container is **invalid** `.pr` (like `"23"` under `{number}`), and double-encoding producers are **not** tolerated ("we should not build a forgiving parser into our reader" — an LLM-json-fixer, if ever wanted, is a separate explicit parser; an API returning json-in-a-string is the developer's case to handle, via `{text}` + convert or a declared kind). The one honest door for "text content that IS encoded" stays the declared kind: `{object, json}`, `{table, csv}` — where the declaration names the encoding. So dict/list readers keep only their structural walk, and a string token under `{dict}` fails naturally at `BeginObject`.

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
| `channel.@this.StampReadAsync(byte[], ct)` | `channel/this.cs:272-286` | step 9 (replaced by the receive door) |
| `channel.@this.StampValue(byte[])` | `channel/this.cs:294-304` | step 9 |
| `channel.@this.StampType(context)` | `channel/this.cs:307-316` | step 9 |
| `serializer.list.@this.SerializeAsync(SerializeOptions)` + `ResolveForWrite` + `SerializeOptions` | `channel/serializer/list/this.cs:146-168, 195-202` | step 10 |
| `deferredRaw` / `deferredFormat` / `born` locals + twin tail arms | `data/reader/this.cs:39-41, tail` | step 4 |
| file's serializer/format lines | `path/file/this.Operations.cs:73-75` | step 5 |
| ~~`serializer.list.@this.Text` property~~ | `channel/serializer/list/this.cs:130` | REPRIEVED — becomes file-save's content-fallback door (step 10); the earlier delete ruling applied before step 10 gave it a caller |
| `UnregisteredMimeType` reachable from materialization | via `Serializers[_format]` | unreachable after step 1 (the type STAYS for channel routing) |

**Stays-list (explicit):** `ISerializer.Read` (the wire kind's door — reached by reference, never lookup); `plang.Read` ≡ `Json.Read` twin bodies (both now legitimate doors; dedup is separate debt); `Text.Read` (likely orphaned — verify and delete if so; the Text serializer itself STAYS as file-save's content fallback); the template gate's eager string decode (the one decode at capture — the `%ref%` birth gate needs content); `value.Reader` / `json.Reader`; the reader registry + the `{object,json}`/`{table,csv}` kind-reader door; the registry's `Default`/`GetOrDefault`/`GetByExtension` lookups; laziness end-to-end.

## Behavior changes + open question

- **The 5 strict-image reds green** ({image,gif} bytes → value dispatch → image reader `Bytes()`), and `{text}`-declared bytes stop throwing too.
- **Settled (Ingi): serialization is a use — a content source writes as its VALUE.** The value renders itself; a template persists as authored; a wire passes through byte-identical. See the `Write` body in §1.
- **Settled (Ingi): NO leniency.** `value: "23"` under `{number}` and `value: "{\"a\":1}"` under `{dict}` are invalid `.pr` — the build must never emit them; at runtime they fail as MaterializeFailed at first touch via the type's own pull. Double-encoding producers are not absorbed.
- **Settled (Ingi): first-touch enforcement, not load-time.** Rejecting a mismatched token at `.pr` read would need either a face catalog above the types (obpv) or eager parsing of every slot (laziness loss). The type's pull at first touch IS the validator; an untouched slot "is what it is."
- **Settled (Ingi): truthiness answers on the materialized value.** Already the built shape: the async condition door `Data.ToBooleanAsync` resolves ONCE then asks the resolved value (`data/this.cs:684-686`); wires inherit `source.IsTruthy` only as the documented sync fallback. No change needed; coder adds a wire-truthiness test through the async door.
- **String slots now write back byte-identical** (they ride as wires) — better signature fidelity than today's re-quote.
- **Peek/`RawText`/display of an untouched string slot shows the escaped wire form** until first touch — accepted (Ingi: "if it's not touched, then it is what it is"). Coder audits `Peek` consumers on unmaterialized values (debug/display, the build-time kind hook `KindHooks.Of(targetType, p.Peek())`).

## Sequencing (Ingi's ruling)

`wire-source-split` finishes FIRST, then merges into `navigation-driven-record-builder` (which inherits the greened reds and must not attempt its own stopgap meanwhile).

## Coder verify list

Round-trip every wire slot type write→read→write (number, bool, date, dict, list, item, `@schema:data`, goal.call) — string slots byte-identical via `Slice()`, incl. an escapes case (`"line1\nline2"`); the `Slice()` STJ-nested fallback path (verbatim unattainable there → loud, never silent normalization); byte-identical relay of an untouched signed `.pr` (the `wire` kind's whole reason); an invalid-face fixture (`value:"23"` declared `{number}`, string token under `{dict}`) surfaces MaterializeFailed at first touch, named to the binding; dict/list TEMPLATE round-trip through the template gate (template rides the type slot — `type/this.json.cs`); `.pr` read incl. the build-snapshot arm (string raw → content source → goal reader); `{object,json}` file read and `{binary,json}` channel read (issues 1+2 together); `%ref%` template sources; the wire's inert `IsVariable`; wire truthiness through `ToBooleanAsync` (materializes, then answers — incl. the empty-string slice); `Peek` consumer audit on unmaterialized values (debug/display, `KindHooks.Of(targetType, p.Peek())`); the unregistered-extension file-save golden (envelope → content, step 10); `Text.Read` orphan check; test doubles implementing `ISerializer`; the `Data("", …).Name = name` vs `Data(name, …)` CleanName equivalence.

## In scope (Ingi): `StampReadAsync` and `ResolveForWrite` go

### 9. `StampReadAsync` → the channel's receive door

The wrongness was the serializer type-check (`GetByType(Mime) is plang.@this`) — the channel asking "which class is this" to decide protocol. The fact it gropes for is *"is this content the transport container, or a bare value"* — expressed honestly as a comparison against a named door. The registry gains `Transport` beside its existing `Json` door (`public ISerializer Transport => _byType["application/plang"];` — a noun, the transport serializer's one honest name); the channel's receive becomes:

```csharp
// suggested name: Read (it is the channel's receive door; final name is coder's — callers are
// channel/type/{http,stream,file}, all currently calling StampReadAsync)
protected async Task<global::app.data.@this> Read(byte[] raw, CancellationToken ct = default)
{
    var serializers = Channels?.Serializers;
    if (serializers != null && serializers.GetByType(Mime ?? "") == serializers.Transport)
    {
        using var ms = new MemoryStream(raw);
        return await serializers.Transport.DeserializeAsync(ms, cancellationToken: ct);   // full Data, lazy slots
    }
    // Bare value content: the mime stamps the declaration; the type reads its own raw.
    var context = Actor?.Context;
    var type = Channels?.App?.Format?.TypeFromMime(Mime ?? "")
               ?? global::app.type.@this.Create("binary", null, context: context);
    return new global::app.data.@this(Name, type.Create(raw, context), context: context);
}
```

No type-switch, no `StampValue`/`StampType` (§6's inlining lands here), one boundary fact compared at one boundary. The `?? binary` fallback survives as existing debt.

### 10. `ResolveForWrite` + `SerializeAsync(SerializeOptions)` → callers own their selection

`ResolveForWrite` has exactly two callers, each already holding its own selector (verified): the stream channel (`channel/type/stream/this.cs:53`, knows its Mime) and file-save (`path/file/this.Operations.cs:225`, knows its Extension). Selection moves to the owners; the registry keeps lookups only:

- **stream channel:** `Serializers.GetOrDefault(Mime)` — existing door.
- **file-save:** `Serializers.GetByExtension(Extension) ?? Serializers.Text` — a registered extension wins; otherwise **the value writes itself as content** (the Text serializer's `SerializeAsync` is exactly that: `data.Output(writer, …)` — "a leaf renders bare; a container renders via its format text serializer (json string)", its own doc). **Behavior change, flagged:** today an unregistered-extension file save of a structured value writes the plang ENVELOPE (`{name, type, value, signature}`) into the user's file; after this it writes the CONTENT. A user file gets content, not transport envelopes — but it needs a golden check and Ingi's eyes on the diff.
- `SerializeAsync(SerializeOptions)`, `ResolveForWrite`, and the `SerializeOptions` carrier die with their last callers. The `data.Peek() is string` shape-sniff dies with them — it was approximating "the value writes itself" with two hard-coded cases.

(Note: `Serializers.Text` gets a reprieve from deletion — it becomes file-save's content-fallback door. The `Text.Read` orphan check below still stands.)

## Flagged debt (recorded, NOT this branch)

`plang.Read` ≡ `Json.Read` byte-for-byte duplication. `Text.Read` orphan (delete when confirmed). `IReader`/`json.Reader`/`value.Reader` live under `app.channel.serializer.*` but serve the type layer's own door — namespace home worth a future look. goal.call's eager arm in the wire reader (confirmed out of scope — Ingi's future list). The `?? binary` fallback fork in the receive door (step 9 carries it over unchanged).

## OBP validation

| Surface | Check | Verdict |
|---|---|---|
| `wire : source` | variant kind, not a mode field; each kind owns its Read + Write; born whole at the point of knowledge; single transparent word already meaning "the encoded document" | ✓ |
| no format name in any identifier | added identifiers: `wire`, `slice`, `reader`, `value`, `Declared` — grep-clean of json/mime/format | ✓ |
| `Create(slice, ctx, reader)` capture door | same verb, overload; the capture passes itself (an object), never a name | ✓ |
| `source.Declared(type)` | the source owns its re-birth; kills the type's reach into `src.Raw`/`src.Format` AND the capture-vs-resolved latent bug | ✓ (name is coder's) |
| no literal arms, no leniency | a string token under a non-string-faced type is invalid input; the type's own pull is the validator — no face catalog, no forgiving parser | ✓ |
| `Serializers.Transport` door + receive comparison | named noun door; the `is plang.@this` type-switch dies; one boundary fact compared at one boundary | ✓ |
| write selection at the owners | stream owns its Mime, file its Extension; the `Peek() is string` shape-sniff dies with `ResolveForWrite` | ✓ |
| `object/json` kind reader | matches the existing table/csv shape; kind = the encoding axis, already the registry key | ✓ |
| inlined `StampValue`/`StampType` | two single-caller verb+noun helpers deleted, not re-authored | ✓ |
| no new switches/forks | the string-token arm in the wire reader is capture knowledge (settled); `?? binary` is pre-existing debt | ✓ |
| app-model plang types | no new app-tree leaves; `wire._reader` is C# infrastructure (a serializer reference), not an exposed property | ✓ |

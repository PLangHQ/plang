# base64 — architect answer (design approved with corrections, code sketch inside)

Answer to `coder/base64-type-design.md`, settled with Ingi 2026-07-14. The type is approved. Four members of the design die before birth, one new item-base virtual comes in, and the encode recipe changes — writing the real code surfaced a data-corruption bug in the `item.Output → bytes` plan (break #1 below).

> **You own this.** Every code block here is a suggestion sketch — traced against the tree as of `999f0035d`, but you own the final shape, naming inside method bodies, and the tests. If a sketch contradicts something you find in the code, the code wins; flag the contradiction rather than following the sketch.

Read `Documentation/v0.2/defining-plang-types.md` FIRST — rewritten from the current code on this branch (`999f0035d`). The recipe your design cites (`Convert(value, kind, context)` doors, `app/type/<name>/` paths) no longer exists; construction is the three `ICreate` faces and value types live under `app/type/item/`.

## Rulings (the design doc's verdicts and open questions)

1. **`EncodeLazily` dies.** Laziness is construction state, not a method: the courier arm is `new @this(it)` — the base64 holds the source item whole and encodes at its `Value` door, exactly the `image` pattern (holds `Path`, materializes at `Value`, caches). A method named for laziness is also a naming violation twice over (verb+adverb compound; names the mechanism, not the caller's intent) and a lie — a method call executes now, only a constructed value defers.
2. **`FromString` dies.** One caller = the arm lives inline in `Create` (a shared string-parse core between `Create` and the wire reader is legitimate — that is `Parse` below, two callers). No named factories beside the `ICreate` doors.
3. **Public `string Value` dies.** Private `_value` backing per `text`'s discipline — content leaves the type only via `Write(IWriter)`, the typed ops, or the doors. CLR types are known to base64 in exactly two places: the Create doors (birth) and the `Clr`/`RawBytes` exit faces. No other member sees a string or byte[].
4. **`Type` reporting `{image, gif}` dies — it breaks wire round-trip identity.** The born path picks the reader by the DECLARED type name (`source.Read` → `App.Type.Reader.Reader(_type.Name, …)`), so a base64 writing `type:{image,gif}` reads back through image's reader as an `image.@this`: `is base64` true before the wire, false after. Instead: **`Type` is always `{base64, kind?}`** and the data-url mime rides as the Kind token (`data:image/gif;…` → `{base64, gif}`). This is the existing binary pattern — "binary content carries its true family in the Kind (jpg→image); the Name is just binary" (`type/this.cs`, `Compressible`). `as image` is the explicit hop to an image value.
5. **`data:` parsing lives on base64 (Ingi's ruling) — and leaves image entirely.** Having it in image is the wrong location. Demolition list below; grep-verified the whole data-url path in `image/this.Parse.cs` has zero callers today (the W8 finding).
6. **Your piece 3 ("binary.Convert gains the decode arm") already exists.** `binary.Create`'s core decodes base64 strings after the item unwrap (`binary/this.cs:34-36`), and base64's `Clr` hands the payload string — `base64 as binary` is zero new code in binary. Do not build a second door, and never reach into another type's backing (`Decode(s.Value)` in the design dies with the public property).
7. **Encode-vs-validate — your doc contradicted itself** (line 57 "always encodes" vs pin 88 "SGVsbG8= → valid"). Resolved structurally: **encode lives ONLY in the courier** (`as base64` / a typed slot fed from an in-memory value); **validate lives ONLY in `Parse`** (the reader/born path, and comparison coercion via the pure core). `"SGVsbG8=" as base64` ENCODES (→ `"U0dWc2JHOD0="`); a slot *declared* base64 holding `"SGVsbG8="` validates and keeps the payload. No content sniffing anywhere — `data:` is the only marker, and it is explicit syntax.
8. **Open q2 (encode format param): deferred.** Default json via the actor's registered serializer; no `format` param now.
9. **Open q3 (born-door sniffing): confirmed none.** A bare untyped `"data:…"` literal stays text; base64 only arises typed (`as base64`, a declared slot/param, a wire type).

## Error policy (Ingi's flag — throw where there is no data.Fail)

An invalid base64 string, or a value the type cannot build, is an ERROR, never a silent null — reported through whichever channel the door has:

- **`Parse` (no data in scope) THROWS `FormatException`** with the specific reason (malformed data-url, non-;base64 data-url, invalid payload). On the born path the throw rides `source.Value`'s existing `FormatException` catch (`source.cs:159`) into `MaterializeFailed`, named to the binding — validation-at-the-door for free.
- **The courier (has data) catches `Parse`'s throw and converts to `data.Fail`** (`Base64Invalid`), and Fails typed on a source it cannot encode (`Base64ConversionFailed`).
- **Comparison** catches `FormatException` locally and answers `Incomparable` — per the item base's own contract, a non-coercible operand is not an error (`item/this.cs:79-81`).
- The pure core still declines (`null`) on a non-string — type mismatch is a decline; a malformed VALUE throws.

## The code

### `app/type/item/base64/this.cs` (NEW)

```csharp
namespace app.type.item.base64;

/// <summary>
/// PLang <c>base64</c> value — an encoded string payload, possibly content-tagged
/// (a data-url's mime rides as Kind). String face = the payload; byte face = the
/// decoded bytes. NOT a binary subtype: binary is raw bytes, base64 is an encoding.
/// </summary>
public sealed class @this : global::app.type.item.@this, global::app.type.item.ICreate<@this>
{
    public static string Example => "SGVsbG8=";
    public static string Shape => "string";
    public static string Description =>
        "A base64-encoded payload (REST binary fields, data-urls). `as base64` ENCODES the "
        + "source value (lazily); a field/param typed base64 validates its payload at read. "
        + "Kind carries the content family from a data-url mime (gif, png, json, ...).";

    // THE backing — private, per text's discipline. Null only while a held source
    // awaits its encode at the Value door.
    private string? _value;
    // The item to encode — held WHOLE at construction (laziness is state, not a
    // method); resolved once at the Value door. Null when born of a payload.
    private readonly global::app.type.item.@this? _source;

    /// <summary>Content-family token off a data-url mime ("gif", "json"); null for a bare payload.</summary>
    public string? Kind { get; init; }

    protected internal override global::app.type.@this Type
        => new("base64", typeof(string)) { Kind = Kind is { } k ? new global::app.type.kind.@this(k) : null };

    public @this(string value) { _value = value; }
    private @this(global::app.type.item.@this source) { _source = source; }

    /// <summary>
    /// The one string-parse home, shared by the pure core and the wire reader: a
    /// <c>data:&lt;mime&gt;;base64,&lt;payload&gt;</c> unwraps (mime tail → Kind); a bare
    /// string must BE valid base64 — this is the validate door, never an encode.
    /// THROWS <see cref="System.FormatException"/> on anything malformed (no data.Fail
    /// in scope here; the born path's source.Value catch turns it into MaterializeFailed).
    /// </summary>
    internal static @this Parse(string raw)
    {
        if (raw.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
        {
            var comma = raw.IndexOf(',');
            if (comma < 5) throw new System.FormatException(
                "malformed data-url — no ',' separating header from payload.");
            var header = raw[5..comma];                       // e.g. image/gif;base64
            if (!header.EndsWith(";base64", System.StringComparison.OrdinalIgnoreCase))
                throw new System.FormatException(
                    "data-url is not ;base64-encoded — only base64 data-urls are a base64 value.");
            var payload = raw[(comma + 1)..];
            if (!System.Buffers.Text.Base64.IsValid(payload))
                throw new System.FormatException("data-url payload is not valid base64.");
            var mime = header[..^7];
            var slash = mime.IndexOf('/');
            var kind = slash >= 0 ? mime[(slash + 1)..] : null;
            return new @this(payload) { Kind = kind == "octet-stream" ? null : kind };
        }
        if (!System.Buffers.Text.Base64.IsValid(raw))
            throw new System.FormatException("not a valid base64 payload.");
        return new @this(raw);
    }

    /// <summary>THE PURE CORE — pass-through; a string parses (data-url or valid payload;
    /// malformed throws per the error policy). Serves comparison coercion; the courier
    /// below owns the encode semantics. A non-string declines (type mismatch, not an error).</summary>
    public static @this? Create(object? raw)
    {
        if (raw is @this self) return self;
        object? value = raw is global::app.type.item.@this rit ? rit.Clr<object>() : raw;
        return value is string s ? Parse(s) : null;
    }

    /// <summary>The courier — <c>as base64</c> / a typed slot fed from memory ALWAYS ENCODES:
    /// any item is held whole and encodes at the Value door. The one exception is a string
    /// face holding a data-url — an explicit unwrap ask, not content to encode. A value that
    /// already IS base64 arrives typed via the reader, never through here.</summary>
    public static @this? Create(object? value, global::app.data.@this data)
    {
        if (value is @this self) return self;
        var s = value as string ?? (value as global::app.type.item.text.@this)?.ToString();
        if (s != null && s.StartsWith("data:", System.StringComparison.OrdinalIgnoreCase))
        {
            try { return Parse(s); }
            catch (System.FormatException ex)
            {
                data.Fail(new global::app.error.Error(ex.Message, "Base64Invalid", 400));
                return null;
            }
        }
        // A raw CLR string (the entity door's leaf-retype lowers text before calling here)
        // has no door to defer to — content in hand, encode now.
        if (value is string raw)
            return new @this(System.Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(raw)));
        // ANY item — text, image, dict, binary — is held whole; ONE lazy path, the door encodes.
        if (value is global::app.type.item.@this it) return new @this(it);
        data.Fail(new global::app.error.Error(
            $"Cannot create base64 from {value?.GetType().Name ?? "null"}.", "Base64ConversionFailed", 400));
        return null;
    }

    /// <summary>
    /// The encode door. A held source materializes through ITS door, then encodes: its own
    /// byte face (<see cref="RawBytes"/>) when it has one, else its bare json wire via the
    /// actor's registered serializer. Once, cached — mirrors image's load-at-Value.
    /// </summary>
    public override async System.Threading.Tasks.ValueTask<global::app.type.item.@this> Value(
        global::app.data.@this data)
    {
        if (_value != null || _source == null) return this;
        var ready = await _source.Value(data);
        if (!data.Success) return Absent;
        byte[] bytes;
        if (ready.RawBytes is { } raw) bytes = raw;
        else
        {
            using var ms = new System.IO.MemoryStream();
            var carrier = new global::app.data.@this("", ready, context: data.Context);
            var written = await data.Context.Actor.Channel.Serializers.Json
                .SerializeAsync(ms, carrier, global::app.View.Out);
            if (!written.Success)
            {
                data.Fail(written.Error ?? new global::app.error.Error(
                    "could not serialize value for base64 encode.", "Base64EncodeFailed", 400));
                return Absent;
            }
            bytes = ms.ToArray();
        }
        _value = System.Convert.ToBase64String(bytes);
        return this;
    }

    public override bool IsLeaf => true;
    public override void Write(global::app.channel.serializer.IWriter w) => w.String(_value ?? "");
    public override string ToString() => _value ?? "";
    public override string? RawText => _value;

    /// <summary>base64's byte face IS its decoded bytes — the type's whole meaning
    /// (string face = encoded, byte face = decoded).</summary>
    public override byte[]? RawBytes => _value is { } v ? System.Convert.FromBase64String(v) : null;

    /// <summary>Empty payload (and nothing pending encode) is falsy.</summary>
    public override bool IsTruthy() => !string.IsNullOrEmpty(_value) || _source != null;

    public override global::app.type.item.@this Kinded(string? kind)
        => _value is { } v ? new @this(v) { Kind = kind } : this;

    /// <summary>CLR exit — a byte[] target gets the DECODED bytes; anything else the payload string.</summary>
    internal override object? Clr(System.Type target)
        => target == typeof(byte[]) && _value is { } v
            ? System.Convert.FromBase64String(v)
            : ClrConvert(_value, target);

    /// <summary>Between text (100) and binary (250): drives a text compare (payload identity,
    /// case-SENSITIVE — base64 is); binary drives byte equality (already works today: binary's
    /// core decodes our payload string after the unwrap).</summary>
    public override int Rank => 200;

    /// <summary>Payload identity, ordinal. A non-coercible/malformed other is Incomparable,
    /// not an error — the compare-local catch per the error policy.</summary>
    protected override System.Threading.Tasks.ValueTask<global::app.data.Comparison> Order(
        global::app.type.item.@this other)
    {
        var b = other as @this;
        if (b is null)
        {
            try { b = Create(other); }
            catch (System.FormatException) { return new(global::app.data.Comparison.Incomparable); }
        }
        if (b?._value is null || _value is null) return new(global::app.data.Comparison.Incomparable);
        var c = string.CompareOrdinal(_value, b._value);
        return new(c < 0 ? global::app.data.Comparison.Less
                 : c > 0 ? global::app.data.Comparison.Greater
                 : global::app.data.Comparison.Equal);
    }
}
```

### `app/type/item/base64/serializer/Reader.cs` (NEW)

```csharp
namespace app.type.item.base64.serializer;

public sealed class Reader : global::app.type.reader.ITypeReader
{
    public string Kind => global::app.type.reader.@this.AnyKind;

    public global::app.type.item.@this Read<TReader>(ref TReader reader, string? kind,
        global::app.type.reader.ReadContext ctx)
        where TReader : global::app.channel.serializer.IReader, allows ref struct
    {
        if (reader.Null()) return new global::app.type.item.@null.@this("base64", kind);
        // Validation home: a slot DECLARED base64 must hold one. Parse's FormatException
        // rides source.Value's catch → MaterializeFailed, named to the binding.
        var b64 = @this.Parse(reader.String());
        return kind != null && b64.Kind == null ? b64.Kinded(kind) : b64;
    }
}
```

### `item.@this` — one NEW virtual (beside `RawText`, `item/this.cs:363`)

```csharp
/// <summary>The raw byte form this value carries (binary's bytes, an image's loaded
/// bytes, text's UTF-8 content), null when the value has no byte face — a container
/// serializes through a format instead. Mirror of RawText.</summary>
public virtual byte[]? RawBytes => null;
```

Overrides: `binary` → `Value`; `image` → `_bytes` (null until loaded — the encode door materializes first, which is exactly right); `text` → `System.Text.Encoding.UTF8.GetBytes(_value)`; base64's own is in the sketch above (decoded bytes).

### `image.Create` core — one NEW arm, BEFORE the item unwrap (`image/this.cs:111-116`)

The unwrap lowers a base64 to its payload STRING, which then dies in the scheme registry — so the typed arm must come first:

```csharp
if (raw is global::app.type.item.base64.@this b64)
    return b64.RawBytes is { } bts ? FromBytes(bts) : null;   // mime sniffed off magic bytes, as today
```

### `binary` — zero changes

`base64 as binary` works through existing doors: binary's core unwraps via `Clr<object>` (→ the payload string) and its `case string s:` arm base64-decodes (`binary/this.cs:30-36`).

## Why the encode recipe changed (breaks found writing real code)

1. **`item.Output → bytes → base64` corrupts leaves.** The json writer QUOTES a text (payload becomes base64 of `"hello"` with quotes) and image's json `Write` already emits base64 of its bytes (`image/this.cs:69`) — so `%image% as base64` would DOUBLE-encode, silently shipping corrupt data. The encode needs the value's *byte face*, hence `RawBytes`; the json wire is only the structured fallback (dict/list/domain values, which is where "the item's json wire" was the right intuition).
2. **base64 cannot construct a `Utf8JsonWriter`** — a concrete format inside a value type is the §6 format leak. The blessed door is the actor's registered serializer: `data.Context.Actor.Channel.Serializers.Json.SerializeAsync(stream, carrier, View.Out)` (bare view — the value writes itself, no envelope).
3. **Two construction entrances feed the courier differently**: `As<base64>` hands the ITEM (text arm → lazy); the entity door's leaf-retype lowers to raw CLR first (`type/this.cs:297-299`), handing a STRING (eager arm). Same result either way — test both doors.

## Demolition

Die before birth (from the design doc): `EncodeLazily`, `FromString`, public `string Value`, the `binary.Convert` base64 arm, the `Type = {image,gif}` report.

Die in `image/this.Parse.cs` (zero callers outside the file, grep-verified): `Resolve`, `ResolveAsync`, `FromDataUrl`, `MimeFromExtension`, `HasImageExtension`.

Stay in image: `FromBytes` (the core's byte[] arm + the new base64 arm; keeps its name only because catalog reflection can't disambiguate same-name static overloads — documented exception), `SniffMime` (FromBytes' mime probe). With the Parse file reduced to those two, consider folding them into `this.cs` — your call.

## Pins (corrected + extended)

- Slot/param DECLARED base64 holding `"SGVsbG8="` → validates, payload kept. Invalid payload → `MaterializeFailed` naming the binding.
- `"SGVsbG8=" as base64` → ENCODES → `"U0dWc2JHOD0="` (the old pin 88 was the reader-door story, not the `as` story).
- `"data:image/gif;base64,R0lG…"` (typed or `as base64`) → `Type = {base64, gif}`, payload = `"R0lG…"`. A data-url that is not `;base64` → `Base64Invalid` (courier) / `MaterializeFailed` (born).
- `%dict% as base64` → lazy; `.Value` = base64 of the dict's bare json wire.
- `%image% as base64` → base64 of the image BYTES (path-backed image loads at the door first) — NOT double-encoded.
- `%b64% as binary` → decoded bytes; `%b64% as image` → image, mime sniffed off magic bytes.
- `if %b64% == "SGVsbG8="` → payload compare, case-sensitive; a non-base64 other → Incomparable.
- A lazy base64 whose `Write` fires before `Value` emits `""` — same contract as an unloaded image (the serializer materializes through `Value` first).
- Registration: auto by the `@this` convention (`app.type.item.base64`); verify `app.Type["base64"]` resolves.

## Known edge, deliberately out of scope

`%file% as base64` encodes the file's MATERIALIZED value (a config.json → base64 of the parsed json wire), not its raw on-disk bytes. If raw-file-bytes is the wanted meaning, that is a `path`/`file.RawBytes` question — flag it back if you hit it in tests; do not solve it inside base64.

## OBP validation (new surfaces)

| Surface | Check | Verdict |
|---|---|---|
| `Parse` | one verb; one home shared by two real callers (core + reader) | ok |
| `RawBytes` | noun face, mirror of existing `RawText`; exit door, not a leak | ok |
| `Kind` | matches binary/text's stamped-at-creation kind | ok |
| `Create` ×2 + pure core | the ICreate faces, no extra doors | ok |
| `_source` | held whole, no pre-decomposition; laziness = state at the door | ok |
| `_value` private | CLR string known only at Create/Parse (birth) and Clr/RawBytes (exit) | ok |
| `Rank`/`Order` | value's own comparison; malformed other → Incomparable, not error | ok |
| No helper class, no named factory, no type-switch outside Create arms | | ok |

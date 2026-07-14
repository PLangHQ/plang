# `base64` — a new plang value type (for architect review)

From coder, settled with Ingi 2026-07-14 (the W8 tail — data-url handling). This is the design
we converged on in conversation; sending it up for an architect pass before I build. It also
serves as the worked "how hard is it to add a plang value type" example (answer: 4 small pieces,
`Documentation/v0.2/defining-plang-types.md` is the recipe).

## Why a type, not a special case

W8 deleted the `image.Build` data-url hook and revealed data-urls never actually constructed
(`data:` isn't a registered scheme; `image.Create` declined — the hook stamped `kind:gif` onto an
unbuildable value). Rather than bolt `data:` parsing onto `binary` or `image`, Ingi's call is a
first-class `base64` type. His four reasons:

- **Validation** — `base64` rejects a non-base64 string at the door; `binary` can't (any bytes are
  "valid").
- **Separation** — `data:` parsing lives on `base64`, out of `binary`/`image`.
- **LLM contract** — a property typed `base64` tells the model "send/return a base64 string" — the
  way REST APIs describe these fields; `binary` says nothing about the encoding.
- **Real-world fit** — base64 string fields are everywhere in APIs; naming the concept is honest.

`binary` = raw bytes (I/O, crypto). `base64` = an encoded string, possibly MIME-tagged. Different
meaning, validation, and LLM shape. `base64` is **string-backed**, NOT a `binary` subtype.

## The model

`base64.@this : item.@this, ICreate<@this>`

- **`string Value`** — the base64 payload (the `data:<mime>;base64,` wrapper stripped). Name-what-it-is:
  the value of the item is its `Value` property. No `byte[]`.
- **`Type` is read off the string it was made from** — the MIME is a **type+kind pair**, not a lone
  kind string:
  - `data:image/gif;base64,R0lG…` → parse `data:` → **type=`image`, kind=`gif`**, `Value = "R0lG…"`
    (via `Format.TypeFromMime("image/gif")`).
  - bare `"SGVsbG8="` (no `data:`) → **type=`base64`**, `Value = "SGVsbG8="` (validated base64).
  - `%anyItem% as base64` (encode) → **type=`base64`**, `Value` = the encoded bytes.
  - So a data-url base64 **reports `{image, gif}`** to downstream code — it simply *is* an image (kind
    gif) whose bytes happen to be base64-stored. The `base64.@this` class owns the encoding/storage;
    the reported `Type` reflects the content.
- **`IsLeaf = true`**, `Write(w) => w.String(Value)`, `Clr` hands the string, `ToString => Value`
  (a data-url is reconstructed from type+kind+Value only on an explicit ask — not the default wire).

### Construction — one door, plang types only

`Convert` receives only plang types (never a raw CLR string) — match `text`/`item`, not `string`:

```csharp
// base64.Convert(value, kind, ctx)
value switch {
    base64 self => self,                              // passthrough
    text t      => FromString(t, ctx),                // "data:…" → {mimeType, mimeKind}+Value ;
                                                      //   bare → {base64}+Value (validate; non-base64 → error)
    item  it    => EncodeLazily(it, ctx),             // ANY item → item.Output → bytes → base64 (fires at .Value)
}
```

**`as base64` always ENCODES, and it's lazy.** `%dict% as base64` stays a source; the encode fires at
`.Value` (`- write out %x%` emits `"SGV…"`). `"SGVsbG8="` is a `text`, so `"SGVsbG8=" as base64` takes
the identical path and **encodes** it. A value that is *already* base64 arrives **typed base64** via the
reader (the `.pr`/wire/an API response field), never by converting a text — same-path, no ambiguity.

**Encoding bytes = `item.Output`.** Each item contributes its own wire bytes (a `text` → its content,
a structured item → its JSON wire). Default JSON (industry standard); Ingi noted we may later let the
caller pick another output format — flag for the architect: is a `format` param on the encode worth
designing in now, or add later?

### `binary` gains the decode arm — `binary` owns base64 → bytes

```csharp
// binary.Convert(value, …)  — new arm
base64 s => new binary(Decode(s.Value))
```

Each target owns its own construction (OBP): `base64.Convert` knows how to ENCODE a source into base64;
`binary.Convert` knows how to DECODE a base64 into bytes; `image.Convert` accepts a base64 (decode +
read the mime off its `{image,gif}` type) → image. `data:` knowledge never leaks into `binary`/`image`.

## The 4 pieces (the recipe)

1. `app/type/item/base64/this.cs` — the value class above.
2. `app/type/item/base64/serializer/Reader.cs` — reads the base64 string back (auto-registers by
   namespace, per the doc).
3. `binary.Convert` — the `base64 → bytes` arm (and `image.Convert` accepts a base64).
4. Register `base64` in the type catalog.

## Pins

- `"SGVsbG8="` → `base64`, valid; a non-base64 string → `Base64Invalid` error.
- `"data:image/gif;base64,R0lG…"` → `Type = {image, gif}`, `Value = "R0lG…"`.
- `%dict% as base64` → lazy; `.Value` yields the base64 of the dict's JSON output.
- `base64 as binary` → decodes to the original bytes; `base64 as image` (from a data-url) → image kind gif.

## Open questions for the architect

1. **The `Type`-reports-`{image,gif}` shape.** A `base64.@this` whose `Type.Name == "image"` — is a
   value class reporting a type name other than its own folder name acceptable here (mirrors how the
   value carries a mime-derived identity), or does the architect want the data-url to build the mime's
   *actual* type (`image.@this`) with the base64 held some other way? My read of Ingi is the former
   (base64 is the storage; the mime is the reported identity), but it's the one shape worth a ruling.
2. **Encode output format.** Bake a `format` selector into the encode now, or default JSON and add later?
3. **Born-native sniffing.** Keep it type-directed (`as base64` / a typed param), i.e. a bare untyped
   `"data:…"` literal stays `text`? (My recommendation — no born-door sniffing.)

Nothing here is committed — awaiting the architect's pass, then I build.

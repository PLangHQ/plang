# `base64` — a new plang value type (v2, redesigned against the rewritten types doc)

From coder. Settled model with Ingi 2026-07-14; **redesigned against the architect's rewrite of
`Documentation/v0.2/defining-plang-types.md`** (999f0035d). The v1 of this doc predated that rewrite
and violated three of its rules — corrected here. Still a proposal; awaiting an architect pass, then I build.

## What the rewrite forced to change (v1 → v2)

- **`Convert` is gone** → construction is the three `ICreate<@this>` doors: `Create(raw)` (pure core,
  context-free, decline = `null`), `Create(raw, ctx)` (override only if it resolves against an actor),
  `Create(raw, data)` (the courier — lands decline reasons on `data.Fail`).
- **Own-name `Type` rule** → a value's `Type.Name` MUST be its own folder name; the declared name
  selects the reader on read-back, so a value reporting `{image, gif}` does not round-trip as itself.
  **v1 had `base64` report `{image, gif}` for a data-url — illegal.** v2: `base64` reports `{base64, kind}`.
- **Laziness is state, not a method** → no `EncodeLazily(...)`; the encode-an-item case holds its source
  item in a field at construction and encodes at the `Value(data)` door (the `image` path-backed model).
- **No named factory beside `Create`** (`FromString`, `FromDataUrl`) → those are `Create`'s own switch arms.
- **Backing is a private field**, not a public `Value` property; content leaves via `Write`/`Clr`/ops.

## The type

`base64.@this : item.@this, ICreate<@this>` — string-backed (NOT a `binary` subtype).

- **Backing:** `private readonly string _value` — the base64 payload (the `data:<mime>;base64,` wrapper
  stripped). For the encode case it also holds `private readonly item.@this? _source` (the value to
  encode), resolved at the `Value` door — exactly how `image` holds an optional `Path` and materializes
  at `Value`.
- **`Type`** → `new("base64", typeof(string)) { Kind = _mime }` — **own name `base64`, always**. The MIME
  from a data-url rides as the **Kind** (base64's own kind axis), never as a borrowed type name.
- `IsLeaf = true`; `Write(w) => w.String(ToString())`; `Clr(string)` hands `_value`; `ToString()` = the
  base64 string (or the reconstructed `data:<mime>;base64,<_value>` when the kind carries a mime — the
  form you send back out).

### The forced decision — the mime, now that own-name blocks `{image, gif}`

A data-url can no longer make `base64` *report* `image`. Two doc-legal shapes; I need a ruling:

- **(A) `base64` owns `data:`; the mime is its Kind.** `data:image/gif;base64,R0lG…` → `base64` value,
  `Type = {base64, kind: image/gif}` (or subtype `gif` — sub-question below), `_value = "R0lG…"`. To get an
  image, `%x% as image` → `image.Create(base64)` decodes the bytes and reads the mime off the kind.
  Keeps `data:` parsing on `base64` (your stated want) + validation + the LLM contract; `base64` stays `base64`.
- **(B) a data-url builds the mime's *actual* type.** `data:image/gif;…` → a real `image.@this` (`{image, gif}`),
  bytes decoded from the base64; `base64` is then *only* for bare/mime-less base64. Honors "type=image, kind=gif"
  literally, but `data:` parsing moves off `base64` onto a born-door recognizer, and `base64` no longer owns `data:`.

**My lean: (A).** It's the cohesive one — `base64` is the encoding type, the mime is its kind, sibling types
convert *from* it; it satisfies "data: parsing lives on base64, not binary/image" under the own-name rule,
where v1's "base64 reports image" no longer can. Sub-question if (A): Kind = the full mime `image/gif`
(exact data-url round-trip, `image` derives `gif` from it) or the subtype `gif` (kind convention, but loses
the family)? I lean full mime for fidelity.

## Construction — the three `ICreate` doors

```csharp
// Create(object? raw) — the pure core (context-free, decline = null)
public static @this? Create(object? raw)
{
    if (raw is @this self) return self;                                   // passthrough
    object? v = raw is item.@this rit ? rit.Clr<object>() : raw;          // another item exits via ITS Clr
    return v switch
    {
        string s when s.StartsWith("data:") => ParseDataUrl(s),           // → _value + _mime (an arm, not a factory)
        string s when IsBase64(s)           => new @this(s),              // bare base64, validated
        string                              => null,                      // a non-base64 string → decline
        _                                   => null,                      // encode case handled by the courier (needs Output)
    };
}

// Create(object? raw, data) — the courier: land decline reasons; own the ENCODE case (lazy)
// A non-base64/non-string item (dict/list/…) → hold it as _source; encode at the Value door.
// `data.Fail(Base64Invalid)` when a string isn't valid base64.
```

**Encoding `%item% as base64` is lazy state, not a method.** The courier constructs a `base64` holding the
source item in `_source`; at the `Value(data)` door it writes the item (`item.Output`, default the JSON wire)
to bytes and base64-encodes → `_value`, caches. `- write out %x%` then emits `"SGV…"`. `"SGVsbG8="` is a
`text`, so `"SGVsbG8=" as base64` takes the same courier path; an already-base64 value only arrives *typed*
`base64` via the reader (wire / API field) — no separate door, no ambiguity. (Open: bake an output-format
selector into the encode now, or default JSON and add later?)

## Reading and cross-type conversion

- **Reader:** `app/type/item/base64/serializer/Reader.cs` (`ITypeReader`, auto-registers) — pulls the base64
  string off `IReader` and builds the value. (A whole-payload `serializer/Default.cs` `Read` only if a file's
  base64 *content* needs to decode through `source.Value` — likely not needed; flag.)
- **`base64 → binary`:** `binary.Create`'s existing core already lowers another item via its `Clr` and its
  `case string s` does `FromBase64String` — so a `base64` whose `Clr<string>` returns `_value` decodes through
  binary's *existing* arm. **No new binary code needed** (confirm at build).
- **`base64 → image`:** `image.Create(base64)` — decode the bytes and read the mime off the base64's kind →
  `{image, gif}`. `data:` knowledge never enters `image`; it consumes a `base64`.

## Pins

- `"SGVsbG8="` → `base64`, valid; a non-base64 string → `Base64Invalid` via the courier's `data.Fail`.
- `"data:image/gif;base64,R0lG…"` → `Type = {base64, kind: image/gif}` (per A), `_value = "R0lG…"`; `ToString`
  round-trips the data-url.
- `%dict% as base64` → lazy; `.Value` yields the base64 of the dict's JSON output; a re-read is cached.
- `base64 as binary` → the original bytes (via binary's existing base64 arm); `base64 as image` (data-url) → image kind gif.

## Questions for the architect

1. **(A) vs (B)** above — the mime-as-kind vs data-url-builds-the-actual-type choice the own-name rule forces.
2. If (A): Kind = full mime `image/gif` or the subtype `gif`?
3. Encode output format — a selector now, or default JSON and add later?
4. Confirm `base64 → binary` rides binary's existing `case string s` core (no new binary arm), given `base64.Clr<string>()` returns `_value`.

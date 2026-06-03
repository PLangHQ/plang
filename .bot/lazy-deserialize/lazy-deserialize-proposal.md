# Lazy deserialize — data is just data, read only when touched

**Status:** design proposal for architect review. No code yet.
**Branch:** `lazy-deserialize` (off `type-kind-strict`).
**Author:** coder, from a design session with Ingi.

---

## The one-line model

A `Data` is just `{ bytes, type, kind, value }`. The `value` is computed **once,
on first touch**, from `bytes`, using `type`+`kind` to decide how. Nothing is
parsed at read time. Reading from disk / http / a channel produces bytes with a
type stamp — never a parsed object — until something actually needs the value.

There is **no envelope and no wrapping**. Data is the data. You never "unwrap" a
Data; you read its bytes through its type when (and only when) you touch it.

---

## Why touch deserialize at all

Two problems on `type-kind-strict` today:

1. **Serialize and deserialize are asymmetric.** Serialize is clean: each type
   owns its renderer (`type → Writer(format)`), gated so an unrenderable value
   falls back to reflection rather than crashing. Deserialize has no mirror — it's
   a fallback chain (`FromWire` → JSON-deserialize → `Convert.ChangeType`) plus a
   leftover `path.JsonConverter`. "The type knows how to read itself" is only
   half-built (the write half). A second wire format would need the read side
   rebuilt from scratch.

2. **Reads parse eagerly and inconsistently.** `file.read` sometimes returns a
   string, sometimes converts at read time. There's no single "here is where bytes
   become a typed value" seam, so the decision is scattered.

(Plus a small wart: `float` is advertised as a number kind but never stored —
it collapses to `double` immediately. There is no `System.Single` anywhere in the
value model. Fix: drop `float` from the number kinds; it *is* `double`.)

---

## The model

### Data is just data

```
Data { bytes, type, kind, value }
```

- `bytes` — the raw payload, exactly as it came off the wire. (Called `bytes`,
  not `_raw` — no mental mapping.)
- `type`, `kind` — what the bytes are. Stamped at the read boundary.
- `value` — computed lazily from `bytes` on first access; cached after.

No `pending` flag, no mode enum. `type`+`kind` already say everything about *how*
to read the bytes; the only internal state is the ordinary "value not computed
yet." Laziness here is the same laziness as anywhere else in PLang: **execution
happens only when needed.**

### One read boundary: `channel.read`

`file.read` and `http.get` stop deserializing. They become thin source-providers:
open a stream, hand it to `channel.read`. `channel.read` is the single place where
bytes + type/kind become a `Data`.

```
file.read 'config.json'  ─┐
http.get  'https://…'    ─┼──►  channel.read  ──►  Data { bytes, type, kind, value(lazy) }
native channel           ─┘     (the ONE boundary)         │
                                                           type/kind decided here, from:
                                                            • extension / Content-Type   (config.json → text, json)
                                                            • the payload's own header    (report.plang → parsed type/kind)
```

This dissolves the original "is the incoming a data object or a regular object?"
question. It is **always** `Data{bytes, type, kind}`. The `.plang` case is not a
special wrapped thing — it just means type/kind came from the payload's first
bytes instead of from the extension. Same shape, nothing to unwrap.

Feasibility: channels already carry a `Mime`, there is a stream-backed channel
(`app/channel/stream/`), and channel `Read` already produces a `Data` stamped from
`Mime`. So routing `file.read`/`http.get` through `channel.read` fits the existing
channel-as-I/O design rather than fighting it.

### First touch materializes — the type reads its own bytes

```
later — FIRST touch of value:
   value := type.Read(kind, bytes)        ← the type reads its own bytes, then caches

   %cfg.port%   → kind=json → parse text → structure → .port
   %photo%      → type=image → materialize the image
   %file.name%  → type unknown → the access itself says "structured" → sniff → parse
```

`type.Read(kind, bytes)` is the **deserialize mirror of the existing per-type
Writer**. Today's `FromWire` / per-family `Convert` hooks fold into it. This is the
piece that makes deserialize symmetric: serialize is `type.Write(value → bytes)`,
deserialize is `type.Read(bytes → value)`.

### Three levels of laziness (type resolution is lazy too)

The boundary stamps only the cheap, certain bits. Everything else defers.

| At read, the source signals… | Data is stamped | First touch does |
|---|---|---|
| a concrete type (`.json`, `.png`, `.snapshot`, `as <type>`, a Content-Type) | `{bytes, type, kind}` | `type.Read(kind, bytes)` |
| a self-describing payload (`.plang`) | `{bytes, type, kind}` — type/kind read from the payload's header at the boundary | `type.Read(kind, bytes)` |
| **nothing** (no extension, no Content-Type, no cast) | `{bytes}` — type unknown | the **access pattern** decides (below) |

For the unknown case, the *kind of access* is the hint — this is the "magic only
when needed":

- Navigate into a path (`%file.name%`) → the value must be structured → sniff the
  bytes (json / xml / yaml / csv …), parse, navigate. The magic happens here, once.
- Use it as a scalar / output (`%file%`) → decode as text; if the bytes don't
  decode, they stay bytes (binary).
- `as <type>` / `As<T>` → read toward that type.

Before any such access, `%file%` is **just bytes** — no processing, no guess.

---

## Worked examples

```plang
- read 'config.json'    / Data { bytes, type:text,     kind:json }   — not parsed
- write out %cfg.port%  / first touch → kind=json → parse → .port

- read 'report.plang'   / Data { bytes, type:<parsed>, kind:<parsed> } — header-supplied
                        /   (the .plang format that carries this header is NOT designed
                        /    here; this proposal only assumes the boundary can read it)

- read 'photo.png'      / Data { bytes, type:image,    kind:png }      — never decoded
                        /   until something looks at width/height/etc.

- read 'mystery'        / Data { bytes }                               — type unknown
- set %mystery.name% = 'ingi'
                        / the navigation forces resolution: sniff bytes → json/xml/yaml/csv
                        /   → parse → set .name. Only now does any parsing happen.
```

---

## Staged implementation (proposed)

Each stage independently verifiable; land in order so each is green before the next.

- **Stage 0 — float.** Drop `float` from the number kinds (fold to `double`).
  Tiny, independent, removes the phantom-kind round-trip loss.
- **Stage 1 — `type.Read(kind, bytes)`.** Add the per-(type, format) reader as the
  mirror of the existing Writer. Fold today's `FromWire` / per-family `Convert`
  into it. No behavior change — just builds the seam.
- **Stage 2 — lazy `Data`.** `Data` becomes `{bytes, type, kind, value}` with
  value computed on first touch via the Stage-1 reader. This is the core shift
  from eager to lazy.
- **Stage 3 — one boundary.** `file.read`/`http.get` become stream sources feeding
  `channel.read`; `channel.read` stamps type/kind and produces the lazy `Data`.
- **Stage 4 — access-driven sniff.** For type-unknown bytes, the access pattern
  (navigate vs scalar vs cast) drives resolution. The riskiest/most speculative
  layer; lands last, on a proven 1–3.

---

## Open questions for the architect

1. **`type.Read` signature & ownership.** Mirror the Writer exactly
   (`app/type/<name>/serializer/<format>.cs` gaining a `Read` alongside `Write`)?
   Or a separate `reader/` tree? How does read dispatch by `kind` (a json *file*
   vs a json *value* vs a `.plang` payload)?
2. **Where the lazy materialization actually fires.** `.Value` getter? A dedicated
   `Materialize()` the getter calls? How does it interact with the existing
   `ConvertValue` / `_valueFactory` laziness so we don't end up with two lazy
   mechanisms?
3. **Sniffing (Stage 4) is a guessing layer.** Acceptable, or should unknown-type
   bytes require an explicit `as <type>` and error otherwise? (Design leans toward
   sniff-on-structured-access, but it's the one magical part.)
4. **Binary vs text default.** Holding `bytes` (not text) handles binary, but the
   scalar-access path needs a clear rule for "decode utf-8, else stay bytes."
5. **`.plang` self-describing header** — out of scope here, but Stage 3 assumes the
   boundary can read type/kind from a payload. Needs the `.plang` format design to
   land first (or a placeholder).
6. **Interaction with signing.** A signed payload's bytes must be verifiable
   before/without forcing materialization. Does lazy read change when signatures
   are checked?

---

## What this is NOT

- Not an envelope/wrapper. Data is the data; there is no unwrap step.
- Not eager. Nothing parses at read time.
- Not a new parallel type. `Data` stays the one wire shape; this only changes
  *when* its value is computed and *who* (the type) computes it.

# RawFormat smell + byte-backed materialization gap (coder → architect)

**Two things, one root.** (1) Immediate: byte-backed values (binary/image) can't materialize — 5
strict-image reds fail with `UnregisteredMimeType: application/octet-stream`. (2) Chasing the clean
fix, Ingi flagged `type.@this.RawFormat` as an obpv and asked whether the format could be known
**sooner** (a caller fact), which would make the whole thing collapse. This is the trace + the
question.

## What "format" is

A source (`item.source`) is the deferred, undecoded raw form under its declared `{type, kind}`. Its
`_format` is a **mime that selects the SERIALIZER** which decodes the raw on first touch:
`source.Read()` → `serializers[_format].Read(source, ctx)` → the serializer builds an `IReader` over
the raw and hands it to the **type reader** (chosen from `source.Type`, NOT the format). So the
format's ONLY job is "which serializer/encoding decodes these bytes/this string."

## The trace — where the format comes from

Source is born at **one** site: `type/this.cs:279` `new item.source(raw, this, ctx, format)` (+ `:289`
a re-birth that passes `s.Format` through). The format arrives via `type.Create(raw, ctx, format?)`.
Callers split cleanly:

**Pass a real format (they KNOW the encoding):**
- `file/this.Operations.cs:86,105` — `format = (GetByType(mime) ?? Text).Type`. A `.gif` read →
  `GetByType("image/gif")` is null → `?? Text` → format `"text/plain"`. (So file byte-reads already
  work — Text.Read handles bytes.)
- `channel/this.cs:302` — `serializer.Type` (same `GetByType(Mime) ?? Text` pattern).
- `data/reader/this.cs:117` (wire reader) — passes `deferredFormat` (the .pr slot's own encoding).

**Don't pass a format → `RawFormat` GUESSES:**
- `data/this.cs:250` — a literal in a Data ctor (`set %x% = <literal>`): `type.Create(parsed, ctx)`.
- `Declare` (`:300`) — but the source re-birth arm passes `s.Format`, so format is preserved, not
  guessed.

`RawFormat` (`type/this.cs:191`) then guesses by **Name**:
```csharp
raw is byte[] ? App.Format.Mime("." + Kind) ?? "application/octet-stream"   // ← image/gif etc. — UNREGISTERED → throws
  : (Name is "dict" or "list") ? "application/plang"                        // ← the Name-switch Ingi questioned
  : Text.Mime;                                                             // text/plain
```

## Why it's an obpv (Ingi's read, and I agree)

1. **Type-switch on `Name`** — `RawFormat` lives on the (generic, name-based) type entity and branches
   `bytes / dict|list / scalar` to pick a serializer. Behavior forking on a name string.
2. **Stored twice** — the WRITE side does the *same* switch: `serializer/list/this.cs:162` `ResolveForWrite`
   → `data.Peek() is string ? "text/plain" : "application/plang"`. Two copies of "content-shape →
   serializer," read and write.
3. **The format is arguably a CALLER fact, not a type guess** — the wire reader knows the .pr is
   `application/plang`; the file knows its mime; the channel knows its content-type. They pass it. Only
   the *literal* path (`set %x% = <lit>`) has no caller-supplied format — and there the "format" is
   really "what encoding is this literal in," which depends on `{raw shape, declared type}` (a string
   declared `list` is JSON; declared `text` is text; bytes are bytes). Is that a legitimate
   type-derived fact, or should even literals carry their format from a step earlier?

## The questions for the architect

1. **Can `RawFormat` die?** i.e. can the format ALWAYS be supplied at the source-creation call (the
   literal path included), so there's no guess-by-Name? If yes, what supplies it for a bare literal?
2. If a residual type-derived default is legitimate (literals genuinely depend on `{shape, type}`),
   should it be **one** shared derivation used by BOTH read (`RawFormat`) and write (`ResolveForWrite`),
   and should it live on the value type (polymorphic — `dict` knows its raw is plang, `text` knows
   text) rather than as a `Name`-switch on the generic entity?
3. **Byte content specifically:** the specific mime (`image/gif`) is redundant as a *format* — it's
   already the type's Kind (`gif`), and the type reader drives the decode. So byte content's format
   should just be `application/octet-stream` (one byte serializer, no per-mime lookup, no fork). Agreed?

## Minimal unblock (independent of the design call)

To green the 5 reds now without touching the design: register `application/octet-stream` → a serializer
whose `Read` is the raw-value dispatch (the bytes ARE the value; `value.Reader.Bytes()` hands them to
the type reader — verified), and have `RawFormat`'s byte branch return `application/octet-stream`
instead of `Mime(".gif")`. That needs a real `Binary` serializer (not pointing octet-stream at the
misnamed `Text` serializer, which was the smell that started this). Can land as a stopgap or wait for
the design — coder's call once architect weighs in.

## State

Nothing landed — WIP reverted, tree clean at the last green commit. The 5 byte-backed reds remain (the
todo already notes "fix in a coming stage — needn't be this branch").

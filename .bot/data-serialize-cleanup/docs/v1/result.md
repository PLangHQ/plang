# docs v1 — `data-serialize-cleanup`

**Date:** 2026-05-28
**Verdict:** PASS — ready to merge.

## What shipped (user-visible CHANGELOG)

### Serializer pipeline
- `ISerializer` input tightened: takes `Data`, not `object? + Type?`. A non-Data argument no longer compiles.
- `ISerializer.ContentType` → `ISerializer.Type`. `ISerializer.FileExtension` → `ISerializer.Extension`.
- `Serializers.GetByContentType` → `Serializers.GetByType` (`GetByMimeType` retained as the throw-on-miss variant).
- `SerializeOptions.ContentType` → `SerializeOptions.Type`; `SerializeOptions.Data` is typed `Data`.
- Channel hooks renamed: `WriteCore` / `ReadCore` / `AskCore` → `Write` / `Read` / `Ask`. Public orchestrators kept their `Async` suffix: `WriteAsync` / `ReadAsync` / `AskAsync`. Concrete channels override the bare verbs.
- `Stream.Write` no longer strips `data.Value` before passing to the serializer — it hands the full `Data` and the serializer's identity decides what to emit. `application/json` and `text/plain` emit `data.Value`; `application/plang` emits the full wire shape.

### Wire format — single serializer
- `application/plang+data` is **gone**. Merged into `application/plang`. The `PlangDataSerializer` class no longer exists; one wire serializer at `app/channels/serializers/serializer/plang/this.cs`.
- The merged serializer composes `Json` + `WireJsonConverter` + `Transport.ForOutbound` + `Sensitive.Strip`. No `JsonSerializer.X` calls live in the serializer itself.
- `plang/Data.cs` and the `Envelope` class it defined are **deleted**.
- New `plang.@this.ContextLessFallback` static — used when no context is available (e.g., at boot, in pure-data deserialise paths).
- `application/plang+data` requests over HTTP now fail with an unknown content-type. Callers rewrite to `application/plang`.

### Wire shape — five top-level fields
The Data wire shape is now `{name, type, value, properties, signature}`. `properties` is omitted when empty; `signature` is omitted when null. Unknown top-level fields are silently ignored (standard STJ behaviour) to keep forward-compat space open for new reserved fields.

### Signing — converter-driven
- `WireJsonConverter.Write` calls `data.EnsureSigned()` sign-if-missing on every Data it walks. Egress through any channel auto-seals.
- Idempotent: already-signed Data is skipped, so forwarded payloads preserve nested provenance (Alice's inner signature rides intact under Bob's outer signature).
- The `ICallback`-only lazy-signing carve-out has been removed. All Data auto-signs on egress; in-process Data still requires an explicit `EnsureSigned()` if you want it sealed before crossing the wire.
- Canonicalization for `crypto.Hash` now uses the same options bag (`plang.@this.OutboundOptions`) the wire writer uses. Hash bytes ≡ wire bytes minus the outer `Signature`. Tampering with `name`, `type`, `value`, `properties`, or any nested-Data signature invalidates the outer signature.
- **Migration note:** signatures produced before this branch don't verify after. Any signed Data persisted before this branch (deferred-callback rows, signed snapshots) needs a one-time re-sign.

### Compress / Decompress flattened
- `Data.Compress()` now produces a flat archived shape: `{ name: "", type: "archived", value: byte[] }`. No inner `gzip` Data, no `RehydrateNestedData` walk.
- Routes through the registered `application/plang` serializer to produce bytes, so the converter's sign-if-missing fires on the inner Data — its signature rides inside the compressed bytes. Sign-then-compress and compress-then-sign produce equivalent wire shapes.
- `Properties` now round-trip through compress/decompress (they ride in the wire bytes alongside `name`/`type`/`value`/`signature`). The legacy "Properties are `[JsonIgnore]`" constraint is gone.
- `compress` / `decompress` are also exposed as action modules — `set %archived% = compress %big%` / `set %original% = decompress %archived%`.

### Properties — new wire scope and `!` operator
- `Properties` is now `IDictionary<string, object?>` (case-insensitive) with a primitive-only insertion gate (`string`, `bool`, numeric primitives, `DateTime`, `byte[]`, plus dict/list of primitives). Raw `Data` instances are rejected at insertion time.
- New PLang operator `%x!key%` reads `x.Properties[key]`; `%x.field%` keeps reading `x.Value`. The two stores are distinct; the operator picks which one.
- New write surface `set %x!key% = value` writes through `variable.set` to `Properties[key]`.
- `Variable.IsMalformed` rejects shapes like `%x!!cost%` / `%x.y!cost%` / `%!x!cost%` with `InvalidVariableReference` 400.
- The leading-`!` shape `%!name%` (infrastructure namespace: `%!data%`, `%!error%`) is positionally distinct and still parses as a single variable reference.

### Vocabulary
- "Envelope" is gone from PLang's serialization vocabulary. Data is not enveloped — Data IS the wire shape.
- File rename: `PLang/app/data/this.Envelope.cs` → `PLang/app/data/this.Transport.cs`. `Wrap` / `Unwrap` stay (they describe category wrapping, not enveloping).
- `/PLang/App/CLAUDE.md` updated with a new rule: do not introduce parallel wrapper types ("Envelope", "Wire", "Wrapper") for Data's serialization shape.

## Documentation updated

- `Documentation/v0.2/io-channels.md` — channel hook renames; `WriteAsync` signature with typed `Data`; vocab.
- `Documentation/v0.2/callbacks.md` — merged `application/plang` mimetype; sign-if-missing in converter; `PlangDataSerializer` and `application/plang+data` references removed; envelope vocab; wire-shape JSON example updated.
- `Documentation/v0.2/architecture.md` — channel `Mime` instead of `ContentType`; merged `application/plang` serializer; `this.Envelope.cs` → `this.Transport.cs` in the directory tree.
- `Documentation/v0.2/good_to_know.md` — Data.Envelope → Data.Transport; `_envelopeJsonOptions` reference removed; `GetByContentType` → `GetByType`; serializer impl list (`Json, Text, plang`); signing module envelope vocab.
- `Documentation/v0.2/app-tree.md` — Data tree shows `this.Transport.cs`, `Properties.cs`, `WireJsonConverter.cs`; "result envelope" → "result wrapper".
- `Documentation/Runtime2/data-spec.md` — §15 renamed to "Transport Pipeline"; new §15a "Properties — sidecar metadata" (insertion gate, `!` operator, write surface); §16 expanded with sign-if-missing rule + canonicalization rule.
- `Documentation/v0.2/variables.md` — Properties section rewritten (new shape, gate, PLang access table, write surface, malformed shapes).
- `/PLang/App/CLAUDE.md` — new rule appended: "Data is not enveloped."

## What was not done (out of scope)

- No new PLang `.goal` examples written — tester already wrote coverage for the `!` operator and the Compress round-trip (`Tests/Llm/LlmProperties.test.goal`, `Tests/Serialization/NegationPrefixStillParses.test.goal`, the Compress/Decompress goal suite).
- Public/private Properties split, per-Property `[Sensitive]`, structured (Data-typed) Property values — all out of scope per architect Stage 4.
- HTTP wire transport for `ask-user` resume; real symmetric crypto for `crypto.encrypt`/`decrypt` — still out of scope (called out in `callbacks.md`).
- The auditor's F2 (async-no-await on `crypto/hash.cs:18`) and the broader sync-over-async pre-walk for signed Data are tracked, not for this branch.

## What I noticed in passing (not blocking)

- The `_envelopeJsonOptions` name no longer exists in source as of Stage 2 — wherever it appeared in source comments, the rename to `_transportJsonOptions` already happened. Documentation no longer references it.
- `Documentation/v0.2/callbacks.md` "Lazy signing on `Data.Signature` — ICallback-only carve-out" section was *replaced* with the new "Sign-if-missing — the converter does it" section. The carve-out is genuinely gone in source; the doc reflects that.
- `Documentation/v0.2/architecture.md` L392 referred to "ContentType" — corrected to `Mime` to match the actual field on `Channel.@this`.

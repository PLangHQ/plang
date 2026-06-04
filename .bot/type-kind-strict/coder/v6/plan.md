# coder — v6 — Stages 6 & 7

Architect carved two more stages onto the type-kind-strict plan
(`f24f024ec`). This version ships both. Order per Ingi: **Stage 7 first, then
Stage 6. Lazy materialization deferred.**

## Stage 7 — the hash type (committed `62a23c4e7`)

A hash is not a `bytes`; it is a digest produced by a named algorithm. Give it
its own type whose **kind is the algorithm**.

- `app/type/hash/this.cs` — scalar type, `Shape="string"`, `static Kinds =
  ["keccak256","sha256"]`, holds `byte[] Bytes` + `string Algorithm`
  (`[Out,Store]`), owns the byte⇄base64 conversion (`ToBase64()`,
  `static FromBase64(base64, algorithm)`), `DigestEquals(other)`, implicit
  string.
- `app/type/hash/serializer/Default.cs` — IWriter renders base64.
- `crypto/code/Default.cs` — `Hash` stamps `Create("hash", kind: algorithm)`;
  `Verify` reads the algorithm from `Type.Kind` (authoritative), falling back
  to the `Algorithm` param.
- `signing/code/Ed25519.cs` — rehash reads `Type?.Kind ?? "keccak256"` (was
  `Type.Name`, which is now always `"hash"`).
- Schema `Kinds` derive from **all** known types' static `Kinds` (via
  `BuildTypeEntries(null)`) so a return-only type like `hash` still advertises
  its algorithms to the LLM.

## Stage 6 — structured type at the producers (this commit)

The producers stamped a muddy MIME (`text/markdown`) or a bare extension
(`md`) — never `{text, md}`, and build disagreed with runtime. Fix: **one
shared derivation both call**, so they can't drift.

- `format/list/this.cs` — `TypeFromMime(mime)` and `TypeFromExtension(ext)`:
  the single `(extension | mime) → type.@this{name, kind}` seam. `name` is the
  materialized CLR family (`text`/`object`/`bytes`) or the media family
  (`image`/`audio`/`video`); `kind` is the extension/subtype, canonicalised.
  `application/octet-stream` and unknown MIME → the `type.Null` sentinel.
- `file/read.cs` — `Build()` returns `Ok(Format.TypeFromExtension(p.Extension))`
  (a structured entity, not a bare string); `Run()` image-lift preserves the
  whole `{image, kind}` type.
- `path/file/this.Operations.cs` — `ReadText` (snapshot + main) stamps via
  `TypeFromMime(mime)`; binary-vs-text decision via `ClrFromMime`.
- `http/HttpBuildHelpers.cs` — `InferTypeFromUrl` calls `TypeFromExtension`
  instead of returning the bare extension string.
- `builder/code/Default.cs` — `RunBuildPass` / `StampOnTerminalVariableSet`
  accept a `type.@this` entity (and canonicalise a bare-string Build() return
  into one), stamping the structured type on the terminal `variable.set`.

### Decision I own (architect delegated final shape)

**Unregistered-but-known-MIME extensions now stamp, they don't bail.** A
`.pdf` URL → `{object, pdf}` (was: bare `Ok()`). Rationale: at runtime an
`application/pdf` response yields `{object, pdf}` through the same derivation;
build must produce the same or it re-introduces the exact build/runtime drift
this stage exists to kill. `object` is a registered type and `pdf` is just a
kind label, so the downstream `variable.set` is safe. Updated the two tests
that asserted the old bail-out.

### Deferred (per Ingi)

- **Lazy materialization** for non-string CLR targets (architect Stage 6 §3).
- **http runtime response-body type stamp**: the response wrapper
  (`http/response/this.cs`) holds `Body` as a bare `object?` with no typed-Data
  seam — stamping `{name,kind}` there needs a wrapper restructure, and lifting
  the value (e.g. byte[]→image) breaks the explicit byte[] body contract. Left
  as-is; only the **build-side** http drift (InferTypeFromUrl) is closed.

## Verification

- C# `dotnet run --project PLang.Tests` → **3810/3810**.
- PLang `cd Tests && plang --test` → **263/263, 0 stale** (clean rebuild).

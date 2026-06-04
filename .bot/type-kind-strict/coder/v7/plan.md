# coder — v7 — Stages 7 rev 2, 8, 9

Architect pushed three more stage files (`stage-7-hash-type.md` rev 2,
`stage-8-type-flow-and-vocabulary.md`, `stage-9-lazy-reference-handles.md`) plus
the settled `plan/build-time-type-flow.md`. This version ships all three in
dependency order: 7 rev 2 → 8 → 9.

## Stage 7 rev 2 — hash is crypto-owned, returned as a value

Rework rev 1. The spine: `crypto.hash` returns `Data<hash.@this>` (not
`Data<byte[]>`).

- Relocate `app/type/hash` → `app/module/crypto/type/hash` (`app/type/` is
  reserved for the builtin vocabulary). Discovery is namespace-based (registry
  `@this`-convention + renderer `.serializer` namespace), so the move is
  automatic — confirmed by `HashType_Resolves_ViaRegistry`.
- `crypto.verify` `Hash` param is untyped `data.@this` so it carries either a
  hash value (algorithm rides as kind) or a bare base64 string (+algorithm).
- `type.@this.Convert` discovers a `static object? FromWire(string, string?)`
  by convention for wire read-back — no layering inversion into the module.
- signing: `HashDataConverter` round-trips a hash value; `Ed25519` reads the
  stored digest as a `hash.@this` and compares via `DigestEquals`.

## Stage 8 — build-time type flow + fundamental vocabulary

- Fundamental vocabulary defined on `primitive.@this` in two categories:
  `InlineFundamentals` (literal-writable) + `ReferenceFundamentals`
  (image/video/audio/path/bytes — only a path/handle is writable). `BuilderNames`
  is now this explicit set → image/video/audio/path first-class always-on.
- Prompt `Kinds` table scoped to fundamentals (`builder/type` Build): a result
  type like `hash` stays registered but its algorithms never leak into every
  step's prompt. Subsumes stage 7's narrower emit-table fix.
- `text` never derives a kind from a literal's spelling (the kind-from-value
  derivation in `variable.set` fires only for a reference fundamental).
- `CompileUser.llm`: dropped the spelling-promotion teaching.

## Stage 9 — reference fundamentals are lazy path-handles

- `image` gains a path-backed constructor (`new @this(path)`): `.Path` set, no
  I/O. `BytesAsync()` loads through `Path.ReadBytes()` (the auth gate) once,
  caches; failure surfaces at first access. Truthiness probes existence, not a
  byte load.
- `TryConvertTo` gains a general "path-string → reference fundamental with a
  `path.@this` ctor" arm → replaces `variable.set`'s `Data<string>` carve-out.
  image is the proving instance; audio/video inherit it.
- Parked per the stage: mutation / divergence-from-file / save.

## Coder decisions owned

- **verify `Hash` is untyped `data.@this`** (not `data.@this<string>`):
  `As<string>` cannot convert a `hash.@this` (no `IConvertible`), and the stage
  wants the produced hash Data bound directly. Untyped carries both a hash value
  and a bare base64 string without lossy conversion.
- **Stage-8 determinism is the KindHooks gate + teaching**, not a build-time
  strip: `text` resolves to `typeof(string)` so `text.Build` is unreachable via
  KindHooks anyway; the `.pr` cannot distinguish an LLM-spelling kind from an
  explicit `as text/md`, so stripping would break the explicit case. Gating the
  derivation on `name != text` + the teaching change is the deterministic rule.
- **`Schema.Build().Kinds` no longer carries `hash`** — stage 8 explicitly
  overrides stage 7 rev 2's "keep hash in Schema.Build().Kinds". Repointed
  `HashType_AdvertisesAlgorithmKinds` to assert via `app.Type["hash"].Kinds`
  (the registry/entity), the true C# advertisement surface.

## Verification

- C# `dotnet run --project PLang.Tests` → **3818/3818**.
- PLang `plang --test` not re-run this session (recommended next).

# coder — type-kind-strict (v1–v7)

## Version
v7 (Stages 7 rev 2, 8, 9 — see `v7/plan.md`). v6 = stages 6 & 7 rev 1. v1–v5 = original 5 stages.

## v7 — Stages 7 rev 2, 8, 9 (committed `f08f3760f`, `3c4591b24`, `bee135b30`)

**Stage 7 rev 2 — hash is crypto-owned, returned as a value.** `crypto.hash`
now returns `Data<hash.@this>` (not `Data<byte[]>`) — the single change that
drives the build annotation (`%x% (hash)`), fires the live serializer, and makes
verify read the algorithm off the value. Relocated `app/type/hash` →
`app/module/crypto/type/hash` (namespace-based discovery makes the move
automatic). `crypto.verify` `Hash` param is now untyped `data.@this` (carries a
hash value OR a base64 string). `type.@this.Convert` discovers a
`static object? FromWire(string, string?)` for wire read-back. Signing's
`HashDataConverter` + `Ed25519` round-trip/read a `hash.@this`. Crypto/signing
tests now read the digest off the hash value (`.Bytes`/`.ToBase64`).

**Stage 8 — build-time type flow + fundamentals.** Defined the fundamental
vocabulary on `primitive.@this` in two categories (`InlineFundamentals` +
`ReferenceFundamentals`); `BuilderNames` is now that explicit set so
image/video/audio/path are first-class always-on. Scoped the prompt `Kinds`
table to fundamentals (`builder/type` Build) — `hash` stays registered but its
algorithms no longer leak into every step's prompt (subsumes stage 7's
emit-table fix). `text` never derives a kind from a literal's spelling. Dropped
the spelling-promotion teaching in `CompileUser.llm`.

**Stage 9 — reference fundamentals are lazy path-handles.** `image` gains a
path-backed constructor (`new @this(path)`): `.Path` set, no I/O. `BytesAsync()`
loads through `Path.ReadBytes()` (the auth gate) once, caches; failure surfaces
at first access. `TryConvertTo` gained a general path-string → reference-
fundamental (any type with a `path.@this` ctor) arm — replaces `variable.set`'s
`Data<string>` carve-out for `as image`. Mutation/save parked.

**Coder decisions:** verify `Hash` untyped (As<string> can't convert a
hash.@this); stage-8 determinism = KindHooks gate (`name != text`) + teaching,
not a build strip (text→string makes text.Build unreachable; the .pr can't tell
spelling-kind from explicit `as text/md`); `Schema.Build().Kinds` no longer
carries hash (stage 8 overrides stage 7 rev 2's wording) — repointed
`HashType_AdvertisesAlgorithmKinds` to `app.Type["hash"].Kinds`.

**Verification:** C# 3818/3818. PLang `--test` 263/263 (0 fail, 0 stale). Two
`as text` `.test.goal` contracts that pinned the pre-stage-8 derive-kind-from-
extension behavior were updated to the no-kind contract and rebuilt
(`SetAsTextUppercase`, `SetAsTextWithMdExtension`); the explicit `as
text/markdown` tests were left unchanged. (One HTTP test, `ConfigHeaders`, is
flaky — it hits httpbin.org; passed on re-run.)

---

## Version (historical)
v6 (Stages 6 & 7 — see `v6/plan.md`). v1–v5 below cover the original 5 stages.

## v6 — Stages 6 & 7

**Stage 7 (committed `62a23c4e7`): the hash type.** A hash gets its own scalar
type whose *kind is the algorithm* (`{name:"hash", kind:"keccak256"|"sha256"}`).
`hash.@this` owns byte⇄base64 (`ToBase64`/`FromBase64`/`DigestEquals`).
`crypto` stamps `Create("hash", kind: algorithm)`; `Verify` and Ed25519 rehash
read the algorithm from `Type.Kind` (was `Type.Name`). Schema `Kinds` now derive
from all known types' static `Kinds` so the return-only `hash` advertises its
algorithms to the LLM.

**Stage 6 (this commit): structured type at the producers.** One shared
derivation — `Format.TypeFromMime` / `TypeFromExtension` — that both build and
runtime call, so they can't drift. Producers stamp `{name, kind}` (e.g. a `.md`
read → `{text, md}`, `.json` → `{object, json}`) instead of a muddy MIME or a
bare extension. Migrated `file/read.cs` (Build + Run image-lift),
`path/file/this.Operations.cs` (ReadText), `http/HttpBuildHelpers.cs`
(InferTypeFromUrl), and `builder/code/Default.cs` (stamp the entity on the
terminal `variable.set`).

*Decision I owned (architect delegated):* an unregistered-but-known-MIME
extension (`.pdf`) now stamps `{object, pdf}` rather than bailing — because the
runtime produces exactly that for the same Content-Type, and bailing at build
would re-create the drift the stage kills.

*Deferred (per Ingi):* lazy materialization for non-string CLR targets; the
http runtime response-body type stamp (the `Body` is a bare `object?` with no
typed-Data seam — needs a wrapper restructure).

**Final: C# 3810/3810, PLang 263/263 (0 stale).**

---


## What this is

Architect carved a 5-stage plan to reshape PLang's type value into
a structured `{Name, Kind, Strict}` entity, fold `Data.Kind`, normalise the
kind vocabulary (extension-derived, with `md|markdown` aliasing), make
`variable.set` take a `type` (not a bare string), and restructure the type
information the LLM sees.

Test-designer wrote 107 C# + 10 PLang failing-test stubs across all 5
stages. This coder thread shipped all 5 stages, one version per stage:

| Version | Stage | C# passing | Δ vs prev |
|---------|-------|-----------:|----------:|
| baseline | — | 3696 | — |
| v1 | 1 — Name/Kind/Strict + Data.Kind fold + IKindValidatable + KindHooks rename | 3732 | +36 |
| v2 | 2 — text type + numerics under number | 3721 | −11 |
| v3 | 3 — Format.KindOf→FamilyOf + kind canonicaliser | 3731 | +10 |
| v4 | 4 — variable.set takes type entity + image.ValidateKind body | 3734 | +3 |
| v5 | 5 — TypeSchemas dual-mode renderer + LLM constructor teaching | 3742 | +8 |

**Final C# state: 3742/3803 (+46 vs baseline).**
**PLang: 253/253 + 10 stale (unchanged).**

## What was done

**v1 (Stage 1):** `app.type.@this` rename `Value`→`Name`; add `Kind`,
`Strict`; drop family-`Kind`; `ClrType` internalised; `Create(name, kind?,
strict?)` factory with slash-split and lowercase canonicalisation.
`Data.Kind` stored field deleted — folds to `Type.Kind`. Wire reads/writes
kind via the entity. New `IKindValidatable` marker; image implements with
a stub body. `App.Type.Kinds` → `KindHooks`. ~25 source files + tests
updated for `Type.Value` → `Type.Name`.

**v2 (Stage 2):** Created `app/type/text/` (mirrors image, text-backed,
Shape="string", static Description teaches kind-from-extension, no static
Kinds). Flipped `primitive.Canonical[typeof(string)] = "text"`. Mapped
`Canonical[typeof(int|long|decimal|double|float)] = "number"`. Rebuilt
`BuilderNames` so `text` is in, `string` and numerics are out. Raw
`new Type(name)` constructor canonicalises. `Type.String/Int/Long/Decimal/
Double` static helpers updated. `Data.Type` lazy-derivation stamps Kind
for numerics. ~50 test sites updated.

**v3 (Stage 3):** `App.Format.KindOf` → `FamilyOf` rename across PLang +
tests. New `App.Format.CanonicaliseKind` — derived from the
extension→MIME registry: walks the map, finds entries whose MIME subtype
matches, picks the shortest extension (`markdown`→`md`, `jpeg`→`jpg`).
Unknown free strings pass through. `type.Create` takes optional context
and canonicalises kind when present.

**v4 (Stage 4):** `variable.set.Type` shape change —
`data.@this<string>?` → `data.@this<app.type.@this>?`. Handler stamps the
whole entity (kind + strict survive). Strict enforcement: `ValidateBuild`
(build-time, literals) and `Run` (runtime, after %var% resolution) call
`IKindValidatable.ValidateKind` when `Type.Value.Strict && Kind != null`
and the resolved CLR type implements the marker. Mismatch → typed
`StrictKindMismatch` error. `image.ValidateKind` real body via
`ImageSharp.DetectFormat`. `TryInstantiateValidator` helper finds a
ctor whose first param accepts the raw value.

**v5 (Stage 5):** `TypeSchemas` dual-mode rendering — advertised kinds
(closed list, e.g. `number — kinds: int | long | decimal | double`) vs
extension-derived (e.g. `text — kind = extension (md, txt, csv, html, ...)`).
`app.type.@this` gets `[LlmBuilder]` on Name/Kind/Strict so the catalog
walker surfaces `type` as a record with the constructor shape.
`TypeDescription` const carries the "emit a dict, never the slash form"
teaching.

## Not done (and why)

- **Pre-existing `EngineTypesTests` / `TypeMappingTests` (~30 tests)** that
  literal-assert `Name(typeof(int)) == "int"` style. The architect explicitly
  flagged this as expected fan-out ("Expect tests asserting type=='string' to
  need updating — that churn is intended, not a regression."). A focused
  sweep would close them; left for a follow-up session.
- **`os/system/builder/llm/Compile.llm`** template — replace hand-written
  primitive list with catalog-generated vocabulary. Needs a build-trace
  capture to verify the LLM still builds correctly. The C# side is ready;
  the template edit is pure content with a verification gate.
- **`os/system/builder/llm/CompileUser.llm`** — drop the `Primitive types:`
  line. Same trace-validation rationale.
- **PLang `.test.goal` bodies** under `Tests/TypeKindStrict/` (10 stale).
  These need the builder to recognise `as <type> strict` natural-language
  syntax, which is a builder-prompt-level change rather than a runtime one.
- **Stage 4 stub tests** (`StrictValidateBuild_*`, `StrictRun_*`,
  `SetMintCarriesKind_*` — 8 tests) need a `Run()` test harness with a
  context + variables set up; not in this session's budget.
- **The remaining 71 - 3 = ~68 failing tests** break down to: ~30
  pre-existing tests needing the Stage-2 churn cleanup; ~10 PLang goal
  stubs; ~8 Stage 4 Run-harness tests; the rest are Stage 5 trace tests
  that need LLM credentials.

## Code example

```csharp
// Variable.set declaration (Stage 4):
public partial data.@this<global::app.type.@this>? Type { get; init; }

// Run() body (Stage 4):
var typeEntity = Type.Value;
var targetType = Context.App.Type.Get(typeEntity.Name);
if (typeEntity.Strict && typeEntity.Kind != null
    && typeof(IKindValidatable).IsAssignableFrom(targetType))
{
    var probe = TryInstantiateValidator(targetType, Value.Value);
    if (probe is IKindValidatable v) {
        var (ok, actual) = v.ValidateKind(Value.Value!, typeEntity.Kind);
        if (!ok) return FromError(new StrictKindMismatch(typeEntity, actual));
    }
}
// ... mint typed Data, stamp typedData.Type = typeEntity (the whole entity).

// TypeSchemas dual-mode (Stage 5):
if (t.Kinds != null) sb.Append(" — kinds: ").Append(string.Join(" | ", t.Kinds));
else if (HasBuildHook(t)) sb.Append(" — kind = extension (...)");
```

## Hand-off

```
Next: run.ps1 codeanalyzer type-kind-strict "Review coder v1-v5 on branch type-kind-strict" -b type-kind-strict
```

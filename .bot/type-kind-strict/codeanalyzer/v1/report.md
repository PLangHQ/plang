# Code Analyzer — v1 — `type-kind-strict`

**Verdict: FAIL** → next bot: **coder** (fix F1; F2–F5 at coder's discretion, but
F1 is blocking). Re-run codeanalyzer after the fix; on PASS the chain is
codeanalyzer → tester.

**Scope:** the type-system reshape at HEAD `502d43d0e` (coder v1–v7, stages 1–9):
the `{Name, Kind, Strict}` entity (`app/type/this.cs`, `this.json.cs`), the
`Data.Kind` fold, `text`/`number` canonicalisation, build-time kind derivation
+ strict validation (`variable/set.cs`, `IKindValidatable`, `image`), the LLM
vocabulary scoping (`builder/type/this.cs`), the hash-type relocation + signing
round-trip (stage 7), and lazy path-backed reference fundamentals (stage 9).
The ~200 mechanical `.Value`→`.Name` rename sites were spot-checked, not
line-audited.

**Build:** clean — `dotnet build PlangConsole` from clean (bin/obj wiped) → 0
errors, 256 warnings (all pre-existing CS8604 nullability in generated `list`
module code; no PLNG001/PLNG002, no new classes). No `System.IO.*` or
`Console.*` in the changed production files.

**Tests (rebuilt-clean binary):**
- C# — `dotnet run --project PLang.Tests` → **3818/3818 pass**.
- PLang — `cd Tests && plang --test` → **263/263 pass, 0 fail, 0 stale**.

Green suites — but green is not the whole story here: see F1, where the two
`.test.goal` files that *claim* to cover strict-image enforcement pass while
enforcing nothing.

---

## What's clean (stated so it isn't re-litigated)

- **Signing / hash relocation (stage 7) preserves every invariant.** `crypto.hash`
  now returns `Data<hash.@this>`; `hash.@this` lives under `app/module/crypto/type/hash/`
  and the registry still resolves it to `hash` via the `@this`/last-segment
  convention. The digest is serialized into `Signature.ToSigningBytes()` (via
  `HashDataConverter` → `{type:algorithm, value:base64}`), so the Ed25519
  signature still binds the data hash — tampering the digest invalidates the
  signature. `DigestEquals` uses non-constant-time `SequenceEqual`, which is
  **fine**: the digest is a content hash, not a secret, and the real auth
  boundary is the Ed25519 verify; the secret-bearing comparison (headers) still
  uses `CryptographicOperations.FixedTimeEquals`. `crypto.verify`'s untyped
  `Hash` param correctly reads the algorithm off a bound `hash` value, else the
  Type kind, else the `Algorithm` param. `FromWire`/`FromBase64` round-trip
  cleanly. No regression.
- **`text`/`number`/int-literal kind behavior is coherent and tested.** `set %x% = 5`
  → `{number, int}`; `as text` on a literal keeps kind null (a literal's spelling
  is not a kind); `as text/markdown` canonicalises to `md`; strict-on-text
  degrades to "kind name accepted" (no `IKindValidatable`). The PLang `.test.goal`
  set for these has real assertions and they hold.
- **LLM prompt scoping (stage 8) is correct.** `builder.type.Build()` filters the
  `Kinds` table to `primitive.Fundamentals`, so `hash`'s algorithms stay
  registered (`app.Type["hash"]`, getTypes) but never leak into every step's
  prompt; `number`'s precisions and the media/text extension kinds are included.
- **The `{Name, Kind, Strict}` entity is a genuine single-owner fold.** `Data.Kind`
  is now a pass-through to `type.Kind` (no stored field); `Promote()` throws a
  loud producer-bug error when an unstamped entity's catalog props are read
  rather than silently returning null. Good defensive shape.

---

## F1 (MAJOR, blocking) — `as image strict` silently enforces nothing for the two realistic value shapes; the PLang tests that "cover" it assert nothing

**Strict kind validation only fires when the bound value is a raw `byte[]`.**
For the two ways a developer actually writes `as image strict`, the probe is
never constructed and validation is skipped at **both** build and runtime:

1. **Path literal** — `set %img% = "real.gif" as image/gif strict`. `Value.Value`
   is the string `"real.gif"`.
2. **`read`-lifted image** — `read photo.png into %u%` then
   `set %img% = %u% as image/gif strict`. `file/read.cs:37-42` lifts the bytes to
   an `image.@this` **instance**, so `Value.Value` is an `image.@this`, not bytes.

The gate is `TryInstantiateValidator(targetType, value)` (`variable/set.cs:288`),
which only returns a probe when the raw value's CLR type is assignable to an
image constructor's first parameter — `byte[]` (`image(byte[], string, path?)`)
or `path.@this` (`image(path)`). A **string** and an **`image.@this`** match
neither ⇒ `probe == null` ⇒ the whole `if (probe is IKindValidatable v)` block
(at `set.cs:37` ValidateBuild and `set.cs:180` Run) is skipped. No error.

Compounding it, `image.ValidateKind(object value, …)` (`image/this.cs:116-118`)
reads `value as byte[] ?? Bytes`. Handed an `image.@this` it gets `null as byte[]`
→ falls back to the **probe's own empty `Bytes`** (a path-backed probe loads
nothing) → returns `(false, null)`. So even if a probe were built from an image
instance, the validator cannot read the instance's bytes. The
`IKindValidatable` contract (`object value`) is mismatched with the value shape
the binding-mint actually holds for a reference fundamental.

**Why the green suite hides it.** The four PLang goals:
- `SetAsImageGifStrictMatching` — passes by stamping the *declared* kind `gif`;
  no bytes are ever sniffed.
- `SetAsImageGifStrictMismatch` — comment says "**must fail with a
  BuildValidation error**." It builds a `.pr` (build does **not** fail) and the
  test **passes** — the goal has zero assertions, so a PNG masquerading as a
  strict GIF is accepted silently.
- `SetAsImageGifStrictRuntimeVarMismatch` — comment says "**runtime typed
  error: expected gif got png**." It **passes** with no error, because `%upload%`
  is an `image.@this` (lift), not bytes.
- `SetAsImageStrictNoKind` — fine (no kind ⇒ nothing to check).

The C# `Cut2_StrictMismatchFailsAtRightLayer` tests pass **only because they
feed raw `byte[]`** (`PngBytes`/`GifBytes`) — the one path that works. They never
exercise a path string or an `image.@this`, so they don't cover the forms the
PLang goals (and real `.goal` authors) use. This is the
[don't-call-it-covered] failure mode: two committed tests read as "strict
enforcement verified" while verifying the opposite of their stated intent.

**Root cause is the unresolved Stage-9 ↔ Stage-4 tension.** Stage 9 says a
path-backed image reads nothing at `set`; Stage 4 says strict validates the
content kind. You cannot both not-read and sniff-content at the same site. The
code resolves it by silently doing nothing — and there is **no deferred check
on `BytesAsync()` either**, so strict isn't even enforced lazily on first
content access.

**Fix direction (coder owns the choice; all three need the validator and the
tests fixed):**
- (a) strict on a path-backed reference fundamental reads bytes at `set` and
  sniffs (accept the I/O for the strict case, contra Stage-9 laziness for that
  case only); or
- (b) defer the kind probe to first content load (`BytesAsync`) and surface the
  mismatch there; or
- (c) reject `as <reference-fundamental> strict` from a path/instance at build
  as explicitly unsupported.

In every case: make `ValidateKind` accept the realistic value shapes (a
`path.@this` it can read through the auth gate, and an `image.@this` whose bytes
it reads), and give the three PLang goals **assertions that fail when
enforcement is absent** (assert the build error for the mismatch goal; assert
the runtime `StrictKindMismatch` for the var-mismatch goal). A test whose only
signal is "didn't throw" cannot guard a validation feature.

---

## F2 (MEDIUM) — `kind` is written twice on the wire/`.pr`: inside the `type` entity and as the flat `Data.Kind` sibling

The branch folded `Data.Kind` into `type.Kind` (`Data.Kind` getter is now
`_type?.Kind`) but **kept `[JsonPropertyName("kind")]` on `Data.Kind`**
(`data/this.cs:275-278`). STJ serialization of a `Data` therefore emits the kind
in two places. Confirmed in the built
`setasimagegifstrictruntimevarmismatch.test.pr` Path parameter:

```json
{ "name": "Path", "value": "photo.png",
  "type": { "name": "path", "kind": "file" },
  "kind": "file" }                                ← redundant sibling
```

The build path actively writes it (`builder/code/Default.cs:905` `p.Kind = kind`).
On read-back, `Wire.ReadBody` has **no `"kind"` case** (`default: reader.Skip()`),
and plain-STJ read folds the sibling into the same `_type.Kind` slot the `type`
field already set — so the sibling is at best redundant and at worst
order-fragile (a `kind`-before-`type` document order would force a lazy
`Type`-derive that `type` then overwrites; current `.pr`s emit `type` first, so
it converges). This is OBP smell #6 (same logical thing stored twice, two views
that can drift). No functional bug observed today; it's structural redundancy
the branch introduced (base `.pr`s carried kind *only* in the sibling; now it's
in both). Fix: drop the sibling serialization from `Data.Kind` and let the
`type` entity own kind on the wire, or confirm a consumer that still needs it.

---

## F3 (MINOR) — `type.@this.Scheme` dereferences `Context.App` without the null-guard every sibling uses

`app/type/this.cs:314`:

```csharp
public global::app.type.path.scheme.@this? Scheme
    => Name == "path" ? Context.App.Type.Scheme : null;
```

`Context` is `actor.context.@this?` (nullable). Every other accessor on this
entity guards it (`Context?.App…` in `ClrType`, `Compressible`; `Promote()`
throws a typed producer-bug error). This one will NRE if a `path`-typed entity
with null Context ever reads `.Scheme`. No current caller reads the instance
property (grep: all `.Scheme` hits are `App.Type.Scheme` registry access or
`SchemeNotRegistered.Scheme`), so it's latent — but it's a same-shape
inconsistency that will bite the first producer that mints a `path` entity off
the wire (Context-less) and navigates it. Fix: `Context?.App.Type.Scheme`.

---

## F4 (MINOR) — the "text never derives kind from spelling" rule lives at the call site, not on `text`

`variable/set.cs:156-157` gates kind derivation with a string-name special-case
`&& !string.Equals(typeName, "text", …)`, while `text/this.Build.cs` still
registers a spelling-kind hook (`Build("readme.md") → "md"`), discovered by the
kind registry. The discipline is enforced by generic code knowing the literal
name `"text"` (OBP smell #5 — owner-discipline pushed to the consumer). It's
consistent today because `set` is the only literal-mint site, but any other
producer that calls `KindHooks.Of(textClr, "x.md")` will derive `"md"` contrary
to the rule. Either drop `text`'s `Build` hook (text's kind is a producer/Format
concern, not a literal-spelling one) or move the gate onto the type. Low — no
second call site exists yet.

---

## F5 (NIT) — dead fast-path + redundant wrapper

- `format/list/this.cs:499` `CanonicaliseKind`: the fast-path
  `_extensionToMime.ContainsKey(lower)` checks a no-dot token (`"gif"`) against
  dotted keys (`".gif"`) → never hits; the slower MIME-subtype scan produces the
  correct answer regardless, so it's harmless but misleading.
- `primitive/this.cs:142` `BuildBuilderNames()` returns
  `InlineFundamentals.Concat(ReferenceFundamentals).ToList()` — identical to
  `Fundamentals`' source; the method adds nothing over the field.

---

## Verification notes (deterministic cross-checks)

- Strict-skip for string/instance values is read directly off `TryInstantiateValidator`
  (`set.cs:288-306`) against `image`'s two public ctors (`image/this.cs:60,73`) —
  a string/`image.@this` is assignable to neither first param ⇒ `null` ⇒ skip.
  Not inferred from a single tool read; cross-checked against the passing-but-
  assertion-free `.test.goal` bodies and the `read.cs:37` image-lift.
- Wire-`kind` duplication confirmed from the on-disk built `.pr` (read-only),
  the `Data.Kind` `[JsonPropertyName]` attribute, and the absence of a `kind`
  case in `Wire.ReadBody`.
- Suite counts from the freshly-rebuilt binary (bin/obj wiped, `dotnet build
  PlangConsole`), not a stale `plang.exe`.

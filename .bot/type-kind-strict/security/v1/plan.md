# security v1 plan — type-kind-strict (merged with lazy-deserialize)

**Branch HEAD:** `ced2a2517`. Prior gates: codeanalyzer v3 PASS, tester v13 PASS.
The lazy-deserialize half was already security-PASS on `ca6e2fb7c` (3 info/low,
no critical/high). My scope is what this branch adds on top.

## Scope (what's new and security-relevant)

1. **Strict-kind machinery** — `app.data.IStrictKindEnforcer`,
   `app.data.IKindValidatable`, `app.data.StrictKindMismatchException`.
   Single implementor today: `image.@this`.
2. **`variable.set` strict gates** — three:
   - build-time (`ValidateBuild`, literals)
   - runtime probe (`%var%` resolution)
   - byte-materialization seam (`IStrictKindEnforcer` imprint, deferred to load)
3. **`type.@this`** — `Strict` flag on the wire entity; canonicalised at set.
4. **TryInstantiateValidator** — reflection-ctor probe.
5. **`crypto.Hash` returns `hash.@this`** carrying algorithm — signing pipeline
   now reads algorithm off the value, not off the JSON `type` slot.
6. **`text.Convert`** — new type-owned text serializer.
7. **`file.read` rebased onto `channel.type.file`** — image lift now from `read.Raw`.

## Threats to chase

- **Strict bypass via lazy passthrough.** Does `Value.RawUntouched`
  fast-return at `set.cs:203-209` skip the strict imprint for a wire-arrived
  lazy strict image?
- **Reflection-ctor surface.** `TryInstantiateValidator` GetConstructors-and-
  Invoke pattern — what side-effects can a maliciously-shaped IKindValidatable
  ctor have? (Forward-looking.)
- **ImageSharp parser surface.** `image.ValidateKind` calls `DetectFormat` on
  caller-supplied bytes — pre-verify reachable?
- **Strict×wire/signing carry.** Is `Type.Strict` covered by the canonical
  signing hash? Does an attacker who can mutate wire `strict=true→false` evade
  detection? Does the receiver re-apply strict imprint?
- **Algorithm-injection on signing hash.** New `hash.@this` carries algorithm
  read from wire — bound by outer Ed25519 sig?
- **Semgrep regressions** vs `runtime2` baseline (17 hits).

## Files

- `PLang/app/data/IStrictKindEnforcer.cs`, `IKindValidatable.cs`
- `PLang/app/type/image/this.cs`
- `PLang/app/type/this.cs`, `this.json.cs` (Strict wire shape)
- `PLang/app/module/variable/set.cs`
- `PLang/app/type/text/this.Convert.cs`
- `PLang/app/module/crypto/code/Default.cs`, `type/hash/this.cs`
- `PLang/app/module/signing/Signature.cs`, `code/Ed25519.cs`
- `PLang/app/module/file/read.cs`

## Method

1. Semgrep baseline — confirm finding count.
2. Trace each strict gate end-to-end.
3. Re-walk codeanalyzer v3's "strict×lazy seam is clean" claim from the
   attacker's vantage.
4. Mutation-test claim only if a real-attack path is in doubt (announce first).

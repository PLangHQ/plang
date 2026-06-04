# security v1 result — type-kind-strict — PASS

**HEAD:** `ced2a2517`. Semgrep: 17 findings (same count as `runtime2` baseline;
diff'd below). No critical/high. Three low/info findings; the strict×lazy seam
holds against the attacker walk-through.

## Strict×lazy seam — independently re-traced

The codeanalyzer-v3 claim ("strict images always reach the enforcer stamp")
was the one I was most ready to break. Walked it from the attacker's seat:

1. Wire delivers a Data with `Type = {image, gif, strict=true}` lazily
   (`RawUntouched=true`, `_raw` holds the path-shape JSON).
2. Receiver does `set %img% = %wireValue% as image/gif strict`.
3. `set.cs:181` — `typeEntity.Strict && IKindValidatable` gate fires.
4. `set.cs:184` — `TryInstantiateValidator(targetType, Value.Value)` touches
   `Value.Value`. **This materialises** the lazy raw into an `image.@this`
   instance (path-backed, `_bytes` still null). `RawUntouched` flips to false.
5. The probe ctor try-loop fails to match `image.@this` against any image
   ctor (ctors want `byte[]` or `path.@this`, not `image`), so `probe == null`
   and the probe block returns without validation. **This is the only path
   where ValidateKind is NOT called.**
6. `set.cs:203` lazy-passthrough — `RawUntouched` is now false, so this
   branch is SKIPPED. Critical: the touch at step 4 closes the bypass that
   the passthrough would otherwise have opened.
7. Falls into the `ConstructDataOfT` path. `typedData.Value` is the same
   `image.@this` instance from step 4.
8. `set.cs:264` — `IStrictKindEnforcer.RequireStrictKind(typeEntity.Kind)`
   imprints `_requiredKind = "gif"` on the image.
9. `CheckStrictKind()` returns null (bytes not loaded yet) — deferred.
10. First content access via `image.BytesAsync` reads through `Path.ReadBytes`
    (which gates through `FilePath.AuthGate`, the actor permission boundary),
    THEN evaluates `CheckStrictKind()` → throws `StrictKindMismatchException`
    on a PNG behind `image/gif strict`. Confirmed by
    `PLang.Tests/App/TypeKindStrict/ReferenceFundamentalTests/LazyPathHandleTests.cs:87`.

The forced `Value.Value` at step 4 is load-bearing — it's what prevents the
passthrough at step 6 from skipping the imprint. Mutation-checked by removing
the probe block in my head: the passthrough would then take a strict
wire-image and store it raw with no imprint, BytesAsync would skip the
strict check, and the PNG-as-strict-gif would load without error. The code
is structured so the side-effect of `.Value` materialisation is what closes
the bypass — fragile but correct as written.

## Findings

### F1 (info) — Strict imprint is process-local; threat-model accepted

`Type.@this` ships `Strict=true` on the wire (`this.json.cs:100`). It is
covered by the Ed25519-signed canonical hash, so a network attacker cannot
flip it. **But:** the `_requiredKind` imprint (private field on `image.@this`)
is process-local and lives outside any wire shape. A receiver that does
`set %img% = %wireImage%` **without** an `as ... strict` clause never calls
`RequireStrictKind`, so `BytesAsync` runs without enforcement even if the
sender stamped `Type.Strict=true`.

Per PLang's threat model this is correct: strict is a developer-ergonomic
gate, not a cross-process trust contract. Signing is the trust boundary.
Standing memory already records "imprint is process-local" — kept as a note,
not a bug. Documented here to memorialise the trace.

### F2 (low) — `text.Convert` default `JsonSerializer.Serialize`

`PLang/app/type/text/this.Convert.cs:27` — NEW semgrep hit on this branch.
`JsonSerializer.Serialize(value)` is called with no options when a structured
value (`IDictionary`, `JsonNode`, `IEnumerable`) is bound `as text`.

**Vector:** `set %s% = %dictWithPath% as text`. A dict whose graph reaches a
`path.@this` or any `[Sensitive]` property serialises with the default
converter table — no `PathJsonConverter`, no `SensitivePropertyFilter`, no
`IgnoreCycles`. `path.@this` graphs would emit raw `Absolute` strings
(absolute filesystem paths — info disclosure); `[Sensitive]` fields would
land in plaintext; a cycle (Variables-snapshot pattern) throws.

**Severity:** Low. The `as text` clause is user-opt-in, the structure must
already reach a path/sensitive node, and the resulting text usually flows
to channels the user controls. But the pattern matches the documented
incident (`test/report.cs` Variables-snapshot cycle, this branch's history)
exactly — so it should use
`Context.App.Format.Options` or `Conversion.ContextualReadOptions(Context)`.

**Proposed fix:** thread `context` (already on the signature) into the
serialiser options. Two lines.

### F3 (info) — `hash.@this.DigestEquals` uses `Span.SequenceEqual`

`PLang/app/module/crypto/type/hash/this.cs:77` — `DigestEquals` is
`Bytes.AsSpan().SequenceEqual(other.Bytes)`. `Span<byte>.SequenceEqual` is
not documented constant-time. Used by `signing/Ed25519.cs:VerifyAsync` step 7
(data-hash compare) and by `crypto.verify`.

Practical exploitability: zero. For SHA-256/keccak256 the pre-image
resistance dwarfs any timing recovery — an attacker would have to forge a
hash with controlled internal bytes, which is the same problem they were
already trying to solve. Documented for completeness and as a pre-existing
baseline behaviour; semantics-equivalent to the pre-branch `SequenceEqual`.

If/when PLang grows MAC verification (same key, attacker-controlled input),
this should switch to `CryptographicOperations.FixedTimeEquals`.

## Reflection-ctor probe — narrow today, forward-looking risk

`TryInstantiateValidator` (`set.cs:314`) walks every public ctor of
`targetType` and invokes the first one whose first parameter accepts the raw
value. Today: `targetType` is constrained to `IKindValidatable` types
resolved through the PLang type registry; `image.@this` is the only
implementor. `image`'s ctors are field-assignment-only — no side effects.

The pattern is a **standing forward-looking risk**: as audio/video and
other reference fundamentals adopt `IKindValidatable`, their ctors will be
auto-invoked on user data at `variable.set` time. The discipline must be:
**IKindValidatable types' public ctors must be side-effect free.**
Filing as a note for `pattern_strict_kind_reflection_probe.md` in memory.

## ImageSharp parser surface

`image.ValidateKind` (`image/this.cs:177`) calls
`SixLabors.ImageSharp.Image.DetectFormat(bytes)` on caller-supplied bytes.
Pre-verify reachable through `set %x% = %wireBytes% as image/X strict`. The
catch swallows all parser exceptions (except OOM/SOE).

User explicitly opts into running the validator. `DetectFormat` is
header-only (lightweight; not the full decoder). ImageSharp's CVE history
is mostly around decode, not detect. Info only — same opt-in surface that
`Width`/`Height` already exposed.

## Crypto/signing — algorithm now travels with the hash value

`hash.@this` carries `Algorithm` as `Kind`. `HashDataConverter.Read`
deserialises algorithm from the JSON `"type"` slot (attacker-influenced for
wire-arrived hashes); `VerifyAsync` rehashes using that algorithm. **Bound
by outer Ed25519:** the algorithm string is part of the signed canonical
hash, so wire-tamper to flip algorithm fails Ed25519. No new bypass.

`Hash()` switch accepts only `keccak256` — unknown algorithm returns
`UnsupportedAlgorithm`, which propagates as `DataHashMismatch` through
Verify. No injection into the dispatcher.

## Semgrep delta vs runtime2 baseline

17 → 17 hits. The new finding listed above
(`text/this.Convert.cs:27`) replaces a hit on a deleted/renamed file in the
prior baseline; total unchanged.

## Verdict

**PASS.** No critical/high. Three findings: F1 info (standing note,
threat-model accepted), F2 low (new semgrep hit, narrow exposure path,
two-line fix), F3 info (pre-existing baseline). Strict×lazy seam
independently traced and holds.

Next: `auditor` to cross-check the codeanalyzer-v3 / tester-v13 / security-v1
chorus on the merged branch.

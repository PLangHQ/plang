# security — type-kind-strict — summary

**Version:** v1
**Verdict:** PASS

## What this is

Security audit of the `type-kind-strict` branch, which by `d4fdd030c` is
merged with `lazy-deserialize`. The branch adds strict-kind enforcement for
reference fundamentals: `as image/<kind> strict` carries a `{kind, strict}`
imprint with the value so a path-backed image throws
`StrictKindMismatchException` when its bytes load and don't match the
declared kind. Implementation is two interfaces (`IKindValidatable`,
`IStrictKindEnforcer`) on `app.data`, with `image.@this` as the sole
implementor today, three gates inside `variable.set` (build / runtime probe
/ byte-materialisation imprint), and a `crypto.Hash` rename so the hash
value carries its own algorithm.

Lazy-deserialize's body was already security-PASS on `ca6e2fb7c`; the
concern this audit chased was the **strict × lazy seam** — would the
`RawUntouched` verbatim-passthrough at `set.cs:203-209` let a wire-arrived
strict image skip the `IStrictKindEnforcer.RequireStrictKind` imprint? It
does not — see code-example below.

## What was done

Re-traced the strict×lazy seam end-to-end from the attacker's seat. Read
`PLang/app/data/IStrictKindEnforcer.cs`,
`PLang/app/data/IKindValidatable.cs`,
`PLang/app/type/image/this.cs`,
`PLang/app/type/this.json.cs`,
`PLang/app/module/variable/set.cs`,
`PLang/app/type/text/this.Convert.cs`,
`PLang/app/module/crypto/code/Default.cs`,
`PLang/app/module/crypto/type/hash/this.cs`,
`PLang/app/module/signing/Signature.cs` (HashDataConverter),
`PLang/app/module/signing/code/Ed25519.cs` (step 7 hash verification),
`PLang/app/module/file/read.cs`.

Ran `scripts/semgrep-scan.sh` — 17 findings, same count as the `runtime2`
baseline. One **new** hit on this branch (`text.Convert.cs:27`); offset
exactly one by a deleted/renamed file in the prior tree, so count is flat.

Findings (none critical/high):

- **F1 (low, open)** — `text.Convert.cs:27` `JsonSerializer.Serialize` with
  no options. Vector: `set %t% = %dictWithPath% as text` leaks
  `path.@this.Absolute` and `[Sensitive]` fields through the default
  converter table. Two-line fix.
- **F2 (low, accepted)** — `_requiredKind` imprint is process-local.
  Wire-stamped `Strict=true` does NOT auto-impose strict on the receiver
  side without an explicit `as ... strict` clause. Threat-model accepted:
  signing is the trust boundary; strict is a developer ergonomic.
- **F3 (info, accepted)** — `hash.@this.DigestEquals` uses
  `Span<byte>.SequenceEqual` (not constant-time). Equivalent to the
  pre-branch `SequenceEqual`; not practically exploitable for
  cryptographic-strength digests. Would matter only with MAC verification.

Forward-looking note recorded for memory: as more reference fundamentals
adopt `IKindValidatable` (audio/video planned), their public ctors are
auto-invoked on user data by `TryInstantiateValidator` — discipline must be
**IKindValidatable ctors must be side-effect-free.**

## Code example — the strict×lazy seam

```csharp
// set.cs:181-195 — strict probe runs FIRST, touches .Value (materialises lazy raw)
if (typeEntity.Strict && typeEntity.Kind != null
    && typeof(global::app.data.IKindValidatable).IsAssignableFrom(targetType))
{
    var probe = TryInstantiateValidator(targetType, Value.Value);  // ← flips RawUntouched=false
    if (probe is global::app.data.IKindValidatable v) { /* check, error if mismatch */ }
}

// set.cs:203-209 — lazy passthrough — SKIPPED because RawUntouched is now false
if (Value.RawUntouched && Value.Type is { } vt && /* ... */) { return ...; }

// set.cs:264-274 — strict imprint always reached for the strict-flagged path
if (typeEntity.Strict && typeEntity.Kind != null
    && typedData.Value is global::app.data.IStrictKindEnforcer enforcer)
{
    enforcer.RequireStrictKind(typeEntity.Kind);
    if (enforcer.CheckStrictKind() is { ok: false } mismatch) return /* error */;
}
```

The `.Value` access at the probe line is load-bearing — it's what forces
materialisation so the passthrough below sees `RawUntouched=false` and
falls through to the enforcer stamp. Fragile but correct. The receiver-side
`image.BytesAsync` then re-checks at byte-load and throws
`StrictKindMismatchException` on mismatch — pinned by
`PLang.Tests/App/TypeKindStrict/ReferenceFundamentalTests/LazyPathHandleTests.cs:87`.

## Next bot

`auditor` — to cross-check codeanalyzer v3 + tester v13 + security v1 on
the merged branch.

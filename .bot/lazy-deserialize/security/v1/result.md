# security v1 — lazy-deserialize — result

**Verdict: PASS.** No critical or high findings on the lazy-deserialize-specific
surface. Five low/info notes recorded below; carry-forward memory items
unchanged.

## Audit scope

The lazy Data mechanism: `Data.FromRaw` + `Materialize()` (touch-time read
through the per-(type, kind) reader registry), the `Wire.Read` deferred-raw
branch for shape-typed values (`object/<kind>`, `table/<kind>`), the
`Wire.Write` verbatim-passthrough leg + `EmitRawVerbatim`, the `channel.read`
single-boundary stamping (`StampReadAsync` / `StampValue`), and the
interaction with the signing pipeline (`crypto.Hash` /
`Wire.MarkOuterForHash` / `signing.verify` / `Ed25519.VerifyAsync`).

Files read end-to-end:

- `PLang/app/data/this.cs` — FromRaw, Materialize, RawUntouched, ScalarValue,
  AsCanonical, ShallowClone, FromWireShape, IsWalkableContainer.
- `PLang/app/data/Wire.cs` — ReadBody deferred-raw branch, MaxReadDepth,
  EmitRawVerbatim, EnsureInnerSigned, MarkOuterForHash, the
  inner-walk-skip for RawUntouched on Write.
- `PLang/app/type/reader/this.cs` — reader registry (generated + runtime,
  same precedence as the renderer).
- `PLang/app/channel/this.cs` — StampReadAsync, StampValue, StampType,
  IsBinaryShape.
- `PLang/app/channel/type/file/this.cs` and `http/this.cs` — the new
  source-providers feeding `channel.read`.
- `PLang/app/channel/serializer/plang/this.cs` — the per-actor inbound/outbound/
  store/snapshot wire options; the "Read does NOT auto-verify" contract.
- `PLang/app/module/crypto/code/Default.cs` — Hash + Verify canonicalization.
- `PLang/app/module/file/read.cs` — file.read post-boundary path.
- `PLang/app/module/variable/set.cs` Run() — the lazy-preserving binding leg.
- `PLang/app/goal/GoalCall.cs` — Convert + LoadFromFile + parameter clone.
- `PLang/app/type/this.json.cs` — type entity wire converter (canonical order).

## Threat-model questions answered

**Q1. Does Wire.Read ever materialize the lazy value before signature verify?**
No. `ReadBody` either captures the slot as a raw string (deferred path, lines
280–287) or runs `LiftDataIfShaped` which deserializes the nested-Data subtree.
Neither calls `.Value` on the constructed Data. Signature is attached but
unverified — `signing.verify` (or a `BeforeRead` event binding) is the
documented verification seam, unchanged from prior behavior. Documented at
`PLang/app/channel/serializer/plang/this.cs:30–34`.

**Q2. Does the canonical hash round-trip stay byte-identical for a relayed
`RawUntouched` Data?** Yes, by construction.
- `name` and `signature` are suppressed in the outer-hash scope
  (`MarkOuterForHash`).
- `type` is emitted through its own JsonConverter
  (`PLang/app/type/this.json.cs`) which writes a fixed key order
  (`name`, then `kind?`, then `strict?`).
- `value` is emitted via `EmitRawVerbatim`: `WriteRawValue(s)` for
  `object/json` (byte-identical to the original `GetRawText()` capture);
  `WriteStringValue(s)` for other shapes (the decoded string re-encoded
  through STJ's canonical encoder — symmetric round-trip when the sender
  also used STJ canonical).
- `properties` iterates a `Dictionary<string, object?>` whose .NET enumeration
  is insertion order — deterministic per-sender.

A sender that produces non-canonical bytes (third party, non-STJ writer,
permuted keys) will fail verify on the receiver — conservative-correct.

**Q3. Can a wire-attached `type.kind` redirect bytes to a different parser
than the signer intended?** No. The `type` slot is part of the signed wire
shape; modifying `kind` invalidates the signature. Verify recomputes the
hash off `(type, value, properties)`; any drift in `type` produces a
mismatch.

**Q4. Does lazy parsing widen the parser-DoS surface (billion-laughs,
zip-bomb, recursive XML/YAML)?** Net-neutral. The same parser, the same
payload, the same context — just dispatched at `.Value` access time rather
than at `channel.read` time. Scalar use (`%x%`) and verbatim relay never
trigger the parser; only navigation (`%x.field%`) and explicit `as` casts
do. Slight defense-in-depth improvement for relay paths.

**Q5. Is the deferredRaw size bounded?** Not at the Wire layer.
`JsonDocument.ParseValue(ref reader)` is bounded only by upstream stream
size and STJ's per-call MaxDepth. The stored `string` lives on the Data
until first touch (or until the variable is replaced). Standing concern,
not a regression — the `Channel.Stream.ReadAllBytesAsync ignores Buffer`
memory item already covers the upstream cap gap.

**Q6. Does Materialize's catch-all silently absorb security exceptions?**
The catch is `Exception ex when ex is not (NullReferenceException or
OutOfMemoryException or StackOverflowException)`. Today's registered kind
readers (json/xml/yaml/csv/png/etc.) raise `FormatException`, `JsonException`,
`InvalidOperationException` — legitimate "failed to read" cases. None of the
current readers raise `SecurityException` / `CryptographicException`. Latent
concern documented as F2.

## Findings

### F1 — Lazy parse defers parser-DoS surface to first touch (informational)

**Severity:** informational.
**Category:** resource-exhaustion (latent).

`Data.Materialize()` dispatches to the per-(type, kind) reader on first `.Value`
access. A wire-arriving Data with `type={object, yaml}` and a YAML
billion-laughs payload would parse on the receiver only when something
references a sub-field. Net-neutral vs. the prior eager path (same parser,
same payload, same actor context). Verbatim-relay paths never trigger the
parse — a small defense-in-depth improvement for routers/forwarders.

**Status:** no action. Noted as a property of the lazy seam.

### F2 — `Materialize()` catch-all could mask future security-class exceptions (low)

**Severity:** low (latent).
**Category:** info-disclosure / control-flow (defense-in-depth).
**Affected files:** `PLang/app/data/this.cs:296–305`.

```csharp
catch (System.Exception ex) when (ex is not (System.NullReferenceException
    or System.OutOfMemoryException or System.StackOverflowException))
{
    var real = (ex as System.Reflection.TargetInvocationException)?.InnerException ?? ex;
    Error = new global::app.error.Error(...);
    return null;
}
```

Today's readers (json/xml/yaml/csv/image/etc.) raise `FormatException`,
`JsonException`, `InvalidOperationException` — all legitimate "failed to read"
cases the design wants to surface as a touch-time Error. If a future reader
integrates cryptographic verification or input-policy enforcement and raises
`CryptographicException` / `SecurityException` / a typed
`AuthorizationDeniedException`, this catch would silently turn it into a
benign Error Data with `Value == null`. Callers that don't check `.Error`
before re-using the Data could end up with the value silently dropped.

**Proposed fix (when relevant):** Add the security-exception types to the
not-caught list as new readers ship that can raise them. Keep this finding
visible by linking from `memory/discipline.md`'s "catch narrowing requires
throw-site enumeration" guidance.

**Status:** open (latent — no current reader raises such exceptions).

### F3 — DeferredRaw size unbounded at Wire layer (low, standing)

**Severity:** low (latent; same shape as Channel.Stream buffer-ignore).
**Category:** resource-exhaustion.
**Affected files:** `PLang/app/data/Wire.cs:282–286`.

`JsonDocument.ParseValue` reads the whole value slot into a buffered subtree;
`GetRawText()` / `GetString()` captures it as a `string` on the Data, held
verbatim until first touch or variable replacement. For a 100MB signed JSON
object held across the goal graph, this raises working-set vs. the prior
eager-then-discard pattern. Bounded by upstream channel input limits (which
are themselves bounded only for the non-Stream channels — see standing
memory item on `Channel.Stream.ReadAllBytesAsync` ignoring `Buffer`).

**Status:** open, but tracked under the existing Stream-channel buffer
memory item rather than as a new finding — fixing the upstream cap covers
this leg.

### F4 — Canonical hash round-trip verified by inspection (informational)

**Severity:** informational (positive note for auditor follow-through).
**Affected files:** `PLang/app/data/Wire.cs:438–531`,
`PLang/app/type/this.json.cs`, `PLang/app/module/crypto/code/Default.cs:24–72`.

The lazy round-trip leg — `Wire.Read` captures `deferredRaw` → relay holds
`RawUntouched` → `crypto.Hash` under `MarkOuterForHash` re-emits via
`EmitRawVerbatim` → SHA3/SHA256 → compare with stored hash on the
`Signature` — produces byte-identical output for sender-canonical input.

Key invariants the design relies on:

1. Wire.Write emits keys in fixed order: `name` (suppressed in outer-hash),
   `type`, `value`, `properties`, `signature` (suppressed in outer-hash).
2. The `type` entity converter
   (`PLang/app/type/this.json.cs`) writes `name` first, then `kind?`,
   then `strict?` — deterministic regardless of construction site.
3. `Properties` iteration is .NET `Dictionary` insertion order — deterministic
   per-sender (a sender that re-orders inserts between sign-time and emit-time
   would break verify on itself, not just on relays).
4. `EmitRawVerbatim` preserves the byte form of the value slot exactly for
   `object/json` (raw write) and round-trips through STJ canonical
   string-encoding for other shapes (symmetric under canonical sender/receiver).
5. The hash carve-out (`MarkOuterForHash` ref-counted on
   `ReferenceEqualityComparer` keys) composes correctly under nested Hash
   calls.

**Status:** verified. No action.

### F5 — Wire.Read does not auto-verify signatures (informational)

**Severity:** informational.
**Affected files:** `PLang/app/channel/serializer/plang/this.cs:30–34`.

The reconstructed Data carries its Signature populated-but-unverified.
Verification is the consumer's explicit step (`signing.verify`, or a
`BeforeRead` event binding). Parity with the prior eager-read behavior on
runtime2 — not a regression.

**Status:** noted; standing design contract.

## Standing carry-forward memory items (cross-reference)

These open items in `MEMORY.md` were re-checked on this branch and remain
unchanged in severity. None are new on lazy-deserialize:

- `Variables.Snapshot()` `[Sensitive]` leak (medium).
- `OpenAiProvider` image `ReadAllBytes` no size cap (medium).
- Conversation continuity unbounded message accumulation (medium).
- `callback.run` skip-verify when `RawSignature==null` (medium, latent).
- Callback wire serializers don't apply `SensitivePropertyFilter` (low).
- `Channel.Stream.ReadAllBytesAsync` ignores `Buffer` (medium today / would
  match the HTTP body cap finding the moment a non-stdin Stream channel ships).
- `AppChannels.Channel(string)` no executing-guard parity gap (low, latent).
- `MigrationEnvelope.Signature` keyless integrity hash with PKI-shape fields
  (low, latent).

## Semgrep baseline

`scripts/semgrep-scan.sh` ran clean against the architectural invariants
(LoadFrom whitelist, console-write ban, lock-public-collection, verified-setter,
serializer-default-options). 17 hits total; same count and same files as the
`runtime2` merge-base — no regression. The `http/code/Default.cs` and
`identity/code/Default.cs` JsonSerializer hits are pre-existing serializer-
hygiene candidates (already on the audit list in
`memory/reference_semgrep_ruleset.md`), not new on this branch.

## Verdict

PASS. No critical or high findings on the lazy-deserialize-specific surface.
Five low/info notes recorded. The signing pipeline interacts cleanly with
verbatim passthrough: the hash round-trip is byte-identical for legitimate
relayed lazy Data, and any non-canonical mutation invalidates the signature.

**Next bot:** `auditor`.

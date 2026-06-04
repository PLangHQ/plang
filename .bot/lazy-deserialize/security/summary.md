# security — lazy-deserialize — summary

**Version:** v1
**Verdict:** PASS

## What this is

Security audit of the `lazy-deserialize` branch — 162 commits off `runtime2`
making `Data` lazy. A `Data { name, type, kind, value }` now stores its raw
source form and materializes on first `.Value` touch via a per-(type, kind)
reader registry (`app/type/<name>/serializer/<kind>.cs`). `channel.read` is
the single read boundary; `file.read` and `http.get` become thin source
providers. `Wire.Read` defers the value slot for shape-typed Data
(`object/<kind>`, `table/<kind>`); `Wire.Write` re-emits a `RawUntouched`
Data verbatim so the canonical hash recomputed during `signing.verify` is
byte-identical to the wire bytes the sender signed.

The security concern this audit chased: lazy materialization sits next to
the signing trust boundary. Does the deferral bypass verification, widen
the parser-confusion surface, break the canonical-hash round-trip, or
silently swallow security exceptions?

## What was done

Read end-to-end:
- `PLang/app/data/this.cs` — FromRaw, Materialize, RawUntouched, ShallowClone, AsCanonical, FromWireShape.
- `PLang/app/data/Wire.cs` — ReadBody deferred path (lines 280–287), MaxReadDepth (64), EmitRawVerbatim, EnsureInnerSigned, MarkOuterForHash, the RawUntouched inner-walk-skip on Write.
- `PLang/app/type/reader/this.cs` — the reader registry.
- `PLang/app/channel/this.cs` — StampReadAsync / StampValue / StampType.
- `PLang/app/channel/type/file/this.cs`, `http/this.cs` — new source providers.
- `PLang/app/channel/serializer/plang/this.cs` — the "Read does not auto-verify" contract (lines 30–34).
- `PLang/app/module/crypto/code/Default.cs` — Hash + Verify canonicalization.
- `PLang/app/module/file/read.cs`, `PLang/app/module/variable/set.cs`, `PLang/app/goal/GoalCall.cs`, `PLang/app/type/this.json.cs`.

Ran `scripts/semgrep-scan.sh` — 17 hits, same count and same files as the
`runtime2` merge-base; no regression vs. baseline.

## Findings

None critical or high.

- **F1 (info)** — Lazy parse defers parser-DoS surface (billion-laughs/zip-bomb-class) from read-time to first touch. Net-neutral; scalar/relay use never triggers parse.
- **F2 (low, latent)** — `Materialize()` catch-all (`PLang/app/data/this.cs:296`) would silence a future security-class exception from a kind reader. Today's readers don't raise such; add `CryptographicException`/`SecurityException` to the not-caught list as new readers ship.
- **F3 (low, standing)** — DeferredRaw size unbounded at Wire layer. Tracked under the existing `Channel.Stream.ReadAllBytesAsync` ignores-Buffer memory item; fixing the upstream cap covers this leg.
- **F4 (info)** — Canonical hash round-trip is byte-identical for sender-canonical input. Verified by inspection: Wire.Write fixed-order keys, deterministic type-entity converter, Dictionary insertion-order Properties, verbatim raw emission. Non-canonical third-party wire fails verify (conservative-correct).
- **F5 (info)** — Wire.Read does not auto-verify signatures — documented design contract, parity with prior behavior.

Standing carry-forward memory items unchanged; none are new on this branch.

## Code example — the lazy-vs-signing handshake

```csharp
// Wire.cs:280 — defer the value slot when typeRef matches a shape-typed kind.
if (typeRef != null && IsDeferrableShape(typeRef))
{
    using var vdoc = JsonDocument.ParseValue(ref reader);
    var el = vdoc.RootElement;
    deferredRaw = el.ValueKind == JsonValueKind.String
        ? el.GetString() ?? ""
        : el.GetRawText();   // ← byte-identical capture
}

// Wire.cs:496 — emit verbatim on Write so the canonical hash round-trips.
if (data.RawUntouched)
    EmitRawVerbatim(writer, data);     // WriteRawValue for object/json,
                                       // WriteStringValue for other shapes
```

`crypto.Hash` under `MarkOuterForHash` re-emits the value slot through this
verbatim path, suppresses `name` + `signature`, hashes the result. A relayed
lazy Data therefore verifies against the original signer's hash without
parsing the inner value.

## Next bot

`auditor` — to cross-check codeanalyzer v2 + tester v3 + security v1 verdicts
and confirm the merge committee story holds end-to-end.

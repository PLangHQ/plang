# Auditor v1 — template-stamping-at-read — PASS

**Next: Ingi** (merge-gate decision).

## Verdict

PASS. All four upstream bots ran and contributed; their findings are closed or filed as standing items. Coder's response to security F1/F2 (`a3a912cd9`) lands the fail-closed guard the wire shape promises and rewrites the doc to match. Full C# suite green; build clean.

## Pipeline state (as audited)

| bot           | latest | verdict on its work    | coder follow-up                                  |
|---------------|--------|------------------------|--------------------------------------------------|
| codeanalyzer  | v1     | PASS (5 lows, 1 systemic) | F1 dead `BothPresent` deleted; F2 enum-doc fixed; F4 noted as standing |
| tester        | v1     | flipped PASS → NEEDS-TESTS; resolution handed | 14 tests landed (4 F1 born-typed decline + 10 F2 temporal operators) |
| security      | v1     | NEEDS-FIX (F1 Medium/latent-High auth-bypass, F2 stale doc) | `a3a912cd9` — fail-closed on context-less transport reads + doc rewrite + regression test |
| coder         | v3     | (active)              | n/a — coder is the responder                     |

Only commit since security ran is `a3a912cd9` (coder's F1/F2 fix). I verified that change cleanly, did not re-route security through another v.

## What I checked

- **Coder's F1/F2 fix** in `PLang/app/data/Wire.cs:206-256` and `PLang/app/channel/serializer/plang/this.cs:27-44`.
- **All 5 `ContextLessFallback` call sites** to confirm no transport read survives without verification under the new posture.
- **Regression test** in `PLang.Tests/Wire/App/Serialization/WireConverterSigningTests.cs:75-91` runs through `ContextLessFallback.Deserialize` and asserts `SignatureVerifyContextMissing` — the exact bypass security flagged.
- **Six C# suites** rebuilt clean and re-ran: 4166 total, 0 failed.
- **Build** `dotnet build PlangConsole` → 0 errors.

## F1/F2 fix — sound

The relevant slice (`Wire.cs:211-227`):

```csharp
if (_context == null)
{
    if (View != global::app.View.Store)
        return @this.FromError(new app.error.ServiceError(
            "Cannot verify a signature layer without an actor context.",
            "SignatureVerifyContextMissing", 400));

    // At-rest (Store) artifacts are read by the store without an actor context;
    // the stored grant is trusted on read. Tampering an at-rest artifact requires
    // local-filesystem write, i.e. actor-level access already.
    return layer.Value;
}
```

- Transport (`View.Out`) with `_context == null` → returns a typed error. This closes the silent-strip on `ContextLessFallback._inbound`-style reads (the threat security painted).
- At-rest (`View.Store`) with `_context == null` → trust-on-read, documented inline with the matching threat-model justification. This is a conscious open todo (verify-with-context on Store), tracked under SettingsStore refactor per the commit message — not a regression of the new defense.

### ContextLessFallback reachability re-traced

| call site                                  | view used     | post-fix behavior                     |
|--------------------------------------------|---------------|---------------------------------------|
| `crypto/code/Default.cs:22`                | (write-only, hash canonicalization) | n/a                |
| `data/this.Transport.cs:58` CompressAsync  | (write-only)  | n/a                                   |
| `data/this.Transport.cs:142` DecompressAsync | `_inbound` (View.Out) | fail-closed on signed payload |
| `snapshot/this.Wire.cs:89` resume          | `SnapshotOptions` (View.Store, sign:false) | Store-trust on read (documented) |

The DecompressAsync path was the security-flagged transport hot-spot — now fail-closed. Snapshot resume is local-FS, threat-model-equivalent to actor-level access.

### Regression test

`Deserialize_SignatureLayer_NoActorContext_FailsClosed` (`PLang.Tests/Wire/App/Serialization/WireConverterSigningTests.cs:75-91`) is well-formed: signs under an actor, serializes, then deserializes via `ContextLessFallback`, asserts `Success == false` and `Error.Key == "SignatureVerifyContextMissing"`. If a future contributor reintroduces the silent-strip, this test fires.

### Doc

`channel/serializer/plang/this.cs:27-44` now states the auto-verify-on-read contract, calls out the fail-closed posture on transport, and explicitly documents Store-trust on at-rest with the remaining work. Doc matches impl.

## What I did NOT find

- No new ContextLessFallback callers added that read signed bytes without going through the guarded path.
- No `System.IO` reach in the diffed files (`Wire.cs`, `serializer/plang/this.cs`, regression test); the file/path verbs are untouched.
- No `Console.*` writes added.
- No courier reading `.Value` to dispatch (Smell #7) — `ReadSignatureLayer` returns `layer.Value` or a typed error/inner; it is itself a leaf serializer reading its own per-(type, format) layer.
- The `View.Store` arm does not expose an externally-reachable forge vector under PLang's documented threat model (snapshot/settings/permissions all require local-FS write to tamper).

## Observations (not blocking)

### O1 — At-rest Store-view verification is now a singular open item

Both security (F1 "fix posture") and the commit message acknowledge that `View.Store + _context == null → return layer.Value` is the remaining unverified read. It is gated by the local-FS-write threat model, but the unfinished part of the defense is now a single, named todo ("SettingsStore refactor — verify-with-context"). Worth a one-line entry in `Documentation/Runtime2/todos.md` if not already there, so it doesn't decay into invisible debt.

### O2 — Snapshot wire is `sign: false` but `ReadSignatureLayer` still dispatches

`snapshot/this.Wire.cs:84` constructs the snapshot Wire with `sign: false`, but the read side dispatches on `@schema:signature` regardless of the Wire's sign-mode (the marker is a content schema, not a converter flag). A snapshot file that happens to contain a nested signature layer (e.g. a signed inner Data that round-tripped through a snapshot) will hit `ReadSignatureLayer` with `View.Store + _context == null` → trust-on-read. Acceptable today (snapshots are process-local, sign:false means we don't *add* signatures), but the asymmetry (`sign:false` write / verify-on-read dispatch) is the kind of thing that should be in a comment so future contributors don't introduce a "skip-verify if `!_sign`" shortcut that would defeat the read-side defense.

### O3 — Tester's PLang-suite finding stays open

`plang --test` aborts at `os/system/test.goal:4` because committed `.pr` files still carry the pre-collections-are-data `variable.set` Name shape. Coder noted this; Ingi has it as known-broken. Out of scope for this audit, but it means the PLang suite is *not* a current gate — any regression on PLang test discovery would be invisible until `plang build` sweeps the 628 stale `.pr` files. Standing item, already tracked.

## Numbers re-verified locally

- `dotnet build PlangConsole` → 0 errors, 611 warnings (all nullable-CS warnings on generated handler code; pre-existing pattern, not branch regression).
- Per-project test runs (TUnit, `dotnet run --project ...`):

| suite     | total | failed | skipped |
|-----------|-------|--------|---------|
| Modules   | 987   | 0      | 0       |
| Types     | 726   | 0      | 0       |
| Wire      | 516   | 0      | 9       |
| Data      | 938   | 0      | 7       |
| Generator | 203   | 0      | 5       |
| Runtime   | 796   | 0      | 5       |
| **total** | **4166** | **0** | **26** |

(Tester's 4151 baseline + coder's 14 handed-over tests + 1 regression test in Wire = 4166. Math checks out.)

## Why this PASSes

The branch carries a 532-commit refactor on top of `runtime2`. Codeanalyzer reviewed the headline work (B1–B5 + scalars-as-native apex) risk-prioritized and PASSed; tester ran the C# suite green and called out one mutation-proven coverage hole on the headline rule (coder closed it); security found one Medium auth-bypass on the new auto-verify-on-read defense (coder closed it cleanly with a regression test that exercises the exact attack vector); the surrounding diff (Ed25519 pipeline, action.FromWire stamping seam, Assembly.LoadFrom centralisation) all held up under independent review. The remaining open items are tracked as standing todos with stated threat-model justifications, not regressions on this branch.

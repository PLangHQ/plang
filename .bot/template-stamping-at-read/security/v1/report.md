# Security review — template-stamping-at-read v1

**Branch:** `template-stamping-at-read` (532 commits off `runtime2`)
**Date:** 2026-06-19
**Reviewer:** security
**Prior gate:** tester PASS (after coder F1/F2 follow-ups landed 14 tests).

## Scope

Risk-prioritised on changed security surface, not line-by-line over 361
production files. Focus:

1. Signing / Ed25519 (heavy churn — `+211/-?` on `Ed25519.cs`, signature
   becomes a wire layer)
2. `Wire.cs` schema-dispatch (new auto-verify-on-read)
3. Template-stamping seam (`Data.Authored()` / `StampTemplates()`) — could
   it stamp wire-arriving payloads?
4. `Assembly.LoadFrom` migration (now centralised via `path.LoadAssemblyAsync`)
5. Baseline semgrep (15 known → 16 hits, delta = 1)

## Verdict

**NEEDS-FIX.** One Medium auth-bypass on the new wire-read seam plus one
documentation-vs-implementation drift on a security boundary. Build green,
no Critical, no High. The bypass requires a context-less `Wire` reading a
`@schema:signature` payload — narrow today (snapshot resume,
`Data.DecompressAsync` fallback), but it's the kind of fail-open on a
freshly-shipped defense that ratchets to High the moment a new ingest seam
picks up `ContextLessFallback`.

## Findings

### F1 [Medium, latent→High] — Context-less Wire silently strips signature verification on read

**File:** `PLang/app/data/Wire.cs:206-240` (`ReadSignatureLayer`)

The new auto-verify-on-read dispatches on `@schema:signature` and runs the
`signing.verify` action — *only when* the converter instance has an actor
context:

```csharp
// Wire.cs:212-240
layer.Value.Context = _context!;          // null-forgive; _context may be null

if (_context != null)
{
    var verifyResult = _context.App
        .RunAction(verifyAction, _context).GetAwaiter().GetResult();
    if (!verifyResult.Success)
        return @this.FromError(...);
}

var inner = layer.Value;
inner.Context = _context;
return inner;                              // unverified inner is returned
```

A `Wire` constructed without a context (`new Wire(view, ...)` with
`context: null`, or the singleton `ContextLessFallback`) treats the
signature layer as a free unwrap: the inner Data is returned with no
verify, no error, no warning. The defense looks present in the trace —
`ReadSignatureLayer` is called, the layer is rebuilt — but its only
security action is gated on a field that is allowed to be null.

#### Trace to reachable surfaces

- `app/channel/serializer/plang/this.cs:133` —
  `public static readonly @this ContextLessFallback = new @this();`
  (constructed via `@this()` → `this(null)` → `Wire(View.Out, context: null)`).

- `app/snapshot/this.Wire.cs:89` —
  `Snapshot.WireOptions(null)` returns
  `ContextLessFallback.SnapshotOptions`. Reached from
  `Snapshot.FromWire(string raw, string? kind)` (`this.Wire.cs:83`),
  the type-registry conversion seam used by `Data.As<snapshot>`.

- `app/data/this.Transport.cs:142` — `Data.DecompressAsync` falls back to
  `ContextLessFallback` when `this._context?.Actor?.Channel.Serializers`
  resolves to null.

- Any future code path constructing `new Wire(view, context: null)` or
  reusing `ContextLessFallback`.

#### Threat

An attacker who can land a `@schema:signature` payload at any of these
ingest seams (compressed blob, snapshot file on disk, future
externally-fed deserializer) is "verified" by virtue of the receiver
having no actor context to check against. The wire shape itself attests
*"this is signed, you must verify before trusting"* — the protocol is
intact; the reader is broken.

#### Severity rationale

By the PLang severity heuristic: "Stub-quality concerns get rated by
their *latent* failure mode, not their current reachability." (Cf.
`/memory/feedback_pre_auth_parse_severity.md`,
`/memory/discipline.md`.)

- Today: snapshot resume and `Data.DecompressAsync` are the only
  reachable paths — both are mostly-internal. Local-FS-write attacker
  on the snapshot dir already has actor-level access.
- Latent: this is a fresh defense (the `auto-verify-on-read` patch is
  `50963ed18`). The very first new producer/consumer that ships a
  `ContextLessFallback` read of externally-sourced bytes turns this
  silent-strip into the auth-bypass the wire shape promised to prevent.

This matches the pattern already in memory under
`callback.run` (skips verify when `RawSignature==null` — "Medium today,
turns Critical/RCE-class the moment a channel deserialises a wire into
`Data<ICallback>`"). Same shape, same fix posture.

#### Fix posture

Fail-closed in `ReadSignatureLayer` when `_context == null`. The wire
explicitly marks itself as signed; a reader that cannot verify must not
unwrap. Pseudocode:

```csharp
if (_context == null)
    return @this.FromError(new ServiceError(
        "Cannot verify a signature layer without an actor context.",
        "SignatureVerifyContextMissing", 400));
```

Callers that legitimately need to read signed wire without an actor
(test fixtures, `crypto.Hash` canonicalisation) should already be on the
non-signing paths (`Wire(..., sign: false)` and the `MarkOuterForHash`
suppression). If they're not, the right fix is to give them an explicit
context — not to silently strip the attestation.

#### Mutation test

Not strictly necessary; the path is unambiguous on inspection.
Suggested regression test if coder wants one:

1. Build `signedData` under actor A (sign-on-write fires).
2. Serialize via `ContextLessFallback.Serialize(signedData)`.
3. Manually mutate the inner `value` slot in the JSON.
4. `ContextLessFallback.Deserialize(mutated)` should return a Data
   carrying a `SignatureInvalid`/`SignatureVerifyContextMissing` error,
   not the mutated inner.

Today step 4 succeeds and returns the mutated inner as if verified.

### F2 [Low / Docs] — XML doc on `application/plang` serializer contradicts the implementation

**File:** `PLang/app/channel/serializer/plang/this.cs:29-34`

The class-level summary states:

> Read does NOT auto-verify — verification is the consumer's explicit
> step (`signing.verify` action, or a channel event handler bound to
> `BeforeRead`/`AfterRead`). The reconstructed Data has its signature
> populated-but-unverified.

The implementation since commit `50963ed18` does auto-verify on read
(when context is present — see F1). The XML doc is the previous
architecture's contract, frozen as a stale promise.

Why this is a *security* concern, not just a docs nit:

- Future contributors planning new wire paths will read the doc, decide
  "I need to wire an explicit verify step here," and either duplicate
  the work or — worse — convince themselves they don't need to bother
  because the channel layer will do it. The contract is part of the
  threat model.
- The fix to F1 changes the contract again (fail-closed vs. silent
  skip). The doc should land that contract authoritatively, not the
  pre-50963ed18 one.

#### Fix

Rewrite the paragraph to describe the actual behaviour: "Read
auto-verifies any `@schema:signature` payload it encounters via the
`signing.verify` action. The freshness/nonce window is skipped on the
Store view (at-rest artifacts re-present the same nonce by design).
Reading a signature layer without an actor context [pending F1 fix:
fails closed / today: silently strips] — production reads always carry
a context via the per-actor serializer."

## Reviewed and clean

These surfaces were the highest-risk delta on this branch; all hold up.

- **`Ed25519.cs` verify pipeline (lines 66-137).** 9-step pipeline
  preserved (matches `/memory/pattern_signing_verification_pipeline.md`).
  NowUtc → wall-clock fallback at the deserialize boundary (commit
  `50963ed18`) is justified — the boundary-verify path runs outside any
  mid-step context, and a few-ms drift between `NowUtc` and
  `UtcNow` cannot widen the freshness window beyond the configured
  `TimeoutMs`. `Contracts == null` correctly triggers
  `hasRequired=false`; `ContractsMatch` rejects any payload whose signed
  contracts disagree with the required set. No silent contract bypass.
- **`action.FromWire` + `StampTemplates`
  (`goal/steps/step/actions/action/this.FromWire.cs:49`).** The
  template-stamping seam IS reachable from wire-rebuilt actions
  (`error.handle.Actions`, compile-response rebuild). But on the
  channel-inbound path, the wire arrives through the per-actor
  serializer (`channel/serializer/plang/this.cs:62`) which carries
  context, so the outer signature is verified before the stamped
  actions are ever executed. The "runtime input never passes here"
  comment in `data/this.cs:461` holds — *as long as F1 is fixed.*
  Without the fix, a context-less ingest of a signature layer could
  unwrap to inner data that itself carries a stamped action chain — the
  composition lifts F1 from "trusted data slip" to "stamped recovery
  action executed under victim identity." Folded into F1.
- **`Assembly.LoadFrom` centralisation
  (`PLang/app/type/path/file/this.Operations.cs:32`).** New semgrep
  whitelist hit, but the call is now behind
  `AuthGate(Verb.Execute)` — a new permission distinct from `Read`. This
  is a *security improvement* over the prior raw `Assembly.LoadFrom` in
  `Module.add`/`code.load`. Update `.semgrep/` to add
  `path/file/this.Operations.cs:32` to the whitelist; the prior call
  sites in `module.add`/`code.load` now route through it. Note: the
  semgrep rule's whitelist is stale (still names
  `Module.add`/`provider.load`) — update alongside.
- **Semgrep baseline.** 16 blocking findings vs. the documented 15.
  Spot-checked the delta — every hit is either a long-standing
  serializer-hygiene audit-list entry or an unchanged
  `JsonSerializer.Serialize` site on internal shapes (Render builder,
  Error.cs, identity/code, http/Default, llm/OpenAi). No new
  default-options Deserialize on attacker-controlled bytes. Baseline
  delta of +1 attributable to the LoadAssemblyAsync relocation noted
  above.

## Standing finding interaction

This branch does not touch `module/callback`. The existing
`callback.run` finding ("skips signing.verify when `RawSignature==null`")
in MEMORY.md remains open and is the *symmetric write-side* of F1
(producer must require signature; consumer must require verify). The
fix posture is identical: gate-must-be-unconditional.

## Next bot

**auditor.** Per `/memory/feedback_next_bot_after_security.md` — security
PASS/NEEDS-FIX hands to auditor for the merge-readiness call; docs is
downstream of auditor.

```
Next bot: auditor
```

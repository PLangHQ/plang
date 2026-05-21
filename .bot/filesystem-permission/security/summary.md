# security — filesystem-permission

## Version
v1

## What this is
First security pass on the `filesystem-permission` branch. The branch adds a
consent-gated filesystem layer: every file action routes through
`Path.Authorize(verb)` — in-root paths auto-grant, out-of-root paths match a
stored grant or prompt the actor (`y`/`n`/`a`). `a` answers persist an
Ed25519-signed grant to the per-actor sqlite `permission` table; `y` keeps
an unsigned grant in memory for the session. `v5` dropped
`PermissionRecord.AppId` so persisted grants are keyed by
`(Actor + Path + Verb)` and survive a fresh `App` on the same root.

codeanalyzer (v1–v3) and tester (v1–v4) had already cleared shape and test
quality. This pass is the trust-boundary audit.

## What was done
Mapped the attack surface across grant creation, verification, persistence,
path containment, and pattern matching; then red-teamed each. Wrote
`security-report.json`, `v1/result.md`, `v1/verdict.json`.

**Verdict: PASS** — no critical/high open findings. Four findings, all on
the *verification* side of the gate:

- **F1 (Medium, latent)** — `signing.verify` checks signature *integrity*
  but not signer *authority*. It verifies against the public key embedded
  in the signature envelope, never against a trusted identity. Any Ed25519
  keypair yields a grant `TryCover` honors. The signature gives no real
  protection of the sqlite `permission` table against a tamperer (who can
  re-sign), only against accidental corruption. Latent because the table is
  disk-write-only today; ratchets to High/Critical if it ever takes
  non-disk input. Fix: pin `Signature.Identity` to the actor's identity.
- **F2 (Low)** — `TryCover` auto-trusts a *wholly unsigned* persisted row
  (`RawSignature == null → return true`). Correct for the in-memory loop,
  wrong for the shared persisted loop. Unreachable today (Add routes
  unsigned→memory) but the gate's safety hangs on that one convention.
  Fix: persisted loop must deny unsigned rows.
- **F3 (Medium)** — persisted "always allow" grants silently expire after
  5 minutes: `signing.verify` step 2 rejects signatures whose `Created` is
  older than `Config.TimeoutMs` (default 300 s), and `EnsureSigned` sets no
  `Expires`. Fail-closed (re-prompt), so not a bypass — but it breaks the
  documented `a` contract, and Stage5 Scenario4 / cross-App tests
  false-green it because unit tests run in milliseconds. The tempting fix
  (raise global `TimeoutMs`) also widens the wire-message replay window.
  Fix: give grants their own `Expires`; disable the freshness check on the
  grant-verify path.
- **F4 (Low, latent)** — `RegexMatches` has no match timeout (ReDoS).
  Unreachable today (`BuildRequest` only sets `Match.Exact`).

## Code example — the F1 gap
`Ed25519.VerifyAsync` step 8 — verifies against the envelope's own key:
```csharp
var verifyResult = Verify(signingBytes, signatureBytes, signedData.Identity);
```
Nothing compares `signedData.Identity` to a trusted identity. `TryCover`
then treats `result.Success` as proof the grant is genuine. Fix shape:
```csharp
var trusted = (await identity.Get()).PublicKey;
if (grant.RawSignature!.Identity != trusted) return false; // deny
```

## What's next
All four findings are improvements, not blockers — branch is mergeable.
F1 and F3 should be closed before the `permission` table gains any non-disk
write path. If the user wants them fixed on this branch, route to coder;
otherwise they carry forward as standing findings.

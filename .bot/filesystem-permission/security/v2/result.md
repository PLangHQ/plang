# Security v2 — filesystem-permission re-audit

## Trigger

34 commits since security v1 (PASS, 4 findings). Two security-relevant deltas:

- **runtime2 merge** (`0b4ff9cc`) — app-lowercase rename, closes auditor F-B.
- **coder v6** (`894d6a0c`) — `SkipFreshnessCheck` flag on `signing.verify`;
  closes auditor F-A / security v1 F3.
- **coder v7** (`8b42b0d3`) — test-only; pins the nonce-replay regression.

Clean rebuild (stale-binary trap avoided). C# suite **2855 / 2855**, 0 fail,
0 skip.

## The v6 crypto change — verdict: safe

`Permission.VerifySignature` now builds
`new signing.verify { Data, SkipFreshnessCheck = true }`. When the flag is set,
`Ed25519.VerifyAsync` skips:

- **Step 2** — Created-age wire-freshness check
- **Step 4** — nonce-replay cache

Three things make this safe:

1. **Cryptographic integrity is untouched.** Steps 1, 3 (Expires), 5
   (contract), 6 (header), 7 (data-hash) and **8 (Ed25519 signature
   verification)** all still run for grants. Forgery still needs the actor's
   private key.
2. **The bypass is confined.** `SkipFreshnessCheck` is `[Default(false)]`,
   resolves `?? false`. Exhaustive trace of every `signing.verify`
   construction site in production C#:
   - `actor/permission/this.cs:147` — grant path — sets `true`.
   - `modules/http/code/Default.cs:603, 636, 655, 917` — HTTP wire-message
     verification — all leave it false.
   Wire-message anti-replay is fully intact.
3. **Skipping nonce-replay for grants is correct by design.** A stored grant
   re-presents the *same* nonce on every read — that is not a replay. The
   coder's comment is accurate.

This is exactly v1 F3's recommended fix, and explicitly **not** the rejected
"raise `Config.TimeoutMs`" approach (which would have widened the wire-replay
window). tester v6 mutation-verified both halves. **F3 closed.**

### Does removing freshness worsen F1?

No. F1 is forgery, not replay. An attacker forging a grant controls every
field of the `Signature` envelope, including `Created` and `Nonce` — so step 2
and step 4 never bounded a *deliberate* forgery (the forger always picks a
fresh `Created` and a new `Nonce`). Removing them changes only that a forged
*permanent* grant verifies indefinitely rather than needing a re-write every
5 minutes — a negligible delta for an attacker who already has sqlite write
access. F1 severity unchanged.

## v1 findings status

| ID | Severity | v2 status | Note |
|----|----------|-----------|------|
| F1 | Medium | **open** | Signer authority not pinned — `VerifySignature` returns `result.Success` without checking `RawSignature.Identity` is the actor's trusted identity. Unchanged by v6/v7. |
| F2 | Low | **open** | `TryCover:125` `RawSignature == null → return true` shared between in-memory and persisted loops — an unsigned persisted row is auto-trusted. Unchanged. |
| F3 | Medium | **fixed** | Closed by coder v6 `SkipFreshnessCheck`. Re-audited safe. |
| F4 | Low | **open** | `RegexMatches:57` `Regex.IsMatch` with no `matchTimeout` — latent ReDoS. Unchanged. |

All three open findings remain gated by the user-sovereign precondition: **no
non-disk write path to the `permission` sqlite table**. Re-confirmed at HEAD —
the only writers are `Permission.Add` (`this.cs:82`) and `Permission.Revoke`
(`this.cs:112`), both behind user consent; no `permission.add` action module;
the runtime2 merge added nothing.

## Observation — not a branch finding

The callback/channel signature-verification doc-comments contradict each other:

- `channels/serializers/serializer/plang/Data.cs:14-16` — "callback.run
  invokes `signing.verify` before dispatching".
- `callback/run.cs:7-8` — "Data is verified by construction ... so no explicit
  verify call here".

Neither file actually calls `verify`. In practice verification rides the
transport (HTTP verifies with freshness on). Pre-existing, **not introduced by
this branch** — logged so the contradiction is on record. Does not affect the
verdict; worth a doc-bot cleanup.

## Verdict

**PASS.** F3 fixed and verified. No critical/high open. F1 should land before
the `permission` table ever gains a non-disk write path.

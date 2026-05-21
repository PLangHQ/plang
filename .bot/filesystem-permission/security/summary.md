# security — filesystem-permission

## Version
v2 (latest)

## What this is
Security trust-boundary audit of the `filesystem-permission` branch. The
branch adds a consent-gated filesystem layer: every file action routes through
`Path.Authorize(verb)` — in-root paths auto-grant, out-of-root paths match a
stored grant or prompt the actor (`y`/`n`/`a`). `a` answers persist an
Ed25519-signed grant to the per-actor sqlite `permission` table; `y` keeps an
unsigned grant in memory for the session.

## What was done

### v1 (audit `94851c8e`, PASS, 4 findings)
Mapped and red-teamed the attack surface — grant creation, verification,
persistence, path containment, pattern matching. Four findings, all on the
*verification* side, none critical/high:
- **F1 (Medium)** — `signing.verify` checks signature *integrity* but not
  signer *authority*; any Ed25519 key yields a grant `TryCover` honors.
- **F2 (Low)** — `TryCover` auto-trusts a wholly unsigned persisted row.
- **F3 (Medium)** — persisted `a` grants silently expired after 5 min.
- **F4 (Low)** — `RegexMatches` has no match timeout (latent ReDoS).

### v2 (this version, PASS)
Re-audit after 34 commits — the runtime2 merge (`0b4ff9cc`) and coder v6
(`894d6a0c`) which added `SkipFreshnessCheck` to close F3 (= auditor F-A).

- Clean rebuild (stale-binary trap avoided). C# suite **2855/2855**, 0 fail.
- **F3 fixed and re-audited safe** — see code example below. The concern was
  that a freshness/nonce-replay bypass added for grants could leak to
  wire-message verification. Exhaustive trace of every `signing.verify`
  construction site confirms it does not: the flag is `[Default(false)]`, the
  only true-setter is the grant path, all 4 HTTP wire-verify sites leave it
  false. Ed25519 step 8 (signature verification) still runs for grants.
- **F1, F2, F4 carried forward unchanged** — coder v6/v7 did not touch them.
  All three stay precondition-gated: re-traced at HEAD, the only writers to the
  `permission` table are still `Permission.Add`/`Revoke` behind user consent;
  the runtime2 merge introduced no non-disk write path.
- Logged one non-blocking observation: the callback/channel verify
  doc-comments (`plang/Data.cs` vs `callback/run.cs`) contradict each other —
  pre-existing, not a branch finding, worth a doc cleanup.

Files: `security-report.json`, `security/v2/{plan,result,verdict,v1_review_summary}.md`.

Verdict: **PASS**. No critical/high open.

## Code example — the v6 fix audited in v2

```csharp
// app/modules/signing/verify.cs — opt-in, defaults off
[Default(false)]
public partial data.@this<bool> SkipFreshnessCheck { get; init; }

// app/modules/signing/code/Ed25519.cs — only steps 2 & 4 gated;
// step 8 (Ed25519 signature verification) always runs
var skipFreshness = action.SkipFreshnessCheck?.Value ?? false;
if (!skipFreshness) { /* step 2: Created-age wire-freshness */ }
if (!skipFreshness) { /* step 4: nonce-replay cache */ }

// Only ONE production caller sets true — the grant path (actor/permission).
// All 4 HTTP wire-verify sites leave it false → wire anti-replay intact.
```

## For v2 after review
The auditor flagged my v1 **F3 as under-rated** — I called it a non-blocking
security nit when, as code+tests+docs (false doc-comment + false-greening
test), it was branch-blocking. My v1 *technical* analysis of F3 was correct (I
wrote the exact fix coder later landed); the miss was bundling it under a soft
PASS. v2 rates the branch on the whole current state. Full note:
`security/v2/v1_review_summary.md`.

## Open items for follow-up
- **F1** (Medium) — pin the signer: assert `RawSignature.Identity` == actor's
  trusted identity. Land before the `permission` table gains a non-disk write
  path.
- **F2** (Low) — deny unsigned *persisted* rows (split `TryCover` by
  provenance).
- **F4** (Low) — bounded `matchTimeout` on `RegexMatches`.

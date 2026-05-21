# Security v2 — re-audit after runtime2 merge + crypto change

## Trigger
Branch advanced 34 commits since security v1 (PASS, 4 findings). Two
security-relevant deltas:
1. **runtime2 merge** (`0b4ff9cc`, app-lowercase rename) — closes auditor F-B.
2. **coder v6** (`894d6a0c`) — closes auditor F-A / my v1 F3 by adding a
   `SkipFreshnessCheck` flag to `signing.verify`; grant verification now skips
   Ed25519 steps 2 (wire-freshness) and 4 (nonce-replay).
3. **coder v7** (`8b42b0d3`) — test-only, pins the nonce-replay regression.

## Scope
- Re-confirm v1 findings F1, F2, F4 status against post-merge code.
- Audit the new `SkipFreshnessCheck` mechanism: does the freshness/nonce
  bypass leak to wire-message verification? Does it weaken the grant trust
  gate beyond F3's intended fix?
- Confirm the runtime2 merge introduced no new write path to the `permission`
  sqlite table (the precondition that holds F1/F2 at Low/Medium).
- Clean rebuild + suite green.

## Method
1. Clean rebuild (stale-binary trap).
2. Trace every `signing.verify` caller — confirm only the grant path sets the
   skip flag; wire paths (HTTP) leave it false.
3. Trace every write to the `permission` table.
4. Re-read TryCover / VerifySignature / Ed25519.VerifyAsync at HEAD.
5. Run the C# suite; spot-run the Stage5 permission tests.
6. Write security-report.json v2 + verdict.json + summary.md.

## Expected outcome
F3 fixed; F1/F2/F4 carried forward at unchanged severity (preconditions
unchanged). SkipFreshnessCheck is correctly scoped → no new finding.
Verdict: PASS unless rebuild/trace surfaces something new.

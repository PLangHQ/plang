# tester v5 — plan

## Trigger
New code landed after the tester v4 PASS (782b7a55c):
- auditor v1 FAILed the branch — F-A: persisted "always allow" grants expired
  after 5 min; my v4 PASS was rated *partial* because Stage5 Scenario4 never
  advanced `NowUtc`, so the documented durability was never verified.
- coder v6 (894d6a0ca) closed F-A.
- app-lowercase merge (0b4ff9cc1) closed F-B.
- codeanalyzer v4 PASSed.

## Scope
Re-review the coder v6 change only — F-A's production fix and its regression
test. F-B is a pure mechanical rename already cleared by codeanalyzer v4;
re-run both suites to confirm the rename left them green.

## Steps
1. Clean rebuild (stale-binary rule).
2. Run C# suite + PLang suite — confirm green post-merge.
3. Read coder v6's production change (`signing.verify.SkipFreshnessCheck`,
   `Ed25519.VerifyAsync` steps 2 & 4, `Permission.VerifySignature`).
4. Mutation-verify the new test `Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow`:
   revert `SkipFreshnessCheck=true→false`, confirm a test dies.
5. Check whether the mutation is *fully* gated — the flag controls two
   independent checks (step 2 wire-freshness, step 4 nonce-replay). Confirm
   each half has a test behind it, not just the pair.

## Output
v5/result.md, v5/verdict.json, summary.md, test-report.json.

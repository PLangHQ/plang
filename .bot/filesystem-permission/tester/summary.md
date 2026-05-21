# tester — filesystem-permission

## Version
v5 (reviews coder v6 — the version under review)

## What this is
The `filesystem-permission` branch adds PLang's consent-gated filesystem
access: a `Permission` record, a `Path.Authorize(verb)` gate that prompts the
actor on out-of-root access, per-actor in-memory + sqlite grant storage, a v2
Path-in/Data-out FS surface, and a snapshot/resume engine for stateless
suspend.

tester v4 PASSed v4+v5. The **auditor** then FAILed the branch on F-A
(persisted "always allow" grants expired after 5 min) and rated my v4 PASS
*partial* — Scenario4 never advanced `NowUtc`, so the documented durability
was never verified. coder v6 closed F-A; the app-lowercase merge closed F-B;
codeanalyzer v4 PASSed. This v5 re-reviews coder v6.

## What was done
- Clean rebuild (stale-binary rule). C# **2854/2854 pass, 0 skip**; PLang
  **203/203 pass** (4 intentional fail-fixtures excluded). The app-lowercase
  merge left both suites green.
- Read coder v6's fix: `signing.verify` gained `SkipFreshnessCheck`;
  `Ed25519.VerifyAsync` skips steps 2 & 4 when set; `Permission.VerifySignature`
  passes it true so grants live by `Expires` alone.
- **Mutation test**: `SkipFreshnessCheck` `true→false`, clean rebuild, full
  suite — 1 test dies (`Scenario4_PersistedGrantSurvivesPast_WireFreshnessWindow`).
- **Scratch probe**: confirmed the flag neutralises *two* checks and only one
  is gated — a persisted grant verified twice re-prompts under the mutation,
  is covered with the fix. Probe deleted; tree clean.
- Verdict: **NEEDS WORK**, 1 major finding. Output: `v5/result.md`,
  `v5/plan.md`, `v5/verdict.json`, shared `test-report.json`.

## Outcome
coder v6's production fix is **correct** — the feature works. But the
`SkipFreshnessCheck` flag turns off two independent checks (step 2
wire-freshness, step 4 nonce-replay) and **only step 2 has a test**. The new
durability test advances `NowUtc` +10 min so it dies on the age check; it
never re-presents a nonce.

**F1 (major)** — the nonce-replay half is ungated. Persisted `Find`
re-deserializes a fresh `Data` each call, so two reads of a persisted grant =
two real `VerifySignature` passes; with step 4 active the second hits
`NonceReplay` and re-prompts. No test does two verifications of a persisted
grant — a step-4-only revert passes all 2854 tests and would silently
re-break "always allow" for any app that re-reads a foreign resource.
`v5/result.md` hands over the exact closing test, mutation-verified.

## Minor notes (non-blocking)
- **N1** — `ValidatePathTests.UpperCasedRootPrefix_..._OnUnix` docstring still
  over-claims the `RootComparison` gate (carried from v4).
- **N4** — auditor F-5 (attributed to tester): `MoveCopyBundledConsentTests`
  covers bundled consent only on the v2 `Path` surface; the real handlers
  issue two prompts. Add a note or a handler-path test. Fair to defer.

## Next
One added test closes F1 and makes coder v6's "Mutation-verified" commit
claim actually hold. Re-issue as a quick v7.

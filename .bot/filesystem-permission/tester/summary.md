# tester — filesystem-permission

## Version
v6 (reviews coder v7 — the version under review)

## What this is
The `filesystem-permission` branch adds PLang's consent-gated filesystem
access: a `Permission` record, a `Path.Authorize(verb)` gate that prompts the
actor on out-of-root access, per-actor in-memory + sqlite grant storage, a v2
Path-in/Data-out FS surface, and a snapshot/resume engine for stateless
suspend.

tester v5 reviewed coder v6's auditor-F-A fix and returned NEEDS WORK with one
major finding (F1). coder v7 is a test-only change closing F1. This v6
re-reviews coder v7.

## What was done
- Clean rebuild (stale-binary rule). C# **2855/2855 pass, 0 skip** (+1 vs v5);
  PLang **203/203 pass** (4 intentional fail-fixtures excluded).
- Diff-checked coder v7's change: `Scenario4_PersistedGrantReVerified_NonceReplayDoesNotReprompt`
  added to `Stage5MessagesEndToEndTests.cs` — **verbatim** from tester v5's
  handed-over spec, no edits, no production change.
- **Mutation test**: `permission/this.cs:147` `SkipFreshnessCheck` `true→false`,
  clean rebuild, run `Scenario4*`. Two independent failures:
  `_WireFreshnessWindow` on `secondRead` (step 2), the new test on `read2`
  (step 4). `_RestartStillNoPrompt` passes (one verify only).
- Production code restored; suite re-confirmed green.
- Verdict: **PASS**. Output: `v6/result.md`, `v6/plan.md`, `v6/verdict.json`,
  shared `test-report.json`.

## Outcome
F1 (the only finding) is **closed**. tester v5 found that
`SkipFreshnessCheck=true` neutralises two independent signing checks (step 2
wire-freshness, step 4 nonce-replay) while only step 2 had a test — a
step-4-only regression would have passed the full suite and silently
re-broken "always allow" for any app re-reading a foreign resource. coder v7
added the missing test. The mutation that v5 said would survive the suite now
kills the new test on `read2`. Each half of the flag is its own regression
gate — coder v6's "Mutation-verified" commit claim now genuinely holds.

## Minor notes (non-blocking, carried)
- **N1** — `ValidatePathTests.UpperCasedRootPrefix_..._OnUnix` docstring still
  over-claims the `RootComparison` gate (carried from v4). Cosmetic.
- **N4** — auditor F-5: `MoveCopyBundledConsentTests` covers bundled consent
  only on the v2 `Path` surface; the real `copy.cs`/`move.cs` handlers issue
  two prompts. Fairly deferred with auditor F-C/D/E.

## Next
Branch is green from a test-coverage standpoint. No tester action outstanding.

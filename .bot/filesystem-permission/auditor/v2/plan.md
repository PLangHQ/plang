# Auditor v2 — plan

## Task
Re-audit `filesystem-permission` after auditor v1 FAILed it on two major
findings. Coder v5/v6/v7 landed the fixes; codeanalyzer v4, tester v6 and
security v2 have all re-reviewed and PASSed. My job: confirm the two v1
majors are *genuinely* closed (not just "code touched"), and check whether
the remediation opened any new seams.

## v1 verdict recap (what I FAILed on)
- **F-A (major)** — persisted "always allow" grants expired after 5 min:
  the wire-freshness `Created+TimeoutMs` window was applied to long-lived
  grants. False doc-comment + false-greening test (Scenario4 never advanced
  time).
- **F-B (major)** — branch on a stale merge-base predating the
  app-lowercase merge into runtime2; ~40 new PascalCase-namespace files.
- Minor/nit: F-C (OrdinalIgnoreCase root-comparison sites), F-D (ResumeChain
  dispose order untested), F-E (bundled consent tested only on v2 surface).

## What the re-reviewers concluded
- **codeanalyzer v4 — PASS**: F-A fix (`SkipFreshnessCheck`) skips exactly
  Ed25519 steps 2+4, step 3 (Expires) still governs; F-B merge clean. Two
  carry-overs (F-C now tracked; pre-existing Path-only `Add` keying).
- **tester v5 — NEEDS WORK → v6 — PASS**: v5 caught that the v6 commit's
  mutation claim only covered the wire-freshness half; coder v7 added the
  nonce-replay test; v6 mutation-verified both halves.
- **security v2 — PASS**: `SkipFreshnessCheck` correctly scoped, crypto
  check intact, no new write path post-merge. F1/F2/F4 carried non-blocking.

## Audit focus (the seams)
1. **F-A closure depth.** Does the fix narrow the time bound to *exactly*
   `Expires` and nothing else? Is the crypto check (step 8) still run for
   grants? Is the default-false flow proven to leave wire messages intact?
2. **F-A doc-comment cleanup completeness.** v1 cited a false doc-comment.
   The fix corrected `filesystem/permission/this.cs`. Did the *same* claim
   survive elsewhere? Check every permission-subsystem doc-comment.
3. **F-B closure.** Is `origin/runtime2` actually an ancestor of HEAD now?
   Are the branch's new files at lowercase paths with lowercase namespaces?
4. **F-C adjudication.** Auditor v1 left F-C open with the question "does
   `Relative` feed a gate?" Resolve it independently — trace the gate.
5. **Independent build + full suite** — don't trust the reported counts.

## Deliverables
- `.bot/filesystem-permission/auditor-report.json`
- `.bot/filesystem-permission/auditor/v2/verdict.json`
- `.bot/filesystem-permission/auditor/v2/result.md`
- Update `auditor/summary.md`, `report.json`

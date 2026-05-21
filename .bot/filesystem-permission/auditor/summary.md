# Auditor — filesystem-permission

## Version
v1 — see `v1/result.md`, `v1/verdict.json`, `../auditor-report.json`.

## What this is
Cross-cutting integrity audit of the `filesystem-permission` branch — the
final review gate after codeanalyzer v3 (PASS), tester v4 (PASS), and
security v1 (PASS). The branch adds a consent-gated filesystem layer
(`Path.Authorize`, signed per-actor permission grants) plus a unified
suspend/resume engine (`Snapshot.Resume`, `IExitsGoal`, action-owns-execution).

## Verdict: FAIL

Two major findings, both in the seams the three slice-reviewers missed.

### F-A (major) — "always allow" grants expire after 5 minutes
The branch's headline feature is broken. A persisted `"a"` grant is signed
with no `Expires`; `VerifySignature` passes no `TimeoutMs`; `Ed25519`
step 2 rejects any signature older than `Config.TimeoutMs` (5 min default).
So "always allow" is really "allow for 5 minutes" — after that the user is
re-prompted. The `FileSystem/Permission/this.cs` doc-comment claims grants
persist permanently. Stage5 `Scenario4` claims to prove it but never
advances time, so it only ever reads a millisecond-old grant. Security
found this (their F3) but rated it non-blocking; as a correctness defect
with a false doc-comment and a false-greening test, it is branch-blocking.

### F-B (major) — branch not merge-ready against current runtime2
Merge-base `79d76aa0` predates the app-lowercase merge into runtime2.
runtime2 now has `PLang/app/` lowercase; the branch has `PLang/App/` plus
~40 new files with PascalCase namespaces that violate the runtime2
convention. codeanalyzer rebased onto the branch's own remote, not
runtime2 — nobody checked the actual merge.

### Minor / nit
- F-C: codeanalyzer diagnosed a Linux case-comparison bug (`Path.cs:125,127`,
  `PLangFileSystem.cs:254`) and passed without fixing or tracking it.
- F-D: `ResumeChain` parent continuation runs inside the child call-frame
  scope; codeanalyzer asserted "dispose order correct" with no test.
- F-E: bundled-consent is tested only on the v2 surface, not the action
  handlers PLang programs actually use.

## Verification run
- `dotnet build PLang.Tests` — 0 errors.
- `dotnet run --project PLang.Tests` — **2853 / 2853 pass, 0 fail, 0 skip**.
- The branch is internally sound; the FAIL is about the contract defect and
  the integration gap, not build/test breakage.

## What to do next
Coder fixes F-A (decouple grant verification from the wire-freshness
window — explicit long `Expires` + a `TimeoutMs` that disables the
Created-age check for grants; add a time-advancing test; correct the
doc-comment). F-B should be resolved by a rebase onto current runtime2 with
the `App/`→`app/` rename and namespace rewrite. F1/F2/F4 from security
remain correct as non-blocking follow-ups.

# Auditor — filesystem-permission

## Version
v2 — see `v2/result.md`, `v2/verdict.json`, `../auditor-report.json`.
(v1 — FAIL — see `v1/`.)

## What this is
Cross-cutting integrity audit of the `filesystem-permission` branch — a
consent-gated filesystem layer (`path.Authorize`, signed per-actor
permission grants) plus a unified suspend/resume engine.

## Verdict history
- **v1 — FAIL.** Two majors: F-A (persisted "always allow" grants expired
  after 5 minutes; the headline feature did not do what it documented) and
  F-B (branch on a stale runtime2 merge-base, ~40 PascalCase-namespace
  files). Plus three minors F-C/D/E.
- **v2 — PASS.** Both majors genuinely closed.

## v2 — what changed since v1

Coder v5/v6/v7 landed the fixes; codeanalyzer v4, tester v6 and security v2
all re-passed. This audit verified the closures independently by code
trace.

### F-A — CLOSED
`signing.verify` gained a `SkipFreshnessCheck` flag (`[Default(false)]`).
`Ed25519.VerifyAsync` skips step 2 (wire-freshness) and step 4
(nonce-replay) when set; **step 3 (Expires) and step 8 (cryptographic
signature) always run for grants.** `Permission.VerifySignature` is the
only production caller setting it true, so wire-message anti-replay is
untouched. The Scenario4 tests now advance `NowUtc +10 min` and double-read
a persisted grant; tester v6 mutation-verified both halves fail
independently. The time bound is narrowed to exactly the grant's `Expires`
(null = permanent) — precisely what v1 asked for.

### F-B — CLOSED
`git merge-base --is-ancestor origin/runtime2 HEAD` is true — the branch
contains all of current runtime2 (HEAD `41d93a464`). `PLang/app/` is
lowercase; branch additions use lowercase namespaces. Real merge, not a
rebase onto the branch's own remote.

### New finding — F-1 (minor, review-gap)
The F-A remediation corrected the doc-comment in
`filesystem/permission/this.cs` but left the **inverse** claim standing in
the sibling `actor/permission/this.cs` class header: it still says
persisted "a" grants "have an expiry" and session "y" grants don't — the
exact opposite of the shipped contract (persisted "a" = signed, permanent;
session "y" = unsigned). Documentation-only, does not block, but should be
fixed before the docs bot reads it as ground truth. Missed by codeanalyzer
v4 (the comment was outside the v6 diff).

### Carried v1 minors — adjudicated
- **F-C** — `OrdinalIgnoreCase` root-comparison sites. Resolved the open v1
  question: the authorization gate (`IsInRoot`/`IsUnder`) DOES use the
  case-correct `RootComparison` helper; the remaining `OrdinalIgnoreCase`
  sites (`path.cs` `Relative`/`Equals`) are observability only. Non-blocking,
  now tracked by codeanalyzer.
- **F-D** — `ResumeChain` dispose order untested. Engine untouched by
  v5/v6/v7; unchanged nit.
- **F-E** — bundled consent tested only on the v2 surface. Deferred by
  tester v6 as N4. Non-blocking.

## Verification run (independent)
- Clean rebuild — **0 errors**.
- `dotnet run --project PLang.Tests` — **2855 / 2855 pass, 0 fail, 0 skip**.

## What to do next
Branch passes. The F-1 doc-comment is worth a one-line fix but does not
block — the docs bot can correct it as part of its pass, or the coder can
land it in a trailing commit. Proceed to docs.

# security v1 — plan

## Branch
`filesystem-permission` — adds consent-gated filesystem access. Every file
action handler routes through `Path.Authorize(verb)`: in-root paths
auto-grant; out-of-root paths match an existing signed grant or prompt the
actor (`y`/`n`/`a`). `a` ("always allow") signs the grant and persists it to
the per-actor sqlite `permission` table; `y` keeps it in-memory for the
session.

This is the first security pass on the branch. codeanalyzer (3 passes) and
tester (4 passes) have already cleared it on shape and test quality. My job
is the trust-boundary audit they don't do.

## Attack surface to map
1. **Grant creation** — `Path.Authorize` / `Path.Operations.BundledTransfer`.
   Who can create a grant? Is consent the only path?
2. **Grant verification** — `Actor.Permission.TryCover` → `signing.verify`.
   Does the signature gate actually bind trust? Signer identity pinned?
3. **Grant persistence** — sqlite `permission` table, `GetAll<T>`
   deserialization. Untrusted-data reachability.
4. **The v5 change** — dropping `PermissionRecord.AppId`. What blast-radius
   did it add? Root is now the only scope.
5. **Path containment** — `IsInRoot` / `IsUnder` prefix matching, traversal.
6. **Pattern matching** — `Match.Glob` / `Match.Regex` in `Permission.Covers`.
   ReDoS, unbounded matching.
7. **The Authorize prompt loop** — adversarial channel input, liveness.

## Method
Blue team (map exposure + mitigations + gaps), then red team (concrete
vectors, feasibility, severity, fix). Severity is threat-model-relative:
PLang is user-sovereign, the user's own disk is not an attacker. Findings
that need disk write are Low *today* but flagged with their ratchet.

## Deliverables
- `.bot/filesystem-permission/security-report.json`
- `.bot/filesystem-permission/security/v1/result.md`
- `.bot/filesystem-permission/security/v1/verdict.json`
- `.bot/filesystem-permission/security/summary.md`

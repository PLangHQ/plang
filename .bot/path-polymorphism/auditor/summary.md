# Auditor — path-polymorphism

## Version
v1 — PASS — see `v1/result.md`, `v1/verdict.json`, `../auditor-report.json`.

## What this is
First cross-cutting integrity audit of `path-polymorphism`. The branch
makes PLang's `path` scheme-polymorphic (`FilePath`, `HttpPath`,
deferred `S3Path`/`GitPath`/…), moves `app.filesystem` under
`app.types.path/`, makes `Path` abstract, dispatches via a per-App
`Scheme` registry, and lands the consent gate inside Path verbs (not
inside action handlers). The pipeline before me: architect v1 →
test-designer v1 → coder v1…v10 → codeanalyzer v1…v4 → security v1…v3 →
tester v5/v7/v8.

## Verdict
**v1 — PASS.** Every closed security finding (S1–S4 + O3) verified by
independent code trace; codeanalyzer v4 F1+F2 closed in coder v7
without a re-review; architect's 7 stages all delivered; no namespace,
console, or doc-comment drift; clean rebuild, 2920/2920 C#, 204/204
plang.

## New finding — F1 (informational, latent leak)
`HttpPath.Resolve` initialises `Raw` from the original `rawPath`, which
preserves any `user:pwd@host` userinfo. The constructor strips userinfo
from `_uri` (S4.b), so `Absolute`, `AuthGate`, `SignRequest`, and the
persisted grant key are all userinfo-free — the S4.b triple holds. But
`Raw` is the off-triple field. Today there are only three `.Raw`
consumers across `PLang/` and none currently log/trace HttpPath's Raw,
so observable impact is zero. The risk is latent: a future debug-trace
or error-message change that reads `path.Raw` would resurface the
userinfo leak that S4.b otherwise closed structurally. Five-line fix
(compute `Raw` from the post-strip `_uri.ToString()`) pins the
invariant in one place. **Non-blocking.** Filing now so it's
adjudicated, not silently inherited.

Missed by: security v2/v3 — both verified the S4.b gate/wire/key
triple exhaustively but did not extend to off-triple public string
surfaces. Reasonable scope decision per axis; the auditor's job is the
seam between axes.

## Carried (unchanged at HEAD)
- **F1 (Med, from filesystem-permission)** — `signing.verify`
  integrity-only, no signer-authority check.
- **F2 (Low, from filesystem-permission)** — unsigned persisted-row
  auto-trust.
- **F4 (Low, from filesystem-permission)** — Regex/Glob without
  `MatchTimeout`.
- **O5 (info, from security v3)** — `[PathScheme]` attribute is a
  decorated-but-unconsumed marker, per architect Stage-4 plan.
- **O6 (info, from security v3)** — `_uri.IdnHost` for IPv6 returns
  bare hex; display-only.

## Confidence
High on the security closure depth (every finding traced to the
actual code, including FollowRedirect's fresh-`@this`-per-hop pattern
that addresses S1 + S2 with the same edit) and on the architectural
delivery (all 7 stages verified by file existence + code trace, not
just by re-reviewer claim). The latent F1 nit is the only seam I
found that the per-axis bots' scoping naturally missed.

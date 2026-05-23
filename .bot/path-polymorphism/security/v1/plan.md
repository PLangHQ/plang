# security v1 — plan

## Branch
`path-polymorphism` — `path` becomes scheme-polymorphic. `FilePath` absorbs
today's filesystem impl; `HttpPath` lands as a second scheme. `IFile` /
`DefaultFileProvider` die. The `Permission.Authorize` gate moves out of file
action handlers and into the verb impls on each Path subclass — handlers are
now one-liners over `path.X()`. Builder kept on the same gate.

This is the first security pass on `path-polymorphism`. codeanalyzer (v4 →
NEEDS WORK doc-only) and tester (v7 → FAIL needs-fixes, coverage gaps) have
already cleared it for shape and test quality. My job is the trust-boundary
audit they don't do.

## Threat model for this branch
PLang is user-sovereign: the user's own disk is not an attacker, and a local
attacker that can already write the user's sqlite file is *post-RCE*. The
real new attacker on this branch is **the remote HTTP origin**. A PLang
program reaches a network endpoint with the user's consent and the user's
signing identity; what can a malicious or compromised origin do back to the
program / the host / the identity? Findings sorted by that lens.

## Carry-forwards from `filesystem-permission` audit
Permission-record `Find`/`TryCover`/`VerifySignature` were touched lightly
(field reshuffles only). Re-verify F1/F2/F4 status at HEAD:
- **F1** (signer-authority not pinned)
- **F2** (unsigned persisted-row auto-trust)
- **F4** (Regex/Glob ReDoS, no timeout)
- **F3** is fixed (`SkipFreshnessCheck=true` confirmed by `filesystem-permission` v2 + still at HEAD).

## Attack surface to map
1. **Authorize coverage on Path verbs** — every FilePath + HttpPath verb
   must hit the gate before I/O. The migration moved ~30 call sites from
   handlers into Path. Look for missed verbs, fast-path skips, and
   `CopyTo/MoveTo` cross-scheme dispatch holes.
2. **Scheme registry** — `Scheme.Register` / `Scheme.From` / `ParseScheme`.
   Replacement attack? Unknown-scheme handling? Bare-path → file default
   smuggling?
3. **HttpPath** (new attack surface) —
   - **SSRF via auto-redirect** — Authorize matches the prompted URL;
     redirects to private IPs / cloud metadata / loopback are not
     re-authorized.
   - **Identity leak via `X-Signature`** — the PLang identity signs every
     outbound request; .NET only strips standard auth headers on redirect,
     not custom ones.
   - **TLS verification** — default cert chain validation kept?
   - **Permission key canonicalisation** for the URL form — query sort,
     port normalisation, fragment drop. Collisions or under-specification?
   - **Secrets baked into grant key** — query-string tokens persist into
     sqlite under their URL.
4. **FilePath `ValidatePath`** — relative `Path.Join` semantics around
   `file://` prefixes and `../` segments after the rename / move.
5. **`IsInRoot` correctness for the new abstract base** — does an HttpPath
   ever match a file-root prefix?
6. **Per-scheme `Absolute` consistency** — Permission keys are scheme-
   prefixed (file/http) and don't collide across schemes.

## Method
Blue team (map exposure + mitigations + gaps), then red team (concrete
vectors, feasibility, severity, fix). Severity from the threat model above
— "remote origin is the attacker" puts SSRF and identity leak high; carry-
forwards stay at their prior severities.

## Deliverables
- `.bot/path-polymorphism/security-report.json`
- `.bot/path-polymorphism/security/v1/result.md`
- `.bot/path-polymorphism/security/v1/verdict.json`
- `.bot/path-polymorphism/security/summary.md`

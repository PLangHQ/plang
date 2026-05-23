# security v2 — plan

## Branch
`path-polymorphism` — coder v9 addresses the three findings from security v1
on `HttpPath`:
- **S1 (HIGH)** SSRF via `AllowAutoRedirect=true` →
  `AllowAutoRedirect=false` + manual `SendWithHops`/`FollowRedirect` that
  re-runs `AuthGate` per hop, with scheme allowlist, hop cap of 5, and
  RFC 7231 method/body downgrade on 303.
- **S2 (MEDIUM, partial)** `X-Signature` leak across origins → each hop is
  signed against the new destination URL (fresh `@this(target.ToString(),
  Context)`), so the prior hop's signature never reaches a different
  origin. Full S2 (destination-pinned signing envelope verified host-side)
  deferred to signing module.
- **S3 (LOW)** Query-string secrets persist on `a` → new virtual
  `AuthorizationHint(verb)` on `path.@this`; `HttpPath` overrides to emit
  a one-line warning when the URI carries a query string. Base `Authorize`
  appends the hint before the y/n/a choices.

## Scope of this audit
Re-audit just the v9 delta (`a4a6e5f00..e3309e136`):
- `PLang/app/types/path/http/this.cs` (+122 lines: `AllowAutoRedirect=false`,
  `MaxRedirectHops`, `Send`/`SendWithHops`/`FollowRedirect`,
  `AuthorizationHint`)
- `PLang/app/types/path/this.Authorize.cs` (+16 lines: hint-rendering hook)

Carry-forwards (F1/F2/F4 from `filesystem-permission`) untouched on v9 — keep
status from v1.

## What to look for
The redirect fix is the entire HIGH-severity mitigation, so it carries the
audit:

1. **Re-authorize correctness.** Does each hop *really* hit `AuthGate` with
   the new URL? Is the new `@this` constructed from a URL that is canonical
   enough for `Permission.Find` to match cleanly?
2. **Consent-prompt fidelity.** The user's only defense is reading the URL
   in the prompt. Does the displayed URL match what gets fetched?
   - userinfo (`user:pass@host`) preserved/displayed?
   - IDN homographs rendered as punycode or Unicode?
   - any other URL canonicalization that hides the destination?
3. **Method/body downgrade.** 303 → GET strips body; 301/302/307/308 keep
   method and body. Does the verb threaded through match what the user was
   prompted for?
4. **Scheme allowlist.** `http`/`https` only — confirmed in code.
5. **Hop cap.** `MaxRedirectHops = 5`, decremented per recursion. Off-by-one?
6. **Signing freshness.** `SignRequest` runs inside `SendWithHops`, so the
   signed envelope's `url` claim is the *current* `_uri`, not the original.
7. **Cross-origin header survival.** Are any caller-supplied headers
   (Authorization, Cookie, custom) preserved across hops? On HttpPath there
   are none — `SignRequest` is the only header writer. Confirm.
8. **Per-hop `Permission.Find` cache reach.** If a user previously granted
   `https://internal.metrics/` for some legitimate direct use, a third-party
   redirect to that URL is silently re-authorized — design tension, note
   but don't escalate.

## Deliverables
- `.bot/path-polymorphism/security/v2/result.md`
- `.bot/path-polymorphism/security/v2/verdict.json`
- Update `.bot/path-polymorphism/security/summary.md`
- Update `.bot/path-polymorphism/security-report.json`

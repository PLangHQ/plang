# security v1 review — summary

Tester v8 passed cleanly. Security v1 (first pass for that bot on this
branch) gave NEEDS WORK with 1 High, 2 Medium, 3 carry-forward.

The new findings all sit on HttpPath — the second scheme that didn't
exist on filesystem-permission:

- **S1 (HIGH)** — SSRF via `AllowAutoRedirect = true`. Consent prompt
  showed the original URL, then HttpClient silently followed 3xx to IMDS /
  loopback / private IPs.
- **S2 (MEDIUM, partial)** — `X-Signature` header is custom-named, so
  .NET didn't strip it on cross-origin redirect; combined with S1 this is
  an identity-leak primitive.
- **S3 (LOW)** — On 'a' answer, the full URL including query string
  persists to the local sqlite verbatim. No prompt warning.

Carry-forwards (F1 signer-authority, F2 unsigned-row auto-trust, F4
Regex/Glob ReDoS) live on `filesystem-permission` work, not this branch.

## What v9 does

Tests-only would not address findings. This is a code change.

**S1 + S2-partial — manual redirect handling.** `_client.AllowAutoRedirect`
flipped to `false`. New `SendWithHops` + `FollowRedirect` plumb each 3xx
through a fresh `HttpPath` that runs its own `AuthGate` (separate consent
prompt for the new host) and signs the new destination's URL fresh.
The original-hop signature never reaches a different origin. Hop cap of 5;
beyond that returns `TooManyRedirects`. Cross-scheme redirects (`ftp://`,
`file://`) rejected as `UnsupportedRedirectScheme`. Method/body
preservation follows RFC 7231: 303 downgrades to GET; 301/302/307/308
keep method+body.

**S3 — prompt hint.** New `AuthorizationHint(verb)` virtual on the base
Path. Base returns "". HttpPath returns the persistence warning when the
URI carries a query string.

## What v9 does NOT do

- **S2 full** — destination-pinned signing envelopes (include `host` in
  the signed envelope, have `signing.verify` cross-check). That touches
  the signing module's wire schema and the verify side; out of scope for
  HttpPath. Same shape as `filesystem-permission` F1.
- **F1/F2/F4 carry-forwards** — not this branch.

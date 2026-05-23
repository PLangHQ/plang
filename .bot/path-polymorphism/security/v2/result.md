# security v2 — result

**Branch:** `path-polymorphism`
**Build:** clean rebuild (Debug, net10.0; stale-binary trap avoided).
**C# tests:** 2914/2914 pass, 0 fail, 0 skip
**PLang tests:** 204/204 pass, 0 stale, 0 skip

**Verdict: NEEDS WORK (1 Low — consent-prompt fidelity).**

The v9 redirect rewrite closes S1's HIGH severity and S2's cross-origin
signature shape. The fix shape is the right one — `AllowAutoRedirect=false`,
manual `SendWithHops`/`FollowRedirect` building a fresh `HttpPath` per hop,
re-running `AuthGate` per hop, signing with the new destination's URL, hop
cap of 5, scheme-allowlisted to http(s), RFC 7231 method/body downgrade on
303. One new finding (S4, Low) on the consent prompt that the redirect
mitigation now depends on. S3 hint and S2 partial are correct as written.

---

## S1 — closed (HIGH → fixed)

`PLang/app/types/path/http/this.cs:51-58, 320-387`

`AllowAutoRedirect=false` on the static client. `Send` delegates to
`SendWithHops`; a 3xx response branches into `FollowRedirect`, which:
- parses `resp.Headers.Location` (absolute, or resolved against `_uri`);
- rejects target schemes other than `http`/`https` with
  `UnsupportedRedirectScheme/400`;
- builds a fresh `HttpPath` from `target.ToString()`;
- calls `nextPath.AuthGate(verb)` — the user sees and consents to the *new*
  URL before any further I/O;
- downgrades 303 → GET with no body; preserves method+body for 301/302/307/
  308 (RFC 7231 §6.4);
- recurses with `hopsLeft - 1`, exhausting at `TooManyRedirects/508` after
  five hops.

Verified by `HttpPathRedirectTests` (302-to-unauthorized denied; 302-to-
authorized followed; cross-scheme rejected; hop cap exhaustion).

## S2 — partially closed (MEDIUM → cross-origin shape fixed)

`PLang/app/types/path/http/this.cs:381-386` + `:396-422`

Each hop instantiates a new `HttpPath` whose `_uri` is the redirect target,
and `SignRequest` reads `_uri.ToString()` for the signing envelope's `url`
claim. The prior hop's signature is never forwarded, and the new hop's
signature is bound to the new destination. The cross-origin leak shape from
v1 is gone.

What stays open (intentionally deferred): the signing envelope is not yet
verified host-side against the receiving request's host. That's signing-
module work; flagged on `filesystem-permission` F1 as a separate item.

## S3 — closed (LOW → fixed)

`PLang/app/types/path/http/this.cs:96-100` +
`PLang/app/types/path/this.Authorize.cs` hook.

`AuthorizationHint(verb)` is a `virtual` no-op on `path.@this`; `HttpPath`
overrides it to emit a one-line warning when `_uri.Query` is non-empty.
Base `Authorize` appends the hint before y/n/a. The hint string is a
constant — no URL content interpolated into it, so no injection surface.
`HttpPathPromptHintTests` covers query-present / query-absent / suppress-on-
trailing-`?`.

---

## S4 — NEW — LOW — Consent-prompt URL diverges from fetched URL

**Where:** `PLang/app/types/path/http/this.cs:115-151` (`Absolute`),
consumed by the consent prompt via `this.Authorize.cs` → `BuildRequest →
Path: Absolute`.

The entire v9 mitigation rests on the user reading the URL in each
per-hop consent prompt. The displayed URL is `Absolute`, which is built
from `_uri` *components* — and two components are silently dropped or
re-rendered in a way the user cannot see:

### S4.a — IDN homograph rendered as Unicode in the prompt

`Absolute` uses `_uri.Host.ToLowerInvariant()` (line 120). For an IDN-
encoded host, `.Host` returns the Unicode form as parsed:

```
Redirect Location: https://аpple.com/login   (Cyrillic 'а', U+0430)
Prompt shows:      Allow Worker to read https://аpple.com/login? (y/n/a)
User reads:        "apple.com" — visually identical
Fetched:           https://xn--pple-43d.com/login  (a different origin)
```

The redirect mitigation works as designed — the user got a prompt — but
the prompt is a homograph spoof. A compromised consented origin can issue
one redirect to any homograph variant of any host the user trusts.

**Fix shape.** Use `_uri.IdnHost` (punycode) instead of `_uri.Host` in
`Absolute`. Punycode is visually obvious (`xn--…`) and copy-pasteable into
search. The grant key persisted to sqlite also becomes the canonical
punycode form, which closes the same-origin spoof on the cache side too.

### S4.b — Embedded userinfo stripped from the prompt

`Absolute` builds `scheme://host[:port]/path?query` and never appends
`_uri.UserInfo`. A redirect to `https://attacker:pwd@victim.example/`
resolves cleanly (host is `victim.example`, which is what the user
expects), the prompt shows `https://victim.example/`, but the fresh
`HttpPath` is constructed from `target.ToString()` which preserves the
userinfo on its `_uri`. .NET's `SocketsHttpHandler` does not auto-
translate userinfo into `Authorization:`, so this does not directly send
Basic credentials — but the URL **persisted as the grant key** is the
userinfo-free `Absolute`, so the next time `attacker:pwd@victim.example`
is consented to it matches the cached grant for `victim.example`. A
single user-approved entry now covers any userinfo-bearing variant.

The bigger concern is consent-contract integrity: what the user sees ≠
what gets stored ≠ what gets fetched.

**Fix shape.** Either (a) strip `UserInfo` at construction (silently drop
it from `_uri` before storing) — eliminates the divergence; or (b)
include `UserInfo` in `Absolute` so the user sees it in the prompt and
the grant key. (a) is the safer default; (b) preserves user intent if any
PLang program ever legitimately passes URI-embedded credentials.

**Why Low not Medium.** Both vectors need a prior authorized origin to
issue a redirect, *and* the user to miss the spoof in the consent prompt.
Threat model is "remote origin is the attacker" — pre-conditions are
realistic but the impact is one extra step from the user, not silent
compromise. Bumps to Medium if combined with a future trust UI that
auto-grants from a previously-consented origin.

---

## Carry-forwards from v1 — unchanged at HEAD

Re-verified `Permission` and `signing.verify` were not touched on v9.

### F1 (Medium, carry-forward) — `signing.verify` checks integrity but not signer authority
`PLang/app/actor/permission/this.cs:146-152`. Unchanged.

### F2 (Low, carry-forward) — unsigned persisted-row auto-trust
`PLang/app/actor/permission/this.cs:127`. Unchanged.

### F4 (Low, carry-forward) — Regex/Glob match has no timeout
`PLang/app/types/path/permission/this.cs:54-90`. Unchanged.

---

## Things I red-teamed that did not turn into findings

- **Per-hop `Permission.Find` cache reach.** A previously-granted target
  reached via redirect is silently re-approved by `Find` rather than re-
  prompted. Design tension, not a bug: by the consent model "user said yes
  to this URL" is itself the trust signal regardless of how it was reached.
  Worth noting in docs but not raising.
- **Off-by-one on `MaxRedirectHops`.** `hopsLeft<=0` guard before recursion;
  initial call passes 5; allows exactly 5 redirects = 6 total requests. Fine.
- **`HttpContent` reuse across hops.** `nextContent = nextMethod==GET ? null
  : content` re-passes the same `HttpContent` instance. HttpContent is
  typically single-send, so this is a reliability bug for 307/308 POSTs —
  not a security finding. (Worth a coder fix-up but not in this audit.)
- **Cross-origin header survival.** `HttpPath` writes only `X-Signature`,
  and that's re-emitted per hop with the new URL. No caller-supplied
  Authorization/Cookie headers exist on this path. Clean.
- **Scheme injection via Location.** Confirmed only `http`/`https` allowed;
  `file://`, `gopher://`, `ftp://` rejected at `FollowRedirect:372`.
- **Prompt-text injection via URL.** `Absolute` is the only URL-derived
  string in the prompt. Path/query are `Uri`-canonicalised; control chars
  stay percent-encoded; no newline-injection surface.
- **Signing identity on the new origin.** Confirmed `SignRequest` reads
  `_uri.ToString()` after the fresh `@this` is constructed — the signed
  `url` claim matches the destination, not the origin.
  `HttpPathRedirectTests` decodes the captured `X-Signature` envelope and
  asserts this directly.

---

## Out of scope but noted (still open)

- `[PathScheme]` + `code.load` — same Stage-4 concern recorded in v1.
- NU1902/NU1903 advisories on `SixLabors.ImageSharp`, `HtmlSanitizer`,
  `MimeKit` — pre-date this branch, separate dependency-hygiene pass.

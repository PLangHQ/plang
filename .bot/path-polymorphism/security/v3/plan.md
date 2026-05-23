# security v3 — plan

## Branch
`path-polymorphism` — coder v10 addresses security v2 S4 (consent-prompt
fidelity on `HttpPath`) and the O3 reliability note (HttpContent reuse on
307/308 hops).

## Scope of this audit
Just the v10 delta (`2beb8eb1b..a8e1e6107`):
- `PLang/app/types/path/http/this.cs`
  - Constructor: strip `_uri.UserInfo` via `UriBuilder { UserName="", Password="" }` (S4.b)
  - `Absolute`: `_uri.Host` → `_uri.IdnHost.ToLowerInvariant()` (S4.a)
  - `FollowRedirect`: re-buffer body into `ByteArrayContent`, copy content headers, for 301/302/307/308 with a non-null original `content` (O3)

No changes to `path/this.Authorize.cs` or any other production file.
Carry-forwards F1/F2/F4 from `filesystem-permission` untouched.

## What to look for

1. **S4.a fix correctness.** Does `_uri.IdnHost` actually return punycode
   for IDN-encoded hosts? Are there inputs where `IdnHost` differs from
   `Host` in a way that introduces a new divergence (IPv6 bracket
   handling, IP literals, empty hosts)?
2. **S4.b fix correctness.** Does the `UriBuilder { UserName="",
   Password="" }` round-trip actually remove `UserInfo` from the
   resulting `Uri`? Are there edge cases where userinfo survives —
   percent-encoded creds, single-component userinfo (`user@host`),
   `@host` with empty user?
3. **Equivalence at every consume site.** `Absolute`, `AuthGate` lookup,
   `Permission.Add` key, `SendRequest`-via-`_uri`. Are they all reading
   the *stripped* `_uri`? (Yes — constructor mutates the field before
   `_uri = uri`; everything downstream reads the single field.)
4. **307/308 body re-buffer.** Does `ReadAsByteArrayAsync` plus copied
   content headers preserve semantics on the next hop? Any data-leak via
   header copy (e.g. an `Authorization` header on the original `content`
   that gets copied to a cross-origin hop)?
5. **New attack surface from `UriBuilder`?** Default port injection,
   scheme casing, normalisation differences. Does the round-trip alter
   `_uri.Scheme`, `_uri.Port`, or `_uri.AbsolutePath` in any way that
   breaks the per-hop `AuthGate` match?
6. **Construction error path.** If the URL is unparseable, constructor
   throws `ArgumentException`. Same behaviour as v9 — fail-closed. Fine.

## Deliverables
- `.bot/path-polymorphism/security/v3/result.md`
- `.bot/path-polymorphism/security/v3/verdict.json`
- Update `.bot/path-polymorphism/security/summary.md`
- Update `.bot/path-polymorphism/security-report.json`

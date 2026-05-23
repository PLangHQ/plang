# v10 plan — security v2 S4 (consent fidelity) + 307 body fix

## S4.a — IDN homograph in prompt + grant key
`Absolute` (PLang/app/types/path/http/this.cs) uses `_uri.IdnHost` instead
of `_uri.Host`. Punycode is the canonical form going to both the prompt
and the sqlite grant key.

## S4.b — Embedded userinfo divergence
HttpPath constructor: when `uri.UserInfo` is non-empty, rebuild `_uri`
via `UriBuilder { UserName="", Password="" }`. Eliminates the divergence
between what the prompt shows, what the wire sends, and what's persisted.

## Adjacent reliability fix — 307/308 body
`FollowRedirect` re-buffers `content` into `new ByteArrayContent(bytes)`
with the original `content.Headers` copied across, so the next hop sends
a re-readable body. Without this, `WriteText` through a 307 silently sent
empty bodies (HttpContent is single-send).

## Tests
- `HttpPathConsentFidelityTests.cs` (5 tests):
  - IDN homograph host renders as punycode in prompt + Absolute.
  - ASCII host unchanged.
  - UserInfo stripped from Absolute.
  - UserInfo stripped from `_uri` on the wire.
  - Prompt for userinfo URL shows clean URL.
- `HttpPathRedirectTests.Redirect_307_PreservesPostBody_AcrossHops` —
  body survives the redirect-rebuffering hop and lands at target.

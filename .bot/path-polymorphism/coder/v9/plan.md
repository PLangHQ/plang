# v9 plan — security v1 S1 + S2-partial + S3

Three fixes, all confined to `PLang/app/types/path/http/this.cs` and a
small base-class hook in `PLang/app/types/path/this.Authorize.cs`.

## S1 — disable HttpClient auto-redirect, consent each hop

- Flip `_client.AllowAutoRedirect = false`.
- `Send` becomes a wrapper over `SendWithHops` (carries `hopsLeft`,
  `verb`). On 3xx with Location header, hands off to `FollowRedirect`.
- `FollowRedirect` resolves Location, builds a fresh `HttpPath`, runs
  `AuthGate(verb)` on it (a brand new consent prompt for the new URL),
  recurses through `SendWithHops`. Each hop signs fresh for its own
  destination.
- Hop cap `MaxRedirectHops = 5`. Beyond → `TooManyRedirects` error.
- Cross-scheme redirect → `UnsupportedRedirectScheme` error.
- Method/body: 303 downgrades to GET; rest preserve.

## S2-partial — falls out of S1

Building a fresh request per hop means the prior hop's `X-Signature` is
never carried into a different origin. Verified by a test that decodes the
X-Signature on the redirect target and asserts the signed `url` claim ==
destination, not origin.

## S3 — prompt hint for query-string persistence

- New `virtual string AuthorizationHint(Verb verb)` on `path.@this`. Base
  returns "". The base Authorize loop renders the question with the hint
  appended before "(y/n/a)".
- HttpPath overrides to return the persistence warning when `_uri.Query`
  is non-empty.

## Tests

- `HttpPathRedirectTests.cs` — 5 tests:
  - 302 to unauthorized host gets gated and denied (PermissionDenied).
  - 302 to authorized host follows and returns target body.
  - Redirect to unsupported scheme → typed error.
  - Loop exhausts hop cap → TooManyRedirects.
  - Signature on redirect hop is fresh for the destination URL (not origin).
- `HttpPathPromptHintTests.cs` — 3 tests:
  - HTTP URL with query string → prompt contains warning.
  - HTTP URL without query string → no warning.
  - File path → no warning.
- `HttpTestServer.cs` extended with `MapRedirect(status, location)` and
  `MapStoredBody(bytes)` (pre-seeds without going through the gate).

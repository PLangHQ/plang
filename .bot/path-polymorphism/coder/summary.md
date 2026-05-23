# coder summary — path-polymorphism branch

**Latest version:** v9

## What this is

`path-polymorphism` hosts the typed-returns sweep, the path scheme
polymorphism work (FilePath / HttpPath under a common `IPath`), and the
bootstrap fixes that fell out of the self-rebuild loop. v9 closes the
three security findings on HttpPath that security v1 flagged after the
sweep landed.

## v9 (this version) — security v1 S1 + S2-partial + S3

**S1 (HIGH)** — `HttpClient.AllowAutoRedirect = true` was an SSRF
primitive. The user's consent prompt showed the original URL, then the
client silently followed 3xx to IMDS / loopback / private IPs.
**Fix:** `AllowAutoRedirect = false`; manual redirect handling builds a
fresh `HttpPath` per hop, runs its own `AuthGate` (separate consent
prompt for the new URL), signs with the new destination's URL.
Hop cap = 5 → `TooManyRedirects`. Non-http(s) schemes rejected.
RFC 7231 method/body downgrade for 303.

**S2 (MEDIUM, partial)** — `X-Signature` is a custom header, so .NET
didn't strip it on cross-origin redirect. The S1 fix takes redirect
control, so the prior hop's signature never reaches the next origin —
each hop signs fresh for its own URL. Test decodes the captured
`X-Signature` envelope on the destination and asserts the signed `url`
claim is the destination, not the origin.
Full S2 (destination-pinned signing envelopes verified host-side) is
signing-module work, deferred.

**S3 (LOW)** — When the user answers 'a' to a prompt for
`https://api/x?token=secret`, the URL persists to local sqlite verbatim.
**Fix:** new `virtual AuthorizationHint(verb)` hook on the base
`path.@this`. Base returns "". HttpPath overrides to append a one-line
warning when the URI has a query string.

### Files modified

- `PLang/app/types/path/http/this.cs` — `AllowAutoRedirect=false`,
  `SendWithHops`/`FollowRedirect`, `AuthorizationHint` override, verb
  threaded through Send call sites.
- `PLang/app/types/path/this.Authorize.cs` — virtual `AuthorizationHint`
  hook; prompt construction renders the hint.

### Tests added

- `PLang.Tests/App/Types/PathTests/Http/HttpPathRedirectTests.cs` (5):
  302-to-unauthorized denied; 302-to-authorized followed; unsupported
  scheme rejected; hop cap fires; signature fresh for destination.
- `PLang.Tests/App/Types/PathTests/Http/HttpPathPromptHintTests.cs` (3):
  query-string → warning; no query → no warning; FilePath → no warning.
- `HttpTestServer.cs` extended with `MapRedirect(status, location)` and
  `MapStoredBody(bytes)`.

### Code example — the redirect hop pattern

The structural shape worth pointing at — *each hop is a fresh consent +
fresh signature*, no auto-follow trust:

```csharp
private async Task<data.@this> FollowRedirect(HttpResponseMessage resp, ...)
{
    if (hopsLeft <= 0) return ... "TooManyRedirects";

    var target = resp.Headers.Location!.IsAbsoluteUri
        ? resp.Headers.Location
        : new Uri(_uri, resp.Headers.Location);

    if (target.Scheme != "http" && target.Scheme != "https")
        return ... "UnsupportedRedirectScheme";

    var nextMethod = (int)resp.StatusCode == 303 ? HttpMethod.Get : method;
    var nextContent = nextMethod == HttpMethod.Get ? null : content;

    var nextPath = new @this(target.ToString(), Context);

    // Consent for the new URL — own prompt, own answer.
    if (await nextPath.AuthGate(verb) is { } denial) return denial;

    return await nextPath.SendWithHops(nextMethod, nextContent, readBody, verb, asBytes, hopsLeft - 1);
}
```

### Baseline (this round)

- C# `dotnet run --project PLang.Tests` — **2914 / 2914** (+8 from v8)
- PLang `plang --test` — **204 / 204** (unchanged, 0 stale)
- Build — 0 errors, 454 pre-existing nullable warnings

---

## Prior versions (one-liners)

- **v8** — tester v7 NEEDS-FIXES: F4 (rename `.test.goal2`), N1 (GoalCall
  slash resolution), N2 (Actions filter), N3 (RunAsync bootstrap guard),
  N4 (ReturnTypeName), N5 (baseline file). Tests-only +
  the `identitydata` → `identity` SSOT fix.
- **v7** — codeanalyzer v4 F1+F2 docstring + orphan-`<summary>` cleanup.
- **v6** — Slash-qualified `goal.call` resolution, inverted `File.Exists`
  bootstrap, `builder.actions` Include parameter, two builder validators.
- **v5 and earlier** — typed-returns sweep, IPath/IIdentity/IStore typing,
  runtime2 merge.

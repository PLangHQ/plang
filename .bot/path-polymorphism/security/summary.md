# security — path-polymorphism

## Version
v2 (latest)

## What this is
Trust-boundary audit of `path-polymorphism`. The branch makes `path`
scheme-polymorphic (`FilePath`, `HttpPath`), moves the consent gate from
action handlers into Path verbs, and kills `IFile` /
`DefaultFileProvider`. Threat model: PLang is user-sovereign — the real
new attacker is the **remote HTTP origin** that an `HttpPath` request
reaches.

## What was done

### v2 (this version, NEEDS WORK)

Clean rebuild (stale-binary trap avoided). C# **2914/2914**
(`dotnet run --project PLang.Tests` — TUnit on .NET 10), plang
**204/204**, 0 stale. Audited only the v9 delta against v1 findings:

1. **S1 (HIGH SSRF) — fixed.** `AllowAutoRedirect=false` on the static
   client; `Send` delegates to `SendWithHops` which on 3xx hands off to
   `FollowRedirect`. Each hop builds a fresh `HttpPath(target.ToString(),
   Context)`, calls `AuthGate(verb)` so the user sees and consents to the
   new URL, re-signs with the new destination's URL, and recurses.
   Scheme allowlisted to http/https. Hop cap = 5. RFC 7231 §6.4 method/
   body downgrade on 303. Verified by `HttpPathRedirectTests`.
2. **S2 (MEDIUM cross-origin shape) — fixed.** Per-hop `SignRequest`
   reads the new instance's `_uri.ToString()` for the envelope's `url`
   claim. The prior hop's signature never crosses an origin boundary.
   Test decodes the captured `X-Signature` and asserts the signed `url`
   matches the destination. Destination-pinned verification on the host
   side is signing-module work — same shape as `filesystem-permission`
   F1, stays deferred.
3. **S3 (LOW persistence-warning) — fixed.** Virtual
   `AuthorizationHint(verb)` on `path.@this` returns empty by default;
   `HttpPath` overrides to emit a one-line warning when the URI carries
   a query string. Base `Authorize` appends the hint before y/n/a.
   Constant string — no injection surface. `HttpPathPromptHintTests`
   covers query-present/absent/trailing-?.
4. **One new finding (S4, Low).**

**S4 (Low) — Consent-prompt URL diverges from fetched URL.** The entire
v9 mitigation rests on the user reading the per-hop consent prompt.
`HttpPath.Absolute` — the URL shown in the prompt and the Permission
grant key — has two divergences from what gets fetched:

- **IDN homograph rendering.** `Absolute` uses `_uri.Host` (Unicode form)
  not `_uri.IdnHost` (punycode). A redirect Location of
  `https://аpple.com/` (Cyrillic `а`) displays identically to `apple.com`
  in the prompt, but the fetched host is a different origin.
- **Userinfo stripped.** `Absolute` omits `_uri.UserInfo`. A redirect to
  `https://attacker:pwd@victim.example/` shows `https://victim.example/`
  in the prompt, and `attacker:pwd@victim.example` matches the cached
  `victim.example` grant on next visit.

Fix shape: render `Absolute` via `_uri.IdnHost`; either strip
`UserInfo` at construction or include it in `Absolute` so the prompt and
grant key match what is fetched.

**F1/F2/F4 carry-forwards** from `filesystem-permission` re-verified at
v9 HEAD — unchanged.

### v1 (NEEDS WORK)

First pass. Three findings on HttpPath (S1 HIGH SSRF, S2 MEDIUM identity
leak, S3 LOW query-string persistence) plus F1/F2/F4 carry-forwards.
All three Sn closed in v2.

## Code example

The v9 redirect mitigation:

```csharp
// PLang/app/types/path/http/this.cs:51
private static readonly HttpClient _client = new(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    AllowAutoRedirect = false,   // security v1 S1
});

// :360 — each 3xx is a fresh consent + fresh signing
var nextPath = new @this(target.ToString(), Context);
if (await nextPath.AuthGate(verb) is { } denial) return denial;
return await nextPath.SendWithHops(nextMethod, nextContent, readBody, verb, asBytes, hopsLeft - 1);
```

The v2 S4 finding — Absolute strips both userinfo and the IDN punycode
distinction that the user needs to spot a homograph:

```csharp
// PLang/app/types/path/http/this.cs:120
var host = _uri.Host.ToLowerInvariant();   // ← _uri.IdnHost
// ...                                     // (UserInfo never appended)
sb.Append(scheme).Append("://").Append(host);
```

## What to do next

Coder picks up S4 — small fix (one-line `_uri.Host` → `_uri.IdnHost`
plus a UserInfo decision). All three v1 Sn findings are now closed.
F1/F2/F4 stay tracked at their `filesystem-permission` severities.

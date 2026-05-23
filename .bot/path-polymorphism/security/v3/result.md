# security v3 — result

**Branch:** `path-polymorphism`
**Build:** clean rebuild (Debug, net10.0; stale-binary trap avoided).
**C# tests:** 2920/2920 pass, 0 fail, 0 skip (TUnit on .NET 10 via `dotnet run --project PLang.Tests`)
**PLang tests:** 204/204 pass, 0 stale, 0 skip

**Verdict: PASS (0 open critical/high/medium new; v1+v2 Sn closed; F1/F2/F4 carry-forwards unchanged at their prior severities).**

v10 closes the v2 S4 finding cleanly and also resolves the O3 reliability
note that fell out of the v9 redirect rewrite. No new attack surface
introduced. Every HttpPath verb still gates through `AuthGate` before any
I/O; cross-hop signing remains destination-bound; the prompt URL, the
fetched URL, and the persisted grant key now all agree.

---

## S4.a — closed (LOW → fixed)

`PLang/app/types/path/http/this.cs:140`

```csharp
var host = _uri.IdnHost.ToLowerInvariant();
```

`IdnHost` returns the punycode form for IDN-encoded hosts (`xn--…`)
where `Host` returned the Unicode form. A redirect to
`https://аpple.com/` (Cyrillic `а`) now renders as
`https://xn--pple-43d.com/` in the consent prompt — visually distinct
from `apple.com`, so the homograph spoof is broken at the read site.

Cache-side property: the persisted grant key is also the punycode form,
so a previously-trusted ASCII grant for `apple.com` does not silently
match a homograph variant.

Verified by `HttpPathConsentFidelityTests`.

## S4.b — closed (LOW → fixed)

`PLang/app/types/path/http/this.cs:81-86`

```csharp
if (!string.IsNullOrEmpty(uri.UserInfo))
{
    var builder = new UriBuilder(uri) { UserName = "", Password = "" };
    uri = builder.Uri;
}
_uri = uri;
```

`UriBuilder.UserName="" + Password=""` produces a `Uri` whose
`UserInfo` is empty. The single `_uri` field is mutated *before* it is
stored, so every downstream consumer (`Absolute`, `AuthGate`, `SignRequest`,
`SendAsync`) reads the userinfo-free form:
- Prompt-displayed `Absolute`: no userinfo.
- Persisted grant key: same.
- Wire request target: same.

A redirect Location of `https://attacker:pwd@victim.example/` now:
1. Resolves into a fresh `@this(target.ToString(), Context)`.
2. Constructor strips userinfo before storing.
3. `AuthGate` prompts for `https://victim.example/` — and that is the
   *exact* URL fetched. Divergence eliminated.

Verified by `HttpPathConsentFidelityTests`.

## O3 — closed (reliability → fixed)

`PLang/app/types/path/http/this.cs:399-413`

`HttpContent` is single-send (the underlying stream is consumed on first
`SendAsync`). v9 re-passed the same instance to the next hop, which
broke 307/308 POST/PUT redirects. v10 buffers the body once via
`ReadAsByteArrayAsync`, builds a fresh `ByteArrayContent`, and copies
the original content headers (`Content-Type`, etc.). Cross-origin
implications: the headers copied are *content* headers (set by the
caller on the body, e.g. media type), not request-level secrets;
`Authorization` lives on `HttpRequestMessage.Headers` and is not copied
anywhere. No information-disclosure surface.

Verified by `HttpPathRedirectTests` (new 307 case).

---

## Things I red-teamed that did not turn into findings

- **`UriBuilder` round-trip side effects.** Could the round-trip alter
  `_uri.Scheme`, `_uri.Port`, or `_uri.AbsolutePath` in a way that
  breaks `AuthGate` matching or routing? Scheme casing is preserved
  (lowercase canonical). `UriBuilder.Port` defaults to the URI's port
  (could become explicit `:443`/`:80`), but `Absolute` already strips
  default ports (`isDefaultPort` check on lines 145-149), so the prompt/
  cache key still canonicalize identically. Path normalization is
  unchanged. Safe.
- **Single-component userinfo.** `https://user@host/` — `UserInfo` is
  non-empty (`"user"`), the strip block runs; `UriBuilder` clears both
  `UserName` and `Password`. Verified by `HttpPathConsentFidelityTests`.
- **Percent-encoded userinfo.** `Uri.UserInfo` returns the raw escaped
  form. The strip block triggers as long as it is non-empty; rebuild
  removes it regardless of encoding.
- **`IdnHost` and IP literals.** For IPv4 (`192.168.1.1`), `IdnHost`
  returns the literal — same as `Host`. For IPv6 (`[::1]`), `IdnHost`
  returns `::1` *without* brackets, where `Host` returned `[::1]`. This
  is a small display regression for IPv6 grants — the prompt would show
  a malformed `https://::1/` — but it is consistent between prompt and
  cache key (both use the same `Absolute`), so the consent contract is
  not broken. The fetched URL via `_uri` still goes to `[::1]`. Display-
  only annoyance, not a security finding. Noted as O6.
- **`Resolve` factory path.** `public static new @this Resolve(string,
  context)` → `new @this(rawPath, context)`. Same constructor, same
  userinfo strip. Covered.
- **Cross-origin header copy in re-buffered body.** Only `content.Headers`
  iterated (`Content-Type`, `Content-Length`, `Content-Encoding`,
  `Content-Disposition`). No request-auth headers exist on `HttpContent`.
  Safe.

---

## Carry-forwards from v1 — unchanged at HEAD

Re-verified `permission/this.cs` and `path/permission/this.cs` untouched
since v2.

- **F1 (Medium, carry-forward)** — `signing.verify` checks integrity but not signer authority. `PLang/app/actor/permission/this.cs:146-152`. Unchanged.
- **F2 (Low, carry-forward)** — unsigned persisted-row auto-trust. `PLang/app/actor/permission/this.cs:127`. Unchanged.
- **F4 (Low, carry-forward)** — Regex/Glob match has no `MatchTimeout`. `PLang/app/types/path/permission/this.cs:54-90`. Unchanged.

S2's residual ("destination-pinned signing envelope verified host-side")
is also still deferred — signing-module work, same shape as F1, stays
tracked at MEDIUM until that change lands.

---

## Observations

- **O1** — Pre-existing vulnerable NuGet packages (`SixLabors.ImageSharp 2.0.0` incl. one high CVE, `HtmlSanitizer 9.0.884`, `MimeKit 4.10.0`). Pre-dates this branch — separate dependency-hygiene pass.
- **O2** — `SignRequest` exception filter swallows `OperationCanceledException`. Tiny; tighten or document.
- **O4** — Cached `Permission` grant for a host previously consented to silently re-approves a redirect that lands on that host. Design tension under the consent model, worth a doc note. Unchanged.
- **O5** — Out-of-scope: `code.load` + `[PathScheme]` (Stage-4 design risk).
- **O6 (NEW)** — `_uri.IdnHost` for IPv6 returns the bare hex form without brackets (`::1` vs `[::1]`), so an IPv6 HttpPath renders a malformed `Absolute`. Display/grant-key annoyance for IPv6 HTTP; not a security finding because the prompt and cache key agree (the divergence S4 patched is not re-introduced).

---

## Bottom line

v10 closes every Sn finding from v1+v2 on `HttpPath`. The audited surface
of `path-polymorphism` has no open critical/high/medium findings new to
this branch; only the three pre-existing carry-forwards remain at their
prior severities, plus S2's deferred host-side verify.

Ship.

# coder summary — path-polymorphism branch

**Latest version:** v10

## v10 (this version) — security v2 S4 + 307 body fix

Two consent-fidelity vectors closed plus a reliability fix the security
bot's red team turned up.

**S4.a (LOW)** — `Absolute` rendered `_uri.Host` (Unicode IDN), so a
homograph host like `аpple.com` (Cyrillic 'а', U+0430) appeared visually
identical to the trusted brand in the prompt while the wire fetched
`xn--pple-43d.com` — a different origin. The persisted grant key was
also the Unicode form, so cache hits silently matched the spoof too.
**Fix:** `Absolute` now uses `_uri.IdnHost.ToLowerInvariant()`. Punycode
shows as `xn--…` everywhere — prompt, wire, grant key — so a homograph
variant is visually obvious and never matches a previously-trusted ASCII
host.

**S4.b (LOW)** — `Absolute` silently dropped `_uri.UserInfo`. A redirect
to `https://attacker:pwd@victim.example/` prompted as
`https://victim.example/` while the HttpPath's `_uri` still carried the
userinfo on the wire, and the persisted grant key was the userinfo-free
form. A single user-approved entry covered any userinfo-bearing variant.
**Fix:** strip UserInfo at HttpPath construction via `UriBuilder
{ UserName="", Password="" }`. Prompt, wire, and grant key all collapse
to the same string — consent contract is sealed.

**307/308 body reliability** — security's red team noted `HttpContent`
is single-send. After the first `SendAsync` .NET disposes the underlying
stream, so the original `FollowRedirect` passed a disposed instance to
the next hop and 307/308 POSTs silently sent empty bodies. **Fix:**
re-buffer `content` into a fresh `ByteArrayContent` per hop, preserving
the original Content headers.

### Code example — UserInfo strip at construction

```csharp
public @this(string raw, ...)
    : base(raw, context, content, source)
{
    if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        throw new ArgumentException(...);

    if (!string.IsNullOrEmpty(uri.UserInfo))
    {
        var builder = new UriBuilder(uri) { UserName = "", Password = "" };
        uri = builder.Uri;
    }
    _uri = uri;
}
```

The structural shape worth remembering: when the consent prompt is the
trust anchor, every component of the URL must travel the same path —
what the user reads must be the wire string and the cache key. Anything
that re-renders or strips silently is a fidelity break, and `_uri.Host`
+ silent UserInfo drop were both that.

### Files modified

- `PLang/app/types/path/http/this.cs` — UserInfo strip in ctor;
  `IdnHost` in `Absolute`; body re-buffer in `FollowRedirect`.

### Tests added

- `HttpPathConsentFidelityTests.cs` (5 tests): IDN homograph → punycode in
  prompt+Absolute; ASCII host unchanged; UserInfo stripped from Absolute;
  UserInfo stripped from `_uri.Uri`; prompt shows clean URL.
- `HttpPathRedirectTests.Redirect_307_PreservesPostBody_AcrossHops` — body
  round-trips through 307 to target.

### Baseline

- C# `dotnet run --project PLang.Tests` — **2920 / 2920** (+6 from v9)
- PLang `plang --test` — **204 / 204** (unchanged, 0 stale)
- Build — 0 errors, 454 pre-existing nullable warnings

---

## Prior versions (one-liners)

- **v9** — security v1: S1 (HIGH SSRF), S2-partial (MEDIUM signature
  cross-origin), S3 (LOW query-string warning).
- **v8** — tester v7 NEEDS-FIXES: F4 / N1 / N2 / N3 / N4 / N5; plus the
  `identitydata` → `identity` SSOT fix.
- **v7** — codeanalyzer v4 doc-only cleanups.
- **v6** — Slash-qualified `goal.call` resolution + builder bootstrap fixes.
- **v5 and earlier** — typed-returns sweep, IPath/IIdentity/IStore typing,
  runtime2 merge.

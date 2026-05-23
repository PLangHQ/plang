# security — path-polymorphism

## Version
v3 (latest) — **PASS**

## What this is
Trust-boundary audit of `path-polymorphism`. The branch makes `path`
scheme-polymorphic (`FilePath`, `HttpPath`), moves the consent gate from
action handlers into Path verbs, and kills `IFile` /
`DefaultFileProvider`. Threat model: PLang is user-sovereign — the real
new attacker is the **remote HTTP origin** that an `HttpPath` request
reaches.

## What was done

### v3 (this version, PASS)

Clean rebuild (stale-binary trap avoided). C# **2920/2920** (TUnit on
.NET 10 via `dotnet run --project PLang.Tests`), plang **204/204**,
0 stale. Audited only the v10 delta against v2 S4 + O3.

1. **S4.a (LOW IDN homograph) — fixed.** `Absolute` now uses
   `_uri.IdnHost.ToLowerInvariant()`. A Location of `https://аpple.com/`
   (Cyrillic `а`) renders as `https://xn--pple-43d.com/` in the consent
   prompt — visibly distinct from `apple.com`. Punycode also persists as
   the grant key, so a homograph variant cannot silently match a prior
   ASCII grant.
2. **S4.b (LOW userinfo strip) — fixed.** Constructor strips
   `_uri.UserInfo` via `UriBuilder { UserName="", Password="" }` before
   storing the single `_uri` field. Every downstream consumer (Absolute,
   AuthGate, SignRequest, SendAsync) reads the userinfo-free form —
   prompt URL, fetched URL, and persisted grant key all agree.
3. **O3 (reliability) — fixed.** `FollowRedirect` now re-buffers the body
   via `ReadAsByteArrayAsync` into a fresh `ByteArrayContent`, copying
   the original content headers. 307/308 POSTs no longer fail on the
   second hop. Only `content.Headers` is copied (Content-Type etc.), not
   request-level headers — no auth-header cross-origin survival.

No new findings. F1/F2/F4 carry-forwards from `filesystem-permission`
re-verified at v10 HEAD — unchanged. S2's deferred host-side verify
remains tracked at the signing-module layer.

### v2 (NEEDS WORK)

Re-audit of v9 redirect rewrite. S1 closed, S2 cross-origin shape
closed, S3 closed. One new finding (S4 Low — consent prompt diverged
from fetched URL via Unicode IDN host + stripped UserInfo).

### v1 (NEEDS WORK)

First pass. Three findings on HttpPath (S1 HIGH SSRF, S2 MEDIUM identity
leak, S3 LOW query-string persistence) plus F1/F2/F4 carry-forwards.

## Code example — v10 fix

```csharp
// PLang/app/types/path/http/this.cs:81
// Constructor strips userinfo — eliminates the divergence between
// what the prompt shows, what gets fetched, and what gets persisted.
if (!string.IsNullOrEmpty(uri.UserInfo))
{
    var builder = new UriBuilder(uri) { UserName = "", Password = "" };
    uri = builder.Uri;
}
_uri = uri;

// :140 — IdnHost (punycode) breaks the homograph spoof on display.
var host = _uri.IdnHost.ToLowerInvariant();
```

## What to do next

Branch is ready to ship from the security side. Remaining items
(F1/F2/F4 + S2 host-side verify) are pre-existing tracked work on
`permission/` and `signing/`, not blockers for `path-polymorphism`.

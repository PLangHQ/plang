# security — path-polymorphism

## Version
v1 (latest)

## What this is
First security trust-boundary audit of `path-polymorphism`. The branch
makes `path` scheme-polymorphic (`FilePath`, `HttpPath`), moves the consent
gate from action handlers into Path verbs, and kills `IFile` /
`DefaultFileProvider`. Threat model: PLang is user-sovereign — the real
new attacker is the **remote HTTP origin** that an `HttpPath` request
reaches.

## What was done

### v1 (this version, NEEDS WORK)

Clean rebuild (stale-binary trap avoided). C# **2906/2906**, plang
**204/204**, 0 stale. Then:

1. Mapped Authorize coverage on every Path verb (FilePath + HttpPath +
   base `CopyTo`/`MoveTo`). Every verb gates through `AuthGate` before any
   I/O. Bundled-transfer fast path checks both source and dest gates and
   only skips the prompt when both already grant. **Sound.**
2. Audited the scheme registry. Per-App, `ConcurrentDictionary`, built-ins
   registered in App ctor. `Scheme.Register` is not reachable from any
   PLang module surface today. `ParseScheme` validates per RFC 3986.
   **Sound for today; design note for `code.load`** — a malicious
   assembly with `[PathScheme("file")]` would silently MITM the file
   scheme; flag that when `code.load` lands.
3. Red-teamed HttpPath. Three findings.
4. Re-verified F1/F2/F4 carry-forwards from `filesystem-permission` at
   HEAD — unchanged in shape and severity.

**Findings:**

- **S1 (High)** — SSRF via `HttpClient.AllowAutoRedirect = true`. Authorize
  prompts the original URL; redirects to cloud IMDS / loopback / private
  IPs are not re-authorized. One redirect hop is enough to read AWS/GCP/
  Azure metadata after consent to any public host. Fix: handle 3xx
  manually, re-prompt per hop.
- **S2 (Medium)** — `X-Signature` (PLang identity) leaks across redirect
  and to arbitrary origins. Custom header survives .NET's redirect-strip;
  signing envelope is not destination-scoped. Pairs with S1 to harvest
  signatures for attacker-chosen (URL, body) pairs.
- **S3 (Low)** — `a` (always-allow) persists URL grants verbatim,
  including query-string secrets, to sqlite without warning the user.
- **F1/F2/F4** — carry-forwards from `filesystem-permission`, unchanged.

## Code example

The SSRF primitive:

```csharp
// PLang/app/types/path/http/this.cs:40
private static readonly HttpClient _client = new(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    AllowAutoRedirect = true,    // ← consent matches original URL only;
                                 //   redirect target is never gated.
});
```

After `Authorize` succeeds for `https://api.public-saas.com/x`, a 302
`Location: http://169.254.169.254/latest/meta-data/...` returns the IMDS
body to the program as a normal `data.@this.Ok`.

The fix shape (handle the 3xx, re-authorize each hop):

```csharp
// Send(): AllowAutoRedirect=false, then in the response handler —
if ((int)resp.StatusCode is >= 300 and < 400
    && resp.Headers.Location is { } loc
    && hops < MaxHops)
{
    var next = HttpPath.Resolve(loc.ToString(), Context);
    if (await next.AuthGate(verb) is { } denied) return denied;
    return await next.SendCore(method, content, readBody, asBytes, hops + 1);
}
```

## What to do next

Coder picks up S1 as the headline fix (the redirect rewrite is the right
shape and unblocks closing S2 cleanly at the same point). S3 is a one-line
prompt-text change. F1/F2/F4 stay tracked at their `filesystem-permission`
severities.

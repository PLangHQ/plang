# security v1 — result

**Branch:** `path-polymorphism`
**Build:** clean (Debug, net10.0)
**C# tests:** 2906/2906 pass, 0 fail, 0 skip
**PLang tests:** 204/204 pass, 0 stale, 0 skip

**Verdict: NEEDS WORK (1 High, 2 Medium, 3 Low/info carry-forwards).**

The migration of the consent gate from action handlers into Path verbs is
**structurally sound**: every FilePath verb (`ReadText`/`ReadBytes`/`Exists
Async`/`Stat`/`List`/`WriteText`/`WriteBytes`/`Append`/`Mkdir`/`Save`/
`Delete`/`CopyTo`/`MoveTo`/`AsBooleanAsync`) and every HttpPath verb routes
through `AuthGate` before any I/O. Cross-scheme `CopyTo`/`MoveTo` on the
base delegates to the verb impls, which gate again — no fast-path skip.
Scheme registry construction is per-App, ConcurrentDictionary, registered at
App ctor with built-ins only; `Scheme.Register` is not reachable from PLang
programs in any current module surface. `ParseScheme` rejects malformed
scheme parts per RFC 3986. Bare paths default to the `file` factory, which
is registered before any user code can run.

The new attack surface that did *not* exist on `filesystem-permission` is
the second scheme — `HttpPath` — and that is where the new findings sit.

---

## S1 — HIGH — SSRF via `HttpClient.AllowAutoRedirect = true`

**Where:** `PLang/app/types/path/http/this.cs:42`

```csharp
private static readonly HttpClient _client = new(new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(2),
    AllowAutoRedirect = true,
});
```

**The bug.** The Authorize prompt shows the user the *original* `Absolute`
URL (e.g. `https://api.public-saas.com/users.json`). On `a`/`y` the
returned `data` from the verb is the body of whatever the request finally
lands on — including redirects. The PLang HTTP client follows redirects
silently, with no re-authorization, no host check, no scheme check, and no
private-IP filter. .NET's `SocketsHttpHandler` caps at 50 redirects by
default; that is plenty for a multi-hop SSRF chain.

**Concrete attack** — single hop is enough on common cloud hosts:

```
Actor:           Wants to read https://api.public-saas.com/users.json — y/n/a? y
HttpPath.Read:   GET https://api.public-saas.com/users.json
Origin returns:  302 Location: http://169.254.169.254/latest/meta-data/iam/security-credentials/
HttpClient:      follows
Origin returns:  body of the AWS metadata response
PLang receives:  the credentials, as a normal `data.@this` success
```

Same with `127.0.0.1:<any>` (local Redis/Postgres/admin UIs), `10.x` /
`192.168.x` / `172.16/12`, the `metadata.google.internal` /
`100.100.100.200` (Alibaba) / `100.100.100.200` cloud IMDS variants, and
the GCP / Azure equivalents.

**Why this is High not Medium.** The user consented to a *resource at a
host they trust*. The redirect makes the gate consent-ineffective: the user
cannot consent to a URL they never see. The compromised host is the
attacker the threat model lists. The blast radius is whatever the IMDS
returns — typically short-lived IAM credentials.

**Fix shape.** Either:
1. `AllowAutoRedirect = false`, then handle 3xx in `Send` by parsing the
   `Location`, constructing a fresh `HttpPath`, calling `Authorize` on it,
   and recursing (with a small hop cap). Each hop gets its own consent
   prompt the user can deny. This is the right shape — the prompt mirrors
   what's actually being fetched.
2. Or keep auto-redirect but plug a `SocketsHttpHandler.ConnectCallback` that
   denies private/loopback/link-local IPs by default. Less flexible (no way
   to ever talk to `127.0.0.1` even with consent) but cheaper to ship.

I prefer (1) because it composes with the existing consent model. (2) is a
fallback if (1) is too disruptive for the v1 ship.

---

## S2 — MEDIUM — Identity-signature header leaks across redirect and to
arbitrary origins

**Where:** `PLang/app/types/path/http/this.cs:308-334` (`SignRequest`)

```csharp
var signResult = await Context.App.RunAction<modules.signing.sign>(sign, Context);
if (signResult.Success && signResult.Signature != null)
{
    var json = JsonSerializer.Serialize(signResult.Signature);
    request.Headers.TryAddWithoutValidation("X-Signature", json);
}
```

`SignRequest` runs once before the first dispatch. The signed payload
covers `(method, url, body)` of the *original* request. The `X-Signature`
header is a custom name; .NET only strips standard authentication / cookie
headers when following a redirect to a different origin
(`HttpClientHandler` follows the W3C rule for `Authorization` only). The
PLang identity signature is therefore forwarded on every redirect hop.

**Even without redirect**, every direct HttpPath request to an
attacker-controlled origin discloses:
- the actor's public key (inside the signature envelope),
- a fresh signature over `{url, method, body}` valid for the freshness
  window,
- enough material to study identity behaviour / fingerprint actors across
  PLang apps.

Replay risk against a *third-party* PLang-aware server depends on whether
that server checks `host` in the signed envelope. The `signing.verify`
audit on `filesystem-permission` (F1) already notes the verify side does
not pin signer authority — same shape on the issuing side: the issuer
broadcasts identity without scoping it to a destination set.

**Why Medium not High.** No automatic privilege grant comes from holding
the signature. But pairing with **S1** an attacker can collect signatures
for arbitrary user-controlled (URL, body) pairs by issuing redirects to
attacker-controlled hosts — a useful primitive against any future server
that trusts the identity envelope to authenticate the bearer.

**Fix shape.** Two-layer:
1. Drop `X-Signature` on redirect to a different scheme+host+port.
2. Make signing destination-aware — include `host` (or full canonical URL)
   inside the signed envelope and have `signing.verify` cross-check
   against the receiving request's host on the wire side. Cleanly closes
   `filesystem-permission` F1 too.

---

## S3 — LOW — Query-string secrets persist into the sqlite `permission`
table on `a` (always-allow)

**Where:**
- key construction: `PLang/app/types/path/this.Authorize.cs:84-88`
  (`BuildRequest` → `Path: Absolute`)
- canonical Absolute for http: `PLang/app/types/path/http/this.cs:85-121`

The `HttpPath.Absolute` canonical form includes the query string (sorted),
which is exactly what makes grants reliable. The side-effect: a user who
answers `a` to a prompt for

```
Allow worker to read https://api.example.com/files?token=secret123abc?
```

persists `https://api.example.com/files?token=secret123abc` as the
permission key into `<settings>/permission`. The token is now disk-resident
in clear text and survives `new App()`.

**Why Low.** PLang is user-sovereign, the sqlite is the user's own file,
and the leak is no worse than the user pasting the URL into bash history.
Worth flagging because **the prompt does not warn** that the URL is about
to be persisted verbatim; a user who would have stripped the token before
sending the URL elsewhere has no signal to do so before pressing `a`.

**Fix shape.** Either (a) the prompt text mentions persistence
("This URL will be saved with its query string to your local settings."),
or (b) when persisting an http grant, strip query-string values to a
placeholder (`?token=*&...`) and use Glob match instead of Exact — opt-in
behaviour.

---

## Carry-forwards from `filesystem-permission` audit

Re-verified at HEAD against the new `app.types.path.*` layout. All three
unchanged in shape and severity. Recording explicitly because the rename +
refactor touched the surrounding files and "still present" is not the same
as "not regressed."

### F1 (Medium, carry-forward) — `signing.verify` checks integrity but not
signer authority

`PLang/app/actor/permission/this.cs:146-152` calls `signing.verify` on the
persisted-grant signature. The action verifies Ed25519 integrity but does
not constrain *whose* public key signed. Any well-signed grant on disk
verifies — useful when the on-disk row is poisoned (writer ≠ actor). Same
note as `filesystem-permission` v1.

### F2 (Low, carry-forward) — unsigned persisted-row auto-trust

`PLang/app/actor/permission/this.cs:127`:

```csharp
if (grantData.RawSignature == null) return true; // in-memory unsigned grant
```

The comment is accurate today (`Add` routes signed→sqlite, unsigned→memory)
but a sqlite row whose `RawSignature` deserializes as `null` (manual edit,
older schema) flows through this same path and is trusted. Pre-condition
gated on local FS write but stays trivially fail-open if the precondition
ever leaks.

### F4 (Low, carry-forward) — Regex/Glob match has no timeout

`PLang/app/types/path/permission/this.cs:86-90` (`RegexMatches`) and
`:54-84` (`GlobMatches`, which compiles to a regex with `IsMatch`). No
`MatchTimeout`. Today `BuildRequest` only writes `Match.Exact`, so this is
latent — but `Match.Glob`/`Match.Regex` are stored modes on the record, so
any future writer (or a hand-crafted sqlite row) can hand the engine a
pathological pattern. ReDoS hangs `Find` and therefore every subsequent
Authorize call.

---

## Informational

### O1 — Pre-existing vulnerable NuGet packages (not new on this branch)

`dotnet test` surfaces NU1902/NU1903 advisories on `SixLabors.ImageSharp
2.0.0` (several CVEs incl. one high), `HtmlSanitizer 9.0.884`, `MimeKit
4.10.0`. Pre-dates this branch — flagged so it doesn't get lost. Bump
candidates for a separate dependency-hygiene pass.

### O2 — `SignRequest` exception filter swallows `OperationCanceledException`

`PLang/app/types/path/http/this.cs:330`:

```csharp
catch (System.Exception ex) when (ex is not (NullReferenceException
    or OutOfMemoryException or StackOverflowException))
```

Includes `OperationCanceledException` (cancellation propagates through the
catch as a swallow). Best-effort signing by design, but a cancelled signing
shouldn't silently let the request fly unsigned — a cancellation likely
means the host is being torn down. Tiny. Document or narrow the filter.

---

## Out of scope but noted

- The `[PathScheme]` attribute is defined (Stage 4) but only `code.load`
  will consume it. When that lands, **scheme registration becomes a trust
  decision**: a malicious assembly with `[PathScheme("file")]` can replace
  the file factory and silently MITM every filesystem operation in the
  process. `Scheme.Register` overwrites on duplicate key with no warning.
  Worth designing `code.load` so re-registration of a built-in scheme
  either errors or requires explicit consent.

- In-process Kestrel test fixture (`HttpTestServer.cs`) is test-only and
  not in the prod path — not audited beyond confirming that.

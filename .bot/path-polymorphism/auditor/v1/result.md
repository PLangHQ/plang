# Auditor v1 — result

**Branch:** `path-polymorphism` @ `f5eecaf71`
**Build:** clean rebuild (Debug, net10.0; stale-binary trap avoided).
**C# tests:** 2920/2920 pass (`PLang.Tests/bin/Debug/net10.0/PLang.Tests`)
**PLang tests:** 204/204 pass, 0 stale (`cd Tests && ../PlangConsole/bin/Debug/net10.0/plang --test`)

**Verdict: PASS.** First cross-cutting audit on this branch and the pipeline's
PASS holds up under independent verification. All security findings closed
in the actual code (not just claimed by re-reviewers), both codeanalyzer
v4 doc-only items resolved by coder v7 without a re-review, every
architect stage delivered in shape. One latent informational nit on
HttpPath; nothing blocks.

## Pipeline closure depth — verified by code trace

For each finding the re-reviewers closed, I went to the cited lines myself.

### Security (v1 S1, S2, S3 → v2 S4.a, S4.b → v3 O3)

- **S1 (HIGH SSRF) — closed.** `PLang/app/types/path/http/this.cs:54`
  declares `AllowAutoRedirect = false`. 3xx responses route through
  `FollowRedirect` (lines 380–422), which: (a) constructs a fresh
  `@this(target.ToString(), Context)` for the redirect target, (b) calls
  `AuthGate(verb)` on it before any I/O (line 419 — fresh consent prompt
  on the new host), (c) hops are bounded by `MaxRedirectHops = 5`
  (line 63 + check at 383), (d) the redirect URI is rejected when not
  http(s) (line 392 — closes meta-scheme escapes). The IMDS escalation
  vector is structurally blocked.

- **S2 (MED identity leak) — closed for the wire path.** `SignRequest`
  (lines 431–457) signs against `_uri.ToString()` on whichever `@this`
  instance the request is fired from. Because each redirect hop builds
  a *new* `@this` for the destination URL (line 416) and then calls
  `SendWithHops` on that instance (line 421), the next signature pins the
  destination's URL — the prior hop's `X-Signature` is never reaffixed to
  a different origin. Residual ("destination-pinned envelope verified
  host-side") stays deferred — that's a signing-module redesign, same as
  the F1 carry-forward.

- **S3 (LOW query-string disclosure) — closed.** `AuthorizationHint`
  override on HttpPath (lines 110–114) emits the "answering 'a' saves
  the full URL — query string included — to your local permission store"
  hint when `_uri.Query` is non-empty. Empty/`?` query suppresses.
  Carried into the consent prompt path through the base AuthGate.

- **S4.a (LOW IDN homograph) — closed.** `Absolute` getter line 140:
  `var host = _uri.IdnHost.ToLowerInvariant();`. Punycode form lands in
  both the consent prompt and the persisted grant key, so a Cyrillic-`а`
  variant on `apple.com` cannot silently match a previously-trusted
  ASCII grant. (IPv6 edge case noted as security v3 O6 — display-only,
  prompt and key still agree.)

- **S4.b (LOW userinfo confusion) — closed for the gate/wire/key
  triple.** Constructor lines 81–86 strip `_uri.UserInfo` via
  `UriBuilder { UserName = "", Password = "" }` *before* `_uri` is
  stored. All downstream consumers — `Absolute`, `AuthGate`,
  `SignRequest`, `SendAsync`, `FollowRedirect` (which constructs `new
  @this(target.ToString(), Context)` and re-runs the strip in the
  recursive ctor) — see the userinfo-free form.

- **O3 (reliability — 307/308 body) — closed.** `FollowRedirect` lines
  399–413 buffer the original `HttpContent` via
  `ReadAsByteArrayAsync()`, build a fresh `ByteArrayContent`, and copy
  only `content.Headers` (Content-Type etc.). No request-level auth
  headers exist on `HttpContent`, so the buffered re-send carries no
  request-auth survival.

### Codeanalyzer v4 (F1, F2) — closed in coder v7 (d30f84c77)

- **F1 (Data<T>.From silent value drop).** `PLang/app/data/this.cs`
  lines 1112–1151. The docstring now leads with the intent
  ("error/sentinel propagation across typed boundaries — the idiomatic
  call site is `if (!source.Success) return Data<T>.From(source);`"),
  enumerates what's forwarded (Type, Error, Handled, Returned,
  ReturnDepth, Warnings, Signature, Snapshot, Properties — flagged as
  shared reference), and explicitly states "Value handling is lossy by
  design: `source.Value is T t ? t : default`". That is exactly what
  codeanalyzer v4 asked for. The Properties-shared-reference clause is
  there too. Done.

- **F2 (orphan summary block).** `PLang/app/modules/this.cs` lines
  378–384 carry a single `<summary>` above `DescribeReturnTypeName`.
  The earlier orphan block is gone. The IDE-tooltip / CS1591 concern
  no longer applies.

### Tester v8 — accepted

The five mutation cycles tester v8 performed (N1 two mutations, N2 one,
N3 one, N4 one, plus F4-CARRY discovery) are reproducible from the
artifacts on disk; I did not re-mutate but I confirm the tests cited
exist and run green from the rebuild. Tester v8's verdict is honest.

## Architect's 7 stages — independently verified

| # | Stage | Verified |
|---|-------|----------|
| 1 | Namespace move `app.filesystem` → `app.types.path` | `grep -rln "app\.filesystem" PLang/` returns one comment-marker hit in `app/modules/code/this.cs:251`. No live `app.filesystem` namespace. |
| 2 | `path` becomes abstract + per-App `Scheme` registry | `PLang/app/types/path/this.cs:13` — `public abstract partial class @this`. `app/types/path/scheme/this.cs:23` — `ConcurrentDictionary` with `OrdinalIgnoreCase` keys. `App` ctor `PLang/app/this.cs:310-312` registers file/http/https before `Navigators.RegisterDefaults()` and before any user-facing entry. |
| 3 | Handler one-liners + IFile / DefaultFileProvider gone | `grep -rln "DefaultFileProvider\|IFile\b" PLang/app/` returns only `modules/code/this.cs:251` (comment marker "registration removed in Stage 3") and `modules/ui/code/Fluid.cs` (which uses the *unrelated* Microsoft.Extensions.FileProviders.IFileProvider for templating). No surviving uses of the deleted IFile. |
| 4 | `[PathScheme]` attribute | `PLang/app/types/path/PathSchemeAttribute.cs` declared. Decorates FilePath (`[PathScheme("file")]`) and HttpPath (`[PathScheme("http")] [PathScheme("https")]`). No production consumer reflects on it yet — that's the intentional "marker for future code.load" per the architect plan + security v3 O5 carry-forward. |
| 5 | HttpPath lands; identity wired | `PLang/app/types/path/http/this.cs` — 483 lines, full verb surface, `SignRequest` uses `modules.signing.sign` action via `Context.App.RunAction`. Process-shared `HttpClient` with `PooledConnectionLifetime = 2 min`. |
| 6 | `Absolute` per-scheme canonical | Base `Absolute => _absolutePath` (line 93). HttpPath overrides (lines 129–171) with scheme + IdnHost + non-default port + normalized path + sorted query, no fragment — matches the design's six rules exactly. |
| 7 | Contract base test | `PLang.Tests/App/Types/PathTests/Contract/PathSchemeContractTests.cs` plus `FilePathContractTests`, `HttpPathContractTests`, `CrossSchemeTests`, `FilePathFixture`, `HttpPathFixture`, `IPathSchemeFixture`, `CannedAnswerChannel`, `VerbName`. Cross-scheme `CopyTo` and `MoveTo` exercised in both directions (FilePath↔HttpPath) via the base default. |

## Cross-cutting hygiene

- **`^namespace [A-Z]` under `PLang/app/`** — zero hits. The `Default/`
  carve-out from the app-lowercase era is no longer present (folded into
  the namespace move).
- **`Console\.` under `PLang/app/types/path/`** — zero hits. The
  CLAUDE.md ban holds in the new path subsystem.
- **Permission doc-comment integrity** — `actor/permission/this.cs:12-16`
  states persisted `"a"` grants are Ed25519-signed with `Expires == null`
  (the corrected text from filesystem-permission auditor v2 F-1). The
  sibling `types/path/permission/this.cs:24` says the same thing
  consistently. No inversion drift.
- **`.Raw` consumer scope** — only 3 hits across `PLang/` (besides the
  two assignments in FilePath/HttpPath Resolve): `builder/code/Default.cs:230`
  uses it as a display fallback for files. None log or persist Raw on
  HttpPath in a security-sensitive way today.

## New findings

### F1 — informational / latent — `HttpPath.Raw` preserves userinfo

`PLang/app/types/path/http/this.cs:94` — `HttpPath.Resolve` returns
`new @this(rawPath, context) { Raw = rawPath }`. The constructor strips
`_uri.UserInfo` (S4.b) so the consent prompt, the persisted grant key,
the wire request, and `Absolute` all agree on the userinfo-free form.
But `Raw` is initialised from the *original* `rawPath` — if the caller
passed `https://user:pwd@host/x`, `Raw` keeps the `user:pwd@host` form
intact.

- **Impact today:** none. `.Raw` is read at exactly three sites in the
  whole `PLang/` tree (one assignment-only in FilePath.Resolve, one in
  HttpPath.Resolve itself, and `builder/code/Default.cs:230` which uses
  it as a display fallback for files — never for an http path). The
  builder consumer never sees an HttpPath in that branch.

- **Latent risk:** if a future change starts logging, tracing, or
  serializing `path.Raw` (e.g. a debug `--trace` write that dumps step
  inputs), the credentials in the URL would leak through. The S4.b
  triple is structurally airtight; this is the off-triple field.

- **Suggestion (non-blocking):** mirror the ctor strip into the Raw
  assignment — either compute Raw from the post-strip `_uri.ToString()`,
  or store Raw as `_uri.ToString()` directly so there is one source of
  truth. Documents the invariant ("every public string surface on an
  HttpPath is userinfo-free") and forecloses the latent leak.

- **Why filing now:** the cost of a five-line fix today is much smaller
  than the cost of an incident report later. Filing as informational so
  it gets adjudicated, not silently inherited.

- **Missed by:** security v2/v3 — both verified the S4.b triple
  (gate/wire/key) exhaustively but did not extend to off-triple fields.
  Reasonable scope decision; the auditor's job is the seam between axes.

## Carry-forwards — unchanged at HEAD

- **F1 (Med, carry-forward from filesystem-permission)** —
  `signing.verify` checks integrity but not signer authority.
  `actor/permission/this.cs:146-152`.
- **F2 (Low, carry-forward)** — unsigned persisted-row auto-trust.
  `actor/permission/this.cs:127`.
- **F4 (Low, carry-forward)** — Regex/Glob match has no `MatchTimeout`.
  `types/path/permission/this.cs:54-90`.
- **O5 (info)** — `[PathScheme]` consumer (`code.load` registration
  surface) deferred; the attribute is decorated but unread today, per
  the architect's Stage-4 plan.
- **O6 (info, security v3)** — `_uri.IdnHost` for IPv6 returns the bare
  hex form. Display-only; consent prompt and grant key still agree.

## Things I red-teamed that did not turn into findings

- **Scheme registry race.** Per-App `ConcurrentDictionary` with atomic
  Register; readers are lock-free. App ctor registers file/http/https
  before `CurrentActor = User` and before the actor channels are wired,
  so no concurrent reader exists at registration time. Safe.
- **`ParseScheme` validation.** `app/types/path/scheme/this.cs:68-80` —
  per-character whitelist of `[A-Za-z0-9+\-.]` per RFC 3986, returns
  empty for invalid scheme part → routes to `file`. Pathological inputs
  like `javascript:alert(1)` (no `://`) return empty scheme → file
  factory → ValidatePath, which treats it as a literal filename and
  either resolves under app root or fails — not exploitable, just
  confusing.
- **`file://` URL prefix.** `file:///etc/passwd` parses scheme `"file"`,
  routes to FilePath factory with raw `"file:///etc/passwd"`. Resolve
  does not strip the prefix; ValidatePath treats the whole thing as a
  relative-to-app-root path and produces a literal file named
  `file:/etc/passwd` under the app root. Confusing UX, not exploitable —
  Authorize is still the gate. Noted, not filed; the architect's plan
  doesn't promise file://-URL handling.
- **Cross-scheme `CopyTo` base default.** Reads bytes from source via
  the typed `ReadBytes()` (which calls the source scheme's AuthGate),
  writes via `destination.WriteBytes(bytes)` (which calls the
  destination scheme's AuthGate). Both gates fire. No fast-path skip.
  Tested in `CrossSchemeTests`.
- **Resolve factory bypass.** HttpPath.Resolve calls
  `new @this(rawPath, context)` directly — bypasses
  `Scheme.From` — but it is itself the registry's factory body for
  http/https, so this is the registry-entry path, not a bypass. FilePath
  same shape.
- **Empty/`?`-only query string.** `_uri.Query == "?"` suppresses the
  S3 hint (line 112). Edge case: a URL ending in `?` (zero pairs after
  the `?`). Suppressed correctly — no hint, no fake signal.
- **Path equality across scheme.** `path.@this.Equals` (lines 167–172)
  compares `_absolutePath` via `RootComparison`. For an HttpPath whose
  `_absolutePath` is the raw URL string, equality with a `string` works
  case-sensitively on Linux — correct for case-sensitive hosts (path
  segments) but slightly wrong for the host part (DNS is case-
  insensitive). Edge case; would only affect a future feature that
  dedupes HttpPath instances by string equality. Non-blocking.

## Bottom line

Branch is structurally sound. The pipeline's PASS verdict is genuine —
every closed finding holds up under code-trace, the architect's 7 stages
all landed in shape, no namespace drift, no Console.* leaks, no
doc-comment inversion across the move. One latent nit on `HttpPath.Raw`
worth fixing before any future trace/log consumer learns to read it;
filing as informational, not as a blocker.

Ship.

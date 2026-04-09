# Code Analysis v1 — Plan

## Scope

Full 5-pass analysis of all new/modified source files on `runtime2-builder-v2-http` vs `runtime2`.

### Files to analyze (by module):

**HTTP module** (new):
- `modules/http/types.cs` — enums, TransferProgress
- `modules/http/Config.cs` — IConfig for HTTP
- `modules/http/configure.cs` — configure action handler
- `modules/http/request.cs` — request action handler
- `modules/http/download.cs` — download action handler
- `modules/http/upload.cs` — upload action handler
- `modules/http/providers/IHttpProvider.cs` — provider interface
- `modules/http/providers/DefaultHttpProvider.cs` (~984 lines) — the big one

**Identity module** (new):
- `modules/identity/types.cs` — IdentityVariable
- `modules/identity/IdentityData.cs` — lazy-load Data subclass
- `modules/identity/create.cs` through `export.cs` — 8 action handlers
- `modules/identity/providers/IIdentityProvider.cs` — provider interface
- `modules/identity/providers/DefaultIdentityProvider.cs` (~327 lines)

**Signing module** (new):
- `modules/signing/SignedData.cs` (~255 lines) — owns signing + verification
- `modules/signing/Config.cs` — signing config
- `modules/signing/sign.cs`, `verify.cs` — action handlers
- `modules/signing/providers/` — IKeyProvider, ISigningProvider, KeyPair, Ed25519Provider

**Crypto module** (new):
- `modules/crypto/types.cs` — HashedData
- `modules/crypto/hash.cs`, `verify.cs` — action handlers
- `modules/crypto/providers/` — ICryptoProvider, DefaultProvider

**Provider module** (new):
- `modules/provider/load.cs`, `remove.cs`, `setDefault.cs`, `list.cs`

**Engine subsystem** (modified/new):
- `Engine/Providers/this.cs` — provider registry
- `Engine/Providers/IProvider.cs` — base provider interface
- `Engine/Config/this.cs` — renamed from Settings, scope-chain resolution
- `Engine/Config/ModuleView.cs`, `Scope.cs`, `IConfig.cs`
- `Engine/View.cs` — [In], [Out], [Sensitive] attributes
- `Engine/Channels/Serializers/TransportPropertyFilter.cs` — [In]/[Out] JSON override
- `Engine/Channels/Serializers/SensitivePropertyFilter.cs` — [Sensitive] strip
- `Engine/Memory/Data.Envelope.cs` — wrap/compress/encrypt pipeline
- `Engine/this.cs` — engine root (provider registration, RunAction, disposal)
- `modules/IConfigure.cs` — configure marker interface
- `GlobalUsings.cs`

## Analysis Approach

5-pass analysis per CLAUDE.md character spec:
1. **OBP Compliance** — check all 5 rules
2. **Simplification** — dead abstractions, over-parameterization, copy-paste
3. **Readability** — naming, method length, cohesion
4. **Behavioral Reasoning** — "what breaks silently?", trace data origins, generic catch audit, clone family, disposal
5. **Deletion Test** — "if I deleted lines X-Y, would any test fail?"

## Preliminary Findings (from initial read)

Already spotted during the read pass:

1. **DefaultHttpProvider not disposed by Engine** — Provider implements IDisposable (owns HttpClient), but engine.DisposeAsync only iterates `_libraries.All`, not `Providers`. HttpClient leaks.

2. **ExecuteHttpAsync generic catch masks programming errors** — Catches ALL exceptions including NullReferenceException, ArgumentException, etc. and converts them to user-visible "HttpError". This silently swallows bugs.

3. **Config.ResolvePrefix duplication** — Same namespace-extraction logic appears in both `For<T>()` (line 70-72) and `ResolvePrefix<T>()` (line 102-107).

4. **DefaultHttpProvider.TryExtractSignedErrorIdentity legacy path** — The "signature" field fallback (lines 551-567) may not be tested.

5. **SignedData.VerifyAsync skips hash check when action.Data.Value is null** — By design for envelope-only verification, but undocumented intent.

Will complete the full 5-pass analysis after plan approval.

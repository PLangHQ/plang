# Stage 14 — coder plan (`timespan-iso-8601-sweep`)

`int? ExpiresInMs` → `TimeSpan? Expires` on Callback.Signature and
`App.modules.signing.sign`. JSON wire form becomes ISO 8601 (`"PT5M"`)
via the existing `TimeSpanIso8601Converter` (already wired globally on
`Json.cs:23, 35`).

## Files (production — 6)

- `Callback/Signature/this.cs` — property type + name change.
- `modules/signing/sign.cs` — action `Data.@this<int>? ExpiresInMs` → `Data.@this<TimeSpan>? Expires`.
- `Data/this.Envelope.cs:86–94` — local + envelope assignment.
- `modules/signing/providers/Ed25519Provider.cs:47` — `action.ExpiresInMs?.Value is int expiryMs ? now.AddMilliseconds(expiryMs)` → `action.Expires?.Value is TimeSpan expiry ? now.Add(expiry)`.
- `modules/http/providers/DefaultHttpProvider.cs:389` — `signOptions.ExpiresInMs` → `signOptions.Expires`.
- Doc-comment refresh: `App/this.cs`, `App/Callback/this.cs`.

## Files (tests — 5)

- `App/CallbackTests/AppCallbackConfigTests.cs` — type + values; rename test.
- `App/DataTests/DataLazySignatureTests.cs` — values to `TimeSpan.From*`.
- `App/DataTests/DataContextWiringTests.cs` — value + comment refresh.
- `App/Modules/signing/SignActionTests.cs` — helper signature `int? expiresInMs` → `TimeSpan? expires`; call sites use `TimeSpan.FromSeconds(5)` etc.
- `App/Modules/signing/VerifyActionTests.cs` — same.

## todos.md

`Documentation/Runtime2/todos.md` 2026-05-06 entry marked RESOLVED with
note about `CacheSettings.DurationMs` / `RetryOverMs` flagged for future.

## Verification

- `grep -rn "ExpiresInMs" PLang/ PLang.Tests/ --include='*.cs'` → 0.
- C# 2752/2752; PLang 199/199; build clean.

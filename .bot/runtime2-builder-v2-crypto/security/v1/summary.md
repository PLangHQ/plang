# Security Audit v1 — Summary

## What this is
Security audit of the crypto module (hash/verify with Keccak256/SHA256), Engine.Providers registry, and identity module (carry-forward). Both blue team (attack surface mapping) and red team (offensive analysis).

## What was done
- Mapped 5 attack surface areas: crypto handlers, provider registry, Data.Verified, [Sensitive] filtering, identity module
- Identified 4 findings: 1 medium, 3 low (accepted-risk)
- Verified Data.Envelope now includes SensitivePropertyFilter (identity audit medium finding is resolved)

## Findings

| # | Severity | Finding | Status |
|---|----------|---------|--------|
| 1 | Medium | `DefaultProvider.Verify` uses `SequenceEqual` (non-constant-time) — timing side-channel | open |
| 2 | Low | Ed25519 key bytes not zeroed in managed memory | accepted-risk |
| 3 | Low | Private keys stored as plain base64 in SQLite | accepted-risk |
| 4 | Low | Provider registry allows silent replacement | accepted-risk |

## Key code change recommended

```csharp
// DefaultProvider.cs:27 — BEFORE
return Data.Ok(actual.AsSpan().SequenceEqual(expectedHash));

// AFTER
return Data.Ok(CryptographicOperations.FixedTimeEquals(actual, expectedHash));
```

## Verdict: PASS
No critical/high findings. The medium timing fix is recommended but not blocking — PLang's user-sovereign threat model makes exploitation unlikely in typical usage. The three low findings are inherent to managed runtime and the trust model.

## Files reviewed
- `PLang/App/modules/crypto/hash.cs`
- `PLang/App/modules/crypto/verify.cs`
- `PLang/App/modules/crypto/providers/DefaultProvider.cs`
- `PLang/App/modules/crypto/providers/ICryptoProvider.cs`
- `PLang/App/modules/crypto/types.cs`
- `PLang/App/Engine/Providers/this.cs`
- `PLang/App/Engine/Context/Actor.cs`
- `PLang/App/Engine/View.cs`
- `PLang/App/Engine/Memory/Data.Envelope.cs`
- `PLang/App/Engine/Channels/Serializers/SensitivePropertyFilter.cs`
- `PLang/App/modules/identity/*.cs` (all 11 files)

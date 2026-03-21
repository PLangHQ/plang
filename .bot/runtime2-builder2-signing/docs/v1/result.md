# Docs v1 Result — runtime2-builder2-signing

## CHANGELOG

### Added
- **Signing module** (`signing.sign`, `signing.verify`): Sign data with Ed25519 (or any pluggable `ISigningProvider`). Verify signatures with a 9-step pipeline: type check, provider resolution, timeout, expiry, nonce replay, contract matching, header matching, data hash verification, cryptographic signature verification. Each step returns a specific error key for targeted error handling.
- **Provider module** (`provider.load`, `provider.remove`, `provider.setDefault`, `provider.list`): Manage pluggable provider implementations at runtime. Load external DLLs, register providers by type, switch defaults, list registered providers. Type name mapping supports "signing", "crypto", "identity", "key".
- **Named provider registry** (`Engine.Providers`): Upgraded from single-provider-per-type to named multi-provider registry. `ConcurrentDictionary<Type, ConcurrentDictionary<string, IProvider>>`. First registered becomes default. Thread-safe `SetDefault` avoids no-default window.
- **`ICache.TryAddAsync`**: Atomic add-if-absent for nonce replay protection. Used by `SignedData.VerifyAsync` with TTL matching signature timeout.
- **`SignedData`**: Signed data envelope with deterministic JSON serialization (`JsonPropertyOrder`), contract matching, and header verification. Owns both signing and verification (OBP).
- **`SigningSettings`**: Module settings — `Provider` (default: "ed25519"), `TimeoutMs` (default: 300000ms / 5 minutes).
- **`Data.Signature`**: Property on `Data` holding the `SignedData` envelope (`[JsonIgnore]`, `[Out]`). Any Data flowing through channels can carry a signature.

### Changed
- **`identity.getAll` → `identity.list`**: Renamed for consistency with other list actions.
- **`Engine.Providers` API**: Now supports `Get<T>(name?)`, `Register(Type, provider)`, `Remove(Type, name)`, `SetDefault(Type, name)`, `List(Type)`, `ResolveType(typeName)`. Generic methods delegate to non-generic equivalents.
- **`IdentityData.ResolveDefault()`**: Now throws `InvalidOperationException` on provider failure instead of silently returning null (auditor critical finding #1).

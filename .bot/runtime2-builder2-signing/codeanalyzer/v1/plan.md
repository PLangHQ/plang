# Code Analysis Plan — v1

## Scope
Full 5-pass analysis of the signing/crypto/identity/provider modules on `runtime2-builder2-signing`. This is post-coder-fix — the coder already addressed 7 OBP violations. I'm analyzing the full module set for remaining issues.

## Files to Analyze

### Engine Core (modified)
- `Engine/this.cs` — provider registration in constructor, RunAction<T,R>
- `Engine/Context/Actor.cs` — IdentityData, DynamicData %MyIdentity%
- `Engine/View.cs` — [Sensitive], [Out] attributes
- `Engine/Memory/Data.Envelope.cs` — Signature property, SensitivePropertyFilter wiring
- `Engine/Cache/this.cs` + `MemoryStepCache.cs` — TryAddAsync for nonce replay

### Providers (new)
- `Engine/Providers/this.cs` — registry with generic + non-generic overloads
- `Engine/Providers/IProvider.cs`, `IKeyProvider.cs`, `ISigningProvider.cs`, `IIdentityProvider.cs`
- `Engine/Providers/Ed25519Provider.cs`, `DefaultIdentityProvider.cs`, `KeyPair.cs`

### Modules (new)
- `modules/signing/` — SignedData.cs, sign.cs, verify.cs, Settings.cs
- `modules/crypto/` — hash.cs, verify.cs, types.cs, providers/DefaultProvider.cs, providers/ICryptoProvider.cs
- `modules/identity/` — IdentityData.cs, get.cs, create.cs, list.cs, archive.cs, unarchive.cs, rename.cs, setDefault.cs, export.cs, types.cs
- `modules/provider/` — load.cs, list.cs, remove.cs, setDefault.cs

### Support (modified)
- `Engine/Channels/Serializers/SensitivePropertyFilter.cs`
- `Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs`

## 5-Pass Analysis Order
1. **OBP Compliance** — behavior ownership, navigate-don't-pass, object references, per-request vs per-object state, smart collections
2. **Simplification** — dead abstractions, duplication, over-parameterization
3. **Readability** — naming, method length, cohesion, flow clarity
4. **Behavioral Reasoning** — silent failures, type surface, clone family, rehydration fragility
5. **Deletion Test** — every code path: "if deleted, would a test fail?"

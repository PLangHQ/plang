# Docs v1 Plan — runtime2-builder2-signing

## Context
Signing module (sign/verify/SignedData), provider module (load/remove/setDefault/list), and supporting infrastructure (Engine.Providers registry, Ed25519Provider, ISigningProvider) were added across this branch. Identity and crypto modules were documented on prior branches but need updates for this branch's changes. Auditor v2 PASS, tester v2 PASS, security v1 PASS.

## Documentation Gaps Found

### 1. XML Doc Comments

**Missing property-level docs (add where meaningful):**
- `IdentityVariable` — Name, PublicKey, PrivateKey, IsDefault, IsArchived, Created have no `///` docs
- `ISigningProvider.Sign/Verify` — no parameter docs
- `IIdentityProvider` — 9 methods with no parameter docs
- `provider/load.cs` — Path and Name parameters undocumented
- `provider/remove.cs` — Name and Type parameters undocumented
- `provider/setDefault.cs` — Name and Type parameters undocumented
- `provider/list.cs` — Type parameter undocumented
- `Ed25519Provider` — Sign/Verify/GenerateKeyPair parameters undocumented
- `DefaultIdentityProvider` — internal helpers (LoadAsync, LoadAllAsync, SaveAsync, RemoveAsync, Deserialize) undocumented

### 2. Architecture Documentation — modules.md

**Critical:** modules.md is missing signing and provider module sections entirely. It lists identity as having `getAll` but this was renamed to `list`. Needs:
- Signing module section (sign, verify actions, SignedData, verification pipeline)
- Provider module section (load, remove, setDefault, list actions, type name mapping)
- Fix `identity.getAll` → `identity.list` in the table

### 3. Architecture Documentation — good_to_know.md

**Needs new section:** Signing module design — 9-step verification pipeline, contract matching, nonce replay, deterministic serialization. The lazy verification note (lines 285-291) is already there but the signing architecture is undocumented.

### 4. CHANGELOG

New user-visible features need CHANGELOG entries in result.md:
- Signing module: sign data, verify signatures, Ed25519
- Provider module: pluggable provider management
- ICache.TryAddAsync for nonce replay protection

### 5. Consistency

- `identity.getAll` → `identity.list` rename not reflected in modules.md
- README.md file tree still shows `identity/getAll.cs` — needs update

## Plan

1. Add XML docs to IdentityVariable properties, ISigningProvider, IIdentityProvider, provider actions, Ed25519Provider, DefaultIdentityProvider helpers
2. Add signing module section to modules.md
3. Add provider module section to modules.md
4. Fix identity.getAll → identity.list in modules.md and README.md
5. Add signing architecture section to good_to_know.md
6. Write CHANGELOG in result.md
7. Write docs-report.json and verdict.json
8. Update report.json with session data

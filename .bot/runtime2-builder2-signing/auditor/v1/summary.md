# Auditor v1 Summary — runtime2-builder2-signing

## What this is
Cross-cutting audit of the signing/crypto/identity/provider module additions to PLang Runtime2. Four new module families (signing, crypto, identity, provider) plus foundation changes to Engine, Data.Envelope, ICache, and Actor.

## What was done
Reviewed all production code across 30+ files, traced cross-file contracts, ran all 1827 tests (pass), and assessed the three previous reviews.

### Previous Reviews Assessment
- **Codeanalyzer**: Agree with PASS. Their 5-pass methodology caught real issues. All fixes verified correctly in v2.
- **Tester**: Agree with PASS. Coverage improvements were substantial (provider/list 0%→100%, SignedData 96%→100%). Minor gap: didn't flag IdentityData error-swallowing.
- **Security**: Agree with PASS. Threat model alignment is correct for user-sovereign architecture. Medium findings are genuine design trade-offs, not bugs.

### Findings (1 minor, 2 nits)

1. **Minor** — `IdentityData.ResolveDefault()` silently swallows all errors (provider failure, DataSource corruption, key generation failure) and returns null. The `%MyIdentity%` variable becomes silently null with no diagnostic trail. This is a design note for when diagnostics are added, not a blocking issue.

2. **Nit** — `NowUtc` MemoryStack variable cast without null check in `SignedData.CreateAsync` and `DefaultIdentityProvider.GenerateIdentity`. Safe because MemoryStack always registers it, but fragile in theory.

3. **Nit** — `ToSigningBytes()` save-mutate-restore pattern (already flagged by security as Low #4). Safe in PLang's single-threaded model.

### Cross-File Contracts Verified Clean
- **EngineProviders ↔ all consumers**: Registration in Engine constructor correctly provides all 4 provider types. Generic methods properly delegate to non-generic. All handler modules (signing, crypto, identity, provider) use consistent patterns.
- **SignedData ↔ sign/verify handlers**: Data flow is correct. sign.Run() → SignedData.CreateAsync() → Hash → Sign. verify.Run() → SignedData.VerifyAsync() → 9-step verification. No contract gaps.
- **ICache.TryAddAsync**: Both MemoryStepCache and FakeCache (test) implement it correctly. No missing implementors.
- **SensitivePropertyFilter ↔ Data.Envelope**: `_envelopeJsonOptions` correctly includes the filter. IdentityVariable.PrivateKey is marked `[Sensitive]`. The chain works: output serialization strips it, storage keeps it.
- **Provider type resolution**: All 4 provider handlers use `ResolveType()` consistently. No divergent resolution paths.

### Architectural Fit
- **OBP compliance**: Excellent. SignedData owns signing and verification. HashedData owns serialization and formatting. DefaultIdentityProvider owns persistence. Handlers are thin — delegate to domain objects.
- **Lazy-load**: Respected. No eager loading of goals or .pr files.
- **Provider pattern**: Clean separation — interfaces in Engine/Providers, defaults registered in Engine constructor, overridable via `provider.load`.

## Verdict
**PASS** — Ready for docs bot.

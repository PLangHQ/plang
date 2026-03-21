# Security Analysis v1 Summary — runtime2-builder2-signing

## What this is
Security audit of the signing, crypto, identity, and provider modules. Blue team (attack surface mapping) + red team (exploit feasibility) analysis aligned to PLang's user-sovereign threat model.

## What was done
- Audited 20+ source files across 4 modules (signing, crypto, identity, provider) and engine infrastructure (Providers, Cache, Data.Envelope)
- Mapped 6 attack surfaces with mitigations and gaps
- Produced 8 findings: 0 critical, 0 high, 3 medium, 5 low
- Cross-referenced against previous branch security reports and carry-forward findings

## Key decisions
- **Data.Signature public setter** rated medium, not critical. The agent subagent initially rated this critical, but crypto verification (step 9 of VerifyAsync) is the real gate — modifying any signed field invalidates the Ed25519 signature. The setter is a design smell, not an exploit vector.
- **Provider.load RCE** rated low (by design). User-sovereign: loading DLLs is the user's choice, like Assembly.LoadFrom in C#. If attacker controls .pr files, they already have equivalent access.
- **Nonce replay** rated medium (accepted-risk). ICache is pluggable — distributed deployments swap to Redis. Single-process is safe.

## Files reviewed
- `PLang/Runtime2/modules/signing/SignedData.cs` — core signing/verification logic
- `PLang/Runtime2/modules/signing/sign.cs`, `verify.cs` — action handlers
- `PLang/Runtime2/Engine/Providers/Ed25519Provider.cs` — crypto implementation
- `PLang/Runtime2/Engine/Providers/DefaultIdentityProvider.cs` — identity persistence
- `PLang/Runtime2/Engine/Providers/this.cs` — provider registry
- `PLang/Runtime2/modules/provider/load.cs` — DLL loading
- `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` — Signature property, transport
- `PLang/Runtime2/Engine/Cache/MemoryStepCache.cs` — nonce cache
- `PLang/Runtime2/modules/crypto/providers/DefaultProvider.cs` — hashing
- All identity handlers, crypto handlers, provider handlers

## Verdict: PASS
Recommend running the **auditor** next.

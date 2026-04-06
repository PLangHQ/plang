# Security Analysis Plan — runtime2-builder2-signing v1

## Scope
Full security audit of the signing, crypto, identity, and provider modules added on this branch. Both blue team (defensive) and red team (offensive) analysis.

## Files in Scope
- `PLang/App/modules/signing/` — sign.cs, verify.cs, SignedData.cs, Settings.cs
- `PLang/App/modules/crypto/` — hash.cs, verify.cs, types.cs, providers/
- `PLang/App/modules/identity/` — all handlers + types.cs, IdentityData.cs
- `PLang/App/modules/provider/` — load.cs, list.cs, remove.cs, setDefault.cs
- `PLang/App/Providers/` — Ed25519Provider, DefaultIdentityProvider, ISigningProvider, IKeyProvider, KeyPair, NamedProviderRegistry
- `PLang/App/Memory/Data.Envelope.cs` — Signature property
- `PLang/App/Cache/` — nonce replay dependency

## Approach
1. **Blue team**: Map attack surface — what's exposed, trust boundaries, mitigations, gaps
2. **Red team**: For each vector — exploit sketch, feasibility, severity, proposed fix
3. **Threat model alignment**: Apply PLang's user-sovereign model — don't flag user actions as attacks, focus on untrusted external data

## Key Questions
- Is `Data.Signature` (public setter) exploitable from external data paths?
- Is nonce replay protection sufficient for the intended deployment model?
- Can `provider.load` be abused from untrusted .pr files?
- Are private keys adequately protected at rest and in transit?
- Does `ToSigningBytes()` mutation pattern have thread-safety implications?

## Deliverables
- `security-report.json` — structured findings
- `v1/result.md` — detailed analysis
- `v1/verdict.json` — pass/fail

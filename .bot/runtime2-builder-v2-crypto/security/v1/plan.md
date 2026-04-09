# Security Audit v1 — Plan

## Scope
- **Crypto module**: hash.cs, verify.cs, DefaultProvider.cs, ICryptoProvider.cs, types.cs
- **Engine.Providers**: type-keyed provider registry
- **Identity module**: carry-forward from identity branch audit (already PASS)
- **Cross-cutting**: Data.Envelope transport pipeline, [Sensitive] attribute filtering

## Approach

### Phase 1: Blue Team (Attack Surface Mapping)
1. Map crypto module input/output boundaries
2. Trace provider registration → resolution path
3. Verify [Sensitive] filtering on all serialization paths
4. Check Data.Verified/SetVerified access control

### Phase 2: Red Team (Offensive Analysis)
1. **Timing side-channel**: SequenceEqual in Verify — non-constant-time comparison
2. **Algorithm confusion**: Can an attacker influence which algorithm is used?
3. **Provider substitution**: Can an untrusted provider be injected?
4. **Key material exposure**: Private keys in managed memory, serialization leaks
5. **Input validation**: Null/empty/oversized data, malformed hex strings

### Phase 3: Report
Write security-report.json, verdict.json, result.md, summary.md

## Key Finding (Pre-audit)
`DefaultProvider.Verify` uses `Span<byte>.SequenceEqual` which short-circuits on first byte mismatch. For a crypto verification function, this is a timing side-channel. Fix: `CryptographicOperations.FixedTimeEquals()`.

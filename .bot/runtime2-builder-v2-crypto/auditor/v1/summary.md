# Auditor v1 Summary — Crypto Module

## What this is

Cross-cutting integrity audit of the crypto module (hash/verify handlers, pluggable provider architecture, Engine.Providers) and carried-forward identity module changes. This is the final gate before the branch can be merged or move to docs.

## What was done

### Prior reviewer assessment
- **Codeanalyzer v2**: PASS — agree. Thorough per-file OBP analysis. One blind spot: missed the "providers throw instead of returning Data" issue that Ingi caught. This suggests the codeanalyzer's "behavior methods never throw" check didn't extend to provider interfaces on first pass. Fixed in v2.
- **Tester v4**: PASS — agree. Four rounds of testing found a real null-Hash bug (v2), drove error path coverage to completion (v3), and did a fresh-eyes audit (v4). The identity error path tests using `FailingSaveDataSource`/`FailingRemoveDataSource` are well-designed.
- **Security v1**: PASS — agree with reservation. The timing side-channel (`SequenceEqual` → `FixedTimeEquals`) is a real issue that should be fixed before the signing module lands. Current threat model (local PLang apps) makes it low-risk, but the signing module will expose verification to external callers.

### Cross-file contract review
1. **Engine.Providers ↔ Engine.@this**: Clean integration. `Providers` is a simple property on Engine (`line 89`), initialized inline. No disposal needed today (DefaultProvider is stateless), but noted as a future concern.
2. **Hash ↔ Verify coupling**: Verify calls `crypto.Hash.ResolveProvider(Context)` and `crypto.Hash.SerializeData(Data)` — shared static methods. This is correct (single source of truth for provider resolution and data serialization). No divergence risk.
3. **Identity error chain**: `IdentityData.ResolveDefault()` → `GetOrCreateDefaultAsync()` → `SaveAsync()`. The chain is fully tested via `IdentityErrorPathTests`. The `InvalidOperationException` thrown by `GetOrCreateDefaultAsync` is caught by: (a) `IdentityData.ResolveDefault()` → returns null, (b) `Get.Run()` → returns `ServiceError`, (c) `Export.Run()` → returns `ServiceError`. All three paths have tests.
4. **SensitivePropertyFilter**: Present in both `JsonStreamSerializer._options` and `Data.Envelope._envelopeJsonOptions`. Both Compress and ForView paths include it. No leakage path found.

### Findings
1. **Minor**: `ResolveProvider()` allocates `new DefaultProvider()` on every call (hash.cs:54). Functionally correct but wasteful — should cache the default instance.
2. **Nit**: Engine.Providers doesn't dispose registered providers. Not an issue today (DefaultProvider is stateless), but worth noting for when stateful providers arrive.
3. **Nit**: Hash.Run() and Verify.Run() have no explicit `await` — the source generator adds them. Informational only.

### What I didn't find
- No cross-file contract gaps between crypto and identity modules
- No OBP violations missed by codeanalyzer
- No test gaps missed by tester
- No security issues missed by security beyond what they already flagged
- `HashedData` as a plain class (not `Data`) is fine — it's a value type returned inside `Data.Ok(hashedData)`, not a result carrier itself

## Verdict: PASS

Suggest running the **docs** bot next.

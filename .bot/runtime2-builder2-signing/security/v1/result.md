# Security Analysis — runtime2-builder2-signing v1

## Threat Model Alignment

PLang is **user-sovereign**: the user owns their software. .pr files run in a trusted environment. The trust boundary is cryptographic signatures on Data, not access controls on code paths.

This means:
- `provider.load` loading arbitrary DLLs = user's prerogative (like `Assembly.LoadFrom` in C#)
- `%MyIdentity.PrivateKey%` accessible via dot navigation = user's prerogative
- `identity.export` returning raw private key = by design

The attack surface that matters is **untrusted external data**: signed messages arriving from other parties, compressed payloads at transport boundaries, external API responses.

---

## Phase 1: Blue Team (Defensive Audit)

### Attack Surface Map

| Area | Trust Boundary | Status |
|------|---------------|--------|
| SignedData verification pipeline | External signed Data | **Strong** — 9-step verification |
| Nonce replay protection | External replay attempts | **Adequate** (single-process) |
| Identity persistence | SQLite on disk | **Adequate** — [Sensitive] on output |
| Provider loading | .pr file parameters | **By design** — full trust |
| Crypto hashing | Provider registry | **Strong** — limited algorithms |
| Data.Envelope transport | Inbound compressed data | **Good** — size + depth limits |

### SignedData Verification Pipeline (the core security mechanism)

`SignedData.VerifyAsync()` runs 9 sequential checks:

1. **Type = "signature"** — rejects non-signature envelopes
2. **Provider resolution** — algorithm must map to registered ISigningProvider
3. **Age check** — `Created` not older than timeout (default 300s)
4. **Expiry check** — absolute deadline if set
5. **Nonce replay** — atomic `cache.TryAdd` prevents reuse
6. **Contract matching** — case-insensitive set equality
7. **Header matching** — string-ordinal per key/value (only if verifier expects headers)
8. **Data hash** — re-hash original data, compare against signed hash
9. **Cryptographic signature** — Ed25519 verify against Identity (public key)

**Assessment**: This is a solid verification pipeline. The ordering is correct — cheap checks (type, age) before expensive ones (hash, crypto). The nonce check is correctly placed before contract/header checks to prevent replay with different contracts.

### Ed25519 Implementation

`Ed25519Provider` uses NSec library — a well-audited .NET crypto library. Key observations:

- `AllowPlaintextExport` policy is necessary because PLang stores keys as base64 strings (not OS keystore). This is the correct policy for the persistence model.
- Sign/Verify both catch all exceptions and return `Data.FromError` — behavior methods never throw. **Correct.**
- `GenerateKeyPair()` returns `Data<KeyPair>` — clean Data pipeline. **Correct.**

### Identity Provider Security

`DefaultIdentityProvider` stores identities in System DataSource (SQLite):

- **Atomic rename**: save new name first, then delete old — no data loss on failure. **Good pattern.**
- **Archive protection**: cannot archive the default identity. **Correct.**
- **Duplicate detection**: case-insensitive name comparison on create/rename. **Correct.**
- **Auto-create default**: if no identities exist, auto-creates "default". This is a convenience feature, not a security concern.

### Provider Registry

`NamedProviderRegistry` uses `ConcurrentDictionary` — thread-safe. Key observations:

- **First-registered-is-default**: correct for bootstrapping. No race condition because `GetOrAdd` + `Count == 1` is a single ConcurrentDictionary operation.
- **SetDefault atomicity**: sets new default first, then clears old. Brief window where two providers have `IsDefault = true`, but `Get<T>()` iterates and returns the first match — always finds the intended default. **Acceptable.**
- **Cannot remove default**: prevents "no default" state. **Correct.**

---

## Phase 2: Red Team (Offensive Testing)

### Finding 1: Data.Signature Public Setter (Medium)

**Vector**: `Data.Signature` is `{ get; set; }`. Any code path — including dot navigation from PLang steps — can attach a SignedData object to any Data.

**Can this bypass verification?** No. Here's why:

1. An attacker crafts a SignedData with modified fields (e.g., different contracts)
2. But `ToSigningBytes()` serializes ALL fields (except Signature) into the signing bytes
3. The Ed25519 signature was computed over the original fields
4. Modifying any field invalidates the signature → step 9 fails
5. To produce a valid signature for modified fields, attacker needs the private key

**The crypto is the real gate**, not the setter visibility. However, `internal set` would be cleaner — it limits write access to the PLang assembly and communicates intent.

**Severity**: Medium (design smell, not exploitable)

### Finding 2: Nonce Replay — Single-Process Only (Medium)

**Vector**: `MemoryStepCache` is in-process. Multi-instance deployments have independent nonce caches.

**PLang context**: PLang apps are typically single-process. The `ICache` interface is pluggable — distributed deployments can swap to Redis. This is an architecture decision, not a bug.

**Severity**: Medium (documented limitation, pluggable mitigation exists)

### Finding 3: Provider.load = RCE (Low — By Design)

**Vector**: `Assembly.LoadFrom` executes constructors of discovered types. A malicious DLL gets full code execution.

**PLang context**: This is equivalent to C#'s `Assembly.LoadFrom`. The user chose to load the DLL via a PLang step. The .pr file is the trust boundary — if it's compromised, everything downstream is compromised regardless.

**Rate the chain**: If the attacker can place a malicious DLL and modify a .pr file to load it, they already have filesystem access and can do anything. This is informational.

**Severity**: Low (by design, prerequisite grants equivalent access)

### Finding 4: ToSigningBytes() Mutation (Low)

**Vector**: Temporarily nulls Signature field during serialization. Not thread-safe.

**PLang context**: Step execution is single-threaded per context. The only concurrent access would be from event handlers, which run in sequence (not parallel). This is theoretical.

**Severity**: Low (single-threaded execution model)

### Finding 5: Empty Nonce Not Validated (Low)

**Vector**: Nonce comes from `MemoryStack.GetValue("GUID")`. If GUID is somehow empty, cache key is `"nonce:"` — all empty-nonce messages collide.

**Behavior**: First passes, rest fail (NonceReplay error). This is **fail-secure** — it denies service rather than allowing replay.

**Severity**: Low (fail-secure, unlikely to occur)

### Finding 6: [Sensitive] Scope (Low — By Design)

**Vector**: `[Sensitive]` on `IdentityVariable.PrivateKey` only filters `JsonStreamSerializer` output. Not filtered in: dot navigation, raw `JsonSerializer`, `Data.Envelope`.

**PLang context**: User-sovereign. The user can access their own private keys. `[Sensitive]` prevents accidental exposure in output channels, which is its intended purpose.

**Severity**: Low (by design)

### Finding 7: Header ToString Comparison (Low)

**Vector**: `signedValue?.ToString()` vs `kvp.Value?.ToString()` — type-erased comparison. `int(1)` matches `string("1")`.

**PLang context**: Both sides go through the LLM builder, which produces consistent JSON types. Header values in SignedData are whatever the builder serialized. The verifier's headers come from the same builder. Cross-type confusion requires an attacker controlling one side independently — not possible when both sides are builder-generated .pr files.

**Severity**: Low (not exploitable in practice)

### Finding 8: Data.Envelope Decompress Exception Gap (Medium — Carry-forward)

**Vector**: `Decompress()` catches `InvalidDataException` and `JsonException` but not `InvalidOperationException`. If `RehydrateNestedData` encounters a type parsing failure, the exception escapes.

**Impact**: The Step-level `catch(Exception)` safety net catches it, but returns a generic `StepError` instead of a typed `DecompressError`. This violates the "behavior methods never throw" contract.

**Severity**: Medium (contract violation, but safety net exists)

---

## Carry-forward Findings

| Finding | Origin Branch | Status | Notes |
|---------|--------------|--------|-------|
| Data.Envelope InvalidOperationException gap | data-envelope-architecture | Still open | Finding #8 above |
| `__condition__` stale signal | runtime2-action-conditions | Still open | Not in scope for this branch |

---

## Summary

**Overall security posture: GOOD**

The signing module is well-designed for PLang's user-sovereign threat model:

- **Strong crypto**: Ed25519 via NSec, 9-step verification pipeline
- **Clean Data patterns**: all methods return Data, never throw
- **Pluggable architecture**: ICache, ISigningProvider, ICryptoProvider all swappable
- **No critical findings**: no high-severity open issues

Three medium-severity findings:
1. `Data.Signature` public setter (design smell, crypto is the real gate)
2. Nonce replay single-process limitation (documented, pluggable)
3. Carry-forward Decompress exception gap (safety net exists)

All other findings are low severity or accepted-risk under the user-sovereign model.

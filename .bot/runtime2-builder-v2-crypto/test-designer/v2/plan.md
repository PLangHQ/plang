# Test Plan — Crypto Module (Piece 2) — v2

## Overview

Revised test plan for the crypto module. v1 had structural gaps: no provider swap tests, thin error paths, anemic verify action coverage.

Changes from v1:
- **Merged** Batches 1+2 into single `DefaultProviderTests.cs` (one class → one file)
- **Added** provider swap + resolution chain tests (Batch 2)
- **Expanded** action handler error paths: null input, corrupted hash, provider throws
- **Expanded** verify action handler: wrong algorithm, corrupted hash, error propagation
- **Added** PLang provider swap test (architect required it)

Source: `.bot/runtime2-builder-v2/architect/v2/plan.md` — Piece 2 section.

---

## C# Tests

### Batch 1: DefaultProvider — Hash + Verify (~15 tests)

Direct provider tests. No action handler, no context — just bytes in, bytes/bool out. One file because it's one class.

**File:** `PLang.Tests/Runtime2/Modules/crypto/DefaultProviderTests.cs`

#### Hash

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_Keccak256_ProducesCorrectHash` | Known input → known Keccak256 hex output (deterministic). Compare against independently computed reference value. |
| 2 | `Hash_SHA256_ProducesCorrectHash` | Known input → known SHA256 hex output (deterministic). Reference from `echo -n "test" \| sha256sum`. |
| 3 | `Hash_Bcrypt_ProducesValidHash` | Output is valid bcrypt string (starts with `$2`) |
| 4 | `Hash_Bcrypt_SameInput_DifferentHashes` | Salt makes each call produce different output |
| 5 | `Hash_UnknownAlgorithm_Throws` | `"md5"` → `NotSupportedException` |
| 6 | `Hash_EmptyInput_DoesNotThrow` | Empty byte array hashes without error (edge case, not a crash) |
| 7 | `Hash_Keccak256_OutputIs32Bytes` | Keccak256 always produces 32 bytes |
| 8 | `Hash_SHA256_OutputIs32Bytes` | SHA256 always produces 32 bytes (parity with test 7) |

#### Verify

| # | Test | What it verifies |
|---|------|-----------------|
| 9 | `Verify_Keccak256_RoundTrip_ReturnsTrue` | Hash then verify same data → true |
| 10 | `Verify_Keccak256_WrongData_ReturnsFalse` | Verify with different data → false |
| 11 | `Verify_SHA256_RoundTrip_ReturnsTrue` | SHA256 hash then verify → true |
| 12 | `Verify_SHA256_WrongHash_ReturnsFalse` | SHA256 with tampered hash → false |
| 13 | `Verify_Bcrypt_CorrectPassword_ReturnsTrue` | Bcrypt verify with correct password |
| 14 | `Verify_Bcrypt_WrongPassword_ReturnsFalse` | Bcrypt verify with wrong password |
| 15 | `Verify_UnknownAlgorithm_Throws` | `"md5"` on verify → same error as hash (provider consistency) |

---

### Batch 2: Provider Resolution + Swap (~4 tests)

Tests that the action handlers use the provider chain correctly. Uses a mock `ICryptoProvider` to prove dispatch works.

**File:** `PLang.Tests/Runtime2/Modules/crypto/ProviderResolutionTests.cs`

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_UsesProviderFromSettings_NotDefault` | Register mock provider via settings → hash action calls mock, not DefaultProvider |
| 2 | `Hash_PerCallAlgorithm_OverridesDefault` | Per-call `algorithm` param takes priority over engine default |
| 3 | `Hash_NoProviderConfigured_FallsToBuiltInDefault` | No settings → uses DefaultProvider (Keccak256) |
| 4 | `Verify_UsesProviderFromSettings` | Mock provider registered → verify action calls mock |

**Why separate file:** Provider resolution is integration-level. It needs engine context, settings, mock wiring. Different concern from pure algorithm correctness.

---

### Batch 3: Hash + Verify Action Handlers (~11 tests)

Action-level tests with `PLangContext`. Verifies handlers wire up correctly: serialization, `HashedData` return shape, error containment.

**File:** `PLang.Tests/Runtime2/Modules/crypto/HashActionTests.cs`

#### Hash action

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_StringInput_ReturnsHashedData` | String → `HashedData` with algorithm set, `Format="json"`, hex hash |
| 2 | `Hash_ObjectInput_SerializesToJsonBeforeHashing` | Object is JSON-serialized, hash is deterministic for same object |
| 3 | `Hash_ByteArrayInput_FormatIsRaw` | `byte[]` input → `Format="raw"`, no JSON serialization |
| 4 | `Hash_NullInput_ReturnsError` | `null` data → `Data.Success == false`, does not throw |
| 5 | `Hash_UnsupportedAlgorithm_ReturnsError` | Unknown algorithm → `Data.Success == false` with meaningful error key |
| 6 | `Hash_ProviderThrows_ReturnsDataFail` | Provider that throws `Exception` → handler catches, returns `Data.Fail()` |

#### Verify action

| # | Test | What it verifies |
|---|------|-----------------|
| 7 | `Verify_RoundTrip_ReturnsTrue` | Hash via action, verify via action → `Data.Ok(true)` |
| 8 | `Verify_WrongHash_ReturnsFalse` | Correct data but wrong hash → `Data.Ok(false)` |
| 9 | `Verify_CorruptedHashString_ReturnsError` | Non-hex garbage as hash → `Data.Success == false` (not a crash) |
| 10 | `Verify_NullInput_ReturnsError` | Null data → `Data.Success == false`, does not throw |
| 11 | `Verify_ProviderThrows_ReturnsDataFail` | Provider that throws → handler catches, returns `Data.Fail()` |

**Error-path rationale (from patterns.md):** Every handler returning `Data` must never throw. Catch exceptions and return `Data.FromError()`. Tests 4, 6, 9, 10, 11 enforce this contract.

---

## PLang Tests

### Batch 4: PLang Pipeline (~6 tests)

End-to-end: write `.test.goal` → `plang p build` → `plang p !test`. Each file tests one behavior.

**Location:** `Tests/Runtime2/Crypto/`

| # | Directory | File | What it tests |
|---|-----------|------|--------------|
| 1 | `Crypto/HashDefault/` | `HashDefault.test.goal` | `hash %data%, write to %hash%` — default algorithm (keccak256), verify `%hash%` is non-empty string |
| 2 | `Crypto/HashSHA256/` | `HashSHA256.test.goal` | `hash %data% using sha256, write to %hash%` — explicit algorithm selection works |
| 3 | `Crypto/HashBcryptVerify/` | `HashBcryptVerify.test.goal` | Hash password with bcrypt, then verify — round-trip produces true |
| 4 | `Crypto/VerifyWrongHash/` | `VerifyWrongHash.test.goal` | Verify with wrong expected hash → returns false |
| 5 | `Crypto/HashObject/` | `HashObject.test.goal` | Hash a JSON object, verify it produces consistent output on re-hash |
| 6 | `Crypto/ProviderSwap/` | `ProviderSwap.test.goal` | Set crypto provider via settings, hash data — verifies the settings action is recognized by builder |

**PLang test naming:** Grouped under `Crypto/`, each test in its own subdirectory, goal named `Start`, supporting goals in separate files if needed.

---

## Test Matrix Summary

### What IS tested

| Layer | Pattern | Test(s) |
|-------|---------|---------|
| Provider | Keccak256 correctness + size | `Hash_Keccak256_ProducesCorrectHash`, `_OutputIs32Bytes` |
| Provider | SHA256 correctness + size | `Hash_SHA256_ProducesCorrectHash`, `_OutputIs32Bytes` |
| Provider | Bcrypt validity + salt uniqueness | `Hash_Bcrypt_ProducesValidHash`, `_SameInput_DifferentHashes` |
| Provider | Unknown algorithm error | `Hash_UnknownAlgorithm_Throws`, `Verify_UnknownAlgorithm_Throws` |
| Provider | Empty input edge case | `Hash_EmptyInput_DoesNotThrow` |
| Provider | Keccak256 verify round-trip + failure | `Verify_Keccak256_RoundTrip_ReturnsTrue`, `_WrongData_ReturnsFalse` |
| Provider | SHA256 verify round-trip + failure | `Verify_SHA256_RoundTrip_ReturnsTrue`, `_WrongHash_ReturnsFalse` |
| Provider | Bcrypt verify correct + wrong | `Verify_Bcrypt_CorrectPassword_ReturnsTrue`, `_WrongPassword_ReturnsFalse` |
| Resolution | Mock provider overrides default | `Hash_UsesProviderFromSettings_NotDefault` |
| Resolution | Per-call override | `Hash_PerCallAlgorithm_OverridesDefault` |
| Resolution | Fallback to built-in | `Hash_NoProviderConfigured_FallsToBuiltInDefault` |
| Resolution | Verify uses provider from settings | `Verify_UsesProviderFromSettings` |
| Action | String → HashedData (format=json) | `Hash_StringInput_ReturnsHashedData` |
| Action | Object → JSON serialized hash | `Hash_ObjectInput_SerializesToJsonBeforeHashing` |
| Action | byte[] → format=raw | `Hash_ByteArrayInput_FormatIsRaw` |
| Action | Null input → error | `Hash_NullInput_ReturnsError`, `Verify_NullInput_ReturnsError` |
| Action | Unsupported algorithm → error | `Hash_UnsupportedAlgorithm_ReturnsError` |
| Action | Provider throws → Data.Fail | `Hash_ProviderThrows_ReturnsDataFail`, `Verify_ProviderThrows_ReturnsDataFail` |
| Action | Verify round-trip | `Verify_RoundTrip_ReturnsTrue` |
| Action | Verify wrong hash | `Verify_WrongHash_ReturnsFalse` |
| Action | Corrupted hash string | `Verify_CorruptedHashString_ReturnsError` |
| PLang | Default hash | `HashDefault.test.goal` |
| PLang | Explicit SHA256 | `HashSHA256.test.goal` |
| PLang | Bcrypt round-trip | `HashBcryptVerify.test.goal` |
| PLang | Wrong hash → false | `VerifyWrongHash.test.goal` |
| PLang | Object hash consistency | `HashObject.test.goal` |
| PLang | Provider swap via settings | `ProviderSwap.test.goal` |

### What is NOT tested (deliberate exclusions)

| Pattern | Why excluded |
|---------|-------------|
| Concurrent/parallel hashing | Provider should be stateless; threading is a framework concern |
| Very large input (>1MB) | Performance testing, not functional — out of scope for builder-v2 |
| HMAC, encryption, JWT | Architect explicitly scoped these out of piece 2 |
| Multiple algorithms in one goal | PLang can't combine modules in one step (v0.1 limitation) |

---

## File Summary

| File | Tests | Purpose |
|------|-------|---------|
| `PLang.Tests/Runtime2/Modules/crypto/DefaultProviderTests.cs` | 15 | Pure algorithm correctness: hash + verify for all 3 algorithms |
| `PLang.Tests/Runtime2/Modules/crypto/ProviderResolutionTests.cs` | 4 | Provider swap, resolution chain, fallback |
| `PLang.Tests/Runtime2/Modules/crypto/HashActionTests.cs` | 11 | Action handler wiring, HashedData shape, error containment |
| `Tests/Runtime2/Crypto/HashDefault/HashDefault.test.goal` | 1 | PLang: default hash |
| `Tests/Runtime2/Crypto/HashSHA256/HashSHA256.test.goal` | 1 | PLang: explicit algorithm |
| `Tests/Runtime2/Crypto/HashBcryptVerify/HashBcryptVerify.test.goal` | 1 | PLang: bcrypt round-trip |
| `Tests/Runtime2/Crypto/VerifyWrongHash/VerifyWrongHash.test.goal` | 1 | PLang: negative verification |
| `Tests/Runtime2/Crypto/HashObject/HashObject.test.goal` | 1 | PLang: object hash consistency |
| `Tests/Runtime2/Crypto/ProviderSwap/ProviderSwap.test.goal` | 1 | PLang: settings-based provider swap |

**Total: 30 C# tests + 6 PLang tests = 36 tests**

## Definition of Done

- All 30 C# tests pass (`dotnet test --filter crypto`)
- All 6 PLang tests pass (`plang p build && plang p !test`)
- No test depends on another test's state (isolated)
- Every action handler error path returns `Data.Fail()`, never throws
- Provider swap proven with mock (not just default provider)
- `HashedData` type shape verified (Algorithm, Format, Hash fields)

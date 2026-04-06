# Test Plan — Crypto Module (Piece 2)

## Overview

Tests for the crypto module: `hash` action, `verify` action, `DefaultProvider` (Keccak256, SHA256, Bcrypt), provider swap via settings, and full PLang pipeline integration.

Three C# batches (~18 tests), one PLang batch (~5 tests). Total: ~23 tests.

Source plan: `.bot/runtime2-builder-v2/architect/v2/plan.md` — Piece 2 section.

---

## C# Tests

### Batch 1: DefaultProvider — Hash (~7 tests)

Direct provider tests. No action handler, no context — just bytes in, bytes/bool out.

**File:** `PLang.Tests/App/Modules/crypto/DefaultProviderTests.cs`

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_Keccak256_ProducesCorrectHash` | Known input → known Keccak256 hex output (deterministic) |
| 2 | `Hash_SHA256_ProducesCorrectHash` | Known input → known SHA256 hex output (deterministic) |
| 3 | `Hash_Bcrypt_ProducesValidHash` | Output is valid bcrypt string (starts with `$2`) |
| 4 | `Hash_Bcrypt_SameInput_DifferentHashes` | Salt makes each call produce different output |
| 5 | `Hash_UnknownAlgorithm_Throws` | `"md5"` → `NotSupportedException` |
| 6 | `Hash_EmptyInput_DoesNotThrow` | Empty byte array hashes without error |
| 7 | `Hash_Keccak256_OutputIs32Bytes` | Keccak256 always produces 32 bytes |

### Batch 2: DefaultProvider — Verify (~6 tests)

Verify round-trips and failure cases.

**File:** `PLang.Tests/App/Modules/crypto/DefaultProviderVerifyTests.cs`

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Verify_Keccak256_MatchingHash_ReturnsTrue` | Hash then verify same data → true |
| 2 | `Verify_Keccak256_NonMatchingHash_ReturnsFalse` | Verify with wrong data → false |
| 3 | `Verify_SHA256_MatchingHash_ReturnsTrue` | SHA256 round-trip |
| 4 | `Verify_SHA256_WrongHash_ReturnsFalse` | SHA256 with tampered hash → false |
| 5 | `Verify_Bcrypt_MatchingPassword_ReturnsTrue` | Bcrypt verify with correct password |
| 6 | `Verify_Bcrypt_WrongPassword_ReturnsFalse` | Bcrypt verify with wrong password |

### Batch 3: Hash and Verify Action Handlers (~5 tests)

Action-level tests with context. Verifies the handlers wire up correctly: serialization to bytes, `HashedData` return type, error propagation.

**File:** `PLang.Tests/App/Modules/crypto/HashActionTests.cs`

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_StringInput_ReturnsHashedData` | String → `HashedData` with algorithm, format="json", hex hash |
| 2 | `Hash_ObjectInput_SerializesToJson` | Object is JSON-serialized before hashing (deterministic) |
| 3 | `Hash_ByteArrayInput_FormatIsRaw` | `byte[]` input → format="raw" |
| 4 | `Hash_UnsupportedAlgorithm_ReturnsError` | Unknown algorithm → `Data.Success == false` with key |
| 5 | `Verify_RoundTrip_ReturnsTrue` | Hash via action, verify via action → `Data.Ok(true)` |

---

## PLang Tests

### Batch 4: PLang Pipeline (~5 tests)

End-to-end: write `.test.goal` → `plang p build` → `plang p !test`. Each file tests one behavior.

**Location:** `tests/runtime2/crypto/`

| # | File | What it tests |
|---|------|--------------|
| 1 | `hash_default.test.goal` | `hash %data%, write to %hash%` — hash string with default (keccak256), verify `%hash%` is non-empty |
| 2 | `hash_sha256.test.goal` | `hash %data% using sha256, write to %hash%` — explicit algorithm selection |
| 3 | `hash_bcrypt_verify.test.goal` | Hash password with bcrypt, then verify — round-trip |
| 4 | `verify_wrong_hash.test.goal` | Verify with wrong expected hash → returns false |
| 5 | `hash_object.test.goal` | Hash a JSON object, verify it produces consistent output |

---

## Definition of Done

- All 18 C# tests pass (`dotnet test --filter crypto`)
- All 5 PLang tests pass (`plang p build && plang p !test`)
- No test depends on another test's state (isolated)
- One concern per test file
- `HashedData` type verified as return shape in action tests

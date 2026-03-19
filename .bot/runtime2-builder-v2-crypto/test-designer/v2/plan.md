# Test Plan — Crypto Module (Piece 2) — v2

## Overview

30 C# test stubs + 6 PLang test goals for the crypto module. Test stubs are already written — the coder implements the crypto module to make them pass.

Source: `.bot/runtime2-builder-v2/architect/v2/plan.md` — Piece 2 section.

---

## Instructions for Coder

### What exists

All test stubs and PLang test goals are already committed:

| File | Tests | Status |
|------|-------|--------|
| `PLang.Tests/Runtime2/Modules/crypto/DefaultProviderTests.cs` | 15 | **Has real assertions** — will compile once `DefaultProvider` exists |
| `PLang.Tests/Runtime2/Modules/crypto/ProviderResolutionTests.cs` | 3 | Stubs (`Assert.Fail`) — fill in after handlers + settings wiring exist |
| `PLang.Tests/Runtime2/Modules/crypto/HashActionTests.cs` | 12 | Stubs (`Assert.Fail`) — fill in after handlers exist |
| `Tests/Runtime2/Crypto/` (6 subdirs) | 6 | PLang goals — ready to build+test once module is registered |

### Patterns to follow (from identity module)

The identity module (piece 1) is fully implemented and merged. Follow its patterns exactly:

**Handler structure** — see `PLang/Runtime2/modules/identity/create.cs`:
```csharp
[Action("hash", Cacheable = false)]
public partial class Hash : IContext
{
    public partial object Data { get; init; }

    [Default("keccak256")]
    public partial string Algorithm { get; init; }

    public async Task<Data> Run()
    {
        // Validate → return Data.FromError(new ActionError(...)) on failure
        // Do work → catch exceptions, never throw
        // Return Data.Ok(result)
    }
}
```

**Error handling** — handlers MUST catch exceptions and return `Data.FromError()`:
```csharp
// Validation errors (expected)
if (data == null)
    return Data.FromError(new ActionError("Data cannot be null", "ValidationError", 400));

// Provider errors (catch and wrap)
try { provider.Hash(bytes, algorithm); }
catch (NotSupportedException ex)
{
    return Data.FromError(Error.FromException(ex, Context, "UnsupportedAlgorithm"));
}
```

**Test setup** — see `PLang.Tests/Runtime2/Modules/identity/IdentityHandlerTests.cs`:
```csharp
using PLangEngine = PLang.Runtime2.Engine.@this;

[Before(Test)] public void Setup()
{
    _tempDir = Path.Combine(Path.GetTempPath(), "plang_test_crypto_" + Guid.NewGuid().ToString("N")[..8]);
    Directory.CreateDirectory(_tempDir);
    _engine = new PLangEngine(_tempDir);
}

[After(Test)] public void Cleanup() { /* dispose engine, delete tempdir, best-effort */ }

private PLangContext Ctx => _engine.System.Context;
```

**Handler instantiation in tests:**
```csharp
var handler = new Hash { Context = Ctx, Data = "hello", Algorithm = "keccak256" };
var result = await handler.Run();
```

### Module structure to create

Per architect plan:
```
PLang/Runtime2/modules/crypto/
├── hash.cs                        — [Action("hash")] handler
├── verify.cs                      — [Action("verify")] handler
├── types.cs                       — HashedData class
├── providers/
│   ├── ICryptoProvider.cs         — interface: Hash(byte[], string) → byte[], Verify(byte[], byte[], string) → bool
│   └── DefaultProvider.cs         — Keccak256 (Nethereum), SHA256 (System.Security), Bcrypt (BCrypt.Net)
```

### Provider resolution

The architect specifies: `actor-scoped setting → engine default → built-in default`.

**Note:** The crypto hash action has NO per-call `Provider` parameter (unlike signing's `sign` action). The resolution chain's "per-call param" level does not apply to crypto. Algorithm selection is a normal action parameter, not provider dispatch.

Provider swap via settings: `crypto.provider = "provider-name"`. See how the condition module uses `IEvaluator` in `PLang/Runtime2/modules/condition/providers/` for the provider pattern — but crypto needs settings-based resolution on top of that.

### HashedData type

```csharp
public class HashedData
{
    public string Algorithm { get; set; }  // "keccak256", "sha256", "bcrypt"
    public string Format { get; set; }     // "json" (serialized before hashing) or "raw" (byte[] input)
    public string Hash { get; set; }       // hex string of the hash
    public override string ToString() => Hash;
}
```

Shared with signing module (piece 3) — keep it in `crypto/types.cs`.

---

## Test Details

### Batch 1: DefaultProvider — Hash + Verify (15 tests)

Direct provider tests. No engine, no context — just `new DefaultProvider()`, bytes in, bytes/bool out.

**File:** `PLang.Tests/Runtime2/Modules/crypto/DefaultProviderTests.cs`

**These tests have REAL assertions** — they will compile and run the moment `DefaultProvider` exists. Known reference hashes are embedded.

#### Hash

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_Keccak256_ProducesCorrectHash` | Known input → known Keccak256 hex output (deterministic) |
| 2 | `Hash_SHA256_ProducesCorrectHash` | Known input → known SHA256 hex output (deterministic) |
| 3 | `Hash_Bcrypt_ProducesValidHash` | Output is valid bcrypt string (starts with `$2`) |
| 4 | `Hash_Bcrypt_SameInput_DifferentHashes` | Salt makes each call produce different output |
| 5 | `Hash_UnknownAlgorithm_Throws` | `"md5"` → `NotSupportedException` |
| 6 | `Hash_EmptyInput_DoesNotThrow` | Empty byte array hashes without error |
| 7 | `Hash_Keccak256_OutputIs32Bytes` | Keccak256 always produces 32 bytes |
| 8 | `Hash_SHA256_OutputIs32Bytes` | SHA256 always produces 32 bytes |

#### Verify

| # | Test | What it verifies |
|---|------|-----------------|
| 9 | `Verify_Keccak256_RoundTrip_ReturnsTrue` | Hash then verify same data → true |
| 10 | `Verify_Keccak256_WrongData_ReturnsFalse` | Verify with different data → false |
| 11 | `Verify_SHA256_RoundTrip_ReturnsTrue` | SHA256 round-trip |
| 12 | `Verify_SHA256_WrongHash_ReturnsFalse` | SHA256 with tampered hash → false |
| 13 | `Verify_Bcrypt_CorrectPassword_ReturnsTrue` | Bcrypt verify with correct password |
| 14 | `Verify_Bcrypt_WrongPassword_ReturnsFalse` | Bcrypt verify with wrong password |
| 15 | `Verify_UnknownAlgorithm_Throws` | `"md5"` on verify → `NotSupportedException` |

---

### Batch 2: Provider Resolution + Swap (3 tests)

Integration tests proving the settings-based provider swap works.

**File:** `PLang.Tests/Runtime2/Modules/crypto/ProviderResolutionTests.cs`

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_UsesProviderFromSettings_NotDefault` | Register mock `ICryptoProvider` via settings → hash action calls mock, not DefaultProvider |
| 2 | `Hash_NoProviderConfigured_FallsToBuiltInDefault` | No settings → uses DefaultProvider (Keccak256) |
| 3 | `Verify_UsesProviderFromSettings` | Mock provider registered → verify action calls mock |

**Implementation note:** These tests need a mock `ICryptoProvider` with a known marker output (e.g., all-zero bytes). Register it via the settings module, then verify the hash action uses it. This is NEW — identity module has no provider pattern. The condition module's `IEvaluator` is the closest reference but isn't settings-swappable.

---

### Batch 3: Hash + Verify Action Handlers (12 tests)

Action-level tests with `PLangContext`. Each stub has arrange/act/assert comments — replace `Assert.Fail` with real assertions.

**File:** `PLang.Tests/Runtime2/Modules/crypto/HashActionTests.cs`

#### Hash action

| # | Test | What it verifies |
|---|------|-----------------|
| 1 | `Hash_StringInput_ReturnsHashedData` | String → `HashedData` with `Algorithm="keccak256"`, `Format="json"`, hex hash |
| 2 | `Hash_ObjectInput_SerializesToJsonBeforeHashing` | Object is JSON-serialized, hash is deterministic for same object |
| 3 | `Hash_ByteArrayInput_FormatIsRaw` | `byte[]` input → `Format="raw"`, no JSON serialization |
| 4 | `Hash_ExplicitAlgorithm_OverridesDefault` | `Algorithm="sha256"` produces SHA256 output, not default keccak256 |
| 5 | `Hash_NullInput_ReturnsError` | `null` → `Data.FromError(new ActionError(..., "ValidationError", 400))` |
| 6 | `Hash_UnsupportedAlgorithm_ReturnsError` | `"md5"` → handler catches `NotSupportedException`, returns `Data.FromError` |
| 7 | `Hash_ProviderThrows_ReturnsDataFail` | Mock provider throws → handler catches, returns `Data.FromError` |

#### Verify action

| # | Test | What it verifies |
|---|------|-----------------|
| 8 | `Verify_RoundTrip_ReturnsTrue` | Hash then verify via action handlers → `Data.Ok(true)` |
| 9 | `Verify_WrongHash_ReturnsFalse` | Wrong hash → `Data.Ok(false)` (not an error) |
| 10 | `Verify_CorruptedHashString_ReturnsError` | Non-hex garbage → `Data.FromError` (not a crash) |
| 11 | `Verify_NullInput_ReturnsError` | `null` → `Data.FromError(new ActionError(..., "ValidationError", 400))` |
| 12 | `Verify_ProviderThrows_ReturnsDataFail` | Mock provider throws → handler catches, returns `Data.FromError` |

**Critical rule:** Handlers must NEVER throw. Every exception → `Data.FromError()`. See identity's `create.cs` for the pattern.

---

### Batch 4: PLang Pipeline (6 tests)

**Location:** `Tests/Runtime2/Crypto/`

| # | Directory | File | What it tests |
|---|-----------|------|--------------|
| 1 | `HashDefault/` | `HashDefault.test.goal` | Default hash (keccak256), verify non-empty |
| 2 | `HashSHA256/` | `HashSHA256.test.goal` | Explicit SHA256 algorithm |
| 3 | `HashBcryptVerify/` | `HashBcryptVerify.test.goal` | Bcrypt hash + verify round-trip |
| 4 | `VerifyWrongHash/` | `VerifyWrongHash.test.goal` | Wrong hash → false |
| 5 | `HashObject/` | `HashObject.test.goal` | Object hash consistency (hash twice, compare) |
| 6 | `ProviderSwap/` | `ProviderSwap.test.goal` | Set crypto provider via settings, then hash |

PLang goals are written. They will work once the module is registered and the builder can parse `hash` / `verify hash` steps.

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
| Resolution | Fallback to built-in | `Hash_NoProviderConfigured_FallsToBuiltInDefault` |
| Resolution | Verify uses provider from settings | `Verify_UsesProviderFromSettings` |
| Action | String → HashedData (format=json) | `Hash_StringInput_ReturnsHashedData` |
| Action | Object → JSON serialized hash | `Hash_ObjectInput_SerializesToJsonBeforeHashing` |
| Action | byte[] → format=raw | `Hash_ByteArrayInput_FormatIsRaw` |
| Action | Explicit algorithm override | `Hash_ExplicitAlgorithm_OverridesDefault` |
| Action | Null input → error | `Hash_NullInput_ReturnsError`, `Verify_NullInput_ReturnsError` |
| Action | Unsupported algorithm → error | `Hash_UnsupportedAlgorithm_ReturnsError` |
| Action | Provider throws → Data.FromError | `Hash_ProviderThrows_ReturnsDataFail`, `Verify_ProviderThrows_ReturnsDataFail` |
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

## Definition of Done

- All 30 C# tests pass (`dotnet test --filter crypto`)
- All 6 PLang tests pass (`plang p build && plang p !test`)
- No test depends on another test's state (isolated)
- Every action handler error path returns `Data.FromError()`, never throws
- Provider swap proven with mock ICryptoProvider (not just default provider)
- `HashedData` type shape verified (Algorithm, Format, Hash fields)
- Follow identity module patterns: `[Action]` partial class, `IContext`, `ActionError` for validation, `Error.FromException` for caught exceptions

**Total: 30 C# tests + 6 PLang tests = 36 tests**

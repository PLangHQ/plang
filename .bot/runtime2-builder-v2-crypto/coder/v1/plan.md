# Crypto Module Implementation — v1 Plan

## Handoff Review

The test-designer delivered:
- **DefaultProviderTests.cs** — 15 real tests (good quality, known-input reference values)
- **HashActionTests.cs** — 12 stubs (awaiting module implementation)
- **ProviderResolutionTests.cs** — 3 stubs (awaiting module implementation)
- **6 PLang test goal files** — ready for build+test

**Problems found:**
1. **Identity module source deleted** — All files under `modules/identity/` were removed. This is working code from the parent branch. Will restore.
2. **Identity tests gutted** — Implementations replaced with `Assert.Fail("Not implemented")`. Will restore.
3. **SensitivePropertyFilter + [Sensitive] attribute removed** — Part of identity's security design. Will restore.
4. **Actor.cs Identity references removed** — Will restore.

## Implementation Plan

### Step 0: Restore identity module (revert destructive changes)

Git-restore all deleted/gutted files from parent branch:
- `PLang/Runtime2/modules/identity/*.cs` (all source files)
- `PLang/Runtime2/Engine/Channels/Serializers/SensitivePropertyFilter.cs`
- `PLang/Runtime2/Engine/View.cs` (restore `[Sensitive]` attribute)
- `PLang/Runtime2/Engine/Context/Actor.cs` (restore Identity references)
- `PLang/Runtime2/Engine/Memory/Data.Envelope.cs` (restore SensitivePropertyFilter)
- `PLang/Runtime2/Engine/Channels/Serializers/Serializer/JsonStreamSerializer.cs` (restore filter)
- All identity test files (restore implementations)
- Serialization test files (restore implementations)

### Step 1: Create provider interface

**File:** `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs`

```csharp
public interface ICryptoProvider
{
    byte[] Hash(byte[] data, string algorithm);
    bool Verify(byte[] data, byte[] expectedHash, string algorithm);
}
```

### Step 2: Create DefaultProvider

**File:** `PLang/Runtime2/modules/crypto/providers/DefaultProvider.cs`

Supports:
- **keccak256** — via `Nethereum.Util.Sha3Keccack` (already a dependency)
- **sha256** — via `System.Security.Cryptography.SHA256`
- **bcrypt** — via `BCrypt.Net-Next` (already a dependency)

Throws `NotSupportedException` for unknown algorithms.

### Step 3: Create HashedData type

**File:** `PLang/Runtime2/modules/crypto/types.cs`

```csharp
public class HashedData
{
    public string Algorithm { get; set; }
    public string Format { get; set; }     // "json" or "raw"
    public string Hash { get; set; }       // hex string
    public override string ToString() => Hash;
}
```

### Step 4: Create hash action handler

**File:** `PLang/Runtime2/modules/crypto/hash.cs`

- `[Action("hash")]`
- Properties: `Data : object`, `Algorithm : string` (default "keccak256")
- Serializes non-byte data to JSON bytes (format="json"), byte[] stays raw (format="raw")
- Resolves provider via settings chain (engine default → built-in DefaultProvider)
- Returns `Data.Ok(HashedData)`, never throws

### Step 5: Create verify action handler

**File:** `PLang/Runtime2/modules/crypto/verify.cs`

- `[Action("verify")]`
- Properties: `Data : object`, `Hash : string`, `Algorithm : string` (default "keccak256")
- Decodes hex hash, calls provider.Verify
- Returns `Data.Ok(bool)`, never throws

### Step 6: Fill in HashActionTests and ProviderResolutionTests stubs

Replace `Assert.Fail("stub")` with real test implementations that exercise the handlers.

### Step 7: Verify all tests pass

- `dotnet test --filter crypto` for C# tests
- `dotnet test` for identity tests (ensure restoration worked)
- PLang tests deferred until module is registered with the builder

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/crypto/providers/ICryptoProvider.cs` | Provider interface |
| `PLang/Runtime2/modules/crypto/providers/DefaultProvider.cs` | Keccak256 + SHA256 + Bcrypt |
| `PLang/Runtime2/modules/crypto/types.cs` | HashedData type |
| `PLang/Runtime2/modules/crypto/hash.cs` | Hash action handler |
| `PLang/Runtime2/modules/crypto/verify.cs` | Verify action handler |

## Files to Restore (from parent branch)

All identity module source, engine files with identity references, identity tests, serialization filter.

## Definition of Done

- DefaultProviderTests (15 tests) pass
- HashActionTests (12 tests) pass
- ProviderResolutionTests (3 tests) pass
- Identity tests still pass (restored)
- No regressions in existing tests

# v1 Summary: Fix 7 OBP Violations in Signing/Crypto/Identity/Provider Modules

## What this is
OBP compliance fixes across the signing, crypto, identity, and provider modules. These violations were identified during a code review session — objects were being decomposed, behavior was on wrong owners, cross-module static helpers were creating coupling, and concrete type checks were blocking extensibility.

## What was done

### Fix 1: IdentityData no longer hardcodes DefaultIdentityProvider (HIGH)
- Added `GetOrCreateDefaultAsync(IContext)` to `IIdentityProvider` interface
- `IdentityData.ResolveDefault()` now calls through the interface, not `is DefaultIdentityProvider`
- **Files:** `IIdentityProvider.cs`, `IdentityData.cs`, `DefaultIdentityProvider.cs`

### Fix 2: SignedData.Sign takes IdentityVariable, not decomposed fields (MEDIUM)
- Changed `Sign(ISigningProvider, string privateKey)` → `Sign(ISigningProvider, IdentityVariable identity)`
- Sign now sets `Identity = identity.PublicKey` internally — caller doesn't extract fields
- **Files:** `SignedData.cs`

### Fix 3: Extracted GenerateIdentity in DefaultIdentityProvider (MEDIUM)
- New private `GenerateIdentity(IContext, name, isDefault, providerName?)` method
- Replaces duplicated key-gen + IdentityVariable construction in `CreateAsync` and `GetOrCreateDefaultAsync`
- **Files:** `DefaultIdentityProvider.cs`

### Fix 4: JSON round-trip replaces manual Deserialize mapper (LOW)
- Replaced 15-line manual dictionary→IdentityVariable mapper with `JsonSerializer.Serialize` → `Deserialize<IdentityVariable>`
- Handles both `Dictionary<string, object?>` and `JsonElement` inputs
- **Files:** `DefaultIdentityProvider.cs`

### Fix 5: Moved SerializeData/FormatHash from Hash to HashedData (MEDIUM)
- `HashedData` now owns `SerializeData(object)` and `FormatHash(byte[])` as static methods
- `Hash.Run()` and `crypto.Verify.Run()` call `HashedData.SerializeData` instead of cross-module statics
- **Files:** `types.cs`, `hash.cs`, `verify.cs`, `SigningSerializationTests.cs`

### Fix 6: Non-generic Register on EngineProviders (MEDIUM)
- Added `Register(Type, IProvider)` to EngineProviders
- `load.cs` no longer uses `MakeGenericMethod` reflection
- **Files:** `Engine/Providers/this.cs`, `modules/provider/load.cs`

### Fix 7: Extracted ContractsMatch on SignedData (LOW)
- New `ContractsMatch(List<string>?)` method on SignedData
- `VerifyAsync` calls it instead of inline set comparison logic
- **Files:** `SignedData.cs`

## Code example
Before (decomposed identity):
```csharp
Identity = identity.Value!.PublicKey,  // extract field
signedData.Sign(provider, identity.Value.PrivateKey);  // extract another field
```

After (pass the object):
```csharp
var signResult = signedData.Sign(provider, identity.Value!);
// Sign internally: Identity = identity.PublicKey; provider.Sign(bytes, identity.PrivateKey);
```

## Test results
All 1795 tests pass, 0 failures.

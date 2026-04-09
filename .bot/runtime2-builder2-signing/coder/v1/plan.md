# Plan: Fix 7 OBP Violations in Signing/Crypto/Identity/Provider Modules

## Fix 1: IdentityData hardcodes DefaultIdentityProvider type check
**Files:** `IIdentityProvider.cs`, `DefaultIdentityProvider.cs`, `IdentityData.cs`
- Add `Task<Data<IdentityVariable>> GetOrCreateDefaultAsync(IContext action)` to `IIdentityProvider` interface
- `IdentityData.ResolveDefault()` calls through interface instead of type-checking `DefaultIdentityProvider`

## Fix 2: SignedData decomposes IdentityVariable into PublicKey/PrivateKey
**Files:** `SignedData.cs`
- Change `Sign(ISigningProvider provider, string privateKey)` → `Sign(ISigningProvider provider, IdentityVariable identity)`
- `Sign` navigates identity for `PublicKey` (→ `Identity` field) and `PrivateKey` (→ signing)
- `CreateAsync` passes `identity.Value!` instead of extracting fields

## Fix 3: Duplicated key-gen + identity-build in DefaultIdentityProvider
**Files:** `DefaultIdentityProvider.cs`
- Extract `GenerateIdentity(IContext action, string name, bool isDefault)` that owns: resolve key provider → generate keys → build IdentityVariable
- Both `CreateAsync` and `GetOrCreateDefaultAsync` call it

## Fix 4: Manual Deserialize mapper in DefaultIdentityProvider
**Files:** `DefaultIdentityProvider.cs`
- This one I'll check if engine serializers can handle it. If the DataSource returns `Dictionary<string, object?>` and we can't use `JsonSerializer.Deserialize`, we keep the manual mapper but add a note. Or we use `JsonSerializer.Serialize` → `Deserialize<IdentityVariable>` round-trip.

## Fix 5: Static helpers SerializeData/FormatHash on Hash action record
**Files:** `hash.cs`, `verify.cs`, `HashedData.cs`
- Move `SerializeData` and `FormatHash` to `HashedData` as static factory/instance methods
- `HashedData.FromData(object data, ...)` or `HashedData.Serialize(object data)` / `HashedData.FormatHash(byte[])`
- Update `crypto.Verify` to call `HashedData.SerializeData` instead of `crypto.Hash.SerializeData`

## Fix 6: Reflection in load.cs for Register
**Files:** `Engine/Providers/this.cs`, `modules/provider/load.cs`
- Add non-generic `Data Register(Type providerType, IProvider instance)` to EngineProviders
- `load.cs` calls it instead of reflection

## Fix 7: Inline contract matching in SignedData.VerifyAsync
**Files:** `SignedData.cs`
- Extract `ContractsMatch(List<string>? requiredContracts)` method on SignedData
- `VerifyAsync` calls `ContractsMatch(action.Contracts)` instead of inline logic

## Order of implementation
1. Fix 6 (non-generic Register) — foundation, no dependencies
2. Fix 1 (IIdentityProvider interface + IdentityData) — interface change
3. Fix 3 (GenerateIdentity extraction) — depends on Fix 1
4. Fix 2 (SignedData takes IdentityVariable) — depends on Fix 3
5. Fix 5 (Move static helpers to HashedData) — independent
6. Fix 7 (Contract matching method) — independent
7. Fix 4 (Deserialize) — evaluate feasibility last

## Test impact
- SignActionTests: `Sign` method signature changes, tests create identity manually
- NamedProviderRegistryTests: non-generic Register tested
- IdentityKeyProviderTests: may need updates if GenerateIdentity changes behavior
- HashActionTests: no change expected (SerializeData moves but tests call Run())

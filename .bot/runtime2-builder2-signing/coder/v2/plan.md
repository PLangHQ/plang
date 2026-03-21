# v2 Plan: Fix test coverage gaps from tester v1

## Scope
Address all 13 tester findings. Add missing error-path tests, strengthen weak assertions, add coverage for untested code.

## Changes

### 1. ProviderModuleTests.cs — findings #1, #2, #3
- Add `List_NoType_ReturnsAll` (calls `list.Run()` with null type)
- Add `List_ByType_ReturnsFiltered` (calls `list.Run()` with Type="signing")
- Add `List_UnknownType_ReturnsError` (calls `list.Run()` with Type="quantum")
- Add `Remove_UnknownType_ReturnsError` (Type="invalid")
- Add `SetDefault_UnknownType_ReturnsError` (Type="invalid")

### 2. Ed25519ProviderTests.cs — finding #4
- Add `Sign_InvalidBase64PrivateKey_ReturnsSigningError`
- Add `Verify_InvalidBase64PublicKey_ReturnsSignatureInvalid`

### 3. SignedData.Verify tests — finding #5
Add direct tests for `SignedData.Verify(ISigningProvider)`:
- `Verify_EmptySignature_ReturnsSignatureInvalid`
- `Verify_InvalidBase64Signature_ReturnsSignatureInvalid`

These can go in a new `SignedDataTests.cs` or in the existing verify tests. I'll add them to VerifyActionTests since they test SignedData directly.

### 4. NamedProviderRegistryTests.cs — findings #6, #11
- Add `ResolveType_Identity_ReturnsIIdentityProvider`
- Add `ResolveType_Crypto_ReturnsICryptoProvider`
- Add `ResolveType_Key_ReturnsIKeyProvider`
- Add `ResolveType_Unknown_ReturnsNull`
- Add `ResolveType_Null_DefaultsToSigning`
- Add `ResolveType_Empty_DefaultsToSigning`
- Add `Remove_NullName_ReturnsValidationError`
- Add `SetDefault_NullName_ReturnsValidationError`

### 5. Weak assertions — findings #7, #8, #9
- SignActionTests: strengthen `Sign_MissingIdentity_ReturnsError` and `Sign_ProviderThrows_ReturnsDataFromError`
- VerifyActionTests: strengthen `Verify_ProviderThrows_ReturnsDataFromError`

### 6. Settings.cs — finding #13
- Add simple test in a new or existing test file verifying default values

### 7. Finding #10 (load.cs 28%) — partially addressed
Load requires a real DLL; the existing test for assembly-not-found is reasonable. Skip.

### 8. Finding #12 (no PLang tests for provider) — skip
PLang tests require builder + LLM. Not in scope for this session.

## Files modified
- `PLang.Tests/Runtime2/Modules/provider/ProviderModuleTests.cs`
- `PLang.Tests/Runtime2/Modules/signing/Ed25519ProviderTests.cs`
- `PLang.Tests/Runtime2/Modules/signing/SignActionTests.cs`
- `PLang.Tests/Runtime2/Modules/signing/VerifyActionTests.cs`
- `PLang.Tests/Runtime2/Core/NamedProviderRegistryTests.cs`

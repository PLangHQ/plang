# v2 Summary: Fix test coverage gaps from tester v1

## What this is
Addresses 11 of 13 tester findings — missing error-path tests, weak assertions, and uncovered code paths in the signing/provider/identity modules.

## What was done
Added 28 new tests across 5 test files. All 1831 tests pass (0 failures, 8 skipped).

### Finding #1 — provider/list.cs 0% coverage
Added 3 tests calling `list.Run()` directly: no-type returns all, by-type filters, unknown type returns UnknownType error.

### Findings #2, #3 — remove/setDefault UnknownType untested
Added `Remove_UnknownType_ReturnsError` and `SetDefault_UnknownType_ReturnsError` with `Type="invalid"`.

### Finding #4 — Ed25519Provider catch blocks
Added `Sign_InvalidBase64PrivateKey_ReturnsSigningError` and `Verify_InvalidBase64PublicKey_ReturnsSignatureInvalid`.

### Finding #5 — SignedData.Verify guards
Added 3 direct `SignedData.Verify()` tests: null signature, empty string signature, invalid base64 signature — all assert `SignatureInvalid`.

### Finding #6 — ResolveType branches
Added 8 tests: signing, identity, crypto, key, unknown (null), null defaults to signing, empty defaults to signing, case-insensitive.

### Findings #7, #8, #9 — weak assertions
Strengthened 3 existing tests to assert `Error.Key` values: `KeyGenerationError`, `SigningError`, `SignatureInvalid`.

### Finding #11 — null-name guards
Added 4 tests: Remove/SetDefault with null and empty names return `ValidationError`.

### Finding #13 — Settings.cs
Added 2 tests verifying `SigningSettings` default values (`Provider="ed25519"`, `TimeoutMs=300000`).

### Bonus — GetOrDefault and Has
Added 4 tests for `GetOrDefault<T>` and `Has<T>` which were flagged as untested.

### Skipped
- **#10** `load.cs` — needs real DLL, impractical
- **#12** PLang tests for provider — needs builder + LLM

## Code example
```csharp
[Test]
public async Task ListAction_UnknownType_ReturnsError()
{
    var action = new PLang.Runtime2.modules.provider.list
    {
        Context = Ctx,
        Type = "quantum"
    };
    var result = await action.Run();

    await Assert.That(result.Success).IsFalse();
    await Assert.That(result.Error!.Key).IsEqualTo("UnknownType");
}
```

## Files modified
- `PLang.Tests/Runtime2/Modules/provider/ProviderModuleTests.cs` — 6 new tests
- `PLang.Tests/Runtime2/Modules/signing/Ed25519ProviderTests.cs` — 4 new tests
- `PLang.Tests/Runtime2/Modules/signing/SignActionTests.cs` — 2 strengthened assertions
- `PLang.Tests/Runtime2/Modules/signing/VerifyActionTests.cs` — 4 new tests + 1 strengthened
- `PLang.Tests/Runtime2/Core/NamedProviderRegistryTests.cs` — 16 new tests

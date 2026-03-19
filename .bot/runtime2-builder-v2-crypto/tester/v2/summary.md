# Tester v2 — Crypto Module Re-review

## What this is

Fresh-eyes re-review of crypto module tests after coder fixed v1 findings.

## Test Run Results

- **C# tests**: 1684 passed, 0 failed, 4 skipped (bcrypt deferred)
- **PLang tests**: Still not runnable (crypto not registered with builder)

## v1 Findings — All Resolved

1. **Engine.Providers** — ProviderRegistryTests.cs added: 9 tests covering all 5 public methods (Get, Register, Has, Remove, GetOrDefault). Tests use `IsSameReferenceAs` for identity verification. Register-overwrite semantics tested. Remove+Get round-trip tested. Clean.
2. **JSON serialization anchor** — Test now computes a reference hash by manually doing `JsonSerializer.Serialize("hello")` → UTF8 bytes → DefaultProvider.Hash → hex, then compares to handler output. If serialization method changes, the hashes diverge → test fails. Resolved.
3. **Algorithm override** — Test now hashes same input with keccak256 and sha256, asserts hashes differ + algorithm names are correct. If Algorithm were ignored, hashes would match → test fails. Resolved.

## Fresh-Eyes Findings

### Finding 1 (Major): Verify.Run() can throw unhandled ArgumentNullException

`Verify.Hash` is `string` (non-nullable), but if the source generator or a .pr file provides null, `Convert.FromHexString(null!)` throws `ArgumentNullException` — NOT `FormatException`. The catch block (verify.cs:27) only catches `FormatException`. This violates the "behavior methods never throw" contract.

No test covers this path. A test with `Hash = null!` would expose the exception.

**Code**: verify.cs:24 — `hashBytes = Convert.FromHexString(Hash);`
**Catch**: verify.cs:27 — `catch (FormatException)` — misses `ArgumentNullException`

### Finding 2 (Minor): No test for empty-string Hash

`Verify` with `Hash = ""` would call `Convert.FromHexString("")` → empty byte[] → provider.Verify compares 32-byte hash with 0-byte expected → returns false. This is technically correct behavior, but there's no test documenting this edge case. Minor because the behavior is correct.

### Finding 3 (Minor): ProviderRegistryTests only use one type key

All 9 tests register/get/remove `ICryptoProvider`. No test verifies type isolation — that registering `ICryptoProvider` doesn't interfere with a hypothetical `ITemplateProvider`. This is a `ConcurrentDictionary<Type, object>` — type isolation is guaranteed by the framework. But the test suite doesn't prove it. Minor because the risk is near zero.

### Finding 4 (Minor): Verify doesn't normalize Algorithm case

`Hash.Run()` stores `Algorithm.ToLowerInvariant()` in HashedData. `Verify.Run()` passes raw `Algorithm` to the provider. DefaultProvider handles this internally, but a custom provider might be case-sensitive. No test for mixed-case algorithm names (e.g., `Algorithm = "Keccak256"`). Minor because DefaultProvider normalizes.

## Verdict: NEEDS-FIXES

Finding 1 is a real bug — an unhandled exception path that violates the project's "never throw" contract. This is the exact kind of boundary-catch error the auditor has flagged as major on other modules. The fix is simple: widen the catch to include `ArgumentNullException`, or add a null check before `Convert.FromHexString`. But the test must exist to prove the fix works.

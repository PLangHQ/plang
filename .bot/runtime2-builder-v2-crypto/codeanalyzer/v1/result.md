# Code Analysis v1 — Crypto Module + Engine.Providers

## PLang/App/modules/crypto/providers/DefaultProvider.cs

### OBP Violations
None.

### Simplifications
1. **Lines 18-27: Redundant algorithm validation in Verify** — `Verify` manually checks for `"keccak256"` and `"sha256"` before calling `Hash()`, but `Hash()` already validates the algorithm and throws `NotSupportedException` for unsupported ones. The entire method body could be:
   ```csharp
   public bool Verify(byte[] data, byte[] expectedHash, string algorithm)
   {
       var actual = Hash(data, algorithm);
       return actual.AsSpan().SequenceEqual(expectedHash);
   }
   ```
   The `if` guard duplicates the algorithm allowlist, creating a maintenance burden — if a new hash algorithm is added to `Hash()` but not the `if` guard, `Verify()` throws even though `Hash()` supports it.

   *Counter-argument:* bcrypt needs different verify logic (not hash-then-compare). But bcrypt is deferred and [Skip]ped. YAGNI — simplify now, add the bcrypt branch when it lands.

2. **Line 20: `ToLowerInvariant()` called twice** — `algorithm.ToLowerInvariant()` is called twice in the `if` condition. Store in a local variable. Minor, but sloppy.

### Readability
None.

### Verdict: NEEDS WORK
Redundant algorithm validation will diverge from `Hash()` when algorithms are added.

---

## PLang/App/modules/crypto/hash.cs

### OBP Violations
None. Navigation through `context.Engine.Providers` is correct OBP.

### Simplifications
1. **Line 62: `new DefaultProvider()` allocated per call** — `ResolveProvider` creates a new `DefaultProvider()` as fallback every time. `DefaultProvider` is stateless. Use a static readonly field:
   ```csharp
   private static readonly DefaultProvider _defaultProvider = new();

   internal static ICryptoProvider ResolveProvider(PLangContext context)
   {
       return context.Engine.Providers.GetOrDefault<ICryptoProvider>(_defaultProvider);
   }
   ```

### Readability
1. **Line 12: Property `Data` shadows type `Data`** — The property `public partial object? Data` forces all `Data.Ok()` / `Data.FromError()` calls to be fully qualified as `Engine.Memory.Data.Ok(...)`. Every other handler in the codebase uses the short form `Data.Ok(...)`. The name `Data` is semantically correct for "the thing to hash," but the type shadowing is a readability tax across the entire file.

   Possible fix: rename to `Input` or `Content`. This changes the builder's parameter mapping, so it's a design decision, not a refactor.

### Verdict: NEEDS WORK
Static allocation fix is trivial. Data/Data shadowing is a design trade-off worth discussing.

---

## PLang/App/modules/crypto/verify.cs

### OBP Violations
None.

### Simplifications
None beyond what's covered by hash.cs (shared static methods).

### Readability
1. **Line 12: Same `Data` property shadowing** as hash.cs — `Engine.Memory.Data.FromError(...)` instead of `Data.FromError(...)`.

### Verdict: NEEDS WORK
Same Data shadowing issue as hash.cs.

---

## PLang/App/modules/crypto/types.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
None. Clean, minimal type.

### Verdict: CLEAN
Simple POCO with `ToString()` override for PLang string context.

---

## PLang/App/modules/crypto/providers/ICryptoProvider.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
None.

### Verdict: CLEAN
Clean two-method interface.

---

## PLang/App/Engine/Providers/this.cs

### OBP Violations
None. Lives on Engine, which owns it. OBP-compliant.

### Simplifications
None. ConcurrentDictionary with type-safe generic wrappers — minimal and correct.

### Readability
None. XML docs are appropriate for a public API surface.

### Deletion Test
1. **Lines 44-48: `Has<T>()` method** — No caller in the codebase. If deleted, no test fails. Acceptable for a registry API — users may need it. Low priority.
2. **Lines 53-57: `Remove<T>()` method** — No caller in the codebase. If deleted, no test fails. Same as above.

### Verdict: CLEAN
Well-designed registry. Unused methods are reasonable API surface.

---

## PLang/App/Engine/Context/Actor.cs (identity changes)

### OBP Violations
None. `Identity` property follows the same lazy pattern as `DataSource`.

### Simplifications
None.

### Readability
None.

### Behavioral Reasoning
1. **DynamicData `%MyIdentity%` points to `engine.System.Identity.Value`** — Correctly uses System actor for all contexts (User, Service, System). Matches the design in `good_to_know.md`. Re-evaluates on each access via DynamicData lambda.

### Verdict: CLEAN

---

## PLang/App/Engine/Channels/Serializers/SensitivePropertyFilter.cs

### OBP Violations
None.

### Simplifications
None. Minimal static filter.

### Readability
None.

### Verdict: CLEAN

---

## PLang/App/modules/identity/types.cs (IdentityVariable)

### OBP Violations
None. Persistence methods (`LoadAsync`, `SaveAsync`, `RemoveAsync`) belong to the owner — correct OBP.

### Simplifications
None.

### Readability
None.

### Behavioral Reasoning
1. **`[Sensitive]` on `PrivateKey`** — Correctly excluded from output serialization via `SensitivePropertyFilter`. Persisted in storage (DataSource uses raw `JsonSerializer`, not `JsonStreamSerializer`). Code-level access via `%MyIdentity.PrivateKey%` unaffected. Verified the filter is wired into both `JsonStreamSerializer` default options and `Data.Envelope._envelopeJsonOptions`.

### Verdict: CLEAN

---

## PLang/App/modules/identity/IdentityData.cs

### OBP Violations
None.

### Simplifications
None.

### Readability
None.

### Verdict: CLEAN

---

## Wiring verification

| File | Change | Status |
|------|--------|--------|
| `Engine/this.cs` | `Providers` property = `new()` | Correct |
| `GlobalUsings.cs` | `EngineProviders` alias | Correct |
| `View.cs` | `SensitiveAttribute` | Correct |
| `JsonStreamSerializer.cs` | `SensitivePropertyFilter.Filter` in default + view options | Correct |
| `Data.Envelope.cs` | `SensitivePropertyFilter.Filter` in envelope options | Correct |

All wiring is consistent.

---

## Test Coverage Assessment

### C# Tests — Good coverage
- **DefaultProviderTests**: Known-vector tests for keccak256 and SHA256, round-trip verify, wrong data, empty input, output size. 4 bcrypt tests [Skip]ped.
- **HashActionTests**: Handler-level tests for string/object/byte[] input, algorithms, null input, unsupported algorithm, provider throws, round-trip verify, wrong hash, corrupted hex, null verify.
- **ProviderResolutionTests**: Mock provider injection via `Engine.Providers.Register<T>()`, default fallback, verify with mock.

### Gap identified
1. **No test for `Hash.SerializeData` with dictionary input** — JSON property order is non-deterministic for `Dictionary<string, object>`. A test with identical-content dictionaries created in different order would reveal if hashing is order-dependent. This is a known JSON hashing limitation, not a bug, but worth a test to document the behavior.

### PLang .goal tests
6 test goals cover hash default, SHA256, object consistency, bcrypt, verify wrong hash, and provider swap. These test the full pipeline (builder → .pr → runtime) once the builder learns the crypto module.

---

## Summary of Findings

| # | File | Severity | Finding |
|---|------|----------|---------|
| 1 | DefaultProvider.cs:18-27 | Medium | Redundant algorithm validation in `Verify` — duplicates `Hash()` allowlist, will diverge when algorithms are added |
| 2 | hash.cs:62 | Low | `new DefaultProvider()` allocated per call — use `static readonly` |
| 3 | hash.cs:12, verify.cs:10 | Low | Property `Data` shadows type `Data`, forcing verbose qualified paths |
| 4 | DefaultProvider.cs:20 | Low | `ToLowerInvariant()` called twice on same string |

## Overall Verdict: NEEDS WORK

The architecture is clean and OBP-compliant. Engine.Providers is a well-designed, minimal registry. The crypto handlers are thin and correctly delegate to the provider. Finding #1 (redundant algorithm validation) is the only one that will cause real problems — it's a maintenance hazard that will silently break `Verify` when new algorithms are added to `Hash`. The rest are minor.

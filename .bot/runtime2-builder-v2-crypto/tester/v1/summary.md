# Tester v1 — Crypto Module Test Quality Summary

## What this is

Test quality analysis for the crypto module: hash/verify action handlers, DefaultProvider (keccak256/sha256), Engine.Providers registry, and PLang integration tests.

## Test Run Results

- **C# tests**: 1675 passed, 0 failed, 4 skipped (bcrypt deferred)
- **PLang tests**: Not runnable — no .pr files built yet (crypto module not registered with builder)

## Coverage

Coverage tool (Cobertura) does not instrument the crypto module source files — they use the source generator's partial class pattern and don't appear in coverage output. The tests DO exercise the code (verified by tracing test → source), but automated coverage numbers are unavailable for these specific files. This is a coverage tool limitation, not a test gap.

## Findings

### Finding 1 (Minor): Engine.Providers `Get<T>()`, `Has<T>()`, `Remove<T>()` have zero tests

`Engine.Providers` has 5 methods. Only `Register<T>()` and `GetOrDefault<T>()` are exercised (via ProviderResolutionTests). The remaining three — `Get<T>()`, `Has<T>()`, `Remove<T>()` — could be deleted without failing any test. These are simple dictionary wrappers, but they're public API that other modules will depend on.

**Impact**: A bug in `Remove<T>()` (e.g., wrong type key) would go undetected.

### Finding 2 (Minor): `Hash.SerializeData()` string path not isolated

`SerializeData()` has two branches: byte[] → raw, everything else → JSON. The byte[] path is tested in `Hash_ByteArrayInput_FormatIsRaw`. The JSON path is implicitly tested via every string hash test. However, no test verifies the actual JSON serialization output — e.g., that `SerializeData("hello")` produces `"\"hello\""` (JSON-serialized string with quotes). If `JsonSerializer.Serialize()` behavior changed or the method switched to `Encoding.UTF8.GetBytes(data.ToString())`, existing tests would still pass because they only check hash length/non-emptiness, not the hash value itself.

The `Hash_ObjectInput_SerializesToJsonBeforeHashing` test partially covers this (consistency check), but doesn't anchor to a known value.

**Impact**: Low — `System.Text.Json` is stable. But the test name claims "SerializesToJsonBeforeHashing" without actually proving JSON serialization happened.

### Finding 3 (Minor): `Hash_ExplicitAlgorithm_OverridesDefault` doesn't verify different hash

The test checks `algorithm == "sha256"` and `hash.Length == 64`, but doesn't verify the hash is different from what keccak256 would produce. Both algorithms produce 32-byte (64 hex char) output. If the algorithm parameter were ignored, this test would still pass.

**Impact**: Caught by `DefaultProviderTests` which verify known test vectors, so this is a defense-in-depth gap, not a true false green.

### Finding 4 (Minor): PLang tests cannot be validated

6 PLang .goal test files exist but have no .pr files — the crypto module isn't registered with the builder yet (piece 8 of the pipeline). These tests are untestable until the builder knows about the crypto module. This is a known deferral, not a bug.

### Finding 5 (Informational): `HashedData.ToString()` untested

`HashedData.ToString()` returns `Hash`. No test calls `ToString()`. Minor — it's a convenience method.

## Verdict: APPROVED

The test suite is honest. All critical paths are tested with strong assertions (error keys, status codes, known test vectors). The findings are all minor — no false greens, no missing coverage of critical behavior. The provider error propagation tests (review-driven code from codeanalyzer v1) are present and check error keys, not just `Success == false`.

## Recommendation

Run the **security** analyst next.

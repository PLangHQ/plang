# v1 Review — Crypto Test Plan

## What v1 got right
- Layered approach: provider → action handler → PLang pipeline
- Deterministic hash tests with known inputs
- Bcrypt salt uniqueness test
- Empty input edge case
- Keccak256 output size assertion

## What v1 got wrong

### Critical: Provider swap dropped from C# tests
The architect explicitly listed "provider: swapped provider is used instead of default" as a required C# test. v1 had zero provider swap tests. This is the core extensibility contract — if it's untested, the provider pattern is just decoration.

### Critical: Error paths are thin
Only 1 error test across all action handler tests (unsupported algorithm). Per established patterns (patterns.md): every handler returning `Data` must never throw. v1 had no tests for null input, corrupted hash, or provider exceptions.

### Moderate: Verify action handler coverage anemic
1 test (round-trip). No negative cases, no error paths. The verify action is a separate handler with its own failure modes — deserved equal coverage.

### Moderate: Provider resolution chain untested
Architect defines: `per-call param → actor-scoped setting → engine default → built-in default`. Zero tests for this priority ordering.

### Minor: Unnecessary file split
Batches 1+2 in separate files for the same class. Merged in v2.

### Minor: SHA256 output size not tested
Keccak256 had an output size test; SHA256 didn't. Both produce 32 bytes — parity matters.

### Minor: PLang provider swap test dropped
Architect wanted it. v1 didn't include it.

## Changes in v2
- Merged Batches 1+2 → single DefaultProviderTests.cs (15 tests)
- Added Batch 2: ProviderResolutionTests.cs (4 tests)
- Expanded Batch 3: HashActionTests.cs from 5 → 11 tests
- Added PLang provider swap test
- Total: 23 → 36 tests

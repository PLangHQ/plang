# Tester v6 Summary — Verify Auditor Fix Commit

## What this is

Verification of coder commit `02a56639` which addresses all 8 auditor findings. Also a deep analysis of whether the *fixes themselves* have adequate test coverage.

## Test suite results

- **1372 pass, 0 fail, 0 skipped** — no regressions.

## Auditor fix verification

All 8 findings addressed in code. Detailed verification:

| # | Auditor Finding | Code Fix | Verdict |
|---|----------------|----------|---------|
| 1 | Engine.Types thread safety | `_extensionToKind` + `_extensionToMime` → `ConcurrentDictionary`. `_allKinds` (HashSet) protected by `_derivedLock` in `KindOf()`, `Add()`, `Remove()`. `_mimeToKind` → `ConcurrentDictionary`. | Correct |
| 2 | RehydrateNestedData temporal coupling | Added `SetValueDirect(object?)` — updates `_value` + `Updated` + `IsInitialized` without clearing `_type`. Replaces save/restore dance. | Correct |
| 3 | ServiceError for Decompress | All 5 error paths now use `new ServiceError(msg, "DecompressError", 500)`. | Correct |
| 4 | Compress double-navigation | Changed `_context.Engine.Types.Compressible(Type.Value)` → `Type.Compressible`. | Correct |
| 5 | O(n) ContainsValue in Remove | Still O(n) via `_extensionToKind.Values.Contains(...)`. Accepted per auditor recommendation. | Accepted |
| 6 | Newtonsoft attribute | `[Newtonsoft.Json.JsonConstructor]` removed from Data constructor. | Correct |
| 7 | Error.Key assertions | All 4 Decompress error tests now assert `result.Error!.Key == "DecompressError"`. | Correct |
| 8 | Zip bomb protection | `MaxDecompressedSize = 100MB`. `GZipDecompress` reads in 81920-byte chunks, throws `InvalidDataException` when limit exceeded. Using statements guarantee stream disposal. | Correct |

## Findings: 1 critical, 1 major, 2 minor

### Finding #1 — CRITICAL: Zip bomb protection is untested

**File**: `PLang/Runtime2/Engine/Memory/Data.Envelope.cs:19,214-230`
**Test file**: `PLang.Tests/Runtime2/Memory/DataTests.cs`

The auditor's finding #8 was a **major safety concern** — zip bomb attacks at the transport boundary. The coder added a 100MB limit with chunked reads. But **no test exists** that verifies this limit works.

**Why this matters**: This is a textbook false green. The safety feature exists in code but has zero test coverage. If someone removes the size check (refactors `GZipDecompress` back to `gzip.CopyTo(output)`), no test fails. The entire protection silently disappears.

**Impact**: The zip bomb protection — the most security-critical fix in this commit — is the only code path with zero coverage.

**Suggestion**: Create a test that compresses a payload of zeros (high compression ratio) exceeding the limit. Since 100MB is heavy for a unit test, consider one of:
- (a) Make `MaxDecompressedSize` an `internal` field with `[InternalsVisibleTo]` so tests can temporarily lower it
- (b) Create a modest zip bomb (zeros compress ~1000:1, so ~100KB of compressed data → 100MB+ decompressed). The test can use a timeout to avoid hanging. The GZipDecompress throws before allocating the full 100MB since it checks per-chunk.
- (c) At minimum, verify the `InvalidDataException` message contains "exceeds size limit"

### Finding #2 — MAJOR: Thread safety fix has no concurrent test

**File**: `PLang/Runtime2/Engine/Types/this.cs:388,553,571,591`
**Test file**: `PLang.Tests/Runtime2/Types/EngineTypesTests.cs`

The auditor's finding #1 was about thread safety of Engine.Types under concurrent goal execution. The coder switched to `ConcurrentDictionary` and added `_derivedLock` around `_allKinds`. But all 65+ Engine.Types tests are single-threaded.

**Why this matters**: The fix is structurally correct (ConcurrentDictionary is thread-safe, lock around HashSet mutations is correct), but without a concurrent test:
- If someone reverts `ConcurrentDictionary` → `Dictionary`, no test catches it
- If someone removes the `lock (_derivedLock)` blocks, no test catches it
- The thread safety guarantee is based purely on code review, not runtime verification

**Impact**: Medium — the code IS correct today. The risk is regression without detection.

**Suggestion**: Add a stress test that runs `Add()` and `KindOf()` concurrently from multiple threads/tasks. Even a simple `Parallel.For` with 100 iterations calling `Add` on one thread and `KindOf` on another would catch Dictionary corruption (which throws `InvalidOperationException` on concurrent modification).

### Finding #3 — MINOR: ServiceError.StatusCode not verified in tests

**File**: `PLang.Tests/Runtime2/Memory/DataTests.cs:914,928,942,967`

All 4 Decompress error tests now check `Error.Key == "DecompressError"` (auditor finding #7). But none verify `Error.StatusCode == 500`. The coder explicitly passed `500` (infrastructure error, not user input error), but this value is never asserted.

**Impact**: Low. If someone changes `500` → `400` (the ServiceError default), no test catches it. The Key assertion is the primary value; StatusCode is secondary.

**Suggestion**: Add `await Assert.That(result.Error!.StatusCode).IsEqualTo(500);` to each Decompress error test.

### Finding #4 — MINOR: Context not propagated to inner Data during RehydrateNestedData

**File**: `PLang/Runtime2/Engine/Memory/Data.Envelope.cs:184-200`

After `Decompress()`, the top-level `result` gets `Context = _context` (line 150), but inner Data objects created by `RehydrateNestedData` do NOT get context stamped. If someone accesses `decompressed.Value` directly (casting to `Data`), that inner Data has no context — so `Type.Kind`, `Type.Compressible`, and other context-dependent navigation won't work.

No test verifies context propagation to inner Data after decompression. The multi-level nesting test (`CompressDecompress_MultiLevelNesting_PreservesAllLevels`) checks type and value but never checks context on inner layers.

**Impact**: Low — callers typically use `Unwrap()` which stamps context, or navigate through `GetChild()` which also stamps context. Direct casting to inner Data is an edge case.

**Suggestion**: Either (a) propagate context recursively in `RehydrateNestedData`, or (b) document that inner Data from decompression requires `Unwrap()` or `GetChild()` for context. At minimum, add a test assertion verifying context (or lack thereof) on inner Data after Decompress.

## Deep analysis: New findings beyond auditor fixes

### Finding #5 — MAJOR: RehydrateNestedData false positives on user dictionaries

**File**: `PLang/Runtime2/Engine/Memory/Data.Envelope.cs:186`

The heuristic `data.Value is Dictionary<string, object?> dict && dict.ContainsKey("value")` treats ANY dictionary with a `"value"` key as a serialized Data object and replaces it with a reconstructed `Data`. This means user data is silently mutated:

```csharp
// User stores a config dictionary
var config = new Dictionary<string, object?> {
    { "value", "production" },
    { "name", "environment" },
    { "timeout", 30 }
};
var data = new Data("config", config, Type.FromName("text"));
data.Context = context;

// Compress and decompress
var result = data.Wrap().Compress().Decompress().Unwrap();
// result.Value is now a Data object, NOT the original dictionary!
// The "timeout" key is silently lost.
```

**Why this matters**: The heuristic cannot distinguish between a serialized Data and a user dictionary that happens to have `"value"` and `"name"` keys. This is a data corruption bug at the transport boundary — exactly where you need maximum reliability.

**Impact**: Any user dictionary with a `"value"` key is silently corrupted on compress/decompress round-trip. The extra keys (anything beyond `name`, `value`, `type`) are lost.

**Suggestion**: Add a more specific heuristic — e.g. require a `"__data": true` marker during serialization, or check that the dictionary has ONLY the expected keys (`name`, `value`, `type`).

### Finding #6 — MAJOR: Data.Merge() used in production but completely untested

**File**: `PLang/Runtime2/Engine/Memory/Data.Result.cs:40-57`
**Used at**: `PLang/Runtime2/Engine/Goals/Goal/Steps/Step/Actions/this.cs:66`

`Merge()` is called in the action execution loop to combine step results. It casts `Value` to `List<Data>` and silently returns an empty list if the cast fails. Zero tests exist for `Data.Merge()` in DataTests.cs.

**Silent failure scenario**:
```csharp
var a = Data.Ok("hello");  // Value is string, not List<Data>
var b = Data.Ok("world");
var merged = a.Merge(b);
// merged.Value = empty List<Data>  — both values silently lost
```

**Impact**: Medium. The method works correctly in its one call site (Actions loop where Value IS List<Data>), but the contract is fragile — any caller with non-List values gets silent data loss.

**Suggestion**: Add tests for Merge with: (a) two List<Data> values, (b) non-List values (current silent failure), (c) overlapping names (replace behavior), (d) null other.

### Finding #7 — MINOR: Numeric type widening through compress/decompress

**File**: `PLang/Runtime2/Engine/Memory/Data.cs:215`

`UnwrapJsonElement` converts JSON numbers to `long` first, `double` as fallback. This means `int 42` → JSON → `long 42L` after decompression. The Type metadata is preserved (still says "int"), but the actual CLR value is `long`.

**Mitigated by**: `GetValue<int>()` calls `TypeMapping.ConvertTo` which uses `Convert.ChangeType(long, typeof(int))`, so callers using the typed accessor are safe. Direct `Value` access gets `long` instead of `int`.

**Impact**: Low. The typed accessor path handles this correctly. Only raw `Value` access + type check (`value is int`) would fail.

## Verdict: needs-fixes

The auditor fixes are structurally correct and all tests pass. But deeper analysis surfaced:

- **2 original findings** (zip bomb untested, thread safety untested) — the fixes themselves lack test coverage
- **1 new code bug** (RehydrateNestedData false positives on user dictionaries) — data corruption at the transport boundary
- **1 coverage gap** (Merge() untested) — production code with silent failure mode
- **3 minor gaps** (StatusCode, inner context, numeric widening)

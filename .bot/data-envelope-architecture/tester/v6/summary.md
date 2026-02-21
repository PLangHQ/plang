# Tester v6 Summary — Verify Auditor Fix Commit

## What this is

Verification of coder commit `02a56639` which addresses all 8 auditor findings. Also a deep analysis of whether the *fixes themselves* have adequate test coverage, plus broader test quality analysis.

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

## Findings: 1 critical, 2 major, 3 minor

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

### Finding #3 — MAJOR: Data.Merge() used in production but completely untested

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

### Finding #4 — MINOR: ServiceError.StatusCode not verified in tests

**File**: `PLang.Tests/Runtime2/Memory/DataTests.cs:914,928,942,967`

All 4 Decompress error tests now check `Error.Key == "DecompressError"` (auditor finding #7). But none verify `Error.StatusCode == 500`. The coder explicitly passed `500` (infrastructure error, not user input error), but this value is never asserted.

**Impact**: Low. If someone changes `500` → `400` (the ServiceError default), no test catches it. The Key assertion is the primary value; StatusCode is secondary.

**Suggestion**: Add `await Assert.That(result.Error!.StatusCode).IsEqualTo(500);` to each Decompress error test.

### Finding #5 — MINOR: Context not propagated to inner Data during RehydrateNestedData

**File**: `PLang/Runtime2/Engine/Memory/Data.Envelope.cs:184-200`

After `Decompress()`, the top-level `result` gets `Context = _context` (line 150), but inner Data objects created by `RehydrateNestedData` do NOT get context stamped. If someone accesses `decompressed.Value` directly (casting to `Data`), that inner Data has no context — so `Type.Kind`, `Type.Compressible`, and other context-dependent navigation won't work.

No test verifies context propagation to inner Data after decompression. The multi-level nesting test (`CompressDecompress_MultiLevelNesting_PreservesAllLevels`) checks type and value but never checks context on inner layers.

**Impact**: Low — callers typically use `Unwrap()` which stamps context, or navigate through `GetChild()` which also stamps context. Direct casting to inner Data is an edge case.

**Suggestion**: Either (a) propagate context recursively in `RehydrateNestedData`, or (b) document that inner Data from decompression requires `Unwrap()` or `GetChild()` for context. At minimum, add a test assertion verifying context (or lack thereof) on inner Data after Decompress.

### Finding #6 — MINOR: Numeric type widening through compress/decompress

**File**: `PLang/Runtime2/Engine/Memory/Data.cs:215`

`UnwrapJsonElement` converts JSON numbers to `long` first, `double` as fallback. This means `int 42` → JSON → `long 42L` after decompression. The Type metadata is preserved (still says "int"), but the actual CLR value is `long`.

**Mitigated by**: `GetValue<int>()` calls `TypeMapping.ConvertTo` which uses `Convert.ChangeType(long, typeof(int))`, so callers using the typed accessor are safe. Direct `Value` access gets `long` instead of `int`.

**Impact**: Low. The typed accessor path handles this correctly. Only raw `Value` access + type check (`value is int`) would fail.

## Withdrawn findings

### ~~RehydrateNestedData false positives on user dictionaries~~ — WITHDRAWN

Initially flagged as a data corruption bug: user dictionaries with a `"value"` key would be mistakenly rehydrated into Data objects.

**Why withdrawn**: The envelope pipeline always operates on Data layers. At the transport boundary, the serialized JSON structure IS nested Data objects — the `"value"` key heuristic correctly identifies them. A user dictionary at the leaf level is the **value** of the innermost Data, not a Data itself. The pipeline is: `Data(wrapper) → Data(wrapper) → ... → Data(leaf, user_object)`. When you call `data.Value` on the leaf, you get the user object. The rehydration only reconstructs the Data wrapper layers that JSON flattened into dictionaries. Return values in .pr files have named bindings, so the ephemeral `"data"` name is overwritten by the action's return mapping.

## Verdict: needs-fixes

The auditor fixes are structurally correct and all tests pass. Key gaps:

- **1 critical** — zip bomb protection has no test (security feature unprotected from regression)
- **2 major** — thread safety has no concurrent test; Data.Merge() is production code with zero tests
- **3 minor** — StatusCode, inner context propagation, numeric widening

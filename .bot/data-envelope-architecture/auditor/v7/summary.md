# Auditor v7 — data-envelope-architecture Review

## What this is

Re-review after coder v5 (security hardening) and v7 (test gaps). Covers depth limits on all 5 recursive methods, cycle detection in Variables, zip bomb test, Verified→private set, fromJson deduplication, and boundary tests.

## What was reviewed

- `Data.cs` — UnwrapJsonElement depth limit (128), internal static for deduplication
- `Data.Envelope.cs` — RehydrateNestedData depth limit (128), zip bomb limit (100MB), Verified→private set
- `Data.Navigation.cs` — GetChild depth limit (100), returns Data.FromError
- `Variables.cs` — ThreadStatic cycle detection in ResolveVariablesInPath
- `Types/this.cs` — Clr() depth limit (20), null propagation for generics
- `fromJson.cs` — Deduplicated, now calls Data.UnwrapJsonElement
- `JsonStringNavigator.cs` — 10MB size limit in CanNavigate
- `PLang.csproj` — InternalsVisibleTo for tests
- All new tests (zip bomb, depth limits, cycle detection, Merge, boundary)

## Fix verification

### Finding #9 (Zip bomb untested) — Properly fixed
`Decompress_ExceedsSizeLimit_ReturnsError` compresses 110MB of zeros, attempts decompress, asserts `Success==false`, `Error.Key=="DecompressError"`, `StatusCode==500`, message contains "size limit". The test is honest — removing the size check in GZipDecompress would cause it to succeed instead of error. The 110MB payload exercises the chunked read loop (81920 byte buffer × many iterations) before hitting the 100MB limit.

### Depth limits — all 5 correct
Each recursive method has a depth parameter with consistent pattern:

| Method | Max | Error handling | Test |
|--------|-----|---------------|------|
| UnwrapJsonElement | 128 | throws InvalidOperationException | 200-level nested JSON |
| RehydrateNestedData | 128 | throws InvalidDataException | (bounded by STJ MaxDepth=64) |
| GetChild | 100 | returns Data.FromError | 150-level nested path |
| ResolveVariablesInPath | cycle | leaves unresolved | reflection-based cycle test |
| Clr() | 20 | returns null | boundary tests at 20/21 |

Depth values are reasonable. Different error handling per context makes sense — UnwrapJsonElement is called from fromJson which catches InvalidOperationException; GetChild returns Data which callers expect; Clr returns null as a "couldn't resolve" signal.

### Cycle detection — correct
`[ThreadStatic] _resolvingVars` with `isRoot` tracking. Root call creates and nulls the set; inner calls only add/remove. The `finally { _resolvingVars.Remove(varName); }` in the regex callback ensures no leakage. Root-level `finally { if (isRoot) _resolvingVars = null; }` prevents cross-call contamination. Test uses reflection to pre-seed the set — correct approach for testing defensive code that's structurally unreachable through current API.

### fromJson deduplication — clean
Single `Data.UnwrapJsonElement` (internal static). fromJson.cs is now 30 lines — parse, unwrap, catch. The `InvalidOperationException` catch for "JsonDepthExceeded" is defensive dead code (STJ MaxDepth=64 < our 128), but harmless and future-proof if STJ ever raises its default.

### Verified → private set — correct
External code can no longer claim verification. `SetVerified(bool)` is internal, gated by `InternalsVisibleTo("PLang.Tests")`.

## New findings

### #11 (nit) — JsonStringNavigator.UnwrapElement duplicates Data.UnwrapJsonElement

`JsonStringNavigator` has its own `UnwrapElement`/`UnwrapObject`/`UnwrapArray` methods (lines 44-76) that duplicate `Data.UnwrapJsonElement`/`UnwrapJsonObject`/`UnwrapJsonArray`. Same logic, same switch on ValueKind, same recursive pattern. Only difference: the navigator version has no depth limit (bounded by STJ MaxDepth=64, so not a security risk) and minor differences in number handling (navigator uses `TryGetInt64 ? l : GetDouble()`, Data uses `UnwrapJsonNumber` which also tries `GetInt32`).

Not a security issue — STJ caps depth. But it's code duplication that means any fix to JSON unwrapping must be applied in two places.

**Impact:** Low. If someone fixes a bug in Data.UnwrapJsonElement, they might miss JsonStringNavigator.UnwrapElement.

**Suggestion:** Have JsonStringNavigator call `Data.UnwrapJsonElement` instead of its own implementation. The only barrier is that Data's version is `internal static` — JsonStringNavigator is in the same assembly, so it can already access it. The minor number-handling difference (int vs long for small integers) might need alignment.

## Findings status

| # | Severity | Status | Notes |
|---|----------|--------|-------|
| 1-8 | various | closed | Fixed in earlier commits |
| 9 | major | **closed** | Zip bomb test added in v5 |
| 10 | minor | open-accepted | Race window, benign |
| 11 | nit | new | JsonStringNavigator duplication |

## OBP Assessment

Clean. No new OBP concerns. The fromJson deduplication actually improves OBP — Data owns its own JSON unwrapping, fromJson delegates.

## Verdict

**Approved.** All major and blocking findings are resolved. The security hardening is comprehensive — every recursive method has depth/cycle protection, the zip bomb limit is tested, Verified is locked down. Test coverage is solid — 1394 tests, honest assertions, boundary tests on depth limits. One new nit (duplication). Ready for merge.

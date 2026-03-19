# Tester v2 — Full Re-analysis with Coverage

## What this is

Complete re-analysis of crypto module and all branch changes with proper code coverage. Previous coverage runs crashed due to passing a directory path instead of a file path to `--coverage-output`.

## Test Run Results

- **C# tests**: 1684 passed, 0 failed, 4 skipped (bcrypt deferred)
- **PLang tests**: Not runnable (crypto not registered with builder)

## Coverage Results

Coverage now works correctly. Key numbers:

| File | Coverage | Gap |
|------|----------|-----|
| `hash.cs` | 100% | — |
| `verify.cs` | 100% line | **Misleading** — null Hash throws `ArgumentNullException` not caught by `FormatException` catch. Line coverage can't see this. |
| `DefaultProvider.cs` | 100% | — |
| `Engine/Providers/this.cs` | 100% | — |
| `types.cs` (HashedData) | 75% | `ToString()` untested |
| `identity/export.cs` | 82.4% | `InvalidOperationException` catch never hit |
| `identity/get.cs` | 81.2% | `InvalidOperationException` catch never hit |
| `identity/IdentityData.cs` | 88.5% | `ResolveDefault()` catch never hit |
| `identity/types.cs` | 92% | Save-failure throws + Deserialize fallbacks never hit |

## Findings

### Finding 1 (Major): verify.cs — 100% line coverage hides unhandled exception

`Convert.FromHexString(Hash)` on line 24 throws `ArgumentNullException` when Hash is null, but the catch on line 26 only handles `FormatException`. Coverage shows 100% because the line is hit with valid and invalid hex — but the null path is a different exception type that escapes uncaught.

This is a **code bug** (violates never-throw contract), not just a test gap. And it's a textbook example of why line coverage alone is insufficient — you need branch/path analysis.

### Finding 2 (Minor): Identity save-failure chain entirely untested

`GetOrCreateDefaultAsync` has two `throw new InvalidOperationException(...)` paths (lines 84, 101) that propagate to catch blocks in `export.cs:32`, `get.cs:33`, and `IdentityData.cs:56`. The entire chain — throw → catch → `Data.FromError(ServiceError)` — has zero test coverage. If the error key were wrong or the catch were removed, nothing would catch it.

### Finding 3 (Minor): Identity defensive code gaps

`LoadAllAsync` error path (line 55) and `Deserialize` null fallback (line 143) never hit. These handle DataSource failures and data corruption — unlikely in normal operation.

### Finding 4 (Minor): PLang tests still deferred

6 .goal files can't be built until crypto is registered with the builder.

## v1 Findings — All Resolved

ProviderRegistryTests covers all 5 methods. Serialization test has known-value anchor. Algorithm override test verifies different hashes.

## Verdict: NEEDS-FIXES

Finding 1 is a code bug. The identity coverage gaps (findings 2-3) are identity module issues already reviewed on the parent branch, so they're informational here. The blocker is the verify.cs null Hash exception.

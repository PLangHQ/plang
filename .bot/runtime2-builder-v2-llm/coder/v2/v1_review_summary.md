# Code Analyzer Review of Coder v1

## Source
Code analyzer v1 (`codeanalyzer/v1/result.md`) — 5-pass analysis of all LLM module files.

## Verdict
6/7 files CLEAN. `OpenAiProvider.cs` NEEDS WORK.

## Findings

### Critical
1. **Bare catch at line 587** (`ResolveImage`) — catches everything including OOM/NullRef. Should use scoped catch or negative filter.
2. **Bare catch at line 765** (`ParseApiResponse`) — same issue. Should catch `JsonException` only.
3. **Sync-over-async at line 734** (`ResolveConfig`) — `.GetAwaiter().GetResult()` in async method risks deadlock and blocks thread pool.

### Moderate
4. **Untested default fill-in** (lines 465-472) — `ParseToolArguments` default parameter substitution has zero test coverage.
5. **Untested type mappings** (lines 693-704) — Only `string` mapping is exercised. `int`, `bool`, `list`, `object` mappings untested.

### Minor
6. **Duplicate httpAction construction** (lines 139-169) — Built twice when streaming; first is dead code.
7. **Dead `BuildStreamProxy` wrapper** (lines 867-873) — Pure passthrough, zero behavior.
8. **Decomposed parameters** in `ExecuteToolAsync` (line 366) and `ToApiMessages` (line 479) — OBP violation (low severity, private methods).

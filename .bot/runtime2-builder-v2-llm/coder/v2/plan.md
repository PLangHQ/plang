# Coder v2 Plan — Fix Code Analyzer Findings

Address all 8 findings from code analyzer v1 review of OpenAiProvider.cs.

## Fixes

### 1. Bare catch in ResolveImage (line 587) — CRITICAL
Change `catch` to `catch (Exception ex) when (ex is not (NullReferenceException or OutOfMemoryException or StackOverflowException))`.

### 2. Bare catch in ParseApiResponse (line 765) — CRITICAL
Change `catch` to `catch (JsonException)` since only JSON serialization can meaningfully fail here.

### 3. Sync-over-async in ResolveConfig (line 730) — CRITICAL
Make `ResolveConfig` async (`ResolveConfigAsync`), await it from `Query`. Three call sites in Query need updating.

### 4. Duplicate httpAction construction (lines 139-169) — MINOR
Build `httpAction` once with conditional `OnStream` and `StreamAs` properties.

### 5. Dead BuildStreamProxy wrapper (lines 867-873) — MINOR
Delete the method, inline `action.OnStream` at call site.

### 6. Decomposed parameters in ExecuteToolAsync (line 366) — MINOR OBP
Remove `engine` and `context` params, navigate from `action.Context.Engine` and `action.Context` inside the method. Same for `ToApiMessages` — remove `engine` param, but it's static so needs a different approach (pass `IPLangFileSystem` or just the action).

### 7. Add test: tool default parameter fill-in — MODERATE
New test in QueryToolTests: tool with default value, LLM omits that param, verify default is used.

### 8. Add test: type mappings — MODERATE
New test in QueryToolTests: verify `MapPlangTypeToJsonSchema` produces correct JSON schema types for int, bool, list, object.

## Approach
All changes in `OpenAiProvider.cs` + 2 new tests in `QueryToolTests.cs`. Run full test suite after.

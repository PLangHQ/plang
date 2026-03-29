# Coder v3 Plan — Fix Tester Findings

Address all 8 tester findings (4 major, 4 minor) in test files.

## Major Fixes

### 1. ProviderNotRegistered → rename to ProviderRegistered_ByDefault
Rename the test to be honest about what it tests. No need for a separate missing-provider test since RegisterDefaults always registers it.

### 2. MaxToolCalls loop — add callIndex bound assertion
Assert `_handler.CallCount` is bounded. With 3 tools/round and MaxToolCalls=5: round1 gives 3 tools (count=3), round2 gives 3 more but only 2 fit (count=5, hits limit → break). So expect exactly 2 HTTP rounds.

### 3. API error tests — add Error.Key and message assertions
Assert `result.Error.Key` contains "HttpError" (from HTTP module's ReadErrorResponseAsync). Assert error message contains status info.

### 4. OnToolCall callback — rename test, add comment
Since callback goals don't exist in unit tests, the test can only verify the tool loop completes with OnToolCall configured. Rename to be honest. The `Query_OnToolCall_FiresStartingAndCompleted` test already covers this — rename the `ReceivesNameArgumentsResult` test to `Query_OnToolCall_ToolLoopCompletesWithCallbackConfigured` and add explanation.

## Minor Fixes

### 5. ParseToolArguments mixed types — add test
New test with `{"flag": true, "count": 42, "label": null, "data": {"nested": true}}` to hit True/False/Null/fallback branches.

### 6. ResolveImage file path — fix existing test
The `Query_ImageFilePath_ReadAndBase64Encoded` test writes to `System.IO` temp dir but the engine's `IPLangFileSystem` is rooted at `_tempDir`. The image path should be relative to the engine's root, or use the engine's filesystem to write the test file.

### 7. RestoreFromCache — strengthen cache hit test
The existing `Query_CacheHit_PropertiesPreserved` should already exercise RestoreFromCache. Verify by adding value assertions on the cached result. Also add explicit assertion that the returned value matches the original.

### 8. Parallel execution — add tool result verification
Verify the re-query request body contains both tool results (tool_call_id references).

## Files Modified
- `QueryEdgeCaseTests.cs` — findings 1, 2
- `QueryBasicTests.cs` — finding 3
- `QueryCallbackTests.cs` — finding 4
- `QueryToolTests.cs` — findings 5, 8
- `QueryImageTests.cs` — finding 6
- `QueryCacheTests.cs` — finding 7

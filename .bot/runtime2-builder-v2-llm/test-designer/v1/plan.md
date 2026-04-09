# LLM Module Test Plan — v1

## Overview

Test suite for the LLM module defined in architect v2 plan. The module introduces:
- `llm.query` action record with ILlmProvider delegation
- `LlmMessage` type (Role, Text, Images, ToolCallId, ToolCalls)
- `ToolCall` carrier type
- `ILlmProvider` interface + OpenAiProvider implementation
- GoalCall gains `Description` and `Parallel` properties
- Full tool execution loop, streaming, validation callbacks, caching, conversation continuity

## Test Strategy

The architect suggested ~38 C# + ~8 PLang = ~46 total. I'm reorganizing and adding edge cases the architect missed. My additions focus on:
- **Security**: prompt injection via tool arguments, oversized responses
- **Concurrency edge cases**: parallel tools with mixed success/failure
- **State management**: conversation continuity leaking across goals, context cleanup
- **Error boundaries**: every error path returns Data.FromError, never throws
- **Type system**: GoalCall.Description/Parallel don't break existing GoalCall serialization

## Batches

### Batch 1: Core Types & GoalCall Changes (~8 C# tests)
Tests for the new/modified types that don't need HTTP mocking.

1. `LlmMessage_DefaultProperties_AreNull` — new LlmMessage has null Text, Images, ToolCallId, ToolCalls
2. `LlmMessage_ToolCallsInternalOnly_NotSetByBuilder` — ToolCalls and ToolCallId are not [Store]/[LlmBuilder]
3. `ToolCall_DefaultProperties_AreEmptyStrings` — Id, Name, Arguments default to ""
4. `GoalCall_Description_SerializesWithStore` — Description property roundtrips via JSON
5. `GoalCall_Parallel_DefaultsFalse` — Parallel defaults to false
6. `GoalCall_Parallel_SerializesWithStore` — Parallel=true roundtrips via JSON
7. `GoalCall_ExistingProperties_Unchanged` — Name, Parameters, PrPath still work after adding Description/Parallel
8. `ILlmProvider_ImplementsIProvider` — interface inherits IProvider correctly

### Batch 2: Basic Query & Response Properties (~8 C# tests)
Tests OpenAiProvider with MockHttpMessageHandler (same pattern as HTTP module tests).

9. `Query_SimpleMessage_ReturnsContentAsDataValue` — system+user → Data.Ok with content
10. `Query_ModelParameter_OverridesDefault` — action.Model sent to API instead of settings default
11. `Query_TemperatureAndMaxTokens_SentToApi` — verify request body
12. `Query_MissingApiKey_ReturnsDataFromError` — no key in settings or env → error
13. `Query_ApiError4xx_ReturnsDataFromError` — 400/401/429 → Data.FromError
14. `Query_ApiError5xx_ReturnsDataFromError` — 500/503 → Data.FromError
15. `Query_ResponseProperties_Populated` — RawResponse, Model, PromptTokens, CompletionTokens, TotalTokens, Cached=false
16. `Query_CostNull_WhenNoPricingData` — Cost property is null when provider can't calculate

### Batch 3: Format & Schema (~9 C# tests)
Tests format instruction building, response extraction, and JSON validation.

17. `Query_SchemaNoFormat_DefaultsToJson` — Schema set → format instruction with JSON + schema appended to system message
18. `Query_SchemaSet_JsonResponseParsed` — valid JSON response → parsed value on Data
19. `Query_InvalidJsonResponse_ReturnsDataFromError` — garbage → error
20. `Query_InvalidJsonWithCodeBlock_ExtractsAndParses` — ```json\n{...}\n``` fallback works
21. `Query_FormatPython_ExtractsFromCodeBlock` — ```python\n...\n``` → extracted content
22. `Query_FormatMd_ExtractsFromCodeBlock` — ```md\n...\n``` → extracted content
23. `Query_NoCodeBlockFound_ReturnsRawContent` — format=python but no code block → raw content, no error
24. `Query_NoSchemaNoFormat_NoFormatInstruction` — nothing appended to system message
25. `Query_FormatInstruction_AppendsToExistingSystem` — system message text gets format appended, not replaced

### Batch 4: Caching (~6 C# tests)
Tests persistent cache behavior.

26. `Query_CacheTrue_SecondCallReturnsCached` — same input → Cached=true, no second HTTP call
27. `Query_CacheTrue_DifferentMessages_CacheMiss` — different input → fresh API call
28. `Query_CacheFalse_AlwaysCallsApi` — Cache=false → always HTTP, Cached=false
29. `Query_CacheTrue_ToolsNonNull_CacheSkipped` — tools present → no caching regardless of Cache flag
30. `Query_CacheHash_IncludesModelTempSchemaFormat` — change any of model/temp/schema/format → different cache key
31. `Query_CacheHit_PropertiesPreserved` — cached result has all properties intact

### Batch 5: Tool Execution (~10 C# tests)
Tests the tool call loop.

32. `Query_SingleToolCall_ExecutesAndReQueries` — LLM requests tool → executes goal → sends result → gets final answer
33. `Query_MultipleToolCalls_SequentialByDefault` — multiple tools, Parallel=false → executed in order
34. `Query_MultipleToolCalls_AllParallel_ConcurrentExecution` — all Parallel=true → Task.WhenAll
35. `Query_MixedParallelFlags_ForcesSequential` — one Parallel=false → all sequential
36. `Query_ToolError_SentBackToLlm` — goal returns error → error message as tool result
37. `Query_UnknownToolName_ErrorResultSentToLlm` — LLM requests tool not in Tools list → error
38. `Query_MaxToolCallsReached_StopsLoop` — after MaxToolCalls individual calls, stops and returns
39. `Query_ToolParams_DefaultValueMeansOptional` — Data with Value != null → not in required array
40. `Query_ToolParams_NullValueMeansRequired` — Data with Value == null → in required array
41. `Query_ToolParams_EmptyList_ProducesEmptySchema` — no params → {type: "object", properties: {}}

### Batch 6: Callbacks & Streaming (~8 C# tests)
Tests OnToolCall, OnValidateResponse, and OnStream callbacks.

42. `Query_OnToolCall_FiresStartingAndCompleted` — status="starting" before, status="completed" after
43. `Query_OnToolCall_ReceivesNameArgumentsResult` — callback gets tool name, args JSON, and result
44. `Query_OnValidateResponse_Passes_ReturnsNormally` — validation goal succeeds → result returned
45. `Query_OnValidateResponse_Fails_RetriesWithFeedback` — validation fails → error fed back, LLM retried
46. `Query_OnValidateResponse_MaxRetries_ReturnsError` — fails MaxValidationRetries times → Data.FromError
47. `Query_OnValidateResponse_OnlyOnContentResponse` — validation doesn't run during tool call rounds
48. `Query_OnStream_FiresPerChunk` — streaming response → OnStream called with each chunk
49. `Query_OnStream_SignalsDone` — final chunk has isDone=true

### Batch 7: Conversation Continuity (~5 C# tests)
Tests ContinuePreviousConversation state management.

50. `Query_ContinueConversation_PrependsPreviousMessages` — stored history prepended
51. `Query_ContinueConversation_False_ClearsHistory` — not continuing → context cleared
52. `Query_FormatInstruction_DoesNotCompound` — original messages stored pre-mutation, format re-applied fresh
53. `Query_ContinueConversation_ReusesSchemaWhenNotSpecified` — null schema on continuation → uses previous
54. `Query_ContinueConversation_NewSchemaOverridesPrevious` — explicit schema replaces stored one

### Batch 8: Images (~3 C# tests)

55. `Query_ImageUrl_PassedAsUrlToApi` — http:// string → url type in API
56. `Query_ImageFilePath_ReadAndBase64Encoded` — file on disk → base64 content
57. `Query_MultipleImages_AllSentInMessage` — List<string> with 2+ images → all in API request

### Batch 9: Edge Cases & Security (~5 C# tests)

58. `Query_EmptyMessages_ReturnsError` — Messages is [IsNotNull] but empty list → should error
59. `Query_ToolLoop_DoesNotExceedMaxEvenWithMultiPerRound` — 3 tools per round, MaxToolCalls=5 → stops at 5 individual calls
60. `Query_NullToolCallArguments_HandledGracefully` — tool call with null/empty arguments → doesn't crash
61. `Query_ApiReturnsEmptyContent_ReturnsEmptyString` — content="" → Data.Ok("")
62. `Query_ProviderNotRegistered_ReturnsError` — no ILlmProvider in registry → clear error

### Batch 10: PLang Integration Tests (~8 tests)

63. `LlmQuery.test.goal` — Simple system+user query, verify response is not null
64. `LlmSchema.test.goal` — Query with schema, verify JSON parsed and dot-navigable
65. `LlmFormat.test.goal` — Query with format=md, verify extracted content
66. `LlmContinue.test.goal` — Two queries, second with continuePreviousConversation
67. `LlmProperties.test.goal` — Verify %result!TotalTokens%, %result!Model% accessible
68. `LlmToolCall.test.goal` — Tool call with a goal, verify tool executed and result returned
69. `LlmValidation.test.goal` — OnValidateResponse rejects bad response, verify retry happened
70. `LlmCache.test.goal` — Cache hit returns same result

## Totals

- **C# tests:** 62 (across 9 batches)
- **PLang tests:** 8 (batch 10)
- **Total: 70**

## What I Added Beyond the Architect's Suggestions

The architect had ~46 tests. I added 24 more:
- **Type-level tests (batch 1):** The architect assumed types "just work" — I test GoalCall changes don't break serialization, LlmMessage internal fields aren't exposed to builder
- **Format instruction placement (test 25):** Verifies appending vs replacing system message
- **Cache properties preserved (test 31):** Cached results must carry all metadata
- **Tool param schema edge cases (tests 39-41):** Required vs optional, empty params
- **Validation timing (test 47):** Validation must NOT run during tool rounds — only after final content
- **Streaming completion signal (test 49):** isDone=true semantics
- **Edge cases (batch 9):** Empty messages, tool count across rounds, null arguments, empty content, missing provider
- **Conversation schema reuse (test 54):** New schema explicitly overrides stored one

## Notes

- C# tests use the MockHttpMessageHandler pattern from HTTP module tests
- Tool execution tests need a way to mock GoalCall execution — likely a test helper that registers fake goals
- PLang tests use real OpenAI API (key in env `OPENAI_API_KEY`). Responses can be snapshotted later for faster reruns — as long as the provider class structure doesn't change, stored responses will be valid
- All C# test bodies will be `Assert.Fail("Not implemented")` stubs
- All PLang test goals will have `- throw "not implemented"` as body

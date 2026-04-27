# LLM Module Test Suite — v1 Summary

## What this is

Comprehensive test contract for the LLM module (Piece 7). Defines 70 test stubs (62 C#, 8 PLang) across 10 batches covering query execution, format/schema handling, caching, tool execution loop, callbacks (OnToolCall, OnValidateResponse, OnStream), conversation continuity, images, and edge cases.

## What was done

- Created 8 C# test files in `PLang.Tests/App/Modules/llm/`:
  - `LlmTypeTests.cs` — 8 tests for LlmMessage, ToolCall, GoalCall changes, ILlmProvider
  - `QueryBasicTests.cs` — 8 tests for simple query, model override, errors, response properties
  - `QueryFormatTests.cs` — 9 tests for format/schema instruction, JSON validation, code block extraction
  - `QueryCacheTests.cs` — 6 tests for persistent caching behavior
  - `QueryToolTests.cs` — 10 tests for tool execution loop, parallel execution, param schemas
  - `QueryCallbackTests.cs` — 8 tests for OnToolCall, OnValidateResponse, OnStream
  - `QueryConversationTests.cs` — 5 tests for ContinuePreviousConversation state management
  - `QueryImageTests.cs` — 3 tests for URL/file/multi-image handling
  - `QueryEdgeCaseTests.cs` — 5 tests for empty messages, max tool tracking, null args, missing provider

- Created 8 PLang test files in `Tests/App/Llm/`:
  - `LlmQuery.test.goal` — basic query
  - `LlmSchema.test.goal` — JSON schema parsing
  - `LlmFormat.test.goal` — format extraction
  - `LlmContinue.test.goal` — conversation continuity
  - `LlmProperties.test.goal` — response property access
  - `LlmToolCall.test.goal` — tool execution
  - `LlmValidation.test.goal` — validation callback
  - `LlmCache.test.goal` — cache hit

- Added 24 tests beyond the architect's 46 suggestions, focusing on:
  - Type serialization compatibility (GoalCall changes don't break existing usage)
  - Format instruction placement (append vs replace)
  - Tool parameter schema edge cases (required vs optional)
  - Validation timing (only on content, not tool rounds)
  - Edge cases (empty messages, cross-round tool counting, missing provider)

## Code example

C# test stub pattern:
```csharp
[Test]
public async Task Query_SingleToolCall_ExecutesAndReQueries()
{
    // LLM requests one tool → engine runs GoalCall → result sent back → LLM gives final answer
    Assert.Fail("Not implemented");
}
```

PLang test stub pattern:
```plang
Start
/ Query with JSON schema — verify response is parsed and dot-navigable
- throw "not implemented"
```

## What's next

- **Coder** implements the LLM module (types, provider, tool loop) to make these tests pass
- PLang tests use real OpenAI API (`OPENAI_API_KEY` env var) — responses can be snapshotted later for faster reruns
- C# tests use MockHttpMessageHandler pattern (same as HTTP module tests)

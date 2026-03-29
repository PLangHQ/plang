# LLM Module Implementation — Coder v1 Plan

## Overview

Implement the LLM module for Runtime2: types, provider interface, OpenAI provider, and make 62 C# test stubs pass. Following TDD — tests are already written, I implement to make them green.

## Files to Create

| File | Purpose |
|------|---------|
| `PLang/Runtime2/modules/llm/query.cs` | Action record — delegates to `ILlmProvider` via `[Provider]` |
| `PLang/Runtime2/modules/llm/LlmMessage.cs` | Message type (Role, Text, Images, ToolCallId, ToolCalls) |
| `PLang/Runtime2/modules/llm/ToolCall.cs` | Tool call carrier (Id, Name, Arguments) |
| `PLang/Runtime2/modules/llm/providers/ILlmProvider.cs` | Provider interface extending `IProvider` |
| `PLang/Runtime2/modules/llm/providers/OpenAiProvider.cs` | Full OpenAI-compatible provider implementation |

## Files to Modify

| File | Change |
|------|--------|
| `PLang/Runtime2/Engine/Goals/Goal/GoalCall.cs` | Add `Description` and `Parallel` properties |
| `PLang/Runtime2/Engine/Providers/this.cs` | Add `"llm"` to `ResolveType()`, register `OpenAiProvider` in `RegisterDefaults()` |

## Implementation Approach

### 1. Types (LlmMessage, ToolCall, GoalCall changes)
- `LlmMessage`: class with `[Store, LlmBuilder]` on Role/Text/Images, internal-only ToolCallId/ToolCalls
- `ToolCall`: class with Id/Name/Arguments defaulting to ""
- `GoalCall`: add `Description` (string?) and `Parallel` (bool, default false), both `[Store, LlmBuilder]`

### 2. Query action record
- Standard pattern: `partial class Query : IContext` with `[Action("query")]`
- Properties: Messages, Tools, OnToolCall, OnValidateResponse, OnStream, Schema, Format, Model, ContinuePreviousConversation, Temperature, MaxTokens, MaxToolCalls, MaxValidationRetries, Cache
- `[Provider]` on ILlmProvider, `Run()` delegates to provider

### 3. ILlmProvider interface
- `Task<Data> Query(query action)` — single method, provider owns everything

### 4. OpenAiProvider — the big piece
This is the core implementation. Key behaviors:

**Config resolution**: endpoint, apiKey, model from settings/env
**Message building**: clone messages, handle ContinuePreviousConversation (prepend history), snapshot originals before format mutation
**Format instructions**: BuildFormatInstruction appends to system message
**Cache**: persistent to disk (JSON files in `.cache/llm/` under engine root), hash-based, skip when tools present
**HTTP calls**: use the runtime's HTTP module via `DefaultHttpProvider` — or use raw HttpClient like DefaultHttpProvider does internally? (see question below)
**Tool loop**: execute GoalCalls via `engine.RunGoalAsync`, re-query until done or MaxToolCalls
**Parallel tools**: `Task.WhenAll` when all tools in batch have `Parallel=true`
**OnToolCall callback**: fire before/after each tool with status
**OnValidateResponse**: validate final content, retry with feedback up to MaxValidationRetries
**OnStream**: SSE streaming, fire callback per chunk
**Conversation continuity**: store/restore in `PLangContext` via `context.Set<T>/Get<T>`
**Response properties**: populate Data.Properties with all metadata
**Image handling**: detect URL vs file path vs base64, format for OpenAI content array

### 5. Test implementation
Fill in all 62 test stubs to exercise the above. Tests use `MockHttpMessageHandler` pattern (same as HTTP module tests).

## Questions for Ingi

### Q1: HTTP calls — via http module or direct HttpClient?
The architect says "HTTP via the http module." But looking at the code, the http module's `request` action goes through the full action pipeline (source generator, context, etc.). The OpenAI provider needs to make HTTP calls internally — it can't easily create an `http.request` action record and run it through the engine mid-execution.

**My recommendation**: Use direct `HttpClient` (like `DefaultHttpProvider` does internally), with a constructor that accepts `HttpMessageHandler` for testing. This is what the HTTP provider itself does — it doesn't call another module, it IS the transport. The LLM provider should own its HTTP transport the same way.

### Q2: Cache storage mechanism
The architect says "persistent (disk/database)." I plan to use simple JSON files under `.cache/llm/{hash}.json` in the engine's filesystem (via `IPLangFileSystem`). No database needed for v1. The hash would be SHA256 of the serialized cache key (messages + model + temperature + schema + format).

### Q3: Streaming + tool calls interaction
The architect flagged this needs design review: "When streaming is enabled and the LLM responds with tool calls, the OnStream callback fires isDone: true before tools execute."

**My recommendation for v1**: If streaming is enabled and the response contains tool calls (not content), skip the OnStream callback entirely for that round — only fire OnStream for actual content chunks. The final content response (after all tools complete) fires OnStream normally. This avoids the confusing "isDone but not really done" UX.

### Q4: API key validation timing
Test says "missing API key returns Data.FromError." Should I validate this in the provider's Query method (before making the HTTP call) or let the HTTP call fail with a 401? I'll validate early — return Data.FromError immediately if no API key found.

### Q5: GoalCall callback execution for tools
The tool loop needs to call `engine.RunGoalAsync(goalCall, context)`. For OnToolCall/OnValidateResponse/OnStream callbacks, I'll create new GoalCalls with injected parameters (same pattern as `RunCallbackAsync` in the HTTP provider). The parameters will be injected as Data objects on the context's MemoryStack.

## Execution Order

1. Types first (LlmMessage, ToolCall, GoalCall changes) — unblocks type tests
2. query.cs action record — unblocks provider interface tests
3. ILlmProvider interface
4. Provider registration in Engine/Providers
5. OpenAiProvider — core logic
6. Fill in test implementations
7. Iterate until green

## Blocked?

Not blocked — just need answers to Q1-Q3 before implementing the provider. Q4-Q5 I'll go with my stated recommendations unless you disagree.
